using System.Collections.Generic;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using UnityEngine;
using TapTapGameJam.Core;
using TapTapGameJam.Types;

namespace TapTapGameJam.Cockroach
{
    
    /// <summary>
    /// 蟑螂巢穴。既是归巢目标，也是信息素归巢通道的起点。
    /// </summary>
    public partial class CockNest : Interactable
    {
        
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

        [Tooltip("是否已启用")]
        public bool isActive = false;

        [Header("范围虚线圈")]
        [Tooltip("环的线宽（世界单位）")]
        private float _ringWidth = 0.2f;

        [Tooltip("期望的单段实线弧长（世界单位）。段数由 range 反推，range 越大段数越多")]
        private float _targetDashLength = 0.3f;

        [Tooltip("环的不透明度。颜色本身跟着当前指令走（见 HatchColor / ForagingColor）")]
        private float _ringAlpha = 0.35f;

        [Tooltip("孵化指令下的自转速度（度/秒），顺时针")]
        private float _hatchSpinSpeed = 4f;

        [Tooltip("觅食指令下的自转速度（度/秒），逆时针")]
        private float _foragingSpinSpeed = 16f;

        [Tooltip("画范围圈的层。需要在 UI 相机的 Culling Mask 里，才能盖在 3D 物体上层")]
        private string _ringLayerName = "WorldUI";

        public List<Cockroach> CockroachList { get; private set; } = new List<Cockroach>();

        [Header("孵化")]
        [Tooltip("孵出来的蟑螂用它实例化。留空则孵不出东西")]
        [SerializeField] private Cockroach _cockroachPrefab;

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
        public int[,] DistanceField {get; private set;}

        /// <summary>程序化生成的范围虚线圈。只在初始化时生成一次</summary>
        private Transform _ringRoot;

        /// <summary>范围圈的材质。运行时靠改它的颜色来跟随当前指令</summary>
        private Material _ringMaterial;

        /// <summary>上一次套用到环上的指令，用来判断颜色需不需要刷新</summary>
        private CockroachCommand _ringCommand = CockroachCommand.Hatch;
        private bool _isNight;

        private void Awake()
        {
            // TODO: 假设游戏里半径不会变动，所以只生成一次
            BuildRangeRing();
            InitializeInteraction();
            ApplyPhasePresentation();
        }

        private void Update()
        {
            if (IsCarried || !isActive) return;
            float dt = Time.deltaTime;

            UpdateHatch(dt);

            if (_ringRoot == null)
            {
                return;
            }

            // 顺时针为正角度，所以孵化给正值、觅食给负值
            float speed = currentCommand == CockroachCommand.Hatch
                ? _hatchSpinSpeed
                : -_foragingSpinSpeed;

            _ringRoot.Rotate(0f, speed * dt, 0f, Space.Self);

            // 指令变了就换颜色。放在这里是为了不放过任何改 currentCommand 的路径
            if (_ringCommand != currentCommand)
            {
                _ringCommand = currentCommand;
                ApplyRingColor();
            }
        }

        /// <summary>把当前指令的颜色套到范围圈材质上</summary>
        private void ApplyRingColor()
        {
            if (_ringMaterial == null)
            {
                return;
            }

            Color color = RingColorOf(currentCommand);

            // 显式写两个名字：URP Unlit 用的是 _BaseColor，_Color 是为了兼容老材质
            if (_ringMaterial.HasProperty("_BaseColor"))
            {
                _ringMaterial.SetColor("_BaseColor", color);
            }

            _ringMaterial.SetColor("_Color", color);
        }

        /// <summary>取某个指令的颜色，alpha 换成环专用的透明度</summary>
        private Color RingColorOf(CockroachCommand command)
        {
            Color color = command == CockroachCommand.Hatch ? HatchColor : ForagingColor;
            color.a = _ringAlpha;
            return color;
        }

        /// <summary>
        /// 用程序化网格搭一个虚线圆环，画巢穴的覆盖范围。
        /// </summary>
        private void BuildRangeRing()
        {
            var ringObject = new GameObject("RangeRing");
            _ringRoot = ringObject.transform;
            _ringRoot.SetParent(transform, false);

            int layer = LayerMask.NameToLayer(_ringLayerName);
            if (layer >= 0)
            {
                ringObject.layer = layer;
            }

            // 段数跟着 range 走：半径越大，同样的虚线长度需要越多段
            int dashCount = CockroachNestRangeRing.DashesFor(range, _targetDashLength);

            Mesh mesh = CockroachNestRangeRing.Build(range, _ringWidth, dashCount);
            ringObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            _ringMaterial = CockroachNestRangeRing.CreateUnlitMaterial(RingColorOf(_ringCommand));
            ringObject.AddComponent<MeshRenderer>().sharedMaterial = _ringMaterial;
        }

        private void OnEnable()
        {
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
            _isNight = evt.NewPhase == GamePhase.Night;
            ApplyPhasePresentation();
        }

        /// <summary>
        /// 白天仍保留拾取触发器，但只在夜晚显示蟑螂活动范围和巢穴世界 UI。
        /// </summary>
        private void ApplyPhasePresentation()
        {
            bool visible = _isNight && !IsCarried && isActive;
            if (_ringRoot != null)
            {
                _ringRoot.gameObject.SetActive(visible);
            }

            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].enabled = visible;
            }
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
        /// 距离孵出下一只的进度 0~1。没配 prefab 时恒为 0
        /// </summary>
        public float HatchProgress
        {
            get
            {
                float cost = HatchCost;
                return cost > 0f ? Mathf.Clamp01(_hatchNutrition / cost) : 0f;
            }
        }

        private float HatchCost
        {
            get
            {
                CockroachSettings settings = CockroachManager.Instance != null
                    ? CockroachManager.Instance.cockroachSetting
                    : null;

                return settings != null ? settings.HatchNutritionCost : 0f;
            }
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
