using System.Collections.Generic;
using TapTapGameJam.Core;
using UnityEngine;
using Random = UnityEngine.Random;

// Cockroach 在本命名空间里既是命名空间又是实体类型，直接写会被当成命名空间（CS0118），统一走别名
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Cockroach
{
    /// <summary>守卫的可调数值。</summary>
    [System.Serializable]
    public class GuardSettings
    {
        [Header("视野")]
        public float VisionRange = 6f;
        public float VisionCone = 0.4f;         // 半角 rad
        public float ScanSpeed = 0.5f;          // 巡逻扫视角速度 rad/s
        public float ScanIntervalMin = 4.0f;    // 换扫描方向的间隔
        public float ScanIntervalMax = 8.5f;

        [Header("移动")]
        public float PatrolSpeed = 1.5f;
        public float DashSpeed = 8.8f;
        public float TurnSpeed = 4.5f;          // rad/s
        public float WallLook = 1.5f;           // 避障预判距离
        public float BodyRadius = 0.4f;         // 身体半径
        public float WallProbeRadius = 0.28f;   // 避障/挡墙用的半径，要比实际身体略小

        [Header("冲刺")]
        public float DashDuration = 3.2f;
        public float DashCooldown = 6f;
    }

    /// <summary>
    /// 守卫基类：视野扇形 trigger、感知名单、扫视巡逻、避障、转向、推进。
    /// 每帧流程：<see cref="TickTimers"/> → <see cref="Decide"/>（想朝哪边走）
    /// → <see cref="ApplyAvoidance"/>（这条路撞不撞墙）→ 转向 → <see cref="Move"/>。
    /// 变体只覆写 <see cref="Decide"/> 与 <see cref="Move"/>（及速度钩子）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Guard : MonoBehaviour
    {
        /// <summary>视野锥的竖直厚度，要盖得住贴地走的蟑螂。</summary>
        protected const float VisionThickness = 2.5f;

        [Header("数值")]
        public GuardSettings settings = new GuardSettings();

        [Header("视野object")]
        [Tooltip("挂 Spot Light 和扇形 trigger 的子物体。留空则自动取身上第一盏灯")]
        public Transform vision;

        [Header("运行时状态（只读）")]
        [SerializeField] protected float _angle;

        /// <summary>上帧的意图角。FixedUpdate 开头用它把 <see cref="_desiredAngle"/> 摆回原样</summary>
        protected float _targetAngle;

        protected float _desiredAngle;
        protected float _scanDir = 1f;
        protected float _scanTimer;

        protected Rigidbody _rb;

        private Light _visionLight;
        private GuardTrigger _visionTrigger;

        private LayerMask _cockroachMask;
        private LayerMask _wallMask;

        /// <summary>视野 trigger 内的蟑螂</summary>
        protected readonly HashSet<CockroachAgent> CockroachesInSight = new HashSet<CockroachAgent>();

        /// <summary>身体 trigger 内的蟑螂（贴脸了）</summary>
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

        public Vector2 Forward => new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle));

        public float Angle => _angle;

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

            Debug.Assert(vision != null, "Guard 没找到视野物体：vision 字段请挂那盏 Spot Light");
            if (vision == null) return;

            _visionLight = vision.GetComponent<Light>();
            _visionTrigger = vision.GetComponent<GuardTrigger>();

            Debug.Assert(_visionLight != null, "vision 上没有 Light 组件");
            Debug.Assert(_visionTrigger != null, "vision 上没有 GuardTrigger 组件");
            if (_visionLight == null || _visionTrigger == null) return;

            // 灯光锥和检测锥必须严格一致，否则玩家看到的光圈不等于守卫真正的视野
            _visionLight.type = LightType.Spot;
            _visionLight.range = settings.VisionRange;
            _visionLight.spotAngle = settings.VisionCone * 2f * Mathf.Rad2Deg;

            EnsureRig();
        }

        /// <summary>搭好视野扇形和身体两个 trigger。</summary>
        protected virtual void EnsureRig()
        {
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

            // 只清被销毁的。IsInside 的留给子类过滤：在这儿剔除的话，
            // 蟑螂进巢再出来不会再触发 OnTriggerEnter，就永远发现不了它
            CockroachesInSight.RemoveWhere(c => c == null);
            CockroachesTouching.RemoveWhere(c => c == null);

            _desiredAngle = _targetAngle;

            Decide(dt, TargetInSight());

            _desiredAngle = ApplyAvoidance(_desiredAngle);

            _angle = RotateTowards(_angle, _desiredAngle, EffectiveTurnSpeed * dt);
            _targetAngle = _desiredAngle;
            transform.rotation = AngleToRotation(_angle);

            Move(CurrentSpeed, dt);
        }

        protected virtual void TickTimers(float dt) { }

        #endregion

        #region 碰撞（由 GuardTrigger 转发）

        internal void OnPartEnter(Collider other, bool isVision)
        {
            if (!IsCockroachCollider(other)) return;

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

        protected virtual void OnCockroachEnter(CockroachAgent cockroach, bool isVision) { }

        protected virtual void OnCockroachExit(CockroachAgent cockroach, bool isVision) { }

        protected bool IsCockroachCollider(Collider other)
        {
            return other != null && ((1 << other.gameObject.layer) & _cockroachMask) != 0;
        }

        #endregion

        #region 决策 / 移动（子类覆写点）

        /// <summary>
        /// 决策钩子，每帧一次。基类：没目标就扫视，有目标就盯住它。
        /// 覆写时只写 <see cref="DesiredAngle"/>，转向和位移交给基类。
        /// </summary>
        protected virtual void Decide(float dt, CockroachAgent target)
        {
            if (target == null)
            {
                Scan(dt);
            }
            else
            {
                DesiredAngle = AngleTo(Position, target.Position);
            }
        }

        /// <summary>
        /// 移动钩子，每帧一次。基类：沿朝向推进，撞墙就贴着墙滑行，不进墙体。
        /// </summary>
        protected virtual void Move(float speed, float dt)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            MoveWithWallSlide(grid, Position, DirOfAngle(_angle) * (speed * dt));
        }

        /// <summary>本帧移动速度</summary>
        protected virtual float CurrentSpeed => settings.PatrolSpeed;

        /// <summary>本帧转向角速度（rad/s）</summary>
        protected virtual float EffectiveTurnSpeed => settings.TurnSpeed;

        /// <summary>
        /// 本帧想朝哪边走。赋值会同步 <see cref="_targetAngle"/>，
        /// 否则下一帧 FixedUpdate 会把意图摆回老角度，转向先掉头再转回来。
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

        /// <summary>视野里当前锁定的目标，默认最近的一只。子类可换筛选规则。</summary>
        protected virtual CockroachAgent TargetInSight()
        {
            return PickNearest(CockroachesInSight);
        }

        /// <summary>集合里最近的一只、且不在巢里的蟑螂</summary>
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
        /// 沿 <paramref name="desired"/> 探路，撞墙就换一个能走的方向，返回本帧实际采用的角度。
        /// 子类可覆写来定制避障策略。
        /// </summary>
        protected virtual float ApplyAvoidance(float desired)
        {
            if (!IsBlocked(desired)) return desired;

            const float sideStep = 0.7f;
            bool leftOk = !IsBlocked(desired - sideStep);
            bool rightOk = !IsBlocked(desired + sideStep);

            if (leftOk && !rightOk) return NormAng(desired - sideStep);
            if (rightOk && !leftOk) return NormAng(desired + sideStep);
            if (leftOk && rightOk) return NormAng(desired + (Random.value < 0.5f ? -sideStep : sideStep));

            return NormAng(desired + Mathf.PI);     // 两边都堵就掉头
        }

        /// <summary>这个方向前方有没有墙。</summary>
        protected bool IsBlocked(float angle)
        {
            Vector3 d = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            return Physics.SphereCast(transform.position, settings.WallProbeRadius, d, out _, settings.WallLook, _wallMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// 位移 + 撞墙处理。kinematic 刚体不会被静态墙推开，所以这里必须自己挡：
        /// 先整体试，进墙就把位移拆成两个轴分别试（结果就是贴着墙滑行），都进就原地不动。
        /// </summary>
        protected void MoveWithWallSlide(GridManager grid, Vector2 from, Vector2 delta)
        {
            Vector2 next = ClampToGrid(grid, from + delta);
            if (!OverlapsWall(grid, next, settings.WallProbeRadius))
            {
                Position = next;
                return;
            }

            Vector2 slideX = ClampToGrid(grid, new Vector2(next.x, from.y));
            if (!OverlapsWall(grid, slideX, settings.WallProbeRadius))
            {
                Position = slideX;
                return;
            }

            Vector2 slideZ = ClampToGrid(grid, new Vector2(from.x, next.y));
            if (!OverlapsWall(grid, slideZ, settings.WallProbeRadius))
            {
                Position = slideZ;
            }
        }

        private static Vector2 ClampToGrid(GridManager grid, Vector2 p)
        {
            Vector2 o = grid.WorldOrigin;
            Vector2 s = grid.WorldSpan;
            p.x = Mathf.Clamp(p.x, o.x, o.x + s.x);
            p.y = Mathf.Clamp(p.y, o.y, o.y + s.y);
            return p;
        }

        /// <summary>这个位置会不会和墙格重叠（按半径采样周围格子，网格外也算墙）。</summary>
        protected bool OverlapsWall(GridManager grid, Vector2 center, float radius)
        {
            Vector2 cell = grid.CellSize;
            int rx = Mathf.Max(0, Mathf.CeilToInt(radius / Mathf.Max(cell.x, 1e-4f)));
            int ry = Mathf.Max(0, Mathf.CeilToInt(radius / Mathf.Max(cell.y, 1e-4f)));
            Vector2Int c = grid.WorldToGrid(center);

            for (int x = c.x - rx; x <= c.x + rx; x++)
            {
                for (int y = c.y - ry; y <= c.y + ry; y++)
                {
                    if (!grid.IsBlocked(new Vector2Int(x, y))) continue;

                    Vector2 cc = grid.GridToWorld(new Vector2Int(x, y));
                    Vector2 half = cell * 0.5f;
                    float dx = Mathf.Max(Mathf.Abs(center.x - cc.x) - half.x, 0f);
                    float dy = Mathf.Max(Mathf.Abs(center.y - cc.y) - half.y, 0f);

                    if (dx * dx + dy * dy < radius * radius) return true;
                }
            }

            return false;
        }

        #endregion

        #region 工具

        protected static float NormAng(float a) => Mathf.Repeat(a + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;

        // 内部角度：0 = 世界 +x，往 +z 为正。Unity 的 forward 是 +z，所以 yaw 要补 90°
        public static Quaternion AngleToRotation(float angle) => Quaternion.Euler(0f, 90f - angle * Mathf.Rad2Deg, 0f);

        public static float RotationToAngle(float yawDegrees) => NormAng((90f - yawDegrees) * Mathf.Deg2Rad);

        public static Vector2 DirOfAngle(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        protected static float AngleTo(Vector2 from, Vector2 to) => Mathf.Atan2(to.y - from.y, to.x - from.x);

        protected static float RotateTowards(float cur, float target, float maxDelta)
            => NormAng(cur + Mathf.Clamp(NormAng(target - cur), -maxDelta, maxDelta));

        /// <summary>薄楔形视野体：沿 +Z 铺开，竖直方向 thickness 厚。convex MeshCollider 只取顶点做凸包。</summary>
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
                tris.AddRange(new[] { 0, b1, b0 });
                tris.AddRange(new[] { 1, t0, t1 });
                tris.AddRange(new[] { b0, b1, t1, b0, t1, t0 });
            }

            int last = 2 + segments * 2;
            tris.AddRange(new[] { 0, 1, 3, 0, 3, 2 });
            tris.AddRange(new[] { 0, last, last + 1, 0, last + 1, 1 });

            var mesh = new Mesh { name = "GuardVisionCone" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            float angle = Application.isPlaying ? _angle : RotationToAngle(transform.eulerAngles.y);

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 1f);
            Vector3 o = transform.position;
            const int steps = 16;

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
