using System.Collections.Generic;
using TapTapGameJam.Core;
using UnityEngine;
using Random = UnityEngine.Random;

// 这个名字在本文件里是"实体"，而 TapTapGameJam.Cockroach 是命名空间 —— 直接写 Cockroach
// 编译器一律优先当成命名空间（CS0118）。所以统一走这个别名。
// 别名在同一个命名空间里跨文件可见，KillerGuard / CleanerGuard 也用它。
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Cockroach
{
    /// <summary>守卫的可调数值。参考 ant_test 的 guard* 设置，单位换成世界单位。</summary>
    [System.Serializable]
    public class GuardSettings
    {
        [Header("视野")]
        public float VisionRange = 6f;          // 参考 230px
        public float VisionCone = 0.4f;         // 半角 rad
        public float ScanSpeed = 0.5f;          // 巡逻扫视角速度 rad/s
        public float ScanIntervalMin = 4.0f;    // 换扫描方向的间隔
        public float ScanIntervalMax = 8.5f;

        [Header("移动")]
        public float PatrolSpeed = 1.5f;        // 巡逻速度
        public float DashSpeed = 8.8f;          // 冲刺速度
        public float TurnSpeed = 4.5f;          // rad/s
        public float WallLook = 1.5f;           // 障碍预判距离
        public float BodyRadius = 0.4f;         // 击杀判定球半径

        [Header("冲刺")]
        public float DashDuration = 3.2f;       // 一次冲刺最多持续多久
        public float DashCooldown = 6f;         // 击杀后的冷却
    }

    /// <summary>
    /// 守卫基类。搭好所有变体共用的那套"身体"：
    ///
    /// - 视野：一盏 Spot Light + 一个严格同形的扇形 trigger（<see cref="GuardTrigger"/> 转发碰撞事件）；
    /// - 感知：<see cref="CockroachesInSight"/> / <see cref="CockroachesTouching"/> 两份名单；
    /// - 基础行动：没目标时慢慢左右扫视（<see cref="Scan"/>）、撞墙前提前偏转（<see cref="ApplyAvoidance"/>）、
    ///   按 <see cref="DesiredAngle"/> 平滑转向、贴着墙滑行地推进（<see cref="Move"/>）；
    /// - 每帧在主循环里依次调用 <see cref="Decide"/> 与 <see cref="Move"/>。
    ///
    /// 变体只需要覆写这两个钩子：
    /// <list type="bullet">
    /// <item><see cref="Decide"/>：只负责写 <see cref="DesiredAngle"/>（我想朝哪边走），
    /// 以及派生类自己的计时器/状态机。<see cref="KillerGuard"/>（追击扑杀）与
    /// <see cref="CleanerGuard"/>（见蟑螂就逃）就是这么来的。</item>
    /// <item><see cref="Move"/>：负责推进与避障，默认实现是"沿 <see cref="CurrentSpeed"/> 前进 +
    /// 网格边界夹紧"，需要特殊移动方式（瞬移、绕圈）的子类再覆写。</item>
    /// </list>
    ///
    /// 本类自己就是一套最朴素的变体：随机扫视 + 巡逻，看见蟑螂不做任何反应。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Guard : MonoBehaviour
    {
        /// <summary>
        /// 视野体的竖直厚度。灯是挂在守卫身上的（离地有一点高度），
        /// 厚度给足才盖得住贴地走的蟑螂碰撞体。
        /// </summary>
        protected const float VisionThickness = 2.5f;

        [Header("数值")]
        public GuardSettings settings = new GuardSettings();

        [Header("视野object")]
        [Tooltip("挂 Spot Light 和扇形 trigger 的那个子物体。留空则自动取身上第一盏灯")]
        public Transform vision;

        /// <summary>当前朝向，0 = 世界 +x</summary>
        [Header("运行时状态（只读）")]
        [SerializeField] protected float _angle;

        /// <summary>
        /// 扫视/当前想要达到的角度，由 <see cref="DesiredAngle"/> 的 setter 一直同步成最新值。
        /// 单独留一个字段是因为 <see cref="FixedUpdate"/> 每帧开头要用它把"上帧意图"摆回
        /// <see cref="_desiredAngle"/>，这样没目标时守卫才是保持朝向，而不是停转。
        /// </summary>
        protected float _targetAngle;

        /// <summary>本帧决定的方向。<see cref="Decide"/> 写它，<see cref="ApplyAvoidance"/> 可能改写它</summary>
        protected float _desiredAngle;

        protected float _scanDir = 1f;
        protected float _scanTimer;

        /// <summary>避障让行冷却，避免同一堵墙每帧都触发一次转向</summary>
        protected float _avoidCd;

        protected Rigidbody _rb;

        private Light _visionLight;
        private GuardTrigger _visionTrigger;

        private LayerMask _cockroachMask;
        private LayerMask _wallMask;

        /// <summary>视野 trigger 里当前的蟑螂</summary>
        protected readonly HashSet<CockroachAgent> CockroachesInSight = new HashSet<CockroachAgent>();

        /// <summary>身体 trigger 里当前的蟑螂（贴脸了）</summary>
        protected readonly HashSet<CockroachAgent> CockroachesTouching = new HashSet<CockroachAgent>();

        #region 对外状态

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

        /// <summary>世界 XZ 平面上的单位前向</summary>
        public Vector2 Forward => new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle));

        public float Angle => _angle;

        protected LayerMask CockroachMask => _cockroachMask;

        protected LayerMask WallMask => _wallMask;

        #endregion

        #region 生命周期

        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;

            _angle = _targetAngle = _desiredAngle = RotationToAngle(transform.eulerAngles.y);
            _scanTimer = Random.Range(settings.ScanIntervalMin, settings.ScanIntervalMax);

            _cockroachMask = LayerMask.GetMask("Cockroach");
            _wallMask = LayerMask.GetMask("Wall");

            if (vision == null)
            {
                Light found = GetComponentInChildren<Light>(true);
                if (found != null) vision = found.transform;
            }

            Debug.Assert(vision != null, "Guard 没找到视野物体：请在 vision 字段上挂那个 Spot Light（或给守卫加一盏灯）");
            if (vision == null) return;

            _visionLight = vision.GetComponent<Light>();
            _visionTrigger = vision.GetComponent<GuardTrigger>();

            Debug.Assert(_visionLight != null, "vision 上没有 Light 组件");
            Debug.Assert(_visionTrigger != null, "vision 上没有 GuardTrigger 组件，挂一个上去");
            if (_visionLight == null || _visionTrigger == null) return;

            // 灯光锥必须和检测锥严格一致，否则玩家看到的光圈不等于守卫真正看得见的范围
            _visionLight.type = LightType.Spot;
            _visionLight.range = settings.VisionRange;
            _visionLight.spotAngle = settings.VisionCone * 2f * Mathf.Rad2Deg;

            EnsureRig();
        }

        /// <summary>把视野扇形和身体两个 trigger 搭好，省去手搓锥体碰撞。</summary>
        protected virtual void EnsureRig()
        {
            // 检测锥沿守卫的 +Z 铺开，和那盏 Spot Light 的朝向一致
            MeshCollider cone = vision.GetComponent<MeshCollider>();
            if (cone == null) cone = vision.gameObject.AddComponent<MeshCollider>();
            cone.sharedMesh = BuildConeMesh(settings.VisionRange, settings.VisionCone, VisionThickness);
            cone.convex = true;
            cone.isTrigger = true;

            vision.gameObject.layer = gameObject.layer;
            _visionTrigger.BindGuard(this, true);

            Transform bodyT = transform.Find("Body");
            if (bodyT == null)
            {
                bodyT = new GameObject("Body").transform;
                bodyT.SetParent(transform, false);
            }

            bodyT.gameObject.layer = gameObject.layer;
            SphereCollider body = bodyT.GetComponent<SphereCollider>();
            if (body == null) body = bodyT.gameObject.AddComponent<SphereCollider>();
            body.radius = settings.BodyRadius;
            body.isTrigger = true;

            GuardTrigger bodyTrigger = bodyT.GetComponent<GuardTrigger>();
            if (bodyTrigger == null) bodyTrigger = bodyT.gameObject.AddComponent<GuardTrigger>();
            bodyTrigger.BindGuard(this, false);
        }

        protected virtual void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            TickTimers(dt);

            // 只清掉被销毁的；还在 trigger 里但 IsInside 的由子类自己过滤。
            // 不能在这里按 IsInside 剔除 —— 否则蟑螂进巢再出来时不会再触发 OnTriggerEnter，就永远发现不了它了
            CockroachesInSight.RemoveWhere(c => c == null);
            CockroachesTouching.RemoveWhere(c => c == null);

            // 每帧先摆成"保持原样"（扫视目标角），再让 Decide 决定要不要改
            _desiredAngle = _targetAngle;

            // 1. 决策：我要朝哪个方向
            Decide(dt, TargetInSight());

            // 2. 避障：往那个方向走会不会撞墙
            ApplyAvoidance();

            // 3. 转向
            _angle = RotateTowards(_angle, DesiredAngle, EffectiveTurnSpeed * dt);
            transform.rotation = AngleToRotation(_angle);

            // 4. 移动
            Move(CurrentSpeed, dt);
        }

        /// <summary>每帧递减各类冷却。子类的计时器自己在 <see cref="Decide"/> 里走。</summary>
        protected virtual void TickTimers(float dt)
        {
            if (_avoidCd > 0f) _avoidCd -= dt;
        }

        #endregion

        #region 碰撞（由 GuardTrigger 转发）

        internal void OnPartEnter(Collider other, bool isVision)
        {
            if (!IsCockroach(other)) return;

            CockroachAgent c = other.GetComponentInParent<CockroachAgent>();
            if (c == null) return;

            if (isVision) CockroachesInSight.Add(c);
            else CockroachesTouching.Add(c);

            OnCockroachEnter(c, isVision);
        }

        internal void OnPartExit(Collider other, bool isVision)
        {
            if (other == null) return;

            CockroachAgent c = other.GetComponentInParent<CockroachAgent>();
            if (c == null) return;

            if (isVision) CockroachesInSight.Remove(c);
            else CockroachesTouching.Remove(c);

            OnCockroachExit(c, isVision);
        }

        /// <summary>有蟑螂进出某个 trigger 时的回调，给子类挂表现/音效用</summary>
        protected virtual void OnCockroachEnter(CockroachAgent cockroach, bool isVision) { }

        protected virtual void OnCockroachExit(CockroachAgent cockroach, bool isVision) { }

        protected bool IsCockroach(Collider other)
        {
            return other != null && ((1 << other.gameObject.layer) & _cockroachMask) != 0;
        }

        #endregion

        #region 决策 / 移动（子类覆写点）

        /// <summary>
        /// 决策钩子，每帧执行一次。基类实现就是"没目标时左右扫视"。
        /// 覆写时请只写 <see cref="DesiredAngle"/>（想朝哪边走）与子类自己的状态，
        /// 不要直接改 transform —— 转向和位移由基类统一处理。
        /// </summary>
        /// <param name="dt">本帧步长</param>
        /// <param name="target">视野里当前锁定的蟑螂，可能为 null</param>
        protected virtual void Decide(float dt, CockroachAgent target)
        {
            if (target == null)
            {
                Scan(dt);
            }
            else
            {
                // 基类不追击，只是盯住它
                DesiredAngle = AngleTo(Position, target.Position);
            }
        }

        /// <summary>
        /// 移动钩子，每帧执行一次。基类实现：沿当前朝向 <see cref="_angle"/> 前进，
        /// 坐标夹在网格范围内（网格外一律当墙处理，蟑螂那边也是同一套规则）。
        /// </summary>
        protected virtual void Move(float speed, float dt)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            grid.EnsureFootprint();

            Vector2 p = Position + DirOfAngle(_angle) * (speed * dt);

            // 别走出网格
            Vector2 o = grid.WorldOrigin;
            Vector2 s = grid.WorldSpan;
            p.x = Mathf.Clamp(p.x, o.x, o.x + s.x);
            p.y = Mathf.Clamp(p.y, o.y, o.y + s.y);

            Position = p;
        }

        /// <summary>本帧移动速度。子类用它切换巡逻 / 冲刺 / 逃跑。</summary>
        protected virtual float CurrentSpeed => settings.PatrolSpeed;

        /// <summary>本帧转向角速度（rad/s）</summary>
        protected virtual float EffectiveTurnSpeed => settings.TurnSpeed;

        /// <summary>
        /// 子类覆写它来改变"本帧想朝哪边走"。
        /// 赋值会同步 <see cref="_targetAngle"/>：不然子类（如追击中的 KillerGuard）改完方向后，
        /// 下一帧 <see cref="FixedUpdate"/> 又把 <see cref="_desiredAngle"/> 摆回老角度，转向会先掉头再转回来。
        /// </summary>
        protected virtual float DesiredAngle
        {
            get => _desiredAngle;
            set
            {
                _desiredAngle = NormAng(value);
                _targetAngle = _desiredAngle;
            }
        }

        /// <summary>当前锁定的目标：默认取视野里最近的一只。子类可换筛选规则。</summary>
        protected virtual CockroachAgent TargetInSight()
        {
            return PickNearest(CockroachesInSight);
        }

        /// <summary>视野里最近的、不在巢里的蟑螂</summary>
        protected CockroachAgent PickNearest(IEnumerable<CockroachAgent> candidates)
        {
            CockroachAgent best = null;
            float bestSqr = float.MaxValue;
            Vector2 pos = Position;

            foreach (CockroachAgent c in candidates)
            {
                if (c == null || c.IsInside) continue;

                float d = (c.Position - pos).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = c;
                }
            }

            return best;
        }

        /// <summary>没目标时：慢慢转着扫。</summary>
        protected virtual void Scan(float dt)
        {
            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanDir = Random.value < 0.5f ? -1f : 1f;
                _scanTimer = Random.Range(settings.ScanIntervalMin, settings.ScanIntervalMax);
            }

            DesiredAngle = _targetAngle + _scanDir * settings.ScanSpeed * dt;
        }

        #endregion

        #region 避障

        /// <summary>
        /// 前方有墙就往两侧偏，两边都堵就掉头。
        /// 探路方向写死成"当前方向 ±0.7rad"，想改探路方式（多探几条、按网格查）就覆写它。
        /// </summary>
        protected virtual void ApplyAvoidance()
        {
            if (_avoidCd > 0f) return;

            Vector3 fwd = new Vector3(Mathf.Cos(DesiredAngle), 0f, Mathf.Sin(DesiredAngle));

            // Ignore：守卫自己的视野/身体都是 trigger，不排除的话会自己撞自己
            if (!Physics.SphereCast(transform.position, WallProbeRadius, fwd, out _, settings.WallLook, _wallMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            float left = DesiredAngle - 0.7f;
            float right = DesiredAngle + 0.7f;
            bool lOk = !Blocked(left);
            bool rOk = !Blocked(right);

            float next;
            if (lOk && !rOk) next = left;
            else if (rOk && !lOk) next = right;
            else if (lOk && rOk) next = DesiredAngle + (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.6f, 1.1f);
            else next = DesiredAngle + Mathf.PI * (Random.value < 0.5f ? -1f : 1f);

            DesiredAngle = next;
            _avoidCd = 0.45f;
            _scanTimer = Random.Range(0.8f, 2f);

            OnAvoided();
        }

        /// <summary>避障生效后的回调，给子类挂音效/表现</summary>
        protected virtual void OnAvoided() { }

        protected bool Blocked(float angle)
        {
            Vector3 d = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            return Physics.SphereCast(transform.position, WallProbeRadius, d, out _, settings.WallLook * 0.6f, _wallMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>避障探测球半径。默认用身体半径，调大 = 提前更远开始绕。</summary>
        protected virtual float WallProbeRadius => settings.BodyRadius;

        #endregion

        #region 工具

        protected static float NormAng(float a) => Mathf.Repeat(a + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;

        // 内部角度沿用蟑螂那套：0 = 世界 +x，往 +z 为正。
        // 而 Unity 的 forward 是 +z，所以绕 Y 的旋转要补 90°。
        public static Quaternion AngleToRotation(float angle) => Quaternion.Euler(0f, 90f - angle * Mathf.Rad2Deg, 0f);

        public static float RotationToAngle(float yawDegrees) => NormAng((90f - yawDegrees) * Mathf.Deg2Rad);

        public static Vector2 DirOfAngle(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        protected static float AngleTo(Vector2 from, Vector2 to) => Mathf.Atan2(to.y - from.y, to.x - from.x);

        protected static float RotateTowards(float cur, float target, float maxDelta)
            => NormAng(cur + Mathf.Clamp(NormAng(target - cur), -maxDelta, maxDelta));

        /// <summary>
        /// 生成一个薄楔形当视野体：沿 +Z 铺开、水平躺着，竖直方向有 thickness 厚。
        /// convex MeshCollider 只取顶点做凸包，所以厚度给足就行，不用管三角形绕序。
        /// </summary>
        protected static Mesh BuildConeMesh(float range, float halfAngle, float thickness, int segments = 16)
        {
            float h = thickness * 0.5f;
            var verts = new List<Vector3>(4 + segments * 2) { new Vector3(0f, -h, 0f), new Vector3(0f, h, 0f) };
            var tris = new List<int>(segments * 12);

            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                float x = Mathf.Sin(a) * range;
                float z = Mathf.Cos(a) * range;
                verts.Add(new Vector3(x, -h, z));
                verts.Add(new Vector3(x, h, z));
            }

            for (int i = 0; i < segments; i++)
            {
                int b0 = 2 + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                tris.AddRange(new[] { 0, b1, b0 });                 // 底
                tris.AddRange(new[] { 1, t0, t1 });                 // 顶
                tris.AddRange(new[] { b0, b1, t1, b0, t1, t0 });    // 弧面
            }

            int last = 2 + segments * 2;
            tris.AddRange(new[] { 0, 1, 3, 0, 3, 2 });                  // 起始侧壁
            tris.AddRange(new[] { 0, last, last + 1, 0, last + 1, 1 }); // 结束侧壁

            var mesh = new Mesh { name = "GuardVisionCone" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            // 编辑模式下 _angle 是 0，用物体自己的 yaw 反推，免得 gizmo 和实际朝向对不上
            float angle = Application.isPlaying ? _angle : RotationToAngle(transform.eulerAngles.y);

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 1f);
            Vector3 o = transform.position;
            int steps = 16;

            for (int i = 0; i < steps; i++)
            {
                float a0 = angle + Mathf.Lerp(-settings.VisionCone, settings.VisionCone, i / (float)steps);
                float a1 = angle + Mathf.Lerp(-settings.VisionCone, settings.VisionCone, (i + 1) / (float)steps);
                Vector3 p0 = o + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * settings.VisionRange;
                Vector3 p1 = o + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * settings.VisionRange;
                Gizmos.DrawLine(p0, p1);
                if (i == 0) Gizmos.DrawLine(o, p0);
                if (i == steps - 1) Gizmos.DrawLine(o, p1);
            }
        }

        #endregion
    }
}
