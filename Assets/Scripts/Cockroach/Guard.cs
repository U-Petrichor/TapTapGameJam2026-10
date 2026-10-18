using System.Collections.Generic;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using UnityEngine;
using UnityEngine.Assertions;

// Cockroach 在本命名空间里既是命名空间又是实体类型，直接写会被当成命名空间（CS0118），统一走别名
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫本帧的立场。它只决定一件事：<b>能不能离开行动区域</b>。
    /// </summary>
    public enum GuardStance
    {
        /// <summary>没有焦点：在行动区域内巡逻，出不去</summary>
        Patrol,

        /// <summary>有焦点（追击 / 逃离）：允许暂时离开行动区域，焦点消失后由基类送回出生点</summary>
        Engage
    }

    /// <summary>
    /// 子类每帧交给基类的"意图"：往哪走、走多快、转多快、还算不算交战中。
    /// 基类拿到它之后统一做避障、转向、位移和区域约束，子类不碰 transform。
    /// </summary>
    public readonly struct GuardIntent
    {
        public readonly GuardStance Stance;

        /// <summary>想朝哪个方向走（弧度：0 = 世界 +X，+π/2 = 世界 +Z）</summary>
        public readonly float DesiredAngle;

        /// <summary>本帧移动速度，0 = 原地只转向</summary>
        public readonly float Speed;

        /// <summary>本帧转向角速度（rad/s）</summary>
        public readonly float TurnSpeed;

        public GuardIntent(GuardStance stance, float desiredAngle, float speed, float turnSpeed)
        {
            Stance = stance;
            DesiredAngle = desiredAngle;
            Speed = speed;
            TurnSpeed = turnSpeed;
        }
    }

    /// <summary>
    /// 守卫基类。它负责"当守卫"这件事本身：
    /// 视野 / 身体感知（含挡视线的墙）、行动区域、夜间门控、沿边缘路线的巡逻、
    /// 脱离区域后的回程、避障、转向、位移。
    /// 子类只回答一个问题：<see cref="DecideIntent"/> —— 这一帧想干什么（追谁 / 逃向哪 / 回区域）。
    ///
    /// 每帧流程（<see cref="FixedUpdate"/>）：
    ///   夜间检查 → 清掉失效的感知 → <see cref="TickTimers"/>
    ///   → 判定要不要回程（在区域外且没有焦点就必须回；回到区域里就地接上路线）
    ///   → <see cref="DecideIntent"/>（或沿归巢距离场往回走）
    ///   → 避障 → 转向 → 位移（巡逻时不出区域 / 撞墙贴墙滑行 / 卡住上报）
    ///
    /// 分层约定：
    ///   - 这个类不认识"蟑螂 / 玩家"具体怎么打，也不认识杀虫饵，那些是子类的职责；
    ///   - 子类不直接写 transform，位移一律走基类；
    ///   - 子类只描述意图，不要自己再算一遍避障或区域约束。
    ///
    /// 坐标系约定（和 <see cref="Cockroach"/> 不同，注意别混）：
    /// 所有逻辑都在世界 XZ 平面上用 Vector2 计算，角度 0 = 世界 +X，+π/2 = 世界 +Z。
    /// Vector3 只出现在 transform 边界上。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Guard : MonoBehaviour
    {
        #region 常量

        /// <summary>视野锥的竖直厚度，要盖得住贴地走的蟑螂</summary>
        private const float VisionThickness = 2.5f;

        /// <summary>没有显式绑定行动区域时，按这个名字在场景里找一次</summary>
        private const string SearchRangePlaneName = "search_range_plane";

        /// <summary>避障时左右展开的最大步数，以及每步的角度（rad）</summary>
        private const int AvoidanceSideSteps = 3;
        private const float AvoidanceStep = 0.6f;

        /// <summary>实际位移不到预期位移的这个比例，就算"想走但没走动"</summary>
        private const float StallProgressRatio = 0.15f;

        /// <summary>回程时沿距离场往前看多远（世界单位）再瞄准</summary>
        private const float HomeLookahead = 1f;

        /// <summary>巡逻卡住时，沿路线往前挪这么远换一个目标点</summary>
        private const float StallPathNudge = 1.5f;

        #endregion

        #region 序列化配置

        [Header("数值")]
        public GuardSettings settings = new GuardSettings();

        [Header("行动区域")]
        [Tooltip("划定行动区域的外置 plane（只取它世界包围盒的 XZ）。" +
                 "留空时按名字 search_range_plane 在场景里找一次")]
        [SerializeField] private Transform searchRangePlane;

        [Header("视野 object")]
        [Tooltip("挂 Spot Light 和扇形 trigger 的子物体。留空则自动取身上第一盏灯")]
        public Transform vision;

        [Tooltip("挂载动画器")]
        [SerializeField] protected Animator _animator;

        #endregion

        #region 运行时状态

        /// <summary> 行动角度 </summary>
        protected float _angle;
        
        /// <summary>视野 trigger 里的蟑螂（进巢的也在，由取样函数过滤）</summary>
        protected readonly HashSet<CockroachAgent> CockroachesInSight = new HashSet<CockroachAgent>();

        /// <summary>身体 trigger 里的蟑螂（贴脸了）</summary>
        protected readonly HashSet<CockroachAgent> CockroachesTouching = new HashSet<CockroachAgent>();

        private GuardRegion _region;

        /// <summary>贴着区域边缘的固定巡逻路线</summary>
        private GuardPatrolPath _patrolPath;

        /// <summary>沿巡逻路线已经走了多远（前进方向上的距离，会一直累加）</summary>
        private float _pathDistance;

        private Vector2 _homePosition;
        private Vector2Int _homeCell;
        private int[,] _homeField;

        private Light _visionLight;
        private GuardTrigger _visionTrigger;
        private Collider _visionCollider;
        private Collider _bodyCollider;

        private LayerMask _cockroachMask;
        private LayerMask _wallMask;

        private bool _isNight;

        /// <summary>视野和行动区域都搭好了。<see cref="Awake"/> 里配置出错时会是 false，组件也被停掉</summary>
        private bool _rigReady;

        private GuardStance _stance = GuardStance.Patrol;
        private float _engageLinger;
        private bool _returningHome;

        private float _stallTimer;

        #endregion

        #region 对外状态

        /// <summary>世界 XZ 坐标。读写都同步到 transform，Vector3 只在这里出现</summary>
        public Vector2 Position
        {
            get
            {
                Vector3 p = transform.position;
                return new Vector2(p.x, p.z);
            }
            set
            {
                Vector3 p = transform.position;
                transform.position = new Vector3(value.x, p.y, value.y);
            }
        }

        public Vector2 Forward => DirOfAngle(_angle);

        public float Angle => _angle;

        /// <summary>当前是否在夜间行动（白天整个守卫停摆）</summary>
        public bool IsNightActive => _isNight;

        public GuardStance Stance => _stance;

        /// <summary>当前生效的行动区域</summary>
        public GuardRegion Region => _region;

        /// <summary>出生点：行动区域的中心（区域中心被墙占住时会被挪到最近的空地）</summary>
        public Vector2 HomePosition => _homePosition;

        /// <summary>是否正在往出生点回</summary>
        public bool IsReturningHome => _returningHome;

        protected GridManager Grid { get; private set; }

        /// <summary>视野、行动区域、网格、日夜判断都就位了。子类的 Start 里应该先看这个再干活</summary>
        protected bool IsReady => _rigReady;

        protected LayerMask CockroachMask => _cockroachMask;

        /// <summary>
        /// 视野灯。子类可以拿它做预警 / 闪光（例如喷雾蓄势时变色），
        /// 但改完要自己还原：基类只负责开关，不管颜色和亮度。
        /// </summary>
        protected Light VisionLight => _visionLight;

        #endregion

        #region 生命周期

        protected virtual void Awake()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            _cockroachMask = LayerMask.GetMask("Cockroach");
            _wallMask = LayerMask.GetMask("Wall");
            Assert.AreNotEqual(0, _cockroachMask.value, "找不到 Cockroach 层：守卫的视野和身体 trigger 会永远收不到蟑螂");
            Assert.AreNotEqual(0, _wallMask.value, "找不到 Wall 层：守卫的避障会完全失效");

            _angle = RotationToAngle(transform.eulerAngles.y);

            // 这两步是硬前置，缺了就不该继续跑。
            // 每个 Setup 里用 Assert 把问题喊出来；Assert 在编辑器里会抛出，
            // 所以 _rigReady 只在两步都走完之后才变成 true —— 无论怎么失败，组件都保持停用。
            _rigReady = SetupVision() && SetupRegion();

            if (!_rigReady)
            {
                enabled = false;
            }
        }

        protected virtual void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }

        protected virtual void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }

        protected virtual void Start()
        {
            // Awake 的配置校验没过（错误已经报过），不要再往下走
            if (!_rigReady)
            {
                return;
            }

            // 先交还"就绪"再逐项验证：中途任何一步失败（包括 Assert 抛出）都会让组件停在不可用状态
            _rigReady = false;

            Grid = GridManager.Instance;
            Assert.IsNotNull(Grid, "场景里没有 GridManager：守卫没法寻路");
            Assert.IsTrue(GameManager.HasInstance, "场景里没有 GameManager：守卫判断不了白天 / 夜晚");

            if (Grid == null || !GameManager.HasInstance)
            {
                enabled = false;
                return;
            }

            ResolveHome();
            _pathDistance = _patrolPath.Project(Position);

            _rigReady = true;
            SetNightActive(GameManager.IsNight);
        }

        protected virtual void FixedUpdate()
        {
            // 配置没搭好 / 白天：守卫完全不动
            if (!_rigReady || !_isNight)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;

            PrunePerception();
            TickTimers(dt);

            GuardIntent intent = DecideGuardIntent(dt);
            UpdateStance(intent.Stance, dt);
            ApplyMotion(intent, dt);
        }

        /// <summary>子类的计时器钩子：每帧夜间逻辑开始前调一次。不在这里做决策</summary>
        protected virtual void TickTimers(float dt)
        {
        }

        #endregion

        #region 搭建（视野 / 行动区域 / 出生点）

        /// <summary>
        /// 把视野灯、视野扇形和身体 trigger 搭好。
        /// 灯光锥和检测锥必须严格一致，否则玩家看到的圈不等于守卫真正看得见的范围。
        /// </summary>
        private bool SetupVision()
        {
            if (vision == null)
            {
                Light foundLight = GetComponentInChildren<Light>(true);
                if (foundLight != null)
                {
                    vision = foundLight.transform;
                }
            }

            Assert.IsNotNull(vision, "Guard 没找到视野物体：把挂 Spot Light 的子物体拖到 vision 上");
            if (vision == null)
            {
                return false;
            }

            _visionLight = vision.GetComponent<Light>();
            _visionTrigger = vision.GetComponent<GuardTrigger>();
            Assert.IsNotNull(_visionLight, "vision 上没有 Light：灯光锥会和真实视野对不上");
            Assert.IsNotNull(_visionTrigger, "vision 上没有 GuardTrigger：扇形里的蟑螂不会报上来");
            if (_visionLight == null || _visionTrigger == null)
            {
                return false;
            }

            _visionLight.type = LightType.Spot;
            _visionLight.range = settings.VisionRange;
            _visionLight.spotAngle = settings.VisionCone * 2f * Mathf.Rad2Deg;

            MeshCollider cone = vision.GetComponent<MeshCollider>();
            if (cone == null)
            {
                cone = vision.gameObject.AddComponent<MeshCollider>();
            }

            cone.sharedMesh = GuardConeMesh.Build(settings.VisionRange, settings.VisionCone, VisionThickness,
                16, "GuardVisionCone");
            cone.convex = true;
            cone.isTrigger = true;

            vision.gameObject.layer = gameObject.layer;
            _visionTrigger.BindGuard(this, GuardPart.Vision);
            _visionCollider = cone;

            _bodyCollider = EnsureBodyProbe();
            return true;
        }

        /// <summary>身体 trigger：贴脸的蟑螂也躲不掉</summary>
        private Collider EnsureBodyProbe()
        {
            Transform bodyTransform = transform.Find("Body");
            if (bodyTransform == null)
            {
                bodyTransform = new GameObject("Body").transform;
                bodyTransform.SetParent(transform, false);
            }

            bodyTransform.gameObject.layer = gameObject.layer;

            SphereCollider body = bodyTransform.GetComponent<SphereCollider>();
            if (body == null)
            {
                body = bodyTransform.gameObject.AddComponent<SphereCollider>();
            }

            body.radius = settings.BodyRadius;
            body.isTrigger = true;

            GuardTrigger trigger = bodyTransform.GetComponent<GuardTrigger>();
            if (trigger == null)
            {
                trigger = bodyTransform.gameObject.AddComponent<GuardTrigger>();
            }

            trigger.BindGuard(this, GuardPart.Body);
            return body;
        }

        /// <summary>绑定外置 plane，拿到行动区域，并按配置铺好巡逻路线</summary>
        private bool SetupRegion()
        {
            if (searchRangePlane == null)
            {
                // prefab 资产引用不到场景物体，所以允许按名字兜底找一次。
                // 只在 Awake 找一次，之后一律以字段为准，避免运行时偷偷换绑到别的 plane。
                searchRangePlane = FindSearchPlane();
            }

            Assert.IsNotNull(searchRangePlane,
                $"Guard 没有绑定行动区域：场景里放一个名为 {SearchRangePlaneName} 的 plane，拖到 searchRangePlane 上");
            if (searchRangePlane == null)
            {
                return false;
            }

            bool valid = GuardRegion.TryFromPlane(searchRangePlane, out _region);
            Assert.IsTrue(valid,
                $"行动区域 plane「{searchRangePlane.name}」上没有 Renderer，或者它在 XZ 上退化了（面积必须大于 0）");
            if (!valid)
            {
                return false;
            }

            // 巡逻路线贴着区域边缘铺，圆角和方向都在这里定下来
            bool pathReady = GuardPatrolPath.TryBuild(_region, settings.PatrolEdgeMargin,
                settings.PatrolCornerRadius, settings.PatrolClockwise, out _patrolPath);
            Assert.IsTrue(pathReady, "行动区域太小 / 内缩量太大，排不出贴着边缘的巡逻路线");

            return pathReady;
        }

        /// <summary>按名字在场景里找行动区域 plane。只在配置缺省时用，找到之后以序列化字段为准</summary>
        private static Transform FindSearchPlane()
        {
            GameObject found = GameObject.Find(SearchRangePlaneName);
            return found != null ? found.transform : null;
        }

        /// <summary>
        /// 取当前生效的行动区域。运行期直接用 Awake 算好的；
        /// 编辑期 Awake 不跑，就从 plane 现读一次（给 gizmo 用，不写回序列化字段）。
        /// </summary>
        private bool TryResolveRegion(out GuardRegion region)
        {
            region = _region;
            if (region.IsValid)
            {
                return true;
            }

            Transform plane = searchRangePlane != null ? searchRangePlane : FindSearchPlane();
            return GuardRegion.TryFromPlane(plane, out region);
        }

        /// <summary>
        /// 定出生点、建归巢距离场。
        /// 出生点取行动区域中心，但中心如果压着墙就挪到最近的空地，否则守卫一出生就卡死。
        /// </summary>
        private void ResolveHome()
        {
            Vector2 center = _region.Center;
            Vector2Int cell = Grid.WorldToGrid(center);

            if (Grid.OverlapsWall(center, settings.WallProbeRadius))
            {
                cell = ResolveNearestFreeCell(cell);
                center = Grid.GridToWorld(cell);
                Debug.LogWarning($"[{GetType().Name}] 行动区域中心压着墙，出生点改到最近的空地 {center}");
            }

            _homeCell = cell;
            _homePosition = center;
            Position = _homePosition;

            RefreshHomeField();
        }

        /// <summary>重建"回到出生点"的距离场。墙可能在白天被改过，所以每次进入夜晚也重建一次</summary>
        protected void RefreshHomeField()
        {
            Assert.IsNotNull(Grid, "GridManager 还没就位就建距离场了");
            if (Grid == null)
            {
                return;
            }

            _homeField = GridDistanceField.Build(Grid, _homeCell);
        }

        /// <summary>从 <paramref name="cell"/> 开始一圈圈向外找最近的空地</summary>
        private Vector2Int ResolveNearestFreeCell(Vector2Int cell)
        {
            // 只往外找这么多圈：再远就不算"区域中心的出生点"了，何况正常地图不会这么挤
            const int maxRadius = 32;

            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        // 只看这一圈，不进到里面去重复检查
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                        {
                            continue;
                        }

                        Vector2Int candidate = new Vector2Int(cell.x + x, cell.y + y);
                        if (Grid.IsBlocked(candidate))
                        {
                            continue;
                        }

                        // 格子空还不够，守卫有身体半径，得站得下
                        if (!Grid.OverlapsWall(Grid.GridToWorld(candidate), settings.WallProbeRadius))
                        {
                            return candidate;
                        }
                    }
                }
            }

            Assert.IsTrue(false, $"出生点 {cell} 周围 {maxRadius} 格内没有能站人的格子，守卫会一出生就卡住");
            return cell;
        }

        #endregion

        #region 夜间门控

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            if (evt.NewPhase == GamePhase.Night)
            {
                // 白天可能动过墙，进夜晚先重建归巢距离场再开工
                RefreshHomeField();
                SetNightActive(true);
            }
            else
            {
                SetNightActive(false);
            }
        }

        /// <summary>
        /// 白天整个守卫停摆：不移动、不感知，视野灯也关掉。
        /// 停摆时清空感知名单，否则名单里会残留白天撞进来的蟑螂。
        /// </summary>
        private void SetNightActive(bool active)
        {
            // 配置没搭好时组件已经被停掉，这里没有任何东西需要开关
            if (!_rigReady || _isNight == active)
            {
                return;
            }

            _isNight = active;

            _visionLight.enabled = active;
            _visionCollider.enabled = active;
            _bodyCollider.enabled = active;

            if (active)
            {
                _engageLinger = 0f;
                _stallTimer = 0f;
                _returningHome = false;
                RejoinPatrolPath();
            }
            else
            {
                CockroachesInSight.Clear();
                CockroachesTouching.Clear();
            }

            OnNightActiveChanged(active);
        }

        /// <summary>子类钩子：日夜切换时清理自己的运行时状态（冲刺、布药计时之类）</summary>
        protected virtual void OnNightActiveChanged(bool active)
        {
        }

        #endregion

        #region 感知（由 GuardTrigger 转发）

        internal void OnPartEnter(Collider other, GuardPart part)
        {
            CockroachAgent cockroach = FindCockroach(other);
            if (cockroach == null)
            {
                return;
            }

            SetOf(part).Add(cockroach);
        }

        internal void OnPartExit(Collider other, GuardPart part)
        {
            CockroachAgent cockroach = FindCockroach(other);
            if (cockroach == null)
            {
                return;
            }

            SetOf(part).Remove(cockroach);
        }

        private HashSet<CockroachAgent> SetOf(GuardPart part)
        {
            return part == GuardPart.Vision ? CockroachesInSight : CockroachesTouching;
        }

        private CockroachAgent FindCockroach(Collider other)
        {
            if (other == null)
            {
                return null;
            }

            if (((1 << other.gameObject.layer) & _cockroachMask.value) == 0)
            {
                return null;
            }

            return other.GetComponentInParent<CockroachAgent>();
        }

        /// <summary>
        /// 只清已经被销毁的。进巢的蟑螂不能从名单里删：
        /// 删了它出巢时不会再触发 OnTriggerEnter，守卫就永远发现不了它。
        /// 取样时用 <see cref="IsTargetable"/> 过滤即可。
        /// </summary>
        private void PrunePerception()
        {
            CockroachesInSight.RemoveWhere(cockroach => cockroach == null);
            CockroachesTouching.RemoveWhere(cockroach => cockroach == null);
        }

        /// <summary>能当目标的蟑螂：还活着、没躲在巢里</summary>
        protected static bool IsTargetable(CockroachAgent cockroach)
        {
            return cockroach != null && !cockroach.IsDead && !cockroach.IsInside;
        }

        /// <summary>
        /// 视野 / 身体范围内最近的一只可攻击蟑螂。
        /// <paramref name="range"/> 用 Infinity 表示不限距。
        /// 从视野名单里取的必须再验一次视线：视野 trigger 是个锥形碰撞体，墙挡不住它。
        /// 贴身的（身体 trigger 报上来的）本身就贴在一起，不需要再验视线。
        /// </summary>
        protected CockroachAgent PickNearestCockroach(Vector2 from, float range, bool includeTouching)
        {
            float bestSqr = range * range;
            CockroachAgent best = null;

            foreach (CockroachAgent cockroach in CockroachesInSight)
            {
                Consider(cockroach, needsLineOfSight: true);
            }

            if (includeTouching)
            {
                foreach (CockroachAgent cockroach in CockroachesTouching)
                {
                    Consider(cockroach, needsLineOfSight: false);
                }
            }

            return best;

            void Consider(CockroachAgent cockroach, bool needsLineOfSight)
            {
                if (!IsTargetable(cockroach))
                {
                    return;
                }

                if (needsLineOfSight && !HasLineOfSight(cockroach.Position))
                {
                    return;
                }

                float sqr = (cockroach.Position - from).sqrMagnitude;
                if (sqr > bestSqr)
                {
                    return;
                }

                bestSqr = sqr;
                best = cockroach;
            }
        }

        /// <summary>
        /// 目标在不在视野里：距离够近、在视野锥内、并且中间没有墙。
        /// 墙用网格的 DDA 判，和蟑螂自己的视线判定是同一套数据。
        /// </summary>
        protected bool CanSee(Vector2 target)
        {
            Vector2 delta = target - Position;
            if (delta.sqrMagnitude > settings.VisionRange * settings.VisionRange)
            {
                return false;
            }

            float angleToTarget = Mathf.Atan2(delta.y, delta.x);
            if (Mathf.Abs(NormAng(angleToTarget - _angle)) > settings.VisionCone)
            {
                return false;
            }

            return HasLineOfSight(target);
        }

        /// <summary>从守卫到目标之间有没有墙挡着</summary>
        protected bool HasLineOfSight(Vector2 target)
        {
            return Grid.LosClear(Position, target);
        }

        /// <summary>
        /// 玩家（夜间是蟑螂形态）在世界 XZ 上的位置。场景里没有玩家时返回 false。
        /// 只回答"玩家在哪"，怎么打玩家是子类的事。
        /// </summary>
        protected static bool TryGetPlayerPosition(out Vector2 position)
        {
            var player = GameManager.Player;
            if (player == null)
            {
                position = default;
                return false;
            }

            position = CockroachAgent.FlatOf(player.transform.position);
            return true;
        }

        #endregion

        #region 决策（子类覆写点）

        /// <summary>
        /// 子类唯一要想清楚的事：这一帧想干什么。
        /// 只写意图，避障 / 转向 / 位移 / 区域约束由基类统一处理。
        /// 基类的默认实现就是"巡逻"，所以 <see cref="Guard"/> 本身也能当普通守卫用。
        /// </summary>
        protected virtual GuardIntent DecideIntent(float dt)
        {
            return PatrolIntent(dt);
        }

        /// <summary>
        /// 决定本帧真正跑哪个决策。优先级：
        /// 1. 子类说"在交战"——放行，允许暂离区域（回程让位给发现新目标）；
        /// 2. 在区域外又没有焦点——沿归巢距离场往回走（目标仍是区域中心）；
        /// 3. 其余——交还给子类巡逻。
        ///
        /// 回程的终点不是"中心"，而是"重新进入矩形"：一进来就从最近的边缘点接上巡逻路线，
        /// 之后沿着边缘走，不再专门跑回中心。
        /// </summary>
        private GuardIntent DecideGuardIntent(float dt)
        {
            if (_returningHome && _region.Contains(Position))
            {
                _returningHome = false;
                RejoinPatrolPath();
            }

            GuardIntent intent = DecideIntent(dt);

            if (intent.Stance == GuardStance.Engage)
            {
                _returningHome = false;
                return intent;
            }

            if (_stance == GuardStance.Engage)
            {
                // 刚结束交战：就地在最近的边缘点接上路线，别顺着记忆里的旧位置倒着走
                RejoinPatrolPath();
            }

            // 宽限期用上一帧的立场来算：刚丢焦点的那一下不该立刻掉头
            if (_returningHome || (!InEngageLinger && !_region.Contains(Position)))
            {
                _returningHome = true;
                return HomewardIntent();
            }

            return intent;
        }

        /// <summary>
        /// 回区域：沿距离场走，绕过墙，而不是一路往中心怼。
        /// 距离场在这里不可用时（守卫被丢进了和出生点不连通的死角）由 Descend 退化成直线朝向，
        /// 具体卡住由避障和卡住检测兜底。
        /// </summary>
        private GuardIntent HomewardIntent()
        {
            // 沿场往前看一小段再瞄准，而不是盯着相邻格子：格子很小时逐格瞄准会左右抖
            Vector2 cellSize = Grid.CellSize;
            float minCell = Mathf.Max(Mathf.Min(cellSize.x, cellSize.y), 1e-4f);
            int lookaheadCells = Mathf.Clamp(Mathf.CeilToInt(HomeLookahead / minCell), 2, 64);

            Vector2 waypoint = GridDistanceField.Descend(Grid, _homeField, Position, _homePosition, lookaheadCells);
            return new GuardIntent(GuardStance.Patrol, AngleTo(Position, waypoint), settings.PatrolSpeed, settings.TurnSpeed);
        }

        /// <summary>焦点消失后的宽限期还没用完</summary>
        private bool InEngageLinger => _engageLinger > 0f;

        private void UpdateStance(GuardStance stance, float dt)
        {
            _stance = stance;

            if (stance == GuardStance.Engage)
            {
                _engageLinger = settings.EngageLinger;
            }
            else if (_engageLinger > 0f)
            {
                _engageLinger = Mathf.Max(0f, _engageLinger - dt);
            }
        }

        /// <summary>
        /// 巡逻：沿贴着区域边缘的固定路线走（顺 / 逆时针见 <see cref="GuardSettings.PatrolClockwise"/>）。
        /// 瞄准的是路线上"往前看一点"的位置，所以圆角处会自己提前开始拐；
        /// 被挤离路线（追击、逃离之后）也会顺着这个目标自然回到线上。
        /// </summary>
        protected GuardIntent PatrolIntent(float dt)
        {
            _pathDistance = _patrolPath.Wrap(_pathDistance + settings.PatrolSpeed * dt);

            float lookAhead = Mathf.Max(0.05f, settings.PatrolLookAhead);
            _patrolPath.Sample(_pathDistance + lookAhead, out Vector2 target, out _);

            return new GuardIntent(GuardStance.Patrol, AngleTo(Position, target), settings.PatrolSpeed, settings.TurnSpeed);
        }

        /// <summary>就地接上巡逻路线：取离当前位置最近的那一点，不从起点重来</summary>
        private void RejoinPatrolPath()
        {
            _pathDistance = _patrolPath.Project(Position);
        }

        #endregion

        #region 避障 / 位移

        /// <summary>
        /// 沿意图方向探路，撞墙就左右展开找能走的方向。
        /// 全都堵死时保持原方向：位移那边会挡住，不要为了避障掉头冲回威胁或者冲回墙里。
        /// </summary>
        protected virtual float ApplyAvoidance(Vector2 position, float desired)
        {
            if (!IsBlocked(position, desired))
            {
                return desired;
            }

            for (int i = 1; i <= AvoidanceSideSteps; i++)
            {
                float offset = AvoidanceStep * i;

                float left = NormAng(desired - offset);
                if (!IsBlocked(position, left))
                {
                    return left;
                }

                float right = NormAng(desired + offset);
                if (!IsBlocked(position, right))
                {
                    return right;
                }
            }

            return desired;
        }

        /// <summary>这个方向前方 <see cref="GuardSettings.WallLook"/> 距离内有没有墙</summary>
        protected bool IsBlocked(Vector2 position, float angle)
        {
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 origin = new Vector3(position.x, transform.position.y, position.y);

            return Physics.SphereCast(origin, settings.WallProbeRadius, direction, out _,
                settings.WallLook, _wallMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>转向 + 位移。把意图落到 transform 上，这是唯一写位置的地方</summary>
        private void ApplyMotion(GuardIntent intent, float dt)
        {
            Vector2 position = Position;

            _angle = RotateTowards(_angle, ApplyAvoidance(position, intent.DesiredAngle), intent.TurnSpeed * dt);
            transform.rotation = AngleToRotation(_angle);

            _animator.SetBool("IsMoving", true);
            if (intent.Speed <= 0f)
            {
                _stallTimer = 0f;
                _animator.SetBool("IsMoving", false);
                return;
            }
            // ApplyMotion 里，intent.Speed 就是这一帧想走多快
            float mul = intent.Speed > 0f ? Mathf.Clamp(intent.Speed / settings.PatrolSpeed, 1.0f, 2.5f) : 1f;
            if (!Mathf.Approximately(_animator.speed, mul)) _animator.speed = mul;
            

            Vector2 delta = DirOfAngle(_angle) * (intent.Speed * dt);
            Vector2 target = ClampToArea(intent.Stance, position, position + delta);

            Position = SlideAlongWalls(position, target);
            TrackStall(position, delta.magnitude, dt);
        }

        /// <summary>
        /// 巡逻时不能出行动区域；交战 / 回程时放行（"发现蟑螂后可以暂离区域"）。
        /// 但无论哪种情况都不许走出网格。
        /// </summary>
        private Vector2 ClampToArea(GuardStance stance, Vector2 from, Vector2 target)
        {
            Vector2 clamped = target;

            // 已经在区域外时不再夹：回程要能穿回区域，而不是被按在边界上
            if (stance == GuardStance.Patrol && _region.Contains(from))
            {
                clamped = _region.Clamp(clamped);
            }

            // 不管哪种立场都不许走出网格，这层放最后
            return Grid.ClampToWorld(clamped);
        }

        /// <summary>
        /// 挡墙：kinematic 刚体不会被静态墙推开，所以这里必须自己挡。
        /// 整体走不过去就把位移拆成两个轴分别试（表现就是贴着墙滑行），都不行就原地不动。
        /// </summary>
        private Vector2 SlideAlongWalls(Vector2 from, Vector2 to)
        {
            if (!Grid.OverlapsWall(to, settings.WallProbeRadius))
            {
                return to;
            }

            Vector2 slideX = new Vector2(to.x, from.y);
            if (!Grid.OverlapsWall(slideX, settings.WallProbeRadius))
            {
                return slideX;
            }

            Vector2 slideZ = new Vector2(from.x, to.y);
            if (!Grid.OverlapsWall(slideZ, settings.WallProbeRadius))
            {
                return slideZ;
            }

            return from;
        }

        /// <summary>
        /// 想走却几乎没挪动就算卡住。卡住时必须让子类有机会换目标，
        /// 否则守卫会顶着一面墙把整段冲刺耗完。
        /// </summary>
        private void TrackStall(Vector2 from, float expected, float dt)
        {
            if (expected <= 1e-5f)
            {
                return;
            }

            // 这里只比较"走了多远"，方向不重要：贴墙滑行时位移会明显小于预期
            float travelled = (Position - from).magnitude;
            if (travelled >= expected * StallProgressRatio)
            {
                _stallTimer = 0f;
                return;
            }

            _stallTimer += dt;
            if (_stallTimer < settings.StallTimeout)
            {
                return;
            }

            _stallTimer = 0f;
            OnMovementStalled();
        }

        /// <summary>卡住的钩子。基类换一个更靠前的路线目标；子类可以顺手收掉正在做的动作</summary>
        protected virtual void OnMovementStalled()
        {
            // 巡逻卡住（贴着墙角 / 路线被家具挡住）：沿路线往前挪一段，换一个更容易走到的目标点
            _pathDistance = _patrolPath.Wrap(_pathDistance + StallPathNudge);
        }

        #endregion

        #region 工具

        protected static float NormAng(float angle)
        {
            return Mathf.Repeat(angle + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
        }

        /// <summary>内部角度 → 模型朝向。0 = 世界 +X，Unity 的 forward 是 +Z，所以 yaw 要补 90°</summary>
        public static Quaternion AngleToRotation(float angle)
        {
            return Quaternion.Euler(0f, 90f - angle * Mathf.Rad2Deg, 0f);
        }

        public static float RotationToAngle(float yawDegrees)
        {
            return NormAng((90f - yawDegrees) * Mathf.Deg2Rad);
        }

        /// <summary>角度 → 单位方向（0 = 世界 +X，+π/2 = 世界 +Z）</summary>
        public static Vector2 DirOfAngle(float angle)
        {
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>从 <paramref name="from"/> 指向 <paramref name="to"/> 的朝向角</summary>
        protected static float AngleTo(Vector2 from, Vector2 to)
        {
            return Mathf.Atan2(to.y - from.y, to.x - from.x);
        }

        protected static float RotateTowards(float current, float target, float maxDelta)
        {
            return NormAng(current + Mathf.Clamp(NormAng(target - current), -maxDelta, maxDelta));
        }

        #endregion

        #region 调试绘制

        protected virtual void OnDrawGizmosSelected()
        {
            DrawVisionGizmo();
            DrawRegionGizmo();
            DrawPatrolPathGizmo();
        }

        private void DrawVisionGizmo()
        {
            float angle = Application.isPlaying ? _angle : RotationToAngle(transform.eulerAngles.y);

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 1f);
            Vector3 origin = transform.position;
            const int steps = 16;

            for (int i = 0; i < steps; i++)
            {
                float from = angle + Mathf.Lerp(-settings.VisionCone, settings.VisionCone, i / (float)steps);
                float to = angle + Mathf.Lerp(-settings.VisionCone, settings.VisionCone, (i + 1) / (float)steps);
                Vector3 p0 = origin + new Vector3(Mathf.Cos(from), 0f, Mathf.Sin(from)) * settings.VisionRange;
                Vector3 p1 = origin + new Vector3(Mathf.Cos(to), 0f, Mathf.Sin(to)) * settings.VisionRange;

                Gizmos.DrawLine(p0, p1);
                if (i == 0) Gizmos.DrawLine(origin, p0);
                if (i == steps - 1) Gizmos.DrawLine(origin, p1);
            }
        }

        private void DrawRegionGizmo()
        {
            // 编辑期还没有 _region（Awake 不跑），TryResolveRegion 会从 plane 现读一次
            if (!TryResolveRegion(out GuardRegion region))
            {
                return;
            }

            float y = transform.position.y;
            Vector3 a = new Vector3(region.Min.x, y, region.Min.y);
            Vector3 b = new Vector3(region.Max.x, y, region.Min.y);
            Vector3 c = new Vector3(region.Max.x, y, region.Max.y);
            Vector3 d = new Vector3(region.Min.x, y, region.Max.y);

            Gizmos.color = _isNight ? new Color(0.4f, 1f, 0.6f, 0.9f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        /// <summary>
        /// 画巡逻路线。运行期直接用已经建好的路线；编辑期没有 Awake，就用当前 plane + 配置现建一条，
        /// 方便在场景里调内缩量、圆角和顺 / 逆时针。
        /// </summary>
        private void DrawPatrolPathGizmo()
        {
            GuardPatrolPath path = _patrolPath;

            if (path == null)
            {
                if (!TryResolveRegion(out GuardRegion region) ||
                    !GuardPatrolPath.TryBuild(region, settings.PatrolEdgeMargin, settings.PatrolCornerRadius,
                        settings.PatrolClockwise, out path))
                {
                    return;
                }
            }

            float y = transform.position.y;
            Gizmos.color = _isNight ? new Color(0.4f, 1f, 0.6f, 0.9f) : new Color(0.55f, 0.8f, 1f, 0.6f);

            const int steps = 96;
            Vector2 previous = path.PositionAt(0f);
            for (int i = 1; i <= steps; i++)
            {
                Vector2 next = path.PositionAt(path.Length * i / steps);
                Gizmos.DrawLine(new Vector3(previous.x, y, previous.y), new Vector3(next.x, y, next.y));
                previous = next;
            }

            // 再画几个箭头，一眼能看出是顺时针还是逆时针
            Gizmos.color = _isNight ? new Color(1f, 0.9f, 0.3f, 0.95f) : new Color(0.9f, 0.85f, 0.4f, 0.7f);
            const int arrowCount = 4;
            for (int i = 0; i < arrowCount; i++)
            {
                float distance = path.Length * (i + 0.5f) / arrowCount;
                path.Sample(distance, out Vector2 position, out Vector2 tangent);

                Vector3 tip = new Vector3(position.x, y, position.y);
                Vector3 back = tip - new Vector3(tangent.x, 0f, tangent.y) * 0.5f;
                Vector3 side = Vector3.Cross(Vector3.up, new Vector3(tangent.x, 0f, tangent.y)) * 0.25f;

                Gizmos.DrawLine(tip, back + side);
                Gizmos.DrawLine(tip, back - side);
            }
        }

        #endregion
    }
}
