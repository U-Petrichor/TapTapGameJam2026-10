using TapTapGameJam.Core;
using TapTapGameJam.Items;
using UnityEngine;
using Random = UnityEngine.Random;

// Cockroach 在本命名空间里既是命名空间又是实体类型，直接写会被当成命名空间（CS0118），统一走别名
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫变体：清洁工。看见蟑螂就转身逃，安全时定期在地上放一颗杀虫饵。
    /// 它自己不出手，杀蟑螂靠的是药。只覆写 <see cref="Decide"/> 和避障。
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

            [Tooltip("逃跑方向噪声（弧度）")]
            public float FleeJitter = 0.15f;

            [Tooltip("蟑螂近于这个距离才算威胁，<= 0 表示视野内任何距离都怕")]
            public float FearRadius = 4.5f;

            [Tooltip("逃跑候选方向之间的夹角（弧度）。越大越容易找到缺口")]
            public float EscapeStep = 0.45f;

            [Header("布药")]
            public float PlaceInterval = 18f;
            public float FirstPlaceDelay = 4f;

            [Tooltip("离上次布药点走出这么远就再放一颗（0 = 只看计时）")]
            public float PlaceTravelDistance = 3f;

            [Tooltip("布药点与守卫的距离")]
            public float PlaceOffset = 0.8f;

            [Tooltip("布药点的净空半径，这个范围内有蟑螂就先不放")]
            public float PlaceClearance = 1.5f;

            public float PlaceRetryDelay = 0.5f;
        }

        [Header("清洁工数值")]
        public CleanerGuardSettings cleaner = new CleanerGuardSettings();

        [Header("杀虫饵")]
        [Tooltip("布药用的预制体，需要带 CleanerDrugFoodItem 组件")]
        [SerializeField] private CleanerDrugFoodItem _drugPrefab;

        [Tooltip("药物无限（false 时受 DrugStock 限制）")]
        [SerializeField] private bool _unlimitedDrug = true;

        [SerializeField] private int _drugStock = 3;

        float _fleeTimer;
        float _placeTimer;
        Vector2 _lastPlacePos;
        bool _hasPlaced;
        int _placedCount;

        public bool IsFleeing => _fleeTimer > 0f;
        public int PlacedCount => _placedCount;
        public int DrugStock => _drugStock;

        protected override void Awake()
        {
            base.Awake();

            _placeTimer = Mathf.Max(0f, cleaner.FirstPlaceDelay);
            _lastPlacePos = Position;
        }

        #region 决策

        protected override void Decide(float dt, CockroachAgent target)
        {
            // 身后贴上来的蟑螂看不见但撞得到，一并算威胁
            CockroachAgent threat = NearestThreat(target);

            if (threat != null)
            {
                Flee(threat);
                return;
            }

            if (_fleeTimer > 0f)
            {
                _fleeTimer -= dt;
                DesiredAngle = _angle;      // 甩掉了就沿原方向继续跑，不回头
                return;
            }

            Scan(dt);
            TryPlaceDrug(dt);
        }

        /// <summary>视野 + 身体里，<see cref="CleanerGuardSettings.FearRadius"/> 内最近的一只蟑螂。</summary>
        CockroachAgent NearestThreat(CockroachAgent inSight)
        {
            Vector2 pos = Position;
            float range = cleaner.FearRadius;
            float bestSqr = range > 0f ? range * range : float.MaxValue;
            CockroachAgent best = null;

            Consider(inSight);
            foreach (CockroachAgent c in CockroachesTouching) Consider(c);
            return best;

            void Consider(CockroachAgent c)
            {
                if (c == null || c.IsInside) return;

                float d = (c.Position - pos).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = c;
                }
            }
        }

        /// <summary>
        /// 背对威胁逃。只在这里写一次角度，剩下的交给 <see cref="ApplyAvoidance"/> 修正 ——
        /// 之前抽搐就是"逃跑角度"和"避障角度"两套逻辑各写一次、互相覆盖造成的。
        /// </summary>
        void Flee(CockroachAgent threat)
        {
            _fleeTimer = cleaner.FleeDuration;
            float away = AngleTo(Position, threat.Position) + Mathf.PI;
            DesiredAngle = away + Random.Range(-cleaner.FleeJitter, cleaner.FleeJitter);
        }

        #endregion

        #region 避障：逃跑优先

        /// <summary>
        /// 有威胁时走"逃离扇区"避障：在背对威胁的方向上左右展开找能走的缺口，
        /// 保证选出来的方向既可通行、又不会拐回蟑螂脸上。
        /// </summary>
        protected override float ApplyAvoidance(float desired)
        {
            CockroachAgent threat = NearestThreat(TargetInSight());
            if (threat == null) return base.ApplyAvoidance(desired);

            // 找不到缺口就维持逃跑方向：贴着墙总比掉头撞回蟑螂好，位移那边会挡住它
            float away = AngleTo(Position, threat.Position) + Mathf.PI;
            return FindEscapeDirection(away, out float escape) ? escape : away;
        }

        /// <summary>在 away 两侧交替找第一个能走的方向。</summary>
        bool FindEscapeDirection(float away, out float result)
        {
            const int maxSteps = 5;
            float step = Mathf.Max(0.05f, cleaner.EscapeStep);

            for (int i = 0; i <= maxSteps; i++)
            {
                if (i == 0)
                {
                    if (!IsBlocked(away)) { result = away; return true; }
                    continue;
                }

                float offset = step * i;
                if (!IsBlocked(away - offset)) { result = NormAng(away - offset); return true; }
                if (!IsBlocked(away + offset)) { result = NormAng(away + offset); return true; }

                // 拐过 90° 就放弃，免得"逃"变成"从蟑螂旁边绕过去"
                if (offset > Mathf.PI * 0.5f) break;
            }

            result = away;
            return false;
        }

        protected override float EffectiveTurnSpeed => IsFleeing ? settings.TurnSpeed * cleaner.FleeTurnMul : settings.TurnSpeed;

        protected override float CurrentSpeed => IsFleeing ? settings.PatrolSpeed * cleaner.FleeSpeedMul : settings.PatrolSpeed;

        #endregion

        #region 布药

        void TryPlaceDrug(float dt)
        {
            if (_drugPrefab == null) return;        // 每帧都走这里，不能刷警告

            _placeTimer -= dt;

            bool travelled = cleaner.PlaceTravelDistance > 0f
                             && (Position - _lastPlacePos).sqrMagnitude >= cleaner.PlaceTravelDistance * cleaner.PlaceTravelDistance;

            if (_placeTimer > 0f || !travelled) return;
            if (!_unlimitedDrug && _drugStock <= 0) return;

            if (TryFindPlacement(out Vector3 worldPos, out Quaternion rot) && SpawnDrug(worldPos, rot))
            {
                _placeTimer = cleaner.PlaceInterval;
                _lastPlacePos = Position;
                _hasPlaced = true;
                _placedCount++;
                if (!_unlimitedDrug) _drugStock--;
                return;
            }

            _placeTimer = Mathf.Max(_placeTimer, cleaner.PlaceRetryDelay);
        }

        /// <summary>在守卫附近找一个不贴墙、附近也没有蟑螂的落点。</summary>
        bool TryFindPlacement(out Vector3 worldPos, out Quaternion rot)
        {
            GridManager grid = GridManager.Instance;
            rot = transform.rotation;
            worldPos = transform.position;

            if (grid == null || _drugPrefab == null) return false;

            const int attempts = 6;
            float offset = Mathf.Max(0f, cleaner.PlaceOffset);
            float baseAngle = _angle + Mathf.PI;        // 默认丢在身后

            for (int i = 0; i < attempts; i++)
            {
                // 身后 → 身后两侧 → 正前
                float spread = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * (0.5f + 0.3f * (i / 2));
                float a = baseAngle + spread;
                Vector2 flat = Position + DirOfAngle(a) * offset;

                if (grid.IsBlocked(grid.WorldToGrid(flat))) continue;
                if (IsCockroachNear(flat)) continue;

                worldPos = new Vector3(flat.x, transform.position.y, flat.y);
                rot = AngleToRotation(a);
                return true;
            }

            return false;
        }

        /// <summary>落点周围有没有蟑螂。有就换个地方，免得刚丢下就被触发。</summary>
        bool IsCockroachNear(Vector2 flat)
        {
            float r = Mathf.Max(0.01f, cleaner.PlaceClearance);

            // 守卫自己的 trigger 也在同一 layer 上，量之前先关掉
            Collider[] own = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < own.Length; i++) if (own[i] != null) own[i].enabled = false;

            try
            {
                return Physics.OverlapSphere(new Vector3(flat.x, transform.position.y, flat.y), r,
                    LayerMask.GetMask("Cockroach"), QueryTriggerInteraction.Collide).Length > 0;
            }
            finally
            {
                for (int i = 0; i < own.Length; i++) if (own[i] != null) own[i].enabled = true;
            }
        }

        bool SpawnDrug(Vector3 worldPos, Quaternion rot)
        {
            CleanerDrugFoodItem drug = Instantiate(_drugPrefab, worldPos, rot);

            // 药要和守卫同层，否则它自己的 OverlapSphere 找不到蟑螂
            SetLayerRecursively(drug.gameObject, gameObject.layer);
            return drug != null;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;

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

            if (cleaner.FearRadius <= 0f) return;

            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, cleaner.FearRadius);

            if (!Application.isPlaying) return;

            CockroachAgent threat = NearestThreat(TargetInSight());
            if (threat != null)
            {
                float away = AngleTo(Position, threat.Position) + Mathf.PI;
                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
                Gizmos.DrawLine(transform.position, transform.position + new Vector3(Mathf.Cos(away), 0f, Mathf.Sin(away)) * 2.5f);
            }

            if (_hasPlaced)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
                Gizmos.DrawWireSphere(new Vector3(_lastPlacePos.x, transform.position.y, _lastPlacePos.y), 0.3f);
            }
        }
    }
}
