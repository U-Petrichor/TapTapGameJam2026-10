using TapTapGameJam.Core;
using TapTapGameJam.Items;
using UnityEngine;
using Random = UnityEngine.Random;

// 见 Guard.cs 顶部说明：Cockroach 在本命名空间里既是命名空间又是实体类型，必须走别名
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫变体：清洁工。
    ///
    /// 和 <see cref="KillerGuard"/> 正好相反 —— 它自己不动手，只负责"巡查 + 布药"：
    /// <list type="bullet">
    /// <item>视野里发现蟑螂就<b>转身逃</b>（复用基类那套避障：挑一个背对蟑螂、且前方没有墙的方向）；</item>
    /// <item>每次逃跑都会刷新冷却，所以只要还有蟑螂在面前，它就会一直躲，
    /// 逃跑方向上追不到的东西不会让它原地抖；</item>
    /// <item>身边一段时间没有蟑螂，就按 <see cref="CleanerGuardSettings.PlaceInterval"/> 放一颗
    /// <see cref="CleanerDrugFoodItem"/>；</item>
    /// <item>身体被蟑螂撞到（<see cref="CockroachesTouching"/>）也会立刻逃 —— 视野是扇形，
    /// 背后贴上来的蟑螂看不见，但撞得到。</item>
    /// </list>
    ///
    /// 只覆写 <see cref="Guard.Decide"/>，移动/避障/转向全部复用基类。
    /// </summary>
    public class CleanerGuard : Guard
    {
        [System.Serializable]
        public class CleanerGuardSettings
        {
            [Header("逃跑")]
            [Tooltip("逃跑时的速度倍率（相对 GuardSettings.PatrolSpeed）")]
            public float FleeSpeedMul = 1.9f;

            [Tooltip("逃跑急转的角速度倍率（相对 GuardSettings.TurnSpeed）")]
            public float FleeTurnMul = 1.6f;

            [Tooltip("看见蟑螂后至少逃这么久（秒）。逃跑期间不扫视，方向只跟着最近的蟑螂走")]
            public float FleeDuration = 3.5f;

            [Tooltip("方向噪声（弧度），让逃跑路线别那么笔直")]
            public float FleeJitter = 0.25f;

            [Header("布药")]
            [Tooltip("布药间隔（秒）")]
            public float PlaceInterval = 8f;

            [Tooltip("第一次布药前的等待时间（秒）")]
            public float FirstPlaceDelay = 4f;

            [Tooltip("跑动中布药的触发距离（世界单位）。离上次布药点这么远就丢一颗下去")]
            public float PlaceTravelDistance = 3f;

            [Tooltip("布药点与守卫的距离（世界单位）。0 = 丢在脚下")]
            public float PlaceOffset = 0.8f;

            [Tooltip("布药点的最小净空半径：这个范围内有蟑螂就不放（免得刚丢下就炸，看起来像守卫在杀蟑螂）")]
            public float PlaceClearance = 1.5f;

            [Tooltip("连续找不到合法落点时，最多顺延几秒再试")]
            public float PlaceRetryDelay = 0.5f;

            [Header("逃跑距离")]
            [Tooltip("蟑螂比这个距离还近时才算威胁。<= 0 表示视野里任何距离都怕")]
            public float FearRadius = 4.5f;
        }

        [Header("清洁工数值")]
        public CleanerGuardSettings cleaner = new CleanerGuardSettings();

        [Header("杀虫饵")]
        [Tooltip("布药用的预制体。建议直接把 Food.prefab 拖进来（换掉 FoodItem 组件即可）")]
        [SerializeField] private CleanerDrugFoodItem _drugPrefab;

        /// <summary>身上带的药无限（守卫生成，不是玩家从背包里拿的）</summary>
        [Tooltip("是否拥有无限药（false 时受 DrugStock 限制）")]
        [SerializeField] private bool _unlimitedDrug = true;

        [Tooltip("有限药模式下的剩余数量")]
        [SerializeField] private int _drugStock = 3;

        private float _fleeTimer;
        private float _placeTimer;
        private Vector2 _lastPlacePos;
        private bool _hasPlaced;
        private int _placedCount;

        /// <summary>正在逃跑</summary>
        public bool IsFleeing => _fleeTimer > 0f;

        /// <summary>已经放下的药数量</summary>
        public int PlacedCount => _placedCount;

        /// <summary>有限药模式下的剩余数量</summary>
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
            // 身后的蟑螂看不见，但撞得到 —— 一并当成威胁
            CockroachAgent threat = NearestThreat(target);

            if (threat != null)
            {
                Flee(threat, dt);
            }
            else if (_fleeTimer > 0f)
            {
                // 暂时甩掉了，但还没逃够时间：沿当前方向继续逃，不回头扫
                _fleeTimer -= dt;
                DesiredAngle = _angle + Random.Range(-cleaner.FleeJitter, cleaner.FleeJitter);
            }
            else
            {
                // 安全了才继续巡查
                Scan(dt);
                TryPlaceDrug(dt);
            }
        }

        /// <summary>视野 + 身体里，<see cref="CleanerGuardSettings.FearRadius"/> 内最近的一只蟑螂</summary>
        private CockroachAgent NearestThreat(CockroachAgent inSight)
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
        /// 背对威胁跑，并借用基类的避障：前方撞墙就自己往两侧偏，两边都堵才掉头。
        /// 掉头那一下可能正好冲着蟑螂，但这是"被堵在死角"时的合理选择，下一帧会再修正。
        /// </summary>
        private void Flee(CockroachAgent threat, float dt)
        {
            _fleeTimer = Mathf.Max(_fleeTimer, cleaner.FleeDuration);

            float away = AngleTo(Position, threat.Position) + Mathf.PI;
            DesiredAngle = away + Random.Range(-cleaner.FleeJitter, cleaner.FleeJitter);
        }

        #endregion

        #region 布药

        private void TryPlaceDrug(float dt)
        {
            // 没配预制体就直接不布药：这里是每帧都会走到的路径，不能每帧刷一条警告
            if (_drugPrefab == null) return;

            _placeTimer -= dt;

            bool travelled = cleaner.PlaceTravelDistance > 0f
                             && _hasPlaced
                             && (Position - _lastPlacePos).sqrMagnitude >= cleaner.PlaceTravelDistance * cleaner.PlaceTravelDistance;

            if (_placeTimer > 0f && !travelled) return;

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

            // 落点不合法（贴墙 / 有蟑螂蹲守）：过一会儿再来
            _placeTimer = Mathf.Max(_placeTimer, cleaner.PlaceRetryDelay);
        }

        /// <summary>在守卫附近挑一个不贴墙、也不在蟑螂脸上的落点</summary>
        private bool TryFindPlacement(out Vector3 worldPos, out Quaternion rot)
        {
            GridManager grid = GridManager.Instance;
            rot = transform.rotation;
            worldPos = transform.position;

            if (grid == null || _drugPrefab == null) return false;

            grid.EnsureFootprint();

            float offset = Mathf.Max(0f, cleaner.PlaceOffset);
            float baseAngle = _angle + Mathf.PI;      // 默认丢在身后

            const int attempts = 6;
            for (int i = 0; i < attempts; i++)
            {
                // 依次试：身后 → 身后左右 → 正前
                float spread = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * (0.5f + 0.3f * (i / 2));
                float a = baseAngle + spread;
                Vector2 flat = Position + DirOfAngle(a) * offset;
                Vector3 candidate = new Vector3(flat.x, transform.position.y, flat.y);

                if (grid.IsBlocked(grid.WorldToGrid(flat))) continue;
                if (IsCockroachNear(flat)) continue;

                worldPos = candidate;
                rot = AngleToRotation(a);
                return true;
            }

            return false;
        }

        /// <summary>落点周围有没有蟑螂（含巢里的）。有的话先别放，免得刚丢下就触发</summary>
        private bool IsCockroachNear(Vector2 flat)
        {
            float r = Mathf.Max(0.01f, cleaner.PlaceClearance);

            // 守卫自己（视野 trigger + 身体球）也在 layer 上，量之前先关掉，不然永远"有东西"
            Collider[] own = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < own.Length; i++) if (own[i] != null) own[i].enabled = false;

            try
            {
                Collider[] hits = Physics.OverlapSphere(new Vector3(flat.x, transform.position.y, flat.y), r,
                    LayerMask.GetMask("Cockroach"), QueryTriggerInteraction.Collide);

                return hits.Length > 0;
            }
            finally
            {
                for (int i = 0; i < own.Length; i++) if (own[i] != null) own[i].enabled = true;
            }
        }

        private bool SpawnDrug(Vector3 worldPos, Quaternion rot)
        {
            if (_drugPrefab == null)
            {
                Debug.LogWarning($"[{name}]：CleanerGuard 没有配 drugPrefab，无法布药");
                return false;
            }

            CleanerDrugFoodItem drug = Instantiate(_drugPrefab, worldPos, rot);

            // 药要和守卫同层，否则它自己的 OverlapSphere 找不到蟑螂
            SetLayerRecursively(drug.gameObject, gameObject.layer);

            return drug != null;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
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

            // 恐惧圈：蟑螂进这个圈就会被当成威胁
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, cleaner.FearRadius);

            if (!Application.isPlaying) return;

            Gizmos.color = IsFleeing ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(0.6f, 1f, 0.6f, 0.6f);
            Gizmos.DrawLine(transform.position, transform.position + new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * 2f);

            if (_hasPlaced)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
                Gizmos.DrawWireSphere(new Vector3(_lastPlacePos.x, transform.position.y, _lastPlacePos.y), 0.3f);
            }
        }
    }
}
