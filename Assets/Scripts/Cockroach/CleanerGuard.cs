using TapTapGameJam.Audio;
using TapTapGameJam.Core;
using TapTapGameJam.Items;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

// Cockroach 在本命名空间里既是命名空间又是实体类型，直接写会被当成命名空间（CS0118），统一走别名
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫变体：清洁工。它自己不出手：看见蟑螂就逃，安全时在地上丢一颗杀虫饵。
    /// 杀蟑螂靠蟑螂自己啃到，所以它的"交战"是逃跑，同样允许暂离行动区域，
    /// 跑完（威胁消失超过 FleeDuration）之后由基类送回出生点。
    ///
    /// 它只实现 <see cref="DecideIntent"/>（逃跑 / 布药 / 巡逻），其余交给 <see cref="Guard"/>。
    /// </summary>
    public class CleanerGuard : Guard
    {
        [System.Serializable]
        public class CleanerGuardSettings
        {
            [Header("逃跑")]
            [Tooltip("逃跑速度倍率（相对 GuardSettings.PatrolSpeed）")]
            public float FleeSpeedMul = 1.9f;

            [Tooltip("逃跑转弯倍率（相对 GuardSettings.TurnSpeed）")]
            public float FleeTurnMul = 1.6f;

            [Tooltip("甩掉蟑螂后仍继续逃的时间（秒）")]
            public float FleeDuration = 2.5f;

            [Tooltip("逃跑方向的噪声（弧度），避免每次都跑成完全一样的直线")]
            public float FleeJitter = 0.15f;

            [Tooltip("蟑螂近于这个距离才算威胁；<= 0 表示视野里任何距离都怕")]
            public float FearRadius = 4.5f;

            [Header("布药")]
            [Tooltip("两次布药之间的间隔（秒）")]
            public float PlaceInterval = 18f;

            [Tooltip("进入夜晚后第一颗药的延迟（秒）")]
            public float FirstPlaceDelay = 4f;

            [Tooltip("离上次布药点走出这么远才放下一颗（0 = 只看计时）")]
            public float PlaceTravelDistance = 3f;

            [Tooltip("布药点与守卫的距离")]
            public float PlaceOffset = 0.8f;

            [Tooltip("布药点的净空半径，这个范围内有蟑螂就先不放")]
            public float PlaceClearance = 1.5f;

            [Tooltip("这一帧没找到落点时的重试间隔（秒）")]
            public float PlaceRetryDelay = 0.5f;
        }

        /// <summary>找落点时查蟑螂的临时缓冲。单个守卫一帧最多查一次，够用</summary>
        private static readonly Collider[] OverlapBuffer = new Collider[8];

        [Header("清洁工数值")]
        public CleanerGuardSettings cleaner = new CleanerGuardSettings();

        [Header("杀虫饵")]
        [Tooltip("布药用的预制体，需要带 CleanerDrugFoodItem 组件")]
        [SerializeField] private CleanerDrugFoodItem _drugPrefab;

        [Tooltip("药物无限（false 时受 DrugStock 限制）")]
        [SerializeField] private bool _unlimitedDrug = true;

        [SerializeField] private int _drugStock = 3;

        private float _fleeTimer;
        private float _placeTimer;
        private Vector2 _lastPlacePos;
        private int _placedCount;

        /// <summary>有没有可用的杀虫饵预制体。没有的话布药这条路整条跳过，而不是每帧判空</summary>
        private bool _canPlaceDrug;

        public bool IsFleeing => _fleeTimer > 0f;

        public int PlacedCount => _placedCount;

        public int DrugStock => _drugStock;

        protected override void Start()
        {
            base.Start();

            // 基类没能把守卫搭起来（错误已经报过），别在这里再补一遍
            if (!IsReady)
            {
                return;
            }

            _placeTimer = Mathf.Max(0f, cleaner.FirstPlaceDelay);
            _lastPlacePos = Position;

            _canPlaceDrug = _drugPrefab != null;
            Assert.IsTrue(_canPlaceDrug, "CleanerGuard 没配杀虫饵预制体：它唯一的杀蟑手段会失效");
        }

        protected override void TickTimers(float dt)
        {
            if (_fleeTimer > 0f)
            {
                _fleeTimer = Mathf.Max(0f, _fleeTimer - dt);
            }
        }

        #region 决策

        protected override GuardIntent DecideIntent(float dt)
        {
            // 身后贴上来的蟑螂看不见但撞得到，一并算威胁
            CockroachAgent threat = PickNearestCockroach(Position, cleaner.FearRadius, includeTouching: true);

            if (threat != null)
            {
                _fleeTimer = cleaner.FleeDuration;
                return FleeIntent(threat);
            }

            // 甩掉了也把这一段跑完，不马上回头
            if (_fleeTimer > 0f)
            {
                return FleeAheadIntent();
            }

            // 跑的时候不布药：只在这里（安全状态）布
            TryPlaceDrug(dt);
            return PatrolIntent(dt);
        }

        private float FleeSpeed => settings.PatrolSpeed * cleaner.FleeSpeedMul;

        private float FleeTurnSpeed => settings.TurnSpeed * cleaner.FleeTurnMul;

        /// <summary>背对威胁跑。只写意图，避障交给基类，不会出现"逃跑方向"和"避障方向"互相覆盖</summary>
        private GuardIntent FleeIntent(CockroachAgent threat)
        {
            float away = AngleTo(Position, threat.Position) + Mathf.PI;
            away += Random.Range(-cleaner.FleeJitter, cleaner.FleeJitter);

            return new GuardIntent(GuardStance.Engage, away, FleeSpeed, FleeTurnSpeed);
        }

        /// <summary>威胁没了就沿原方向继续跑，不回头</summary>
        private GuardIntent FleeAheadIntent()
        {
            return new GuardIntent(GuardStance.Engage, _angle, FleeSpeed, FleeTurnSpeed);
        }

        #endregion

        #region 布药

        private void TryPlaceDrug(float dt)
        {
            if (!_canPlaceDrug)
            {
                return;
            }

            _placeTimer -= dt;

            bool travelled = cleaner.PlaceTravelDistance <= 0f
                             || (Position - _lastPlacePos).sqrMagnitude >=
                             cleaner.PlaceTravelDistance * cleaner.PlaceTravelDistance;

            if (_placeTimer > 0f || !travelled)
            {
                return;
            }

            if (!_unlimitedDrug && _drugStock <= 0)
            {
                return;
            }

            if (TryFindPlacement(out Vector3 worldPosition, out Quaternion rotation))
            {
                SpawnDrug(worldPosition, rotation);
                Sfx.At(SoundId.PaperBag, worldPosition, this);
                _placeTimer = cleaner.PlaceInterval;
                _lastPlacePos = Position;
                _placedCount++;
                if (!_unlimitedDrug)
                {
                    _drugStock--;
                }

                return;
            }

            // 这一帧没找到落点：过一小会儿再试，别每帧都扫一遍周围
            _placeTimer = Mathf.Max(_placeTimer, cleaner.PlaceRetryDelay);
        }

        /// <summary>在守卫附近找一个不贴墙、附近也没有蟑螂的落点</summary>
        private bool TryFindPlacement(out Vector3 worldPosition, out Quaternion rotation)
        {
            rotation = transform.rotation;
            worldPosition = transform.position;

            float offset = Mathf.Max(0f, cleaner.PlaceOffset);
            float baseAngle = _angle + Mathf.PI;        // 默认丢在身后

            const int attempts = 6;
            for (int i = 0; i < attempts; i++)
            {
                // 身后 → 身后两侧 → 正前
                float spread = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * (0.5f + 0.3f * (i / 2));
                float angle = baseAngle + spread;
                Vector2 flat = Position + DirOfAngle(angle) * offset;

                if (Grid.IsBlockedWorld(flat))
                {
                    continue;
                }

                if (HasCockroachNear(flat))
                {
                    continue;
                }

                worldPosition = new Vector3(flat.x, transform.position.y, flat.y);
                rotation = AngleToRotation(angle);
                return true;
            }

            return false;
        }

        /// <summary>落点周围有没有蟑螂。有就换个地方，免得刚丢下就被触发</summary>
        private bool HasCockroachNear(Vector2 flat)
        {
            float radius = Mathf.Max(0.01f, cleaner.PlaceClearance);
            Vector3 center = new Vector3(flat.x, transform.position.y, flat.y);

            // 守卫自己的 collider 在 Guard 层，查 Cockroach 层时根本不会命中，不用临时关掉
            int count = Physics.OverlapSphereNonAlloc(center, radius, OverlapBuffer, CockroachMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                CockroachAgent cockroach = OverlapBuffer[i].GetComponentInParent<CockroachAgent>();
                if (cockroach != null && !cockroach.IsInside)
                {
                    return true;
                }
            }

            return false;
        }

        private void SpawnDrug(Vector3 worldPosition, Quaternion rotation)
        {
            CleanerDrugFoodItem drug = Instantiate(_drugPrefab, worldPosition, rotation);

            // 药和守卫同层：这样它和守卫 / 墙的相处方式和以前一致
            SetLayerRecursively(drug.gameObject, gameObject.layer);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;

            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
            }
        }

        #endregion

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (cleaner.FearRadius <= 0f)
            {
                return;
            }

            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, cleaner.FearRadius);

            if (!Application.isPlaying || !IsReady)
            {
                return;
            }

            CockroachAgent threat = PickNearestCockroach(Position, cleaner.FearRadius, includeTouching: true);
            if (threat != null)
            {
                float away = AngleTo(Position, threat.Position) + Mathf.PI;
                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
                Gizmos.DrawLine(transform.position,
                    transform.position + new Vector3(Mathf.Cos(away), 0f, Mathf.Sin(away)) * 2.5f);
            }

            if (_placedCount <= 0)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(new Vector3(_lastPlacePos.x, transform.position.y, _lastPlacePos.y), 0.3f);
        }
    }
}
