using System.Collections.Generic;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using UnityEngine;
using TapTapGameJam.Core;
using TapTapGameJam.Player;
using TapTapGameJam.Types;

namespace TapTapGameJam.Cockroach
{

    /// <summary>
    /// 蟑螂巢穴。既是归巢目标，也是信息素归巢通道的起点。
    /// </summary>
    public partial class CockNest : Interactable
    {
        public enum NestLockState
        {
            Locked,
            FirstHatching,
            Unlocked
        }

        [Tooltip("巢穴解锁状态")] 
        public NestLockState nestLockState = NestLockState.Locked;

        [Tooltip("产卵（解锁）所需时间")] [SerializeField]
        private float _spawnTime = 5.0f;
        private float _spawnSpentTime = 0.0f;

        [Tooltip("初次孵化所需时间")] [SerializeField]
        private float _firstHatchTime = 30.0f;
        private float _firstHatchSpentTime = 0.0f;

        [Tooltip("初次孵化产生的蟑螂数量")] [SerializeField]
        private int _firstHatchCount = 3;

        public bool isCockroachActive => nestLockState == NestLockState.Unlocked && !GameManager.IsDay;


        // =========== 夜间巢穴属性 ============== //

        /// <summary>巢穴距离场里"不可达"的哨兵值</summary>
        public const int FarDistance = 1 << 24;

        /// <summary>孵化指令的颜色。巢穴自己的 UI 文本和范围圈都用它，改这里两边一起变</summary>
        public static readonly Color HatchColor = new Color(0.95f, 0.8f, 0.35f, 1f);

        /// <summary>觅食指令的颜色。巢穴自己的 UI 文本和范围圈都用它</summary>
        public static readonly Color ForagingColor = new Color(0.35f, 0.85f, 0.9f, 1f);

        public Vector2 WorldPosition => Cockroach.FlatOf(transform.position);

        [Tooltip("巢穴本体半径（世界单位）。蟑螂走到这个范围内就算到家")]
        public float radius = 0.6f;

        [Tooltip("活动范围半径（世界单位）。超出所有巢穴范围的蟑螂会被拉回来")]
        public float range = 10f;

        [Header("范围虚线圈")]
        [Tooltip("范围虚线圈。留空则先从子物体里找，还找不到就在运行时建一个 RangeRing 子物体")]
        [SerializeField] private CockNestRing _ring;

        public List<Cockroach> CockroachList { get; private set; } = new List<Cockroach>();

        [Header("孵化")] [Tooltip("孵出来的蟑螂用它实例化。留空则孵不出东西")] [SerializeField]
        private Cockroach _cockroachPrefab;

        /// <summary>巢内的蟑螂数量。由蟑螂自己上报，不遍历 CockroachList</summary>
        public int InsideCount { get; private set; }

        /// <summary>距离孵出下一只还差多少营养。分母是 HatchNutritionCost</summary>
        private float _hatchNutrition;

        private CockroachCommand currentCommand = CockroachCommand.Hatch;

        /// <summary>当前指令。给 UI 读取用</summary>
        public CockroachCommand CurrentCommand => currentCommand;

        /// <summary>
        /// 到本巢穴的格数距离场
        /// </summary>
        public int[,] DistanceField { get; private set; }

        // 距离孵出下一只的进度 0~1
        public float HatchProgress =>
            Mathf.Clamp01(_hatchNutrition / CockroachManager.Instance.cockroachSetting.HatchNutritionCost);

        // 距离解锁的进度
        public float UnlockProgress => Mathf.Clamp01(_spawnSpentTime / _spawnTime);

        // 初次孵化的进度
        public float FirstHatchProgress => Mathf.Clamp01(_firstHatchSpentTime / _firstHatchTime);
        
        private void Update()
        {
            if (GameManager.IsDay) return;
            
            if (nestLockState == NestLockState.FirstHatching)
            {
                _firstHatchSpentTime += Time.deltaTime;
                if (InteractState == InteractState.Interacting)
                {
                    // TODO: 玩家加速孵化速率
                    _firstHatchSpentTime += Time.deltaTime * 5;
                }

                if (_firstHatchSpentTime >= _firstHatchTime)
                {
                    // 孵化第一批
                    for(int i = 0; i < _firstHatchCount; i++) SpawnCockroach();
                    SetLockState(NestLockState.Unlocked);
                }
            }
            else if (nestLockState == NestLockState.Locked)
            {
                if (InteractState == InteractState.Interacting)
                {
                    _spawnSpentTime += Time.deltaTime;
                    if (_spawnSpentTime >= _spawnTime)
                    {
                        // 成功产卵
                        SetLockState(NestLockState.FirstHatching);
                        // 打断一下，要重新点才能继续孵化
                        InteractState = InteractState.InteractPending;
                    }
                }
                else
                {
                    _spawnSpentTime = 0;
                }
            } else if (nestLockState == NestLockState.Unlocked)
            {
                UpdateNight();
            }
        }


        void UpdateNight()
        {
            // 夜间蟑螂逻辑
            UpdateHatch(Time.deltaTime);

            // 指令变了就换颜色。放在这里是为了不放过任何改 currentCommand 的路径。
            // 自转不在这里做，由 CockNestRing 自己按当前指令跑。
            _ring?.SetCommand(currentCommand);
        }

        /// <summary>
        /// 拿到范围圈：先用 Inspector 里挂的引用，其次子物体，最后运行时建一个专用子物体。
        /// 网格、材质、自转都在 CockNestRing 里，这里只把当前半径交给它。
        /// </summary>
        private void EnsureRing()
        {
            if (_ring == null) _ring = GetComponentInChildren<CockNestRing>(true);

            if (_ring == null)
            {
                var ringObject = new GameObject("RangeRing");
                ringObject.transform.SetParent(transform, false);
                _ring = ringObject.AddComponent<CockNestRing>();
            }

            _ring.Regenerate(range);
            RefreshRingVisibility();
        }

        /// <summary>范围圈只在夜间、已解锁、且没被玩家搬着的时候显示</summary>
        private void RefreshRingVisibility()
        {
            bool visible = GameManager.IsNight && !IsCarried && nestLockState != NestLockState.Locked;
            if (_ring == null || _ring.IsVisible == visible) return;

            if (visible)
            {
                // 显示出来的这一刻保证颜色跟当前指令一致
                _ring.SetCommand(currentCommand);
            }

            _ring.SetVisible(visible);
        }

        /// <summary>切换解锁状态。内部的迁移都走这里，保证范围圈的显示跟着更新</summary>
        private void SetLockState(NestLockState state)
        {
            if (nestLockState == state) return;

            nestLockState = state;
            RefreshRingVisibility();
        }

        private void OnEnable()
        {
            EnsureRing();
            RefreshDistanceField();
            
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            // 和 PickableItem 一样由自己登记，CockroachManager 不主动遍历场景
            CockroachManager.Instance.CockNestList.Add(this);
            Debug.Log("Nest created.");
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            UnregisterInteractionTargets();
            if(CockroachManager.Instance)
                CockroachManager.Instance.CockNestList.Remove(this);

            CockroachList.Clear();

            // 拆场景时蟑螂的 OnDisable 不保证会跑，计数和进度直接归零，别留给下次启用
            InsideCount = 0;
            _hatchNutrition = 0f;

            // TODO: 移除List中所有蟑螂（删除实例）
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            bool enteringNight = evt.NewPhase == GamePhase.Night;

            // 每次进入夜晚重建一次范围圈：夜间才看得见它，半径这时候可能已经变过
            if (enteringNight)
            {
                _ring.Regenerate(range);
                RefreshDistanceField();
            }
            _ring.SetVisible(!enteringNight);
        }

        // 不含生成/销毁语义，仅记录 & 注册一只蟑螂
        public void Add(Cockroach cockroach)
        {
            CockroachList.Add(cockroach);
            if (cockroach.IsInside) InsideCount++;
        }

        public void Remove(Cockroach cockroach)
        {
            CockroachList.Remove(cockroach);
            if (cockroach.IsInside) InsideCount--;
        }

        /// <summary>蟑螂进巢时自己来报。外部不要直接改 InsideCount</summary>
        public void ReportWentInside()
        {
            InsideCount++;
        }

        /// <summary>蟑螂离巢时自己来报</summary>
        public void ReportWentOutside()
        {
            InsideCount--;
        }

        /// <summary>
        /// 孵化：巢内的每只蟑螂每秒消耗固定营养往下一只上攒，攒够就孵一只。
        /// 只在 Hatch 指令下进行；中途切走会保留进度。
        /// </summary>
        private void UpdateHatch(float dt)
        {
            if (currentCommand != CockroachCommand.Hatch || _cockroachPrefab == null || InsideCount <= 0)
            {
                return;
            }
            
            CockroachManager manager = CockroachManager.Instance;
            CockroachSettings settings = manager.cockroachSetting;

            // 想吃的量和真正吃到的量分开：营养池不够时进度只按实际消耗涨
            float wanted = settings.HatchNutritionPerSecond * InsideCount * dt;
            float spent = Mathf.Min(wanted, settings.HatchNutritionCost - _hatchNutrition);

            if (spent <= 0f || !manager.TrySpendSharedNutrition(spent))
            {
                return;
            }

            _hatchNutrition += spent;

            if (_hatchNutrition >= settings.HatchNutritionCost)
            {
                _hatchNutrition -= settings.HatchNutritionCost;
                SpawnCockroach();
            }
        }

        /// <summary>孵一只新蟑螂，落在巢内并进入 Inside 待命</summary>
        private void SpawnCockroach()
        {
            if (_cockroachPrefab == null)
            {
                return;
            }

            Cockroach cockroach = Instantiate(_cockroachPrefab, transform.position, Quaternion.identity);

            cockroach.Position = WorldPosition;

            cockroach.SpawnInside(this);
        }
        
        /// <summary>
        /// 点击巢穴时调用的入口：切到下一条行动指令，然后派发。
        /// </summary>
        public void CycleCommand()
        {
            if (!isCockroachActive) return;
            int count = System.Enum.GetValues(typeof(CockroachCommand)).Length;
            currentCommand = (CockroachCommand)(((int)currentCommand + 1) % count);

            DispatchCommand(currentCommand);
            Debug.Log("Nest command: " + currentCommand);
        }

        // 给所属所有蟑螂派发行动指令
        public void DispatchCommand(CockroachCommand command)
        {
            foreach (Cockroach cockroach in CockroachList)
            {
                cockroach.nestCommand = command;
            }
        }

        /// <summary>
        /// 更新"绕过障碍到本巢穴"的格数距离场。
        /// </summary>
        public void RefreshDistanceField()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || grid.MapState == null) return;

            int width = grid.width;
            int height = grid.height;

            DistanceField = new int[width, height];

            for (int i = 0; i < width; i++)
                for (int j = 0; j <  height; j++)
                    DistanceField[i, j] = FarDistance;

            Vector2Int center = grid.WorldToGrid(WorldPosition);
            DistanceField[center.x, center.y] = 0;
            
            Vector2Int[] direction = new Vector2Int[4]
            {
                new Vector2Int(0, -1),
                new Vector2Int(0, 1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            };
            Queue<Vector2Int> q = new Queue<Vector2Int>();
            q.Enqueue(center);
            while (q.Count > 0)
            {
                Vector2Int u = q.Dequeue();
                int disu = DistanceField[u.x, u.y];
                foreach (Vector2Int dir in direction)
                {
                    Vector2Int v = u + dir;
                    if (!grid.IsBlocked(v) && DistanceField[v.x, v.y] > disu + 1)
                    {
                        DistanceField[v.x, v.y] = disu + 1;
                        q.Enqueue(v);
                    }
                }
            }
        }

        /// <summary>
        /// 某世界坐标（XZ 平面）到本巢穴的格数距离；不可达返回 />
        /// </summary>
        public int NestDistance(Vector2 worldPos)
        {
            GridManager grid = GridManager.Instance;
            Vector2Int cell = grid.WorldToGrid(worldPos);
            return DistanceField[cell.x, cell.y];
        }
    }
}
