using System.Collections.Generic;
using TapTapGameJam.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 「群颚破壁」（<c>group_wall_gnawing</c>）注入到墙体 / 桌子上的可食用组件。
    ///
    /// 由 <see cref="WallGnawingDirector"/> 在拿到 tag 的那个夜晚一次性 AddComponent，
    /// 之后这面墙就<b>和其它食物一样</b>登记进 <see cref="FoodManager"/> 的格子索引：
    /// 蟑螂能找到它、能啃它，啃出来的营养直接进共享池。
    ///
    /// 和普通食物的三点不同，都是为了"它其实是一面墙"：
    /// <list type="number">
    /// <item><b>占一整片格子</b>：普通食物只占脚下那一格，墙占它包围盒盖住的所有格子
    ///       （见 <see cref="GetOccupiedCells"/>）。蟑螂查的是"周围几格有没有食物"，
    ///       所以沿着墙任意位置都能下嘴，不用再自己算"最近点"。</item>
    /// <item><b>不可拾取</b>：不然夜里玩家能按 E 把墙搬进背包。</item>
    /// <item><b>天亮不清场</b>：<see cref="NightManager"/> 会把地上所有食物清掉，
    ///       而墙是场景本体，清掉就是把地图拆了。</item>
    /// </list>
    ///
    /// 吃空之后：从索引里注销、把吃掉那块几何对应的格子标成可通行、连带自己一起销毁。
    /// 墙体不会跨天恢复 —— 破壁是永久的。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallFoodItem : FoodItem
    {
        /// <summary>裂纹覆盖层的名字，在 Hierarchy 里一眼能认出来</summary>
        public const string OverlayName = "WallCrackOverlay";

        /// <summary>裂纹 shader 的进度属性名，必须和 Assets/Shader/WallCrack.shader 对齐</summary>
        private static readonly int EatProgressId = Shader.PropertyToID("_EatProgress");

        /// <summary>被 <see cref="Configure"/> 配置过。没配过说明是有人手工挂的，走普通食物流程</summary>
        private bool _configured;

        private Material _crackMaterial;

        /// <summary>注入时缓存下来的世界包围盒。销毁之后就取不到了，所以必须在 OnDepleted 用它</summary>
        private Bounds _worldBounds;

        /// <summary>
        /// 裂纹覆盖层（每找到一个 MeshRenderer 就建一层，通常只有一层）。
        /// <see cref="_propertyBlock"/> 惰性创建：场景里会有几千个这个组件，
        /// 没必要给每个都先 new 一块 MPB。
        /// </summary>
        private readonly List<Renderer> _overlays = new List<Renderer>();

        private MaterialPropertyBlock _propertyBlock;

        private bool _overlayBuilt;

        [Tooltip("被吃空时打一条日志：清掉了多少格、有没有销毁。查「墙吃完了还在」时把它打开")]
        [SerializeField] private bool _logDepleted = true;

        /// <summary>食用进度 0..1（1 = 吃空）。只用来驱动裂纹，不参与任何数值</summary>
        public float EatProgress =>
            totalNutrition <= 0f ? 0f : 1f - Mathf.Clamp01(remainingNutrition / totalNutrition);

        /// <summary>墙 / 桌子不是夜晚的消耗品，天亮不许清</summary>
        public override bool IsClearedAtDay => false;

        /// <summary>不可拾取。夜间玩家也不该能把墙搬走</summary>
        public override bool CanInteract(Player.PlayerBasic player) => false;

        public override string InteractionPrompt => "墙体（不可拾取）";

        private void Awake()
        {
            _worldBounds = ResolveWorldBounds();
        }

        /// <summary>
        /// 注入时调用。<paramref name="nutrition"/> 由 Director 按包围盒体积估出来
        /// （大墙比小桌子耐啃），<paramref name="crackMaterial"/> 是裂纹覆盖材质。
        /// </summary>
        public void Configure(float nutrition, Material crackMaterial)
        {
            totalNutrition = Mathf.Max(0.01f, nutrition);
            remainingNutrition = totalNutrition;
            _crackMaterial = crackMaterial;
            _worldBounds = ResolveWorldBounds();
            _configured = true;

            // 运行期 AddComponent 时 OnEnable 已经先跑过了（那时还拿不到 bounds / 营养），
            // 所以登记在这里补上。Register 是幂等的。
            NutritionInitialized = true;
            RegisterToFoodManager();
        }

        protected override void Start()
        {
            // 配过营养的话不能再走 FoodItem.Start —— 它会把 remainingNutrition 重置成 totalNutrition。
            // 注入是在运行期发生的，Start 有可能晚于第一次被啃，那一下会把裂纹进度抹掉。
            if (_configured)
            {
                return;
            }

            base.Start();
        }

        #region 取食

        /// <summary>
        /// 墙 / 桌子占它包围盒覆盖的一整片格子：蟑螂只要查到片里任意一格就能下嘴。
        /// 普通食物（<see cref="FoodItem.GetOccupiedCells"/>）只占脚下那一格。
        /// </summary>
        public override void GetOccupiedCells(GridManager grid, out Vector2Int min, out Vector2Int max)
        {
            Bounds bounds = _worldBounds;
            if (bounds.size.x <= 0f || bounds.size.z <= 0f)
            {
                base.GetOccupiedCells(grid, out min, out max);
                return;
            }

            Vector2Int c0 = grid.WorldToGrid(new Vector2(bounds.min.x, bounds.min.z));
            Vector2Int c1 = grid.WorldToGrid(new Vector2(bounds.max.x, bounds.max.z));

            min = new Vector2Int(Mathf.Min(c0.x, c1.x), Mathf.Min(c0.y, c1.y));
            max = new Vector2Int(Mathf.Max(c0.x, c1.x), Mathf.Max(c0.y, c1.y));
        }

        /// <summary>每啃一口就推进裂纹。没吃空也要推，裂纹是持续反馈</summary>
        protected override void OnConsumed(float eaten)
        {
            // 最后一口：墙这一帧就没了，没必要再为它建覆盖层
            if (!HasNutrition)
            {
                PushProgress();
                return;
            }

            EnsureOverlay();
            PushProgress();
        }

        /// <summary>
        /// 吃空：把这块几何覆盖的格子标成可通行，把裂纹覆盖层拆掉，然后跟着自己被 Destroy。
        ///
        /// 格子只清自己包围盒那一片，不做全量重扫 —— 全量是 15 万条射线，拆一面墙跑一次会把游戏卡死。
        /// 覆盖层本身是墙的子物体，会跟着一起销毁；这里显式再拆一遍，
        /// 是为了保证「墙没了、裂纹还挂在原地」这种半截状态永远不会出现。
        /// </summary>
        protected override void OnDepleted()
        {
            GridManager grid = GridManager.Instance;
            if (grid != null)
            {
                int cleared = grid.ClearWallsInBounds(_worldBounds);
                if (_logDepleted)
                {
                    Debug.Log($"[WallFoodItem:{name}]：被吃空，清掉 {cleared} 个墙格，销毁墙体和裂纹覆盖层。", this);
                }
            }
            else if (_logDepleted)
            {
                Debug.LogWarning($"[WallFoodItem:{name}]：场景里没有 GridManager，墙被吃掉了但格子没更新。", this);
            }

            DestroyOverlays();
        }

        /// <summary>
        /// 覆盖层是墙的子物体，正常情况下会跟着一起没掉。这里兜底再销毁一次：
        /// 万一将来覆盖层被挪到别的父物体下（或者墙被别的逻辑直接销毁），
        /// 也不会留下一层半透明的裂纹浮在空气里。
        /// </summary>
        private void OnDestroy()
        {
            DestroyOverlays();
        }

        #endregion

        #region 裂纹覆盖层

        /// <summary>
        /// 取世界包围盒，用来算"占哪几格"和"营养多少"。
        ///
        /// 顺序是 **自己的 MeshRenderer → 子物体的 Renderer → Collider**：
        /// 「群颚破壁」现在按层级注入，场景里大量杂物没有 Collider，只有网格；
        /// 反过来先取 Collider 的话，父物体上那种整块触发区会把包围盒撑得离谱。
        /// </summary>
        private Bounds ResolveWorldBounds()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                return meshRenderer.bounds;
            }

            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }

            Collider collider = GetComponentInChildren<Collider>();
            if (collider != null)
            {
                return collider.bounds;
            }

            return new Bounds(transform.position, new Vector3(0.2f, 0.2f, 0.2f));
        }

        /// <summary>
        /// 第一次被啃时才建覆盖层。进度 0 的裂纹是完全透明的，所以"晚建"在视觉上和"一开始就建好"没区别，
        /// 但省掉了五六百个空 renderer 和它们的绘制开销。
        /// </summary>
        private void EnsureOverlay()
        {
            if (_overlayBuilt)
            {
                return;
            }

            _overlayBuilt = true;

            if (_crackMaterial == null)
            {
                Debug.LogWarning($"[WallFoodItem:{name}]：没有配裂纹材质，这面墙被啃了不会有任何视觉反馈", this);
                return;
            }

            // true：包含未激活的子物体。注入时墙通常是激活的，这里只是别漏
            MeshRenderer[] sources = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                MeshRenderer source = sources[i];
                if (source == null || source.gameObject.name == OverlayName)
                {
                    continue;
                }

                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Renderer overlay = CreateOverlay(source, filter.sharedMesh);
                if (overlay != null)
                {
                    _overlays.Add(overlay);
                }
            }
        }

        /// <summary>
        /// 用同一个 mesh 长在一张"壳"上，压在墙体表面渲染。
        ///
        /// 为什么不做几何外扩：墙体的 localScale 普遍是非等比的（例如 z = 0.46），
        /// 按法线推出去会被缩放拉歪。压在表面这件事交给 shader 的 Offset -1,-1 + ZTest LEqual。
        /// </summary>
        private Renderer CreateOverlay(MeshRenderer source, Mesh mesh)
        {
            var go = new GameObject(OverlayName);

            Transform t = go.transform;
            t.SetParent(source.transform, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            go.layer = source.gameObject.layer;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _crackMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            return renderer;
        }

        private void PushProgress()
        {
            if (_overlays.Count == 0)
            {
                return;
            }

            float progress = EatProgress;

            for (int i = 0; i < _overlays.Count; i++)
            {
                Renderer overlay = _overlays[i];
                if (overlay == null)
                {
                    continue;
                }

                // MaterialPropertyBlock 而不是 per-object 材质：几千个物体各 new 一份材质
                // 会把 URP 的批处理彻底打散，MPB 只贵一点点。
                // MPB 惰性创建：没建覆盖层的物体（没被啃过的）不需要它。
                if (_propertyBlock == null)
                {
                    _propertyBlock = new MaterialPropertyBlock();
                }

                overlay.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(EatProgressId, progress);
                overlay.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>
        /// 把裂纹覆盖层全部拆掉。墙被吃空（OnDepleted）和墙被销毁（OnDestroy）都会走这里，
        /// 所以「墙还在但裂纹没了」和「墙没了但裂纹还浮着」两种半截状态都不会出现。
        /// </summary>
        private void DestroyOverlays()
        {
            for (int i = 0; i < _overlays.Count; i++)
            {
                Renderer overlay = _overlays[i];
                if (overlay == null)
                {
                    continue;
                }

                // 覆盖层是墙的子物体，Destroy 墙时会跟着没；这里显式销毁，
                // 重复销毁同一个物体是安全的（Unity 会忽略第二次）
                Destroy(overlay.gameObject);
            }

            _overlays.Clear();
            _overlayBuilt = false;
        }

        /// <summary>
        /// 只在运行时有效：把进度摆到 50% 看裂纹长什么样，方便调 shader。
        /// （裂纹是 MaterialPropertyBlock 推的，编辑模式没有运行时上下文）
        /// </summary>
        [ContextMenu("预览裂纹：设为 50% 已吃")]
        private void DebugPreviewHalfEaten()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[WallFoodItem]：裂纹进度只在运行时推，编辑模式预览不了。进 Play 模式再点。");
                return;
            }

            remainingNutrition = Mathf.Max(0f, totalNutrition * 0.5f);
            EnsureOverlay();
            PushProgress();
        }

        /// <summary>
        /// 只在运行时有效：一口吃空，验证"墙 + 裂纹一起销毁 + 格子被放出来"这条链路。
        /// 走的是正常的 <see cref="FoodItem.Consume"/>，不是另开一条销毁路径。
        /// </summary>
        [ContextMenu("立即吃空（验证销毁）")]
        private void DebugConsumeAll()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[WallFoodItem]：销毁只能运行时验证。进 Play 模式再点。");
                return;
            }

            Consume(totalNutrition);
        }

        #endregion
    }
}
