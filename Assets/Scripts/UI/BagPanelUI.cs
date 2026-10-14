using System;
using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
using UnityEditor;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 背包界面：左侧 5x5 网格 + 右侧三个装备槽（头 / 身 / 足）。
    ///
    /// 【分层】
    /// - <see cref="PlayerBag"/> 是后端：只管物品真正存进/移出背包、以及 OnMap / InBag / Equipped 的变换。
    ///   它不知道"手上正拿着什么"。
    /// - 本类是有状态的前端：手持物（<see cref="HandBagItem"/>）、拖动、预览、提示都在这里管。
    ///   手持物的"来源"（未拾取 / 从背包拿出 / 从槽位脱下）只有界面知道，所以必须记在前端。
    ///
    /// 【表现模型】
    /// - 网格是固定的 25 个格子（BagCellUI，黑底 Image），只负责"鼠标落在哪一格"和试放预览变绿/变红。
    /// - 物品由 <see cref="BagItemBlock"/> 整体表现：一个可拖拽、可整体旋转的块。
    ///   放进背包时挂到 _gridRoot 下对齐格子；拿在手上时停在 _itemSpritePivot；拖动中跟着鼠标。
    ///
    /// 【两个打开入口】
    /// - 按 B 打开：只看背包，没有手持物就不显示 _itemInfoRoot。
    /// - 捡起物品打开：后端的 PendingPickupItem 在打开时变成手持物，提示框随之显示。
    ///
    /// 【操作】
    /// - 左键按住物品块拖动：落在格子上就试放，放不下回原位；落在格子外就是丢弃
    /// - 左键把块拖到装备槽上：穿上去；槽里原有的那件送回世界并提示玩家
    /// - R：旋转手持物（块会重建）
    /// - 右键点装备槽：脱下来
    /// </summary>
    [DisallowMultipleComponent]
    public class BagPanelUI : MonoBehaviour
    {
        #region 配置常量

        /// <summary>网格列数。改这里必须同步 PlayerBag 的 Width</summary>
        private const int GridColumns = 5;

        /// <summary>网格行数。改这里必须同步 PlayerBag 的 Height</summary>
        private const int GridRows = 5;

        /// <summary>单格边长</summary>
        private const float CellSize = 100f;

        /// <summary>格与格的间隙</summary>
        private const float CellSpacing = 3f;

        /// <summary>格心间距（block 内相邻格的距离也是它）</summary>
        private const float CellPitch = CellSize + CellSpacing;

        private static readonly Color CellEmptyColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color PreviewOkColor = new Color(0.35f, 0.9f, 0.4f, 0.45f);
        private static readonly Color PreviewBadColor = new Color(0.95f, 0.3f, 0.3f, 0.45f);

        #endregion

        #region 序列化引用

        [Tooltip("背包根节点，右侧拉开")]
        [SerializeField] private RectTransform _bagRoot;
        
        [Header("网格 / 物料")]
        [Tooltip("背包网格根节点，应当是 512x512（100*5 + 3*4），pivot 在左上角 (0,0)。格子由代码生成在这里")]
        [SerializeField] private RectTransform _gridRoot;

        [Tooltip("物品提示框根节点。有东西可显示时才显示")]
        [SerializeField] private RectTransform _itemInfoRoot;

        [Tooltip("物品标记点：手持物停在这里。放在提示框中心")]
        [SerializeField] private RectTransform _itemSpritePivot;

        [Tooltip("拖动中的块临时挂到这里，保证它在最上层。")]
        [SerializeField] private RectTransform _dragLayer;

        [Tooltip("物品提示 - 标题")]
        [SerializeField] private TextMeshProUGUI _infoTextTitle;

        [Tooltip("物品提示 - 内容")]
        [SerializeField] private TextMeshProUGUI _infoTextContent;

        [Header("装备槽（三个现成的 Image）")]
        [Tooltip("TODO: 槽里的饰品图标怎么显示还没定，现在只保证拖上去能穿")]
        [SerializeField] private Image _slotHead;
        [SerializeField] private Image _slotBody;
        [SerializeField] private Image _slotLegs;


        [Header("背包组件")]
        [Tooltip("玩家的 PlayerBag。在 Inspector 里从场景里的 Player 预制体实例上拖过来")]
        [SerializeField] private PlayerBag _bag;

        [Header("排查用（平时关掉）")]
        [Tooltip("打开背包时打印一次：块里哪些 Graphic 能点、鼠标位置命中了哪些 UI。用来查\"图看得见但点不到\"")]
        [SerializeField] private bool _logInteractions;

        #endregion

        #region 运行时状态

        private readonly List<BagCellUI> _cells = new List<BagCellUI>();
        private readonly BagSlotUI[] _slots = new BagSlotUI[3];

        /// <summary>每件被界面显示过的物品对应的表现块</summary>
        private readonly Dictionary<PickableItem, BagItemBlock> _blocks = new Dictionary<PickableItem, BagItemBlock>();

        /// <summary>当前手持物（可能是从地图捡的、从背包拖出来的、从槽位脱下来的）</summary>
        private readonly HandBagItem _hand = new HandBagItem();

        private CanvasGroup _canvasGroup;
        private bool _visible;

        /// <summary>诊断用：上一帧鼠标最上层的 UI，只有变化时才打印</summary>
        private string _lastHoverDiagnostic;

        /// <summary>诊断用：EventSystem 状态只打一次</summary>
        private bool _loggedEventSystemState;


        #endregion

        #region 生命周期

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 兜底：UI 输入必须用自带 UI 动作，不能被赋成只有玩法 map 的 TapTapGameJam.inputactions
            EnsureUiInputModuleUsesDefaultActions();

            BuildGrid();
            BuildSlots();

            _itemInfoRoot?.gameObject.SetActive(false);
            ApplyVisibility();
        }

        private void Start()
        {
            EventPipeline.Subscribe<BackpackEvent>(OnBackpackEvent);
            GameManager.PlayerInputManager.EventOnRotateItemPress += OnRotateItemPressed;
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<BackpackEvent>(OnBackpackEvent);
            GameManager.PlayerInputManager.EventOnRotateItemPress -= OnRotateItemPressed;
        }

        private void LateUpdate()
        {
            if (!_visible)
            {
                return;
            }

            RecoverInterruptedDrags();

            // 手持物没在拖动时停在标记点；拖动中的位置由 BagItemBlock 自己管
            RefreshHandBlock();
            PruneStaleBlocks();

            if (_logInteractions)
            {
                LogHoverDiagnostics();
            }
        }

        /// <summary>
        /// 兜底：左键已经松开，但某个块还停在"拖拽中"。
        /// 出现这种情况通常是松开事件没收到（在窗口外松手、面板被关、块被重建……），
        /// 而卡住的后果很严重 —— 块会永远不再响应 OnPointerEnter，看起来就是"点不到"。
        /// </summary>
        private void RecoverInterruptedDrags()
        {
            if (Mouse.current == null || Mouse.current.leftButton.isPressed)
            {
                return;
            }

            foreach (KeyValuePair<PickableItem, BagItemBlock> pair in _blocks)
            {
                BagItemBlock block = pair.Value;
                if (block == null || !block.IsDragging)
                {
                    continue;
                }

                block.ForceEndDrag(Mouse.current.position.ReadValue());
            }
        }
        

        #endregion

        #region 对外

        /// <summary>
        /// 打开背包。**只监听 BackpackEvent 来触发，不要从别处调**：
        /// 捡东西那条链路是 PlayerBag.PickUp 把物品记成 PendingPickupItem 后自己发 BackpackEvent(true)，
        /// 界面在这里检查那个值，有就把它变成手持物并建块。
        /// </summary>
        private void OpenBag()
        {
            if (_bag == null)
            {
                return;
            }

            SyncBlocksWithBag();

            // 从地图捡起来、还没安顿的那件 → 变成手持物，提示框随之显示。
            // 这里只"检查"不"消费"：PendingPickupItem 由 PlayerBag 自己维护，
            // 物品真的进了背包或有结果时它会清掉。
            PickableItem pending = _bag.PendingPickupItem;
            if (pending != null && !_hand.IsValid)
            {
                _hand.Item = pending;
                _hand.Block = EnsureHandBlock(pending);
                _hand.Origin = HandItemOrigin.Unpicked;
            }

            RefreshHandBlock();
            SetItemInfo(_hand.Item);
        }

        /// <summary>
        /// 收起背包。除了清界面上的块，还要把后端那个"待拾取"标记清掉：
        /// 关掉界面就等于这次拾取结束了，免得下次打开又冒出来一件已经不在手上的东西。
        /// </summary>
        private void CloseBag()
        {
            // 兜底：所有"不在背包格子里"的物品块都删掉，并让它们对应的物品回到世界。
            // 本来就在地图上的（OnMap）不会重复处理，ReturnToWorld 内部会跳过。
            List<PickableItem> loose = null;
            foreach (KeyValuePair<PickableItem, BagItemBlock> pair in _blocks)
            {
                BagItemBlock block = pair.Value;
                if (pair.Key != null && block != null && block.transform.parent == _gridRoot)
                {
                    continue;
                }

                (loose ??= new List<PickableItem>()).Add(pair.Key);

                if (block != null)
                {
                    Destroy(block.gameObject);
                }
            }

            if (loose != null)
            {
                for (int i = 0; i < loose.Count; i++)
                {
                    _bag?.DropToWorld(loose[i]);
                }
            }

            _blocks.Clear();
            _hand.Clear();
            _bag?.ClearPendingPickupItem();
            _itemInfoRoot?.gameObject.SetActive(false);
            HidePreview();
        }

        #endregion
        #region 网格 / 槽位构建

        private void BuildGrid()
        {
            Assert.IsTrue(_gridRoot);

            // 格子和鼠标换算都假设 pivot 在左上角。改成中心 pivot 的话所有坐标都要重算
            if (_gridRoot.pivot != Vector2.zero)
            {
                Debug.LogWarning($"[BagPanelUI:{name}]：_gridRoot 的 pivot 是 {_gridRoot.pivot}，" +
                                 "应该设成 (0,0) 左上角，否则格子和鼠标落点会整体偏掉", this);
            }

            _cells.Clear();

            // 清掉旧格子（可能来自上一次进 Play 的残留）
            for (int i = _gridRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _gridRoot.GetChild(i);
                if (child.GetComponent<BagCellUI>() != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            for (int index = 0; index < GridColumns * GridRows; index++)
            {
                Vector2Int cell = new Vector2Int(index % GridColumns, index / GridColumns);
                _cells.Add(CreateCell(cell));
            }
        }

        private BagCellUI CreateCell(Vector2Int cell)
        {
            GameObject go = new GameObject($"Cell_{cell.x}_{cell.y}", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(_gridRoot, false);
            rect.anchorMin = new Vector2(0f, 1f); // 以网格左上角为基准
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f); // 中心对齐，方便和 block 的格心对齐
            rect.sizeDelta = new Vector2(CellSize, CellSize);
            rect.anchoredPosition = CellLocalPosition(cell) + new Vector2(CellSize * 0.5f, -CellSize * 0.5f);

            Image background = EnsureImage(go, CellEmptyColor, true);
            Image preview = CreateStretchImage(rect, "Preview", PreviewOkColor, false);

            BagCellUI cellUI = go.AddComponent<BagCellUI>();
            cellUI.Initialize(cell, background, preview, CellEmptyColor);
            cellUI.bagPanelUI = this;
            return cellUI;
        }

        public void helloworld()
        {
            Debug.Log("Hello world");
        }

        /// <summary>给三个现成的槽位 Image 挂上 BagSlotUI，让它们能接受拖放和点击</summary>
        private void BuildSlots()
        {
            Image[] images = { _slotHead, _slotBody, _slotLegs };
            for (int i = 0; i < images.Length && i < _slots.Length; i++)
            {
                if (images[i] == null)
                {
                    continue;
                }

                BagSlotUI slotUI = images[i].GetComponent<BagSlotUI>();
                if (slotUI == null)
                {
                    slotUI = images[i].gameObject.AddComponent<BagSlotUI>();
                }

                slotUI.Initialize((EquipSlot)i, images[i]);
                slotUI.bagPanelUI = this;
                _slots[i] = slotUI;
            }
        }

        /// <summary>
        /// 格子中心相对 _gridRoot 左上角的位置（x 向右、y 向下，所以 y 取负）。
        /// block 内的格子也是按同一个公式排的，两层才能正好重合。
        /// </summary>
        private static Vector2 CellLocalPosition(Vector2Int cell)
        {
            return new Vector2(cell.x * CellPitch, -cell.y * CellPitch);
        }

        #endregion

        #region BagItemBlock 生成 / 摆放

        /// <summary>
        /// 为一件物品生成它的表现块。
        ///
        /// 步骤（全部在"未旋转"的偏移上排好，最后整体旋转父节点）：
        /// 1. 建一个空物体 BagItemBlock 作为父节点，pivot 居中在抓取格上（= 旋转轴）；
        /// 2. 按 shape 当前朝向的偏移生成 CellFrame（100x100 纯色 Image），中心放在
        ///    偏移 * 格心间距 处，颜色取 shape.colorInGrid；这样 (0,0)（抓取格）天然在父节点原点；
        /// 3. 给物品图建 Image：anchor/pivot 都居中，大小 = bbox 格数 * 格心间距，
        ///    位置 = bbox 中心相对抓取格的位移，这样图正好盖住整个 bbox；
        /// 4. 按 shape.RotationSteps 整体旋转父节点（UI 里顺时针是 Z 负方向）。
        ///
        /// 第 2 步和第 4 步必须配套：偏移取"旋转后"的、父节点也转同样的角度，
        /// 两者算出来的落点才会重合。
        /// </summary>
        private BagItemBlock GenerateBagItemBlock(PickableItem item)
        {
            if (item == null || item.shape == null)
            {
                return null;
            }

            GameObject go = new GameObject($"BagItemBlock_{item.name}", typeof(RectTransform));
            RectTransform root = (RectTransform)go.transform;
            root.SetParent(_dragLayer, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f); // 旋转轴 = 抓取格中心
            root.sizeDelta = Vector2.zero;
            root.anchoredPosition = Vector2.zero;

            BagItemBlock block = go.AddComponent<BagItemBlock>();
            block.boundedItem = item;
            block.Panel = this;

            List<Vector2Int> offsets = item.shape.Offsets;

            if (_logInteractions)
            {
                Debug.Log($"[BagDiag] 生成块 {item.name}：initialOffsets({item.shape.initialOffsets.Count}) = " +
                          $"[{string.Join(",", item.shape.initialOffsets)}]，" +
                          $"Offsets({offsets.Count}) = [{string.Join(",", offsets)}]，" +
                          $"RotationSteps={item.shape.RotationSteps}", this);
            }

            for (int i = 0; i < offsets.Count; i++)
            {
                AddCellFrame(root, offsets[i], item.shape.colorInGrid);
            }

            AddItemSprite(root, item, offsets);

            if (item.shape.RotationSteps != 0)
            {
                root.localRotation = Quaternion.Euler(0f, 0f, -90f * item.shape.RotationSteps);
            }

            return block;
        }

        private static void AddCellFrame(Transform parent, Vector2Int offset, Color color)
        {
            GameObject go = new GameObject($"CellFrame_{offset.x}_{offset.y}", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CellSize, CellSize);
            rect.anchoredPosition = CellLocalPosition(offset);

            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false; // 点选留给网格格子和物品图，格子框不参与射线
        }

        private static void AddItemSprite(Transform parent, PickableItem item, List<Vector2Int> offsets)
        {
            Sprite icon = item.BagIcon;
            if (icon == null)
            {
                // 还没配图：只显示格子框。TODO: 需要的话在这里画一个占位色块
                return;
            }

            BagShape.GetBounds(offsets, out Vector2Int min, out Vector2Int max);
            Vector2Int sizeInCells = max - min + Vector2Int.one;

            GameObject go = new GameObject("Sprite", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            // anchor 和 pivot 都取中心：anchor 落在父节点原点（抓取格中心），
            // pivot 居中意味着"物品图的中心"在 anchoredPosition 处。
            // 图要盖住整个 bbox，所以它的中心应该在 bbox 中心，而 bbox 中心相对抓取格
            // 就是 min/max 的中点 —— 一步平移就够。
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(sizeInCells.x * CellPitch, sizeInCells.y * CellPitch);

            Vector2 center = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
            rect.anchoredPosition = new Vector2(center.x * CellPitch, -center.y * CellPitch);

            Image image = go.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            // 拖拽和悬停的射线必须打中物品图（块的空物体和格子框都不参与射线）
            image.raycastTarget = true;
        }

        /// <summary>
        /// 每帧判断"鼠标下方最上层的 UI 是谁"，只在这个结果**发生变化**时打印。
        /// 除了命中列表，还会把所有物品块在屏幕上的位置和矩形打出来 ——
        /// 这样能一眼看出"块到底画在哪儿、鼠标在不在它上面"。
        /// 不用时把 _logInteractions 关掉。
        /// </summary>
        private void LogHoverDiagnostics()
        {
            if (EventSystem.current == null)
            {
                Debug.LogError("[BagDiag] 场景里没有 EventSystem", this);
                _logInteractions = false;
                return;
            }

            if (Mouse.current == null)
            {
                return;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            PointerEventData data = new PointerEventData(EventSystem.current) { position = mousePosition };

            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);

            // 鼠标是不是落在某个物品块上？（用块的矩形判定，不依赖射线）
            BagItemBlock blockUnderMouse = null;
            foreach (KeyValuePair<PickableItem, BagItemBlock> pair in _blocks)
            {
                if (BlockContainsScreenPoint(pair.Value, mousePosition))
                {
                    blockUnderMouse = pair.Value;
                    break;
                }
            }

            string key = (hits.Count > 0 ? GetPath(hits[0].gameObject.transform) : "<打不到任何 UI>")
                         + "|" + (blockUnderMouse != null ? blockUnderMouse.name : "-");

            if (key == _lastHoverDiagnostic)
            {
                return;
            }

            _lastHoverDiagnostic = key;

            if (!_loggedEventSystemState)
            {
                _loggedEventSystemState = true;
                LogEventSystemState();
            }

            Debug.Log($"[BagDiag] 鼠标 {mousePosition}｜最上层 UI = " +
                      $"{(hits.Count > 0 ? GetPath(hits[0].gameObject.transform) : "<打不到任何 UI>")}（命中 {hits.Count} 个）", this);

            for (int i = 1; i < hits.Count && i <= 4; i++)
            {
                Debug.Log($"    下层{i}: {GetPath(hits[i].gameObject.transform)}", hits[i].gameObject);
            }

            if (blockUnderMouse != null)
            {
                Debug.Log($"    ✔ 鼠标落在物品块上：{GetPath(blockUnderMouse.transform)}", blockUnderMouse);
            }

            // 把所有块画在哪儿列出来（屏幕坐标 + 尺寸），方便和肉眼看到的对一下
            foreach (KeyValuePair<PickableItem, BagItemBlock> pair in _blocks)
            {
                BagItemBlock block = pair.Value;
                if (block == null)
                {
                    continue;
                }

                if (BlockContentScreenRect(block, out Rect screenRect))
                {
                    Debug.Log($"    · 块 {GetPath(block.transform)} 屏幕矩形={screenRect} " +
                              $"父={block.transform.parent?.name} " +
                              $"{(screenRect.Contains(mousePosition) ? "← 鼠标在这里面" : string.Empty)}", block);
                }
                else
                {
                    Debug.LogWarning($"    · 块 {GetPath(block.transform)} 没有任何可见子物体（包围盒为空）", block);
                }
            }
        }

        /// <summary>
        /// 打印 EventSystem 的运行时状态。专门用来查"UI 完全点不动"：
        /// - 模块在哪、有没有启用；
        /// - 它的 Point / LeftClick 动作是空的还是绑上了；
        /// - 当前鼠标指针有没有被 EventSystem 认为"在 UI 上"。
        /// 只打一次。
        /// </summary>
        private void LogEventSystemState()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogError("[BagDiag] EventSystem.current 是 null —— 场景里没有激活的 EventSystem", this);
                return;
            }

            Debug.Log($"[BagDiag][EventSystem] 对象={GetPath(eventSystem.transform)} " +
                      $"enabled={eventSystem.enabled} activeInHierarchy={eventSystem.gameObject.activeInHierarchy} " +
                      $"dragThreshold={eventSystem.pixelDragThreshold}", eventSystem);

            if (eventSystem.currentInputModule == null)
            {
                Debug.LogError("[BagDiag][EventSystem] currentInputModule 是 null（没有任何输入模块在跑）", eventSystem);
                return;
            }

            Debug.Log($"[BagDiag][EventSystem] 输入模块={eventSystem.currentInputModule.GetType().Name} " +
                      $"enabled={eventSystem.currentInputModule.enabled}", eventSystem);

            if (eventSystem.currentInputModule is InputSystemUIInputModule module)
            {
                // 只要这里不是 DefaultInputActions，UI 动作就可能是空的 —— 点击会完全失效。
                // 注意：pointAction / leftClickAction 这些字段不是公开 API，所以这里只做名字层面的判断。
                string assetName = module.actionsAsset != null ? module.actionsAsset.name : "null";
                bool looksRight = assetName.Contains("DefaultInputActions");

                if (looksRight)
                {
                    Debug.Log($"[BagDiag][EventSystem] actionsAsset={assetName}（看起来是正确的 UI 动作资源）", module);
                }
                else
                {
                    Debug.LogError($"[BagDiag][EventSystem] actionsAsset={assetName} —— " +
                                   "不是 Input System 自带的 DefaultInputActions，UI 动作多半是空的，点击会全部失效。" +
                                   "请选中 EventSystem → Input System UI Input Module 组件 → 右上角菜单 Reset", module);
                }
            }

            Debug.Log($"[BagDiag][EventSystem] IsPointerOverGameObject={eventSystem.IsPointerOverGameObject()}", eventSystem);
        }

        /// <summary>
        /// 保证 EventSystem 的 UI 模块用的是**自带 UI 动作**，而不是玩家输入资源。
        ///
        /// 为什么需要这一步：`TapTapGameJam.inputactions` 里只有 DayPlayer / ESCMenu / Backpack / MiniGame，
        /// **没有名为 "UI" 的 ActionMap**。一旦它被赋给 UI 模块，Point / LeftClick 等动作全部会是空的，
        /// 结果是整个游戏的 UI 点击全废 —— 而且不报错，非常难查。
        ///
        /// 玩家输入（PlayerInput）和 UI 输入（EventSystem）本来就该用两份不同的资源：
        /// 前者是玩法按键，后者是鼠标/键盘的 UI 导航，混在一份里只会互相拖累。
        /// </summary>
        private static void EnsureUiInputModuleUsesDefaultActions()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogError("[BagPanelUI]：场景里没有 EventSystem，所有 UI 交互都不会工作");
                return;
            }

            if (eventSystem.currentInputModule is not InputSystemUIInputModule module)
            {
                return;
            }

            bool badAsset = module.actionsAsset == null
                            || module.actionsAsset.FindActionMap("UI", false) == null;

            if (!badAsset)
            {
                return;
            }

            string assetName = module.actionsAsset != null ? module.actionsAsset.name : "null";
            Debug.LogError($"[BagPanelUI]：EventSystem 的 UI 动作资源是 {assetName}，它里面没有 \"UI\" ActionMap，" +
                           "所有 UI 点击都会失效。已自动改用 Input System 自带的 DefaultInputActions 兜底。\n" +
                           "  根因通常是：Player 上的 PlayerInput 组件把它的 UI Input Module 字段指向了这个 UI 模块，" +
                           "启动时 PlayerInput 会把自己的 actions 分发给 UI 模块，覆盖掉正确的配置。\n" +
                           "  修法：选中 Player → PlayerInput → 把 UI Input Module 设为 None；" +
                           "再选中 EventSystem → Input System UI Input Module → 右上角菜单 Reset。", module);

            module.AssignDefaultActions();
        }

        private static bool BlockContainsScreenPoint(BagItemBlock block, Vector2 screenPosition)        {
            if (BlockContentScreenRect(block, out Rect screenRect))
            {
                return screenRect.Contains(screenPosition);
            }

            return false;
        }

        /// <summary>
        /// 块在屏幕上的实际范围。
        /// 注意不能用块自己的 rect —— 它是个空物体（sizeDelta = 0），
        /// 得用子物体（格子框 + 物品图）算出来的包围盒。
        /// </summary>
        private static bool BlockContentScreenRect(BagItemBlock block, out Rect screenRect)
        {
            screenRect = default;

            RectTransform rect = block != null ? block.transform as RectTransform : null;
            if (rect == null || !rect.gameObject.activeInHierarchy)
            {
                return false;
            }

            Bounds content = RectTransformUtility.CalculateRelativeRectTransformBounds(rect);
            if (content.size.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 worldCenter = rect.TransformPoint(content.center);
            Vector3 worldMin = rect.TransformPoint(content.min);
            Vector3 worldMax = rect.TransformPoint(content.max);

            Vector2 screenA = RectTransformUtility.WorldToScreenPoint(null, worldMin);
            Vector2 screenB = RectTransformUtility.WorldToScreenPoint(null, worldMax);

            Vector2 min = Vector2.Min(screenA, screenB);
            Vector2 max = Vector2.Max(screenA, screenB);
            screenRect = new Rect(min, max - min);
            return true;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                path = p.name + "/" + path;
            }

            return path;
        }

        private BagItemBlock GetOrCreateBlock(PickableItem item)
        {
            if (item == null)
            {
                return null;
            }

            if (_blocks.TryGetValue(item, out BagItemBlock block) && block != null)
            {
                return block;
            }

            block = GenerateBagItemBlock(item);
            if (block != null)
            {
                _blocks[item] = block;
            }

            return block;
        }

        /// <summary>把某件物品的块从登记表里摘掉（块自己负责销毁）</summary>
        public void ForgetBlock(PickableItem item)
        {
            if (item != null)
            {
                _blocks.Remove(item);
            }
        }

        /// <summary>给手持物准备一个块并挂到拖拽层，返回它</summary>
        private BagItemBlock EnsureHandBlock(PickableItem item)
        {
            BagItemBlock block = GetOrCreateBlock(item);
            if (block != null)
            {
                block.transform.SetParent(_dragLayer, false);
            }

            return block;
        }

        /// <summary>让背包里所有已放置物品的块都挂到网格下并对齐格子</summary>
        private void SyncBlocksWithBag()
        {
            if (_bag == null || _gridRoot == null)
            {
                return;
            }

            for (int i = 0; i < _cells.Count; i++)
            {
                PickableItem occupant = _bag.ItemAt(_cells[i].Cell);
                if (occupant == null || occupant == _hand.Item)
                {
                    continue;
                }

                if (!_bag.TryGetPlacement(occupant, out Vector2Int origin))
                {
                    continue;
                }

                BagItemBlock block = GetOrCreateBlock(occupant);
                if (block == null)
                {
                    continue;
                }

                block.transform.SetParent(_gridRoot, false);
                block.transform.localPosition = CellLocalPosition(origin);
                block.transform.localRotation = Quaternion.identity; // 朝向已经烘进 offsets，块本身不再转
            }
        }

        /// <summary>手持物没被拖动时，把它停在 _itemSpritePivot</summary>
        private void RefreshHandBlock()
        {
            if (!_hand.IsValid || _hand.Block.IsDragging)
            {
                return;
            }

            BagItemBlock block = EnsureHandBlock(_hand.Item);
            if (block == null)
            {
                return;
            }

            _hand.Block = block;
            block.transform.SetParent(_itemSpritePivot != null ? _itemSpritePivot : _dragLayer, false);
            block.transform.localPosition = Vector3.zero;
        }

        /// <summary>扔掉已经不属于背包、也不在手上的块（例如被右键用掉了）</summary>
        private void PruneStaleBlocks()
        {
            if (_blocks.Count == 0)
            {
                return;
            }

            List<PickableItem> stale = null;
            foreach (KeyValuePair<PickableItem, BagItemBlock> pair in _blocks)
            {
                if (pair.Key == _hand.Item)
                {
                    continue; // 手上那件由 RefreshHandBlock / 拖拽逻辑负责
                }

                PickableItem.ItemLocation location = pair.Key != null
                    ? pair.Key.Location
                    : PickableItem.ItemLocation.OnMap;

                bool stillOurs = location == PickableItem.ItemLocation.InBag
                                 || location == PickableItem.ItemLocation.Equipped;

                if (!stillOurs)
                {
                    (stale ??= new List<PickableItem>()).Add(pair.Key);
                }
            }

            if (stale == null)
            {
                return;
            }

            for (int i = 0; i < stale.Count; i++)
            {
                if (_blocks.TryGetValue(stale[i], out BagItemBlock block) && block != null)
                {
                    Destroy(block.gameObject);
                }

                _blocks.Remove(stale[i]);
            }
        }

        #endregion

        #region 手持物

        /// <summary>当前是否空着手</summary>
        public bool HasHandItem => _hand.IsValid;

        /// <summary>手持物对应的块</summary>
        public BagItemBlock HandBlock => _hand.Block;

        /// <summary>
        /// 安顿手上那件：已经在背包里有落点的什么都不做，
        /// 否则找地方放进去，背包塞不下就丢到玩家面前。
        /// 用在"手上这件还没放下，又要拿起新的一件"的场合。
        /// </summary>
        private void ResolveHandItem()
        {
            if (!_hand.IsValid)
            {
                _hand.Clear();
                return;
            }

            PickableItem item = _hand.Item;
            BagItemBlock block = _hand.Block;

            if (_bag != null && _bag.TryGetPlacement(item, out _))
            {
                _hand.Clear();
                return;
            }

            if (_bag != null && _bag.TryPlace(item, out _))
            {
                if (block != null)
                {
                    block.transform.SetParent(_gridRoot, false);
                }

                _hand.Clear();
                return;
            }

            _bag?.DropToWorld(item);
            if (block != null)
            {
                Destroy(block.gameObject);
            }

            ForgetBlock(item);
            _hand.Clear();
        }

        /// <summary>提示框：有手持物就显示它，没有就收起来</summary>
        private void SetItemInfo(PickableItem item)
        {
            Assert.IsTrue(item);
            Assert.IsTrue(_infoTextTitle);
            Assert.IsTrue(_infoTextContent);
            Assert.IsTrue(_itemInfoRoot);

            // TODO: 物品名现在取的是 GameObject 名字，需要的话给 PickableItem 加一个显示名字段
            _infoTextTitle.text = item.name;
            _infoTextContent.text = item.HoverPrompt;
            _itemInfoRoot.gameObject.SetActive(true);
        }

        /// <summary>R 键按下：旋转手持物并重建它的块</summary>
        private void OnRotateItemPressed()
        {
            if (!_hand.IsValid)
            {
                return;
            }

            PickableItem item = _hand.Item;
            item.shape?.RotateClockwise();
            
            _hand.Block.SyncRotationWithItem();
        }

        #endregion

        #region 拖拽（由 BagItemBlock 调用）

        /// <summary>
        /// 开始拖拽某个块。这里负责：
        /// 1. 安顿上一个还没放下的手持物；
        /// 2. 记录这件东西是从哪来的（未拾取 / 背包格子 / 装备槽），放不下时要回得去；
        /// 3. 把它从后端摘出来 —— 也就是"从背包里拿出来了"。
        /// </summary>
        public void BeginBlockDrag(BagItemBlock block)
        {
            if (block == null || block.boundedItem == null || _bag == null)
            {
                return;
            }

            PickableItem item = block.boundedItem;
            if (_hand.IsValid && _hand.Block == block)
            {
                return; // 已经在手上的那件，直接继续拖
            }

            ResolveHandItem();

            _hand.Clear();
            _hand.Item = item;
            _hand.Block = block;

            if (_bag.TryGetEquipSlot(item, out EquipSlot slot))
            {
                _hand.Origin = HandItemOrigin.FromSlot;
                _hand.FromSlot = slot;
                _bag.TakeFromSlot(slot, out _);
            }
            else if (_bag.TryGetPlacement(item, out Vector2Int origin))
            {
                _hand.Origin = HandItemOrigin.FromBag;
                _hand.FromOrigin = origin;
                _bag.TakeFromGrid(item);
            }
            else
            {
                _hand.Origin = HandItemOrigin.Unpicked;
                _bag.ClearPendingPickupItem();
            }

            SetItemInfo(item);
        }

        /// <summary>拖拽经过某格时刷新试放预览</summary>
        public void NotifyBlockDragging(BagItemBlock block, Vector2 screenPosition)
        {
            if (!_hand.IsValid || _bag == null)
            {
                return;
            }

            HidePreview();

            if (!TryGetCellUnderScreenPoint(screenPosition, out Vector2Int origin))
            {
                // 鼠标不在网格里：一格都不亮，松手就是丢弃
                return;
            }

            bool ok = _bag.CanPlace(_hand.Item, origin);
            foreach (Vector2Int cell in _bag.CellsOf(_hand.Item, origin))
            {
                if (TryGetCell(cell, out BagCellUI cellUI))
                {
                    cellUI.SetPreview(true, ok ? PreviewOkColor : PreviewBadColor);
                }
            }
        }

        /// <summary>
        /// 松手。按这个顺序判定：装备槽 → 背包格子 → 回原位（未拾取的回物品标记点）。
        /// </summary>
        public void EndBlockDrag(BagItemBlock block, Vector2 screenPosition)
        {
            HidePreview();

            if (!_hand.IsValid || _bag == null || block == null)
            {
                return;
            }

            PickableItem item = _hand.Item;

            if (TryGetSlotUnderScreenPoint(screenPosition, out BagSlotUI slot) && TryEquipToSlot(slot))
            {
                return;
            }

            if (TryGetCellUnderScreenPoint(screenPosition, out Vector2Int cell))
            {
                if (_bag.TryPlace(item, cell))
                {
                    OnPlacedIntoBag(block);
                    return;
                }

                if (RestoreHandItemToOrigin())
                {
                    return;
                }
            }

            // 丢在背包外面（或者放不下又回不了原位）：送回世界，块删掉
            DiscardHandItem();
        }

        private void OnPlacedIntoBag(BagItemBlock block)
        {
            block.transform.SetParent(_gridRoot, false);
            block.transform.localRotation = Quaternion.identity;

            _hand.Clear();
            _bag.ClearPendingPickupItem(); // 已经安顿好了，不再是"待拾取"
            SetItemInfo(null);

            // 挪位置之后，其他块的坐标也可能需要跟上
            SyncBlocksWithBag();
        }

        /// <summary>放不下时把东西还回原来的位置。还回去了返回 true</summary>
        private bool RestoreHandItemToOrigin()
        {
            PickableItem item = _hand.Item;
            BagItemBlock block = _hand.Block;

            switch (_hand.Origin)
            {
                case HandItemOrigin.FromBag:
                    // 回原来的格子；原位被占了就退而求其次找个空位
                    if (_bag.TryPlace(item, _hand.FromOrigin) || _bag.TryPlace(item, out _))
                    {
                        block.transform.SetParent(_gridRoot, false);
                        _hand.Clear();
                        SetItemInfo(null);
                        SyncBlocksWithBag();
                        return true;
                    }

                    return false;

                case HandItemOrigin.FromSlot:
                    // 穿回原来的槽（正常一定成功，因为槽刚被我们腾空了）
                    if (_bag.TryEquip(item, out _))
                    {
                        _hand.Clear();
                        SetItemInfo(null);
                        return true;
                    }

                    return false;

                default:
                    // 未拾取的：回到物品标记点，继续等玩家放
                    block.transform.SetParent(_itemSpritePivot != null ? _itemSpritePivot : _dragLayer, false);
                    block.transform.localPosition = Vector3.zero;
                    SetItemInfo(item);
                    return true;
            }
        }

        /// <summary>把手持物送回世界并删掉它的块</summary>
        private void DiscardHandItem()
        {
            PickableItem item = _hand.Item;
            BagItemBlock block = _hand.Block;

            _bag?.DropToWorld(item);

            if (block != null)
            {
                ForgetBlock(item);
                Destroy(block.gameObject);
            }

            _hand.Clear();
            SetItemInfo(null);
        }

        /// <summary>把东西穿到槽位上。槽里原有的那件送回世界并提示玩家</summary>
        private bool TryEquipToSlot(BagSlotUI slot)
        {
            if (slot == null || !_hand.IsValid || _bag == null)
            {
                return false;
            }

            PickableItem item = _hand.Item;
            if (!_bag.TryEquip(item, out PickableItem replaced))
            {
                return false;
            }

            BagItemBlock block = _hand.Block;
            if (block != null)
            {
                ForgetBlock(item);
                Destroy(block.gameObject);
            }

            _hand.Clear();

            if (replaced != null)
            {
                // TODO: 现在是把换下来的那件送回世界并提示；将来若要"放回背包格子"改这里
                _bag.ReturnToWorld(replaced, _bag.EvaluateDropPosition());
                SetItemInfo(replaced);
            }
            else
            {
                SetItemInfo(null);
            }

            return true;
        }

        /// <summary>鼠标悬停到某个块上（没在拖拽时）</summary>
        public void NotifyBlockHovered(BagItemBlock block, bool entered)
        {
            if (!entered || block == null)
            {
                return;
            }

            SetItemInfo(block.boundedItem);
        }

        /// <summary>拖拽结束后面板侧的收尾</summary>
        public void OnBlockDragFinished(BagItemBlock block)
        {
            HidePreview();
        }

        #endregion

        #region 指针回调（由 BagCellUI / BagSlotUI 转发）

        public void NotifyCellHovered(BagCellUI cell, bool entered)
        {
            HandleCellHovered(cell, entered);
        }

        public void NotifyCellClicked(BagCellUI cell, PointerEventData.InputButton button)
        {
            HandleCellClicked(cell, button);
        }

        public void NotifySlotClicked(BagSlotUI slot, PointerEventData.InputButton button)
        {
            HandleSlotClicked(slot, button);
        }


        /// <summary>
        /// 鼠标落在某格上。通常这格被物品块盖住（事件先到块上），
        /// 所以这里主要是"块没盖住时也要能看提示"的兜底。
        /// </summary>
        private void HandleCellHovered(BagCellUI cell, bool entered)
        {
            if (!entered || _bag == null)
            {
                return;
            }

            SetItemInfo(_bag.ItemAt(cell.Cell));
        }

        /// <summary>右键点格子：使用该格物品</summary>
        private void HandleCellClicked(BagCellUI cell, PointerEventData.InputButton button)
        {
            if (_bag == null || button != PointerEventData.InputButton.Right)
            {
                return; // 左键的移动/摆放全部交给 BagItemBlock 的拖拽流程
            }

            PickableItem item = _bag.ItemAt(cell.Cell);
            if (item == null || !item.IsUseAble)
            {
                return;
            }

            if (_bag.UseItem(item, null))
            {
                return; // 用掉了：PlayerBag 已经销毁它，PruneStaleBlocks 会收掉它的块
            }

            // 没被消耗（例如饰品装备、食物放回地图）：它已经被摘出背包了，
            // 这里当成新的手持物接着处理。先把上一件还没放下的安顿好。
            ResolveHandItem();

            _hand.Item = item;
            _hand.Block = null;
            _hand.Origin = HandItemOrigin.FromBag;
            _hand.FromOrigin = cell.Cell;
            RefreshHandBlock();
            SetItemInfo(item);
        }

        /// <summary>右键装备槽：把饰品脱下来，变成"待拾取的物品"</summary>
        private void HandleSlotClicked(BagSlotUI slot, PointerEventData.InputButton button)
        {
            if (_bag == null || button != PointerEventData.InputButton.Right)
            {
                return; // 左键的穿/脱由拖拽流程负责
            }

            if (!_bag.TakeFromSlot(slot.Slot, out PickableItem item))
            {
                return;
            }

            // 登记成待拾取：下次打开背包它会以手持物的形式出现在标记点上
            _bag.SetPendingPickupItem(item);
        }

        #endregion

        #region 表现 / 工具

        private void ApplyVisibility()
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = _visible ? 1f : 0f;

            // 关键：alpha = 0 只影响渲染，不影响射线。
            // BagPanel 是铺满全屏的（anchor 0,0 → 1,1），底图又开着 RaycastTarget，
            // 所以隐藏时必须把 blocksRaycasts 关掉 —— 否则这张看不见的挡板会把
            // 场景里所有 UI（按钮、格子、其它面板）的点击全部吃掉。
            // 打开时才恢复成挡板，这样"点背包外面 = 丢弃物品"才能生效。
            _canvasGroup.blocksRaycasts = _visible;
            _canvasGroup.interactable = _visible;
        }

        /// <summary>清掉所有格子的试放预览</summary>
        public void HidePreview()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                _cells[i].ClearPreview();
            }
        }

        private void OnBackpackEvent(BackpackEvent evt)
        {
            bool isOpen = evt.IsOpen;
            if (_visible == isOpen)
            {
                return;
            }

            _visible = isOpen;

            if (isOpen)
            {
                HidePreview();
                OpenBag();
            }
            else
            {
                CloseBag();
            }

            ApplyVisibility();
        }

        /// <summary>屏幕坐标 → 格子坐标。网格 pivot 在左上角，所以 local (0,0) 就是第一格中心</summary>
        private Vector2Int ScreenToCell(Vector2 screenPosition)
        {
            if (_gridRoot == null)
            {
                return new Vector2Int(-1, -1);
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _gridRoot, screenPosition, null, out Vector2 local))
            {
                return new Vector2Int(-1, -1);
            }

            int x = Mathf.RoundToInt(local.x / CellPitch);
            int y = Mathf.RoundToInt(-local.y / CellPitch);
            return new Vector2Int(x, y);
        }

        /// <summary>屏幕坐标落在哪一格</summary>
        public bool TryGetCellUnderScreenPoint(Vector2 screenPosition, out Vector2Int cell)
        {
            cell = ScreenToCell(screenPosition);
            return _bag != null && _bag.InBounds(cell);
        }

        private bool TryGetCell(Vector2Int cell, out BagCellUI result)
        {
            result = null;
            if (_bag == null || !_bag.InBounds(cell))
            {
                return false;
            }

            int index = cell.y * GridColumns + cell.x;
            if (index < 0 || index >= _cells.Count)
            {
                return false;
            }

            result = _cells[index];
            return result != null;
        }

        /// <summary>屏幕坐标落在哪个装备槽上</summary>
        private bool TryGetSlotUnderScreenPoint(Vector2 screenPosition, out BagSlotUI slot)
        {
            slot = null;
            for (int i = 0; i < _slots.Length; i++)
            {
                BagSlotUI candidate = _slots[i];
                RectTransform rect = candidate != null ? candidate.transform as RectTransform : null;
                if (rect == null)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null))
                {
                    slot = candidate;
                    return true;
                }
            }

            return false;
        }

        private static Image EnsureImage(GameObject go, Color color, bool raycastTarget)
        {
            Image image = go.GetComponent<Image>();
            if (image == null)
            {
                image = go.AddComponent<Image>();
            }

            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static Image CreateStretchImage(Transform parent, string childName, Color color, bool raycastTarget)
        {
            GameObject go = new GameObject(childName, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return EnsureImage(go, color, raycastTarget);
        }


        #endregion
    }
}
