using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Core;
using UnityEngine;
using Random = UnityEngine.Random;

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
    /// 守卫：慢速巡逻 + 左右扫视，扇形视野里发现蟑螂就冲过去扑杀。
    /// 视野是一个扇形 trigger，击杀是身体上另一个 trigger，两个都按 layer 过滤。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Guard : MonoBehaviour
    {
        public GuardSettings settings = new GuardSettings();

        /// <summary>
        /// 视野体的竖直厚度。灯是挂在守卫身上的（离地有一点高度），
        /// 厚度给足才盖得住贴地走的蟑螂碰撞体。
        /// </summary>
        const float VisionThickness = 2.5f;

        [Header("视野object")]
        public Transform vision;
        
        private Light _visionLight;
        private GuardTrigger _visionTrigger;
        

        Rigidbody _rb;
        float _angle;           // 当前朝向，0 = 世界 +x
        float _targetAngle;
        float _scanDir = 1f;
        float _scanTimer;
        float _avoidCd;
        float _dashTimer;       // > 0 表示冲刺中
        float _dashCd;          // 冲刺冷却
        Cockroach _target;
        
        private LayerMask _cockroachMask;
        private LayerMask _wallMask;
        
        // trigger 里当前的蟑螂。视野和身体各一份，靠 GuardTrigger 转发事件
        readonly HashSet<Cockroach> _inVision = new HashSet<Cockroach>();
        readonly HashSet<Cockroach> _inBody = new HashSet<Cockroach>();

        #region 生命周期

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;

            _angle = _targetAngle = RotationToAngle(transform.eulerAngles.y);
            _scanTimer = Random.Range(settings.ScanIntervalMin, settings.ScanIntervalMax);

            _cockroachMask = LayerMask.GetMask("Cockroach");
            _wallMask = LayerMask.GetMask("Wall");

            if (vision == null)
            {
                Light found = GetComponentInChildren<Light>(true);
                if (found != null) vision = found.transform;
            }

            Assert.IsTrue(vision);
            _visionLight = vision.GetComponent<Light>();
            _visionTrigger = vision.GetComponent<GuardTrigger>();

            Assert.IsTrue(_visionLight, "vision 上没有 Light 组件");
            Assert.IsTrue(_visionLight.type == LightType.Spot, "vision 上的 Light 必须是 Spot");
            Assert.IsTrue(_visionTrigger, "vision 上没有 GuardTrigger 组件，挂一个上去");

            // 灯光锥必须和检测锥严格一致，否则玩家看到的光圈不等于守卫真正看得见的范围
            _visionLight.range = settings.VisionRange;
            _visionLight.spotAngle = settings.VisionCone * 2f * Mathf.Rad2Deg;

            EnsureRig();
        }

        /// <summary>把视野扇形和身体两个 trigger 搭好，省去手搓锥体碰撞。</summary>
        void EnsureRig()
        {
            // 检测锥沿守卫的 +Z 铺开，和那盏 Spot Light 的朝向一致
            MeshCollider cone = vision.GetComponent<MeshCollider>();
            if (cone == null) cone = vision.gameObject.AddComponent<MeshCollider>();
            cone.sharedMesh = BuildConeMesh(settings.VisionRange, settings.VisionCone, VisionThickness);
            cone.convex = true;
            cone.isTrigger = true;

            vision.gameObject.layer = gameObject.layer;
            _visionTrigger.Bind(this, true);

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
            bodyTrigger.Bind(this, false);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (_dashCd > 0f) _dashCd -= dt;
            if (_avoidCd > 0f) _avoidCd -= dt;

            // 只清掉被销毁的；IsInside 的在 PickTarget / TryKill 里过滤，
            // 不在这里剔除 —— 否则蟑螂进巢再出来时不会再触发 OnTriggerEnter，就永远发现不了它了
            _inVision.RemoveWhere(c => c == null);
            _inBody.RemoveWhere(c => c == null);
            _target = PickTarget();

            bool dashing = _dashTimer > 0f;

            if (dashing)
            {
                _dashTimer -= dt;
                if (_target == null)
                {
                    _dashTimer = 0f;        // 目标跟丢了就收
                    dashing = false;
                }
                else
                {
                    _targetAngle = AngleTo(Position, _target.Position);
                }
            }

            if (!dashing)
            {
                if (_target != null)
                {
                    // 盯住目标；冷却好了就起冲
                    _targetAngle = AngleTo(Position, _target.Position);
                    if (_dashCd <= 0f)
                    {
                        _dashTimer = settings.DashDuration;
                        dashing = true;
                    }
                }
                else
                {
                    Scan(dt);
                }
            }

            ApplyAvoidance();
            _angle = RotateTowards(_angle, _targetAngle, settings.TurnSpeed * dt);
            transform.rotation = AngleToRotation(_angle);
            Move(dashing ? settings.DashSpeed : settings.PatrolSpeed, dt);

            if (_dashCd <= 0f) TryKill();
        }

        #endregion

        #region 碰撞（由 GuardTrigger 转发）

        internal void OnPartEnter(Collider other, bool isVision)
        {
            if (((1 << other.gameObject.layer) & _cockroachMask) == 0) return;

            Cockroach c = other.GetComponentInParent<Cockroach>();
            if (c == null) return;

            if (isVision) _inVision.Add(c);
            else _inBody.Add(c);
        }

        internal void OnPartExit(Collider other, bool isVision)
        {
            if (other == null) return;

            Cockroach c = other.GetComponentInParent<Cockroach>();
            if (c == null) return;

            if (isVision) _inVision.Remove(c);
            else _inBody.Remove(c);
        }

        /// <summary>身体 trigger 里碰到蟑螂就杀，然后进冷却。</summary>
        void TryKill()
        {
            Cockroach victim = null;
            foreach (Cockroach c in _inBody)
            {
                if (c != null && !c.IsInside)
                {
                    victim = c;
                    break;
                }
            }

            if (victim == null) return;

            victim.Die();       // 危险信息素爆发在 Die() 里做
            _inBody.Remove(victim);
            _inVision.Remove(victim);
            _target = null;
            _dashTimer = 0f;
            _dashCd = settings.DashCooldown;
        }

        #endregion

        #region 移动

        Vector2 Position
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

        Cockroach PickTarget()
        {
            Cockroach best = null;
            float bestSqr = float.MaxValue;

            foreach (Cockroach c in _inVision)
            {
                if (c == null || c.IsInside) continue;

                float d = (c.Position - Position).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = c;
                }
            }

            return best;
        }

        /// <summary>没目标时：慢慢转着扫。</summary>
        void Scan(float dt)
        {
            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanDir = Random.value < 0.5f ? -1f : 1f;
                _scanTimer = Random.Range(settings.ScanIntervalMin, settings.ScanIntervalMax);
            }

            _targetAngle = NormAng(_targetAngle + _scanDir * settings.ScanSpeed * dt);
        }

        /// <summary>前方有墙就往两侧偏，两边都堵就掉头。</summary>
        void ApplyAvoidance()
        {
            if (_avoidCd > 0f) return;

            Vector3 fwd = new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle));
            // Ignore：守卫自己的视野/身体都是 trigger，不排除的话会自己撞自己
            if (!Physics.SphereCast(transform.position, settings.BodyRadius, fwd, out _, settings.WallLook, _wallMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            float left = _angle - 0.7f;
            float right = _angle + 0.7f;
            bool lOk = !Blocked(left);
            bool rOk = !Blocked(right);

            float next;
            if (lOk && !rOk) next = left;
            else if (rOk && !lOk) next = right;
            else if (lOk && rOk) next = _angle + (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.6f, 1.1f);
            else next = _angle + Mathf.PI * (Random.value < 0.5f ? -1f : 1f);

            _targetAngle = NormAng(next);
            _avoidCd = 0.45f;
            _scanTimer = Random.Range(0.8f, 2f);
        }

        bool Blocked(float angle)
        {
            Vector3 d = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            return Physics.SphereCast(transform.position, settings.BodyRadius, d, out _, settings.WallLook * 0.6f, _wallMask, QueryTriggerInteraction.Ignore);
        }

        void Move(float speed, float dt)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            grid.EnsureFootprint();

            Vector2 p = Position + new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * speed * dt;

            // 别走出网格
            Vector2 o = grid.WorldOrigin;
            Vector2 s = grid.WorldSpan;
            p.x = Mathf.Clamp(p.x, o.x, o.x + s.x);
            p.y = Mathf.Clamp(p.y, o.y, o.y + s.y);

            Position = p;
        }

        #endregion

        #region 工具

        static float NormAng(float a) => Mathf.Repeat(a + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;

        // 内部角度沿用蟑螂那套：0 = 世界 +x，往 +z 为正。
        // 而 Unity 的 forward 是 +z，所以绕 Y 的旋转要补 90°。
        static Quaternion AngleToRotation(float angle) => Quaternion.Euler(0f, 90f - angle * Mathf.Rad2Deg, 0f);

        static float RotationToAngle(float yawDegrees) => NormAng((90f - yawDegrees) * Mathf.Deg2Rad);

        static float AngleTo(Vector2 from, Vector2 to) => Mathf.Atan2(to.y - from.y, to.x - from.x);

        static float RotateTowards(float cur, float target, float maxDelta)
            => NormAng(cur + Mathf.Clamp(NormAng(target - cur), -maxDelta, maxDelta));

        /// <summary>
        /// 生成一个薄楔形当视野体：沿 +Z 铺开、水平躺着，竖直方向有 thickness 厚。
        /// convex MeshCollider 只取顶点做凸包，所以厚度给足就行，不用管三角形绕序。
        /// </summary>
        static Mesh BuildConeMesh(float range, float halfAngle, float thickness, int segments = 16)
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

        void OnDrawGizmosSelected()
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
