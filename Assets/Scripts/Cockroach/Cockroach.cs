using System;
using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using UnityEngine;
using UnityEngine.Rendering;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 蟑螂个体当下的行为状态
    /// </summary>
    public enum CockroachState
    {
        /// <summary>在巢穴里待命</summary>
        Inside,

        /// <summary>外出觅食</summary>
        Forage,

        /// <summary>已经叼着食物，正在回巢</summary>
        Return
    }
    
    /// <summary>
    /// 蟑螂要执行的指令（目标）
    /// </summary>
    public enum CockroachCommand
    {
        // 指令：孵化
        Hatch,
        // 指令：觅食
        Foraging
    }

    /// <summary>
    /// 蟑螂个体。
    /// 决策移植自 ant_test/js/entities.js 的 Ant.decide，行动移植自 Ant.update。
    /// 信息素读写走 GridManager 的四个信息素场，食物信息走 ItemManager。
    ///
    /// 职责划分：
    ///   <see cref="Decide"/> 只算出一个 _targetAngle，由 CockroachManager 按统一节拍调用；
    ///   <see cref="Update"/> 只做移动/转向/撞墙/取食，每帧跑，不含任何决策计时器。
    ///
    /// 坐标系约定：所有逻辑运算都在世界 XZ 平面的 Vector2 上做；
    /// 角度 0 指向世界 +x，π/2 指向世界 +z，即 方向 = (sin(angle), cos(angle))。
    /// Vector3 只出现在 transform 边界上。
    /// </summary>
    public class Cockroach : MonoBehaviour
    {
        private static int _seedCounter = 12345;

        private System.Random _rng;

        /// <summary>当前属于哪个巢穴。由本蟑螂在 Awake 时向它报到</summary>
        [Header("归属")] [Tooltip("当前所属巢穴。留空则运行时自动找最近的一个")]
        public CockNest nest;

        [HideInInspector]
        public CockroachCommand nestCommand = CockroachCommand.Hatch;
        
        [SerializeField] // 临时debug用
        private CockroachState currentState = CockroachState.Inside;
        
        [Header("运动参数")] [SerializeField]
        private CockroachSettings _settings = new CockroachSettings();

        // 朝向。角度 0 = 世界 +x，π/2 = 世界 +z
        private float _forwardAngle;
        private float _targetAngle;

        // 速度抖动让每只蟑螂略有差别；避障让行冷却属于"行动"，不是决策计时器
        private float _speedJitter;

        /// <summary>是否取走了某堆食物的最后一份（返程就不再撒觅食信息素）</summary>
        private bool _lastLoad;


        /// <summary>
        /// 当前所在世界坐标（XZ 平面）。
        /// 读就走 transform，写会同步回 transform。
        /// </summary>
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

        /// <summary>已经叼着的营养</summary>
        private float _carrying;
        
        /// <summary>当前阶段计时（用于信息素浓度衰减）</summary>
        private float _phaseClock;

        /// <summary>当前所在格子</summary>
        public Vector2Int CurrentCell => GridManager.Instance.WorldToGrid(Position);

        private ItemManager Items => ItemManager.Instance;

        private CockroachManager Manager => CockroachManager.Instance;

        #region 生命周期 / 登记

        private void Awake()
        {
            _rng = new System.Random(_seedCounter++);

            // 寻路参数以 CockroachManager 上那一份为准，改一处全场生效；
            // Manager 上没配才用本组件自己的序列化配置
            _settings = Manager.cockroachSetting ?? _settings;

            _forwardAngle = Rand(0f, Mathf.PI * 2f);
            _targetAngle = _forwardAngle;
            _speedJitter = Rand(_settings.SpeedJitterMin, _settings.SpeedJitterMax);

            nest = nest != null ? nest : FindNearestNest(Position);

            Assert.IsTrue(nest != null);
            nest.Add(this);
            Position = JitterInsideNest(nest);
        }

        private void OnEnable()
        {
            EventPipeline.Publish(new EventCockroachCountChange(Manager.CockroachTotalCount(), 1));
        }

        private void OnDisable()
        {
            nest?.Remove(this);

            if (Manager) EventPipeline.Publish(new EventCockroachCountChange(Manager.CockroachTotalCount(), -1));
        }

        /// <summary>
        /// 换巢。新旧两边的名单都由这里维护，外部只管调这一个方法。
        /// </summary>
        public void ChangeNest(CockNest newNest)
        {
            if (newNest == nest)
            {
                return;
            }

            nest?.Remove(this);
            nest = newNest;
            nest?.Add(this);
        }

        private void FixedUpdate()
        {
            UpdateLogic(Time.fixedDeltaTime);
        }

        #endregion

        #region 食物信息

        /// <summary>
        /// 地图上所有还有营养的食物
        /// </summary>
        public IReadOnlyList<FoodItem> GetFoodSources()
        {
            return Items.GetFoodOnMap();
        }

        /// <summary>
        /// 半径内最近的食物（对应参考实现的 world.foodNear）
        /// </summary>
        public FoodItem GetNearestFood(Vector2 origin, float range)
        {
            IReadOnlyList<FoodItem> foods = GetFoodSources();
            FoodItem best = null;
            float bestSqr = range * range;

            for (int i = 0; i < foods.Count; i++)
            {
                FoodItem food = foods[i];
                float sqr = (FlatOf(food.transform.position) - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = food;
                }
            }

            return best;
        }

        /// <summary>
        /// 半径内最近的墙体格子中心（对应参考实现的 world.wallNear）
        /// </summary>
        public bool TryGetNearestWall(Vector2 origin, float range, out Vector2 wallPos)
        {
            wallPos = default;
            GridManager grid = GridManager.Instance;

            Vector2Int center = grid.WorldToGrid(origin);
            float cellAvg = Mathf.Max(grid.CellSize.x, 0.0001f);
            int rInt = Mathf.Max(1, Mathf.CeilToInt(range / cellAvg));

            float bestSqr = range * range;
            bool found = false;

            for (int x = Mathf.Max(0, center.x - rInt); x <= Mathf.Min(grid.width - 1, center.x + rInt); x++)
            {
                for (int y = Mathf.Max(0, center.y - rInt); y <= Mathf.Min(grid.height - 1, center.y + rInt); y++)
                {
                    if (grid.MapState[x, y] != TileState.TileWall)
                    {
                        continue;
                    }

                    Vector2 cellCenter = grid.GridToWorld(new Vector2Int(x, y));
                    float sqr = (cellCenter - origin).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        wallPos = cellCenter;
                        found = true;
                    }
                }
            }

            return found;
        }

        #endregion

        #region 决策

        /// <summary>
        /// 决策核心。只写 _targetAngle
        /// </summary>
        public void Decide()
        {
            GridManager grid = GridManager.Instance;

            bool toReturn = currentState == CockroachState.Return;
            PheromoneKind kind = toReturn ? PheromoneKind.Home : PheromoneKind.Food;
            float permanentRange = _settings.PermanentRange;
            Vector2 pos = Position;

            // ---- Permanent：离目标足够近就直接看过去，不再采样 ----
            if (toReturn && nest != null)
            {
                Vector2 nestPos = nest.WorldPosition;
                if ((nestPos - pos).magnitude < nest.radius + permanentRange)
                {
                    _targetAngle = NormAng(AngleTo(pos, nestPos) + Rand(-_settings.DirectionNoise, _settings.DirectionNoise));
                    return;
                }
            }
            else
            {
                FoodItem food = GetNearestFood(pos, permanentRange);
                if (food != null)
                {
                    Vector2 foodPos = FlatOf(food.transform.position);
                    _targetAngle = NormAng(AngleTo(pos, foodPos) + Rand(-_settings.DirectionNoise, _settings.DirectionNoise));
                    return;
                }
            }

            // ---- 采样 ----
            float baseAngle = _forwardAngle;
            int sampleCount = Mathf.Max(3, _settings.SampleCount);
            float triOffset = Mathf.Min(_settings.DetectCone * 0.5f, 0.7f);

            // 归巢时尽量选"离巢更近"的方向，刚出巢散开时选更远的（都绕开障碍）
            float homeDist = nest != null ? (nest.WorldPosition - pos).magnitude : float.PositiveInfinity;
            bool dispersing = !toReturn && nest != null && homeDist < nest.radius + _settings.LeaveZone;
            float biasWeight = nest == null
                ? 0f
                : (dispersing ? Mathf.Max(_settings.HomeBias, 4f) : (toReturn ? _settings.HomeBias : 0f));
            int distHere = biasWeight > 0f ? nest.NestDistance(pos) : 0;

            // 身处危险信息素里时放开采样范围（含正后方），仍然只是"打分选最安全方向"，不做硬折返
            float dangerHere = grid.SamplePheromone(PheromoneKind.Danger, pos);
            bool flee = _settings.DangerAvoid > 0f && dangerHere > _settings.MarkerMax * 0.2f;
            float coneHalf = flee ? Mathf.PI : _settings.DetectCone;

            float bestScore = float.NegativeInfinity;
            Vector2 bestDir = Vector2.zero;
            bool hasBest = false;
            Vector2 bestSamplePos = pos;

            for (int i = 0; i < sampleCount; i++)
            {
                float angle = baseAngle + Rand(-coneHalf, coneHalf);
                float sampleDist = _settings.DetectRange * Rand(0.35f, 1f);
                Vector2 dir = DirOfAngle(angle);
                Vector2 samplePos = pos + dir * sampleDist;

                if (grid.IsBlocked(grid.WorldToGrid(samplePos)))
                {
                    continue;
                }

                if (!grid.LosClear(pos, samplePos))
                {
                    continue;
                }

                // 采样点落在巢穴上 → 直奔巢口
                if (toReturn && nest != null && (nest.WorldPosition - samplePos).magnitude < nest.radius * 0.95f)
                {
                    bestDir = dir;
                    bestScore = float.PositiveInfinity;
                    bestSamplePos = samplePos;
                    hasBest = true;
                    break;
                }

                float score;
                if (dispersing)
                {
                    // 巢口附近觅食信息素会连成一片"穹顶"，此时只按向外散开 + 避墙来选方向
                    score = grid.WallWeight(samplePos, _settings.WallAvoid) * 6f;
                }
                else
                {
                    float v = grid.SamplePheromone(kind, samplePos);

                    // 玩家的气味走独立通道，觅食时和觅食信息素一起看
                    if (!toReturn)
                    {
                        v = Mathf.Max(v, grid.SamplePheromone(PheromoneKind.Player, samplePos));
                    }

                    score = Mathf.Max(GridManager.MinPheromone, v) * grid.WallWeight(samplePos, _settings.WallAvoid);
                }

                if (biasWeight > 0f)
                {
                    int distSample = nest.NestDistance(samplePos);
                    int gain = toReturn ? distHere - distSample : distSample - distHere;
                    if (gain > 0)
                    {
                        score += biasWeight * Mathf.Min(gain, 12) * 0.4f;
                    }
                }

                // 危险信息素：负权重（绕开，而不是折返）
                if (_settings.DangerAvoid > 0f)
                {
                    score -= _settings.DangerAvoid * grid.SamplePheromone(PheromoneKind.Danger, samplePos);
                }

                // 随机打破平局，避免退化成固定方向的绕圈
                score += Rand(0f, 1e-4f);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = dir;
                    bestSamplePos = samplePos;
                    hasBest = true;
                }

                // 探索性提前中断
                if (Rand01() < _settings.LibertyCoef)
                {
                    break;
                }
            }

            if (!hasBest)
            {
                // 一个能走的采样点都没有：随便偏一点，接下来靠撞墙反弹脱困
                bestDir = DirOfAngle(baseAngle + Rand(-0.7f, 0.7f));
            }

            _targetAngle = NormAng(AngleOfDir(bestDir) + Rand(-_settings.DirectionNoise, _settings.DirectionNoise));

            // 反停滞：轻微削弱刚跟随过的格子
            if (bestScore > GridManager.MinPheromone * 4f && Rand01() < 0.25f)
            {
                grid.WritePheromone(kind, bestSamplePos, grid.SamplePheromone(kind, bestSamplePos) * 0.985f, false);
            }
        }

        #endregion

        #region 信息素释放

        /// <summary>
        /// 释放信息素：强度随「离巢 / 取食」的时间指数衰减。
        /// 从食物处出发的那条路逐渐变淡，最后消失——这是觅食路径"自然遗忘"的机制。
        /// </summary>
        public void Deposit()
        {
            GridManager grid = GridManager.Instance;

            float intensity = _settings.MarkerMax * Mathf.Exp(-_settings.MarkerTimeCoef * _phaseClock);

            if (currentState == CockroachState.Return && _carrying > 0f)
            {
                if (_lastLoad)
                {
                    // 这是那堆食物的最后一份：返程不再沿途撒信息素
                    return;
                }

                // 觅食信息素：快到巢口时淡出，避免在巢口堆出一圈"穹顶"把同伴吸回来
                float v = intensity;
                if (_settings.FoodTrailFade > 0f && nest != null)
                {
                    float d = (nest.WorldPosition - Position).magnitude - nest.radius;
                    if (d < _settings.FoodTrailFade)
                    {
                        v *= Mathf.Clamp01(d / _settings.FoodTrailFade);
                    }
                }

                grid.WritePheromone(PheromoneKind.Food, Position, v, _settings.MarkerSmooth);
            }
            else
            {
                // 外出觅食时撒归巢信息素，这样同伴才知道"家在哪边"
                grid.WritePheromone(PheromoneKind.Home, Position, intensity, _settings.MarkerSmooth);
            }
        }

        #endregion

        #region 感知 / 避障

        /// <summary>
        /// 沿某个方向做射线探测：在身体之外额外多看 extra 距离（不再用身体去蹭墙）
        /// </summary>
        private bool RayAhead(Vector2 origin, Vector2 dir, float extra)
        {
            GridManager grid = GridManager.Instance;

            float total = _settings.InteractRadius + 1f + extra;
            int steps = Mathf.Max(1, Mathf.CeilToInt(total / _settings.InteractRadius));
    
            for (int i = 1; i <= steps; i++)
            {
                float t = total * i / steps;
                if (grid.IsBlocked(grid.WorldToGrid(origin + dir * t)))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 行动（移动 / 转向 / 撞墙）

        /// <summary>
        /// 每帧的行动阶段：只做移动、转向、撞墙反弹、取食、归巢判定。
        /// 决策与信息素释放不在这里，那两件事由 CockroachManager 按统一节拍触发。
        /// </summary>
        private void UpdateLogic(float dt)
        {
            GridManager grid = GridManager.Instance;

            _phaseClock += dt;

            // 状态切换
            if (nestCommand == CockroachCommand.Hatch)
            {
                if (currentState == CockroachState.Forage)
                {
                    _phaseClock = 0f;
                    currentState = CockroachState.Return; // Hatch指令时强制归巢
                }
                if (currentState == CockroachState.Inside)
                {
                    // TODO: 孵化逻辑
                }
            }

            if (nestCommand == CockroachCommand.Foraging && currentState == CockroachState.Inside)
            {
                _phaseClock = 0f;
                currentState = CockroachState.Forage;
            }

            if (currentState != CockroachState.Inside)
            {
                Deposit();
            }

            // 运动逻辑
            if (currentState != CockroachState.Inside)
            {
                /* ---- 平滑转向 ---- */
                _forwardAngle = RotateTowards(_forwardAngle, _targetAngle, dt, _settings.TurnSpeed);

                /* ---- 移动 + 碰撞反弹 ---- */
                float baseSpeed = currentState == CockroachState.Return ? _settings.Speed * 1.06f : _settings.Speed;

                // 近处有墙且还没转开时减速转向，避免"顶着墙往前推"
                float speedMul = 1f;
                if (_settings.WallLook > 0f && RayAhead(Position, DirOfAngle(_forwardAngle), _settings.WallLook * 0.5f))
                {
                    float err = Mathf.Abs(NormAng(_targetAngle - _forwardAngle));
                    speedMul = Mathf.Clamp(Mathf.Cos(Mathf.Min(err, Mathf.PI)), 0.45f, 1f);
                }

                float speed = baseSpeed * _speedJitter * speedMul;

                Vector2 pos = Position;
                Vector2 next = pos + DirOfAngle(_forwardAngle) * (speed * dt);

                // 分轴检测，能沿着墙滑行而不是卡死
                bool hitX = grid.IsBlocked(grid.WorldToGrid(new Vector2(next.x, pos.y)));
                bool hitZ = grid.IsBlocked(grid.WorldToGrid(new Vector2(next.x, next.y)));

                if (hitX)
                {
                    next.x = pos.x;
                }

                if (hitZ)
                {
                    next.y = pos.y;
                }

                if (hitX || hitZ)
                {
                    // 撞墙反弹：瞬间反射当前朝向，不再等平滑转向
                    float reflected;
                    if (hitX && hitZ)
                    {
                        reflected = NormAng(_forwardAngle + Mathf.PI + Rand(-0.4f, 0.4f));
                    }
                    else if (hitX)
                    {
                        reflected = NormAng(-_forwardAngle + Rand(-0.25f, 0.25f));
                    }
                    else
                    {
                        reflected = NormAng(Mathf.PI - _forwardAngle + Rand(-0.25f, 0.25f));
                    }

                    _forwardAngle = reflected;
                    _targetAngle = reflected;
                }

                Position = next;

                /* ---- 活动范围：超出所有巢穴范围视为撞墙 ---- */
                ClampInsideRanges(dt);
            }

            /* ---- 取食 / 归巢 ---- */
            if (currentState == CockroachState.Forage)
            {
                TryEat();
            }
            else if (currentState == CockroachState.Return && nest != null
                                                  && (nest.WorldPosition - Position).magnitude < nest.radius * 1.2f)
            {
                EnterNest();
            }
        }

        /// <summary>
        /// 啃一口食物：拿到营养就立刻折返。
        /// </summary>
        private void TryEat()
        {
            IReadOnlyList<FoodItem> foods = GetFoodSources();
            Vector2 pos = Position;

            for (int i = 0; i < foods.Count; i++)
            {
                FoodItem food = foods[i];
                Vector2 foodPos = FlatOf(food.transform.position);

                float reach = _settings.InteractRadius + food.interactRaius;
                if ((foodPos - pos).sqrMagnitude > reach * reach)
                {
                    continue;
                }

                float take = food.Consume(_settings.CarryCapacity);
                if (take <= 0f)
                {
                    continue;
                }

                // 吃空了：把这一带的觅食信息素抹掉，别再把同伴引过来
                _lastLoad = !food.HasNutrition;
                if (_lastLoad)
                {
                    GridManager.Instance.ClearPheromoneInRadius(PheromoneKind.Food, foodPos, _settings.EmptyClearRadius, 0f);
                }

                _carrying = take;
                currentState = CockroachState.Return;
                _phaseClock = 0f; // 计时重置 → 食物处信息素最浓

                // 吃到食物立即折返（允许瞬间转向）
                _targetAngle = _forwardAngle = NormAng(_forwardAngle + Mathf.PI + Rand(-0.15f, 0.15f));
                return;
            }
        }

        /// <summary>
        /// 到巢：把营养交给共享池，决定继续觅食 or 入巢孵化
        /// </summary>
        private void EnterNest()
        {
            CockroachManager.Instance.sharedNutrition += _carrying;
            _lastLoad = false;
            _carrying = 0f;
            
            if (nestCommand == CockroachCommand.Foraging)
            {
                _phaseClock = 0f;
                currentState =  CockroachState.Forage;
                _targetAngle = _forwardAngle = NormAng(_forwardAngle + Mathf.PI);
            }

            if (nestCommand == CockroachCommand.Hatch)
            {
                _phaseClock = 0f;
                currentState = CockroachState.Inside;
            }
        }

        #endregion

        #region 移动辅助

        /// <summary>
        /// 巢穴里待命时别飘出去：离巢心超过半径的 75% 就往回看
        /// </summary>
        private void ClampInsideNest()
        {
            Vector2 pos = Position;
            Vector2 nestPos = nest.WorldPosition;

            if ((pos - nestPos).magnitude > nest.radius * 0.75f)
            {
                _targetAngle = AngleTo(pos, nestPos) + Rand(-0.5f, 0.5f);
            }
        }

        /// <summary>
        /// 超出所有巢穴活动范围就朝最近的巢穴拉回来
        /// </summary>
        private void ClampInsideRanges(float dt)
        {
            List<CockNest> nests = Manager.CockNestList;

            Vector2 pos = Position;
            CockNest nearest = null;
            float nearestSqr = float.PositiveInfinity;
            bool insideAny = false;

            for (int i = 0; i < nests.Count; i++)
            {
                CockNest nest = nests[i];
                float sqr = (nest.WorldPosition - pos).sqrMagnitude;
                if (sqr <= nest.range * nest.range)
                {
                    insideAny = true;
                    break;
                }

                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = nest;
                }
            }

            if (insideAny || nearest == null)
            {
                return;
            }

            float back = AngleTo(pos, nearest.WorldPosition);
            _targetAngle = NormAng(back + Rand(-0.4f, 0.4f));

            // 轻轻推回来一点，避免在范围边缘反复横跳
            Position = pos + DirOfAngle(back) * (_settings.RangePushSpeed * dt);
        }

        private Vector2 JitterInsideNest(CockNest nest)
        {
            Vector2 nestPos = nest.WorldPosition;
            float a = Rand(0f, Mathf.PI * 2f);
            float rr = Mathf.Sqrt(Rand01()) * nest.radius * 0.72f;
            return nestPos + DirOfAngle(a) * rr;
        }

        /// <summary>
        /// 没配巢穴时，找场上离我最近的一个
        /// </summary>
        private CockNest FindNearestNest(Vector2 from)
        {
            List<CockNest> nests = Manager.CockNestList;

            CockNest best = null;
            float bestSqr = float.PositiveInfinity;

            for (int i = 0; i < nests.Count; i++)
            {
                CockNest nest = nests[i];
                float sqr = (nest.WorldPosition - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = nest;
                }
            }

            return best;
        }

        #endregion

        #region 数学工具（移植自 config.js）

        /// <summary>
        /// 世界 XZ 平面 → Vector2
        /// </summary>
        public static Vector2 FlatOf(Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }

        /// <summary>
        /// 角度 → 单位方向。角度 0 = +x，π/2 = +z
        /// </summary>
        public static Vector2 DirOfAngle(float angle)
        {
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        }

        /// <summary>
        /// 单位方向 → 角度
        /// </summary>
        public static float AngleOfDir(Vector2 dir)
        {
            return Mathf.Atan2(dir.x, dir.y);
        }

        /// <summary>
        /// 把角度归一化到 (-π, π]
        /// </summary>
        public static float NormAng(float a)
        {
            a = (a + Mathf.PI) % (Mathf.PI * 2f);
            if (a < 0f)
            {
                a += Mathf.PI * 2f;
            }

            return a - Mathf.PI;
        }

        /// <summary>
        /// 平滑转向（对应参考实现的 rotateTowards）：
        /// 角速度 ≈ speed·sin(Δθ)，大角度时接近 speed、接近目标时自然减速；
        /// 180° 死点与小角度收敛用线性项补偿，全程不会瞬转。
        /// </summary>
        public static float RotateTowards(float current, float target, float dt, float speed, float maxRate = -1f)
        {
            float d = NormAng(target - current);
            if (Mathf.Abs(d) < 0.008f)
            {
                return target;
            }

            float rate = speed * Mathf.Sin(d);
            if (Mathf.Abs(d) > Mathf.PI * 0.75f)
            {
                rate = speed * 0.6f * Mathf.Sign(d);
            }
            else
            {
                rate += 1.2f * d;
            }

            float cap = maxRate >= 0f ? maxRate : speed * 1.3f;
            rate = Mathf.Clamp(rate, -cap, cap);
            return NormAng(current + rate * dt);
        }

        /// <summary>
        /// 从 from 指向 to 的朝向角
        /// </summary>
        public static float AngleTo(Vector2 from, Vector2 to)
        {
            return AngleOfDir(to - from);
        }

        private float Rand01()
        {
            return (float)_rng.NextDouble();
        }

        private float Rand(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }

        #endregion
    }
}
