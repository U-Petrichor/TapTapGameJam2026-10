using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Player;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 背包界面：左侧网格 + 右侧三格装备栏（头 / 身 / 足）。
    ///
    /// 网格格子全部由代码生成，每一格是独立对象，负责自己的 hover 与点击反馈；
    /// 摆放规则、预览合法性、装备换装全部问 <see cref="PlayerBag"/>，
    /// 这个类只做"表现 + 把输入翻译成 PlayerBag 调用"。
    ///
    /// 操作：
    /// - 左键点格子：手上有东西 → 放下；手是空的且该格有物品 → 拿到手上移动
    /// - R：旋转手上的 piece
    /// - 右键点格子：使用该格物品（饰品装备、食物放回地图）
    /// - 左键点装备槽：手上是能穿的饰品 → 穿上；手是空的 → 脱回手上
    /// - 左键点背包外：把手上的东西丢回地图
    /// </summary>
    [DisallowMultipleComponent]
    public class BagPanelUI : MonoBehaviour
    {
        #region 颜色配置

        private static readonly Color WindowColor = new Color(0.08f, 0.09f, 0.12f, 0.94f);
        private static readonly Color GridBackColor = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color CellEmptyColor = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color CellSlotColor = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color CellOccupiedColor = new Color(1f, 1f, 1f, 0.28f);
        private static readonly Color PreviewOkColor = new Color(0.35f, 0.9f, 0.4f, 0.45f);
        private static readonly Color PreviewBadColor = new Color(0.95f, 0.3f, 0.3f, 0.45f);
        private static readonly Color GhostOkColor = new Color(0.45f, 0.95f, 0.5f, 0.85f);
        private static readonly Color GhostBadColor = new Color(0.95f, 0.4f, 0.4f, 0.85f);
        private static readonly Color FoodColor = new Color(0.85f, 0.75f, 0.4f, 1f);
        private static readonly Color DressColor = new Color(0.5f, 0.8f, 1f, 1f);
        private static readonly Color DefaultIconColor = new Color(0.85f, 0.85f, 0.9f, 1f);

        #endregion

        #region 序列化引用

        [Header("引用（留空则自动在子节点里找 / 代码创建）")]
        [Tooltip("背包网格的父节点，会挂 GridLayoutGroup")]
        [SerializeField] private RectTransform _gridRoot;
        [Tooltip("右侧装备槽的父节点")]
        [SerializeField] private RectTransform _slotRoot;
        [Tooltip("整个背包窗口，建议放一张半透明底板 Image 挡住背景点击")]
        [SerializeField] private RectTransform _window;
        [SerializeField] private TMP_Text _tipText;

        [Header("背包组件")]
        [Tooltip("玩家的 PlayerBag。在 Inspector 里从场景里的 Player 预制体实例上拖过来")]
        [SerializeField] private PlayerBag _bag;

        [Tooltip("玩家的输入管理器（和 PlayerBag 在同一个物体上）。留空则运行时自动取")]
        [SerializeField] private TapTapGameJam.Player.PlayerInputManager _playerInput;

        [Header("外观")]
        [SerializeField] private Vector2 _cellSize = new Vector2(88f, 88f);
        [SerializeField] private float _cellSpacing = 6f;
        [SerializeField] private Vector2 _slotSize = new Vector2(120f, 120f);
        [SerializeField] private float _slotSpacing = 12f;
        [SerializeField, Min(0.1f)] private float _tipSeconds = 2f;

        #endregion

        #region 运行时状态

        private CanvasGroup _canvasGroup;
        private readonly List<BagCellUI> _cells = new List<BagCellUI>();
        private readonly BagSlotUI[] _slotUIs = new BagSlotUI[3];
        private readonly List<Image> _ghostImages = new List<Image>();

        // 当前格子是按多大的背包铺的。和 PlayerBag 的尺寸不一致时重建格子
        private int _builtWidth = -1;
        private int _builtHeight = -1;

        private bool _visible;
        private bool _uiBuilt;
        private bool _layoutDirty;
        private bool _ignoreClickThisFrame;
        private int _openedFrame = -1;
        private float _tipUntil;
        private PickableItem _tipItem;

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[BagPanelUI]：场景里存在多个背包界面，{Instance.name} 会被覆盖", this);
            }

            Instance = this;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            ValidateReferences();

            EnsureUI();
            ApplyVisibility();
        }

        /// <summary>
        /// 配置自检。这里刻意报 error 而不是静默降级：
        /// 背包界面拿不到 PlayerBag 的话，按 B 会什么都没发生，很难查。
        /// </summary>
        private void ValidateReferences()
        {
            if (_bag == null)
            {
                Debug.LogError($"[BagPanelUI:{name}]：没有配置 PlayerBag。" +
                               "请在 Inspector 里把场景中 Player 实例上的 PlayerBag 拖到本组件的 _bag 上", this);
                return;
            }

            // 和 PlayerBag 挂在同一个物体上，直接取最准
            if (_playerInput == null)
            {
                _playerInput = _bag.GetComponent<TapTapGameJam.Player.PlayerInputManager>();
            }

            if (_playerInput == null)
            {
                Debug.LogError($"[BagPanelUI:{name}]：{_bag.name} 所在物体上没有 PlayerInputManager，" +
                               "按 B 收起背包会失效", this);
            }
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<BackpackEvent>(OnBackpackEvent);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<BackpackEvent>(OnBackpackEvent);
        }

        private void LateUpdate()
        {
            if (!_visible)
            {
                return;
            }

            Refresh();
            UpdateGhost();
            HandleInput();
            RefreshPreview();
            RefreshTip();
        }

        #endregion

        #region 对外

        /// <summary>打开/收起背包界面。BackpackEvent 是唯一入口</summary>
        public void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            if (!visible && _bag != null && _bag.HasHandItem)
            {
                // 手上还拿着东西就关界面：丢回地图，避免物品卡在"谁都找不到"的状态
                _bag.DropHandToWorld(_bag.EvaluateDropPosition());
            }

            _visible = visible;

            if (visible)
            {
                _ignoreClickThisFrame = true;  // 忽略"打开背包"那一次点击（B 键/拾取），别顺手把东西丢了
                _openedFrame = Time.frameCount; // 挡掉"打开那一帧的 B"，否则刚开就被自己关掉
                _layoutDirty = true;
            }
            else
            {
                _layoutDirty = false; // 布局要等下次打开再算，省掉关闭状态下的无用重建
                ClearPreview();
                ClearGhost();
                HideTip();
            }

            ApplyVisibility();
        }

        #endregion

        #region 程序化 UI 构建

        private void EnsureUI()
        {
            if (_uiBuilt)
            {
                return;
            }

            if (_window == null)
            {
                // 没配就退化用自己这个节点。注意：这种情况下整块面板就是全屏的，会挡住全屏点击，
                // 所以正式场景建议让 BagPanel 全屏、再挂一个居中的 Window 子节点指到 _window。
                Debug.LogWarning($"[BagPanelUI:{name}]：没有配置 Window，改用自身 RectTransform；" +
                                 "如果它铺满全屏，会挡住整个屏幕的点击", this);
                _window = transform as RectTransform;
            }

            if (_window != null)
            {
                EnsureImage(_window.gameObject, WindowColor, true);

                if (_gridRoot == null)
                {
                    _gridRoot = GetOrCreateChild(_window, "GridRoot");
                }

                if (_slotRoot == null)
                {
                    _slotRoot = GetOrCreateChild(_window, "Slots", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
                }

                if (_tipText == null)
                {
                    _tipText = CreateTip(_window);
                }
            }
            else
            {
                Debug.LogError($"[BagPanelUI:{name}]：没有可用的 RectTransform，无法生成背包界面", this);
                return;
            }

            EnsureImage(_gridRoot.gameObject, GridBackColor, false);
            CreateSlots();

            _uiBuilt = true;
        }

        private void CreateSlots()
        {
            for (int i = 0; i < 3; i++)
            {
                EquipSlot slot = (EquipSlot)i;
                RectTransform root = GetOrCreateChild(_slotRoot, $"Slot_{slot}");
                root.anchorMin = new Vector2(0f, 1f);
                root.anchorMax = new Vector2(0f, 1f);
                root.pivot = new Vector2(0f, 1f);
                root.sizeDelta = _slotSize;
                root.anchoredPosition = new Vector2(0f, -i * (_slotSize.y + _slotSpacing));

                Image background = EnsureImage(root.gameObject, CellSlotColor, true);
                Image icon = CreateStretchImage(root, "Icon", Color.white, false);
                TMP_Text label = CreateLabel(root, "Label", SlotName(slot), new Vector2(0f, 0f), TextAlignmentOptions.BottomLeft);

                BagSlotUI slotUI = root.GetComponent<BagSlotUI>();
                if (slotUI == null)
                {
                    slotUI = root.gameObject.AddComponent<BagSlotUI>();
                }

                slotUI.Initialize(slot, background, icon, label, CellSlotColor);
                _slotUIs[i] = slotUI;
            }
        }

        /// <summary>
        /// 手动重算格子位置。
        /// 不用 GridLayoutGroup：它的布局要等下一次 Canvas 布局才生效，而我们需要在同一帧就能
        /// 用格子的坐标把鼠标位置换算成格子索引。行列式摆法本来就只有两行代码，自己算更可控。
        /// </summary>
        private void RebuildLayout()
        {
            if (_bag == null)
            {
                return;
            }

            EnsureUI();

            int required = _bag.CellCount;

            // 原来格子数对不上（改了尺寸/换了背包）就整体重建
            if (_cells.Count != required)
            {
                for (int i = _gridRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = _gridRoot.GetChild(i);
                    if (child.GetComponent<BagCellUI>() != null)
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }

                _cells.Clear();

                for (int i = 0; i < required; i++)
                {
                    Vector2Int cell = new Vector2Int(i % _bag.Width, i / _bag.Width);
                    _cells.Add(CreateCell(cell));
                }
            }

            float pitchX = _cellSize.x + _cellSpacing;
            float pitchY = _cellSize.y + _cellSpacing;

            for (int i = 0; i < _cells.Count; i++)
            {
                RectTransform rect = _cells[i].transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                int column = i % _bag.Width;
                int row = i / _bag.Width;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = _cellSize;
                rect.anchoredPosition = new Vector2(column * pitchX, -row * pitchY);
            }

            // 网格底板贴着第一行/列，尺寸覆盖整个网格
            RectTransform gridRect = _gridRoot;
            gridRect.anchorMin = new Vector2(0f, 1f);
            gridRect.anchorMax = new Vector2(0f, 1f);
            gridRect.pivot = new Vector2(0f, 1f);
            gridRect.sizeDelta = new Vector2(
                _bag.Width * pitchX - _cellSpacing,
                _bag.Height * pitchY - _cellSpacing);

            _builtWidth = _bag.Width;
            _builtHeight = _bag.Height;
        }

        private BagCellUI CreateCell(Vector2Int cell)
        {
            RectTransform root = GetOrCreateChild(_gridRoot, $"Cell_{cell.x}_{cell.y}");
            Image background = EnsureImage(root.gameObject, CellEmptyColor, true);
            Image marker = CreateStretchImage(root, "Marker", DefaultIconColor, false);
            Image preview = CreateStretchImage(root, "Preview", PreviewOkColor, false);

            BagCellUI cellUI = root.GetComponent<BagCellUI>();
            if (cellUI == null)
            {
                cellUI = root.gameObject.AddComponent<BagCellUI>();
            }

            cellUI.Initialize(cell, background, marker, preview, CellEmptyColor);
            return cellUI;
        }

        private static RectTransform GetOrCreateChild(Transform parent, string childName)
        {
            return GetOrCreateChild(parent, childName, new Vector2(0f, 1f), new Vector2(0f, 1f));
        }

        private static RectTransform GetOrCreateChild(Transform parent, string childName, Vector2 anchorMin, Vector2 anchorMax)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing as RectTransform;
            }

            GameObject go = new GameObject(childName, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
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
            RectTransform rect = GetOrCreateChild(parent, childName);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return EnsureImage(rect.gameObject, color, raycastTarget);
        }

        private TMP_Text CreateLabel(Transform parent, string childName, string text, Vector2 anchoredPosition, TextAlignmentOptions alignment)
        {
            RectTransform rect = GetOrCreateChild(parent, childName);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-8f, -8f);

            TextMeshProUGUI label = rect.GetComponent<TextMeshProUGUI>();
            if (label == null)
            {
                label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            label.text = text;
            label.alignment = alignment;
            label.fontSize = 20f;
            label.color = new Color(0.9f, 0.92f, 0.95f, 0.85f);
            label.raycastTarget = false;
            return label;
        }

        private TMP_Text CreateTip(Transform parent)
        {
            RectTransform rect = GetOrCreateChild(parent, "Tip");
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 12f);
            rect.sizeDelta = new Vector2(900f, 40f);

            TextMeshProUGUI tip = rect.GetComponent<TextMeshProUGUI>();
            if (tip == null)
            {
                tip = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            tip.alignment = TextAlignmentOptions.Center;
            tip.fontSize = 22f;
            tip.raycastTarget = false;
            tip.text = string.Empty;
            return tip;
        }

        private static string SlotName(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Body: return "身";
                case EquipSlot.Legs: return "足";
                case EquipSlot.Head:
                default: return "头";
            }
        }

        #endregion

        #region 刷新表现

        private void ApplyVisibility()
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = _visible ? 1f : 0f;
            // 常驻可见的 Image 负责挡点击；CanvasGroup 只控制透明度，不用 blocksRaycasts 开关，
            // 避免"看不见的背包面板吃掉场景点击"这种坑
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
        }

        private void Refresh()
        {
            if (_layoutDirty)
            {
                _layoutDirty = false;
                RebuildLayout();
            }

            RefreshAll();
        }

        private void RefreshAll()
        {
            if (_bag == null)
            {
                return;
            }

            if (!_uiBuilt)
            {
                EnsureUI();
            }

            // 背包尺寸变了就整体重建格子
            if (_builtWidth != _bag.Width || _builtHeight != _bag.Height)
            {
                RebuildLayout();
            }

            // 格子底色 + 物品图标。
            // 数据每帧直接从 PlayerBag 读，所以没有"内容变化事件"也不会显示陈旧内容。
            for (int i = 0; i < _cells.Count; i++)
            {
                BagCellUI cell = _cells[i];
                PickableItem occupant = _bag.ItemAt(cell.Cell);
                cell.SetBaseColor(occupant == null ? CellEmptyColor : CellOccupiedColor);
                cell.SetMarker(null, Color.white);

                foreach (Vector2Int c in _bag.CellsOf(occupant))
                {
                    if (c == cell.Cell)
                    {
                        cell.SetMarker(ResolveIcon(occupant), IconColor(occupant));
                        break;
                    }
                }
            }

            for (int i = 0; i < _slotUIs.Length; i++)
            {
                if (_slotUIs[i] != null)
                {
                    _slotUIs[i].Show(_bag.GetEquipped((EquipSlot)i));
                }
            }
        }

        private void ClearPreview()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                _cells[i].ClearPreview();
            }
        }

        private void RefreshPreview()
        {
            ClearPreview();

            if (!_visible || _bag == null || !_bag.HasHandItem)
            {
                return;
            }

            Vector2Int origin = MouseToCell(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
            bool ok = _bag.CanPlace(_bag.HandItem, origin);

            // 形状允许负偏移，越界的那部分没有格子对象可画，跳过即可；
            // 鼠标在网格外时 origin 越界 → 一个格子都不会亮
            foreach (Vector2Int cell in _bag.CellsOf(_bag.HandItem, origin))
            {
                if (TryGetCell(cell, out BagCellUI cellUI))
                {
                    cellUI.SetPreview(true, ok ? PreviewOkColor : PreviewBadColor);
                }
            }
        }

        private void UpdateGhost()
        {
            if (_bag == null || !_bag.HasHandItem || Mouse.current == null)
            {
                ClearGhost();
                return;
            }

            PickableItem item = _bag.HandItem;
            List<Vector2Int> offsets = item.shape.offsets;
            EnsureGhostCount(offsets.Count);

            // 和 RefreshPreview 用同一个 origin，保证"预览绿 → 点下去一定放得下"
            Vector2Int origin = MouseToCell(Mouse.current.position.ReadValue());
            bool ok = _bag.CanPlace(item, origin);
            Sprite icon = ResolveIcon(item);
            Color ghostColor = ok ? GhostOkColor : GhostBadColor;

            if (!TryGetCellRect(origin, out RectTransform originRect))
            {
                // 鼠标不在网格里：不画预览（这时候点下去就是丢弃，不该给"能放"的错觉）
                ClearGhost();
                return;
            }

            // 用格子的世界坐标算，避免受 Canvas 缩放影响。
            // 格子和 Ghost 的 pivot 都在左上角，所以 Ghost 的 position（= 它的左上角）
            // 要减去自身尺寸的一半。
            Vector3 originTopLeft = originRect.position - new Vector3(_cellSize.x * 0.5f, _cellSize.y * 0.5f, 0f);

            for (int i = 0; i < _ghostImages.Count; i++)
            {
                Image ghost = _ghostImages[i];
                if (i >= offsets.Count)
                {
                    ghost.enabled = false;
                    continue;
                }

                ghost.enabled = true;
                ghost.sprite = icon;
                ghost.color = ghostColor;
                ghost.rectTransform.sizeDelta = _cellSize;

                // 形状的 y 向下，UI 的 y 向上，所以要取负
                Vector2 delta = new Vector2(offsets[i].x, -offsets[i].y) * (_cellSize.x + _cellSpacing);
                ghost.rectTransform.position = originTopLeft + new Vector3(delta.x, delta.y, 0f);
            }
        }

        private void EnsureGhostCount(int count)
        {
            Transform parent = _gridRoot != null ? _gridRoot.parent : transform;

            while (_ghostImages.Count < count)
            {
                GameObject go = new GameObject($"Ghost_{_ghostImages.Count}", typeof(RectTransform));
                RectTransform rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = _cellSize;

                Image image = go.AddComponent<Image>();
                image.raycastTarget = false; // 关键：不能让预览挡住格子的 hover 事件
                _ghostImages.Add(image);
            }
        }

        private void ClearGhost()
        {
            for (int i = 0; i < _ghostImages.Count; i++)
            {
                _ghostImages[i].enabled = false;
            }
        }

        #endregion

        #region 输入

        private void HandleInput()
        {
            if (Mouse.current == null || _bag == null)
            {
                return;
            }

            // 懒解析：GameManager 绑定背包发生在 Start，比这里的 Awake 晚，所以不能只解析一次
            if (_playerInput == null)
            {
                _playerInput = FindFirstObjectByType<TapTapGameJam.Player.PlayerInputManager>();
            }

            if (_ignoreClickThisFrame)
            {
                _ignoreClickThisFrame = false;
                return;
            }

            // 按 B 收起背包。
            // 为什么直接读键盘而不是靠 Backpack ActionMap 的 B 绑定：
            // 预制体里 `Backpack/B` 那个 action 没有绑定任何回调（只有 action 没有 persistent call），
            // 在代码里补一次最省事，也不依赖 Inspector 接线。
            // 用 _openedFrame 挡掉"打开那一帧的 B"，否则刚开就会被自己关掉。
            if (Time.frameCount != _openedFrame
                && Keyboard.current != null
                && Keyboard.current.bKey.wasPressedThisFrame)
            {
                _playerInput?.CloseBackpack();
                return;
            }

            // R 旋转手上的 piece。
            // 说明：这里直接读 Keyboard，而不是给 Backpack ActionMap 加 action ——
            // 加 action 要同时改 .inputactions 和 Player 预制体的 ActionEvents 数组，容易错位。
            if (_bag.HasHandItem && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                _bag.RotateHand();
            }

            bool left = Mouse.current.leftButton.wasPressedThisFrame;
            bool right = Mouse.current.rightButton.wasPressedThisFrame;
            if (!left && !right)
            {
                return;
            }

            // 背包整体是 UI：点在 UI 上就不可能是"丢到背包外面"
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            DropHandToWorld();
        }

        private void DropHandToWorld()
        {
            if (_bag == null || !_bag.HasHandItem)
            {
                return;
            }

            _bag.DropHandToWorld(_bag.EvaluateDropPosition());
            ShowTip("已丢回地图");
        }

        #endregion

        #region 指针回调（由 BagCellUI / BagSlotUI 转发）

        /// <summary>当前鼠标悬停到的物品，用于格子上的 HoverPrompt</summary>
        public static void NotifyCellHovered(BagCellUI cell, bool entered)
        {
            Instance?.HandleCellHovered(cell, entered);
        }

        public static void NotifyCellClicked(BagCellUI cell, PointerEventData.InputButton button)
        {
            Instance?.HandleCellClicked(cell, button);
        }

        public static void NotifySlotClicked(BagSlotUI slot, PointerEventData.InputButton button)
        {
            Instance?.HandleSlotClicked(slot, button);
        }

        /// <summary>把物品图标解析成 Sprite：优先用物品自己配的 bagIcon，没有就交给子类/兜底</summary>
        public static Sprite ResolveIcon(PickableItem item)
        {
            return item != null ? item.BagIcon : null;
        }

        private void HandleCellHovered(BagCellUI cell, bool entered)
        {
            if (_bag == null)
            {
                return;
            }

            if (!entered)
            {
                if (_tipItem != null)
                {
                    HideTip();
                }

                return;
            }

            PickableItem item = _bag.ItemAt(cell.Cell);
            _tipItem = item;
            if (item != null)
            {
                ShowTip(item.HoverPrompt);
            }
        }

        private void HandleCellClicked(BagCellUI cell, PointerEventData.InputButton button)
        {
            if (_bag == null)
            {
                return;
            }

            if (button == PointerEventData.InputButton.Right)
            {
                UseAt(cell.Cell);
                return;
            }

            if (button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (_bag.HasHandItem)
            {
                if (!_bag.PlaceHandAt(_bag.HandItem, cell.Cell))
                {
                    ShowTip("这里放不下");
                }

                return;
            }

            PickableItem occupant = _bag.ItemAt(cell.Cell);
            if (occupant != null)
            {
                _bag.TakeToHand(occupant);
            }
        }

        private void HandleSlotClicked(BagSlotUI slot, PointerEventData.InputButton button)
        {
            if (_bag == null)
            {
                return;
            }

            if (button == PointerEventData.InputButton.Right)
            {
                _bag.TryUnequip(slot.Slot);
                ShowTip("已脱下");
                return;
            }

            if (button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (_bag.HasHandItem)
            {
                if (!_bag.TryEquip(_bag.HandItem, out _))
                {
                    ShowTip("这件饰品不能穿在这个位置");
                }

                return;
            }

            if (_bag.TryUnequip(slot.Slot))
            {
                ShowTip("已拿到手上");
            }
        }

        private void UseAt(Vector2Int cell)
        {
            PickableItem item = _bag.ItemAt(cell);
            if (item == null)
            {
                return;
            }

            if (!item.IsUseAble)
            {
                ShowTip($"{item.name} 不能使用");
                return;
            }

            _bag.UseItem(item, null);
        }

        #endregion

        #region 工具

        private static BagPanelUI Instance { get; set; }

        private void OnBackpackEvent(BackpackEvent evt)
        {
            SetVisible(evt.IsOpen);
        }

        private Vector2Int MouseToCell(Vector2 screenPosition)
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

            float pitchX = _cellSize.x + _cellSpacing;
            float pitchY = _cellSize.y + _cellSpacing;

            // 网格 Rect 的 pivot 在左上角，所以 local (0,0) 就是第一格的左上角
            int x = Mathf.FloorToInt(local.x / pitchX);
            int y = Mathf.FloorToInt(-local.y / pitchY);
            return new Vector2Int(x, y);
        }

        private bool TryGetCell(Vector2Int cell, out BagCellUI result)
        {
            result = null;
            if (_bag == null || !_bag.InBounds(cell))
            {
                return false;
            }

            int index = cell.y * _bag.Width + cell.x;
            if (index < 0 || index >= _cells.Count)
            {
                return false;
            }

            result = _cells[index];
            return result != null;
        }

        private bool TryGetCellRect(Vector2Int cell, out RectTransform result)
        {
            result = null;
            return TryGetCell(cell, out BagCellUI cellUI) && (result = cellUI.transform as RectTransform) != null;
        }

        private static Color IconColor(PickableItem item)
        {
            if (item is FoodItem)
            {
                return FoodColor;
            }

            if (item is DressItem)
            {
                return DressColor;
            }

            return DefaultIconColor;
        }

        private void ShowTip(string text)
        {
            if (_tipText == null)
            {
                return;
            }

            _tipText.text = text;
            _tipUntil = Time.unscaledTime + _tipSeconds;
        }

        private void HideTip()
        {
            if (_tipText != null)
            {
                _tipText.text = string.Empty;
            }
        }

        private void RefreshTip()
        {
            if (_tipText == null || string.IsNullOrEmpty(_tipText.text))
            {
                return;
            }

            if (Time.unscaledTime >= _tipUntil)
            {
                HideTip();
            }
        }

        #endregion

        #region 单例（供格子/槽位回调找到面板）

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion
    }
}
