using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 背包界面容器：左侧 5x5 网格 + 右侧三个装备槽（头 / 身 / 足）。
    ///
    /// 【分层】
    /// - <see cref="PlayerBag"/> 是后端：只管物品真正存进/移出背包、以及 OnMap / InBag / Equipped 的变换。
    ///   它不知道"界面上正被拖着什么"。
    /// - 本类是容器（<see cref="ItemContainer"/>）：持有"网格里的物品 + 装备中的三件"，
    ///   负责开关时向 <see cref="UIManager"/> 申请/销毁块，以及把块**定位**到格子上
    ///   （布局逻辑在容器里，不在槽位里）。
    /// - <see cref="BagCell"/> / <see cref="EquipSlot"/> 是槽位（<see cref="IItemSlot"/>）：
    ///   自己不做判定，把拖放转发回本类。
    /// - 待拾取物是独立的小容器 <see cref="UnpickItemSlot"/>，自己管自己那件的块。
    ///
    /// 【块的生命周期】容器**不记录**自己持有哪些块：块跟着物品走，谁最后持有那件物品，
    /// 谁在 <see cref="CloseContainer"/> 时把它销毁。物品在容器之间转移时不需要任何代码。
    ///
    /// 【操作】
    /// - 左键拖块：块自己射线找槽位（<see cref="BagItemBlock"/>）→ 放得下就放，放不下回原槽位，
    ///   面板外松手丢到世界
    /// - R：旋转正在拖的那一块（由 <see cref="UIManager"/> 转发）
    /// - 右键点格子：使用该格物品
    /// </summary>
    [DisallowMultipleComponent]
    public class BagPanelUI : ItemContainer
    {
        #region 配置常量

        /// <summary>网格列数。改这里必须同步 PlayerBag 的 Width</summary>
        private const int GridColumns = 5;

        /// <summary>网格行数。改这里必须同步 PlayerBag 的 Height</summary>
        private const int GridRows = 5;

        private static readonly Color CellEmptyColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color PreviewOkColor = new Color(0.35f, 0.9f, 0.4f, 0.45f);
        private static readonly Color PreviewBadColor = new Color(0.95f, 0.3f, 0.3f, 0.45f);

        #endregion

        #region 序列化引用

        [Tooltip("背包根节点，右侧拉开")]
        [SerializeField] private RectTransform _bagRoot;

        [Tooltip("拉开按钮")]
        [SerializeField] private Image _toggleButton;
        
        [Header("网格 / 物料")]
        [Tooltip("背包网格根节点，应当是 512x512（100*5 + 3*4），pivot 在左上角 (0,0)。格子由代码生成在这里")]
        [SerializeField] private RectTransform _gridRoot;

        [Header("装备槽（三个现成的 Image）")]
        [Tooltip("TODO: 槽里的饰品图标怎么显示还没定，现在只保证拖上去能穿")]
        [SerializeField] private EquipSlot _slotHead;
        [SerializeField] private EquipSlot _slotBody;
        [SerializeField] private EquipSlot _slotLegs;

        [Header("拾取组件")]
        [Tooltip("待拾取物品的slot（独立小容器）")]
        [SerializeField] private UnpickItemSlot _unpickItemSlot;

        
        [Header("背包组件")]
        [Tooltip("玩家的 PlayerBag。在 Inspector 里从场景里的 Player 预制体实例上拖过来")]
        [SerializeField] private PlayerBag _bag;

        #endregion

        #region 运行时状态

        private readonly List<BagCell> _cells = new List<BagCell>();

        private CanvasGroup _canvasGroup;

        #endregion

        #region 生命周期

        private void Awake()
        {
            Assert.IsTrue(_gridRoot, "BagPanelUI：没连 _gridRoot");
            Assert.IsTrue(_slotHead && _slotBody && _slotLegs,
                "BagPanelUI：三个装备槽没连全（字段类型已从 Image 变成 EquipSlot，需要在场景里重新拖）");
            Assert.IsTrue(_unpickItemSlot, "BagPanelUI：没连 _unpickItemSlot");
            Assert.IsTrue(_bag, "BagPanelUI：没连 PlayerBag");

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            ApplyVisibility(false);
        }

        private void Start()
        {
            // 格子尺寸要从 UIManager 取，而两个组件的 Awake 顺序是不保证的 —— 所以放 Start
            BuildGrid();

            EventPipeline.Subscribe<BackpackEvent>(OnBackpackEvent);
            EventPipeline.Subscribe<EventToggleBackpack>(OnToggleBackpackEvent);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<BackpackEvent>(OnBackpackEvent);
            EventPipeline.UnSubscribe<EventToggleBackpack>(OnToggleBackpackEvent);
        }

        #endregion

        #region ItemContainer

        /// <summary>
        /// 背包面板上那个"拉开按钮"绑的就是这个方法名（场景里的 UnityEvent），别改名。
        /// 开↔关的逻辑在基类。
        /// </summary>
        public void ToggleOpen()
        {
            ToggleContainer();
        }

        /// <summary>
        /// 打开：为容器持有的每一件物品建块并摆好位置，最后把自己显示出来。
        /// 可安全重复调用；状态与 ActionMap 由基类负责（base 里翻 IsOpen 并通知 UIManager）。
        /// 持有的物品 = 背包格子里的 + 装备中的三件；待拾取那件是 UnpickItemSlot 自己的事。
        /// </summary>
        public override void OpenContainer()
        {
            if (IsOpen)
            {
                return;
            }

            base.OpenContainer();

            foreach (PlayerBag.Placement p in _bag.Placements)
            {
                BagItemBlock block = UIManager.Instance.CreateBlock(p.item);
                LayoutBlockToCell(block, p.origin);
            }

            OpenEquippedBlock(_slotHead);
            OpenEquippedBlock(_slotBody);
            OpenEquippedBlock(_slotLegs);

            ApplyVisibility(true);
            HidePreview();
            _toggleButton.transform.localEulerAngles = new Vector3(0, 0, 0);

            PickableItem pending = _bag.PendingPickupItem;
            if (pending != null)
            {
                _unpickItemSlot.OpenWithItem(pending);
            }
            else
            {
                _unpickItemSlot.CloseContainer();
            }
        }

        /// <summary>关闭：把自己持有的每件物品对应的块交给 UIManager 销毁，然后把自己藏起来。可安全重复调用</summary>
        public override void CloseContainer()
        {
            if (!IsOpen)
            {
                return;
            }

            base.CloseContainer();

            // 拖到一半就关：这块已经不属于任何容器了，按约定直接销毁
            UIManager.Instance.DestroyDraggingBlock();

            foreach (PlayerBag.Placement p in _bag.Placements)
            {
                UIManager.Instance.DestroyBlock(p.item);
            }

            DestroyEquippedBlock(_slotHead);
            DestroyEquippedBlock(_slotBody);
            DestroyEquippedBlock(_slotLegs);

            _toggleButton.transform.localEulerAngles = new Vector3(0, 0, 180);
            _bag.ClearPendingPickupItem();
            _unpickItemSlot.CloseContainer();
            HidePreview();
            ApplyVisibility(false);
        }

        /// <summary>部位用槽位自己记的那个（slot.Slot，Inspector 里指定的），不按字段顺序假设</summary>
        private void OpenEquippedBlock(EquipSlot slot)
        {
            PickableItem item = _bag.GetEquipped(slot.Slot);
            if (item == null)
            {
                return;
            }

            BagItemBlock block = UIManager.Instance.CreateBlock(item);
            LayoutBlockToEquipSlot(block, slot);
        }

        private void DestroyEquippedBlock(EquipSlot slot)
        {
            PickableItem item = _bag.GetEquipped(slot.Slot);
            if (item != null)
            {
                UIManager.Instance.DestroyBlock(item);
            }
        }

        #endregion

        #region 网格构建

        private void BuildGrid()
        {
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
                if (child.GetComponent<BagCell>() != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            for (int index = 0; index < GridColumns * GridRows; index++)
            {
                Vector2Int cell = new Vector2Int(index % GridColumns, index / GridColumns);
                _cells.Add(CreateCell(cell));
            }

            HidePreview();
        }

        private BagCell CreateCell(Vector2Int pos)
        {
            float cellSize = UIManager.Instance.CellSize;

            GameObject go = new GameObject($"Cell_{pos.x}_{pos.y}", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(_gridRoot, false);
            rect.anchorMin = new Vector2(0f, 1f); // 以网格左上角为基准
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f); // 中心对齐，方便和 block 的格心对齐
            rect.sizeDelta = new Vector2(cellSize, cellSize);
            rect.anchoredPosition = UIManager.Instance.CellLocalPosition(pos)
                                    + new Vector2(cellSize * 0.5f, -cellSize * 0.5f);

            Image background = EnsureImage(go, CellEmptyColor, true);
            Image preview = CreateStretchImage(rect, "Preview", PreviewOkColor, false);

            BagCell cell = go.AddComponent<BagCell>();
            cell.Initialize(pos, background, preview, CellEmptyColor);
            cell.bagPanelUI = this;
            cell.ClearPreview();
            return cell;
        }

        #endregion

        #region 容器操作（槽位转发到这里）

        /// <summary>预览：块以 <paramref name="origin"/> 为抓取格落位时放不放得下，顺便把预览刷出来</summary>
        public bool TryDropToCell(BagItemBlock block, Vector2Int origin)
        {
            Assert.IsTrue(block != null, "TryDropToCell：block 是 null");

            PickableItem item = block.boundedItem;
            Assert.IsTrue(item != null && item.shape != null, "TryDropToCell：物品没配 shape");

            // ignore 传自己：和 TryPlace 的默认行为一致（从背包里挪位置时不被自己原来的占格挡住），
            // 这样"预览是绿的，松手一定放得下"才成立
            bool canPlace = _bag.CanPlace(item, origin, item);
            PaintPlacementPreview(origin, item.shape, canPlace);
            return canPlace;
        }

        /// <summary>真放：登记到后端 + 把块对齐到格子上</summary>
        public bool DropToCell(BagItemBlock block, Vector2Int origin)
        {
            Assert.IsTrue(block != null, "DropToCell：block 是 null");

            if (!_bag.TryPlace(block.boundedItem, origin))
            {
                return false;
            }

            LayoutBlockToCell(block, origin);
            return true;
        }

        /// <summary>
        /// 块被从槽里拿起来（按下开始拖）：把它从原来的占格里摘掉、本体回到世界。
        /// 背包格和装备槽是同一件事：PlayerBag.ReturnToWorld 会顺手把 Placement / _equipped 清掉。
        /// </summary>
        public void OnItemPickedUp(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "OnItemPickedUp：block 是 null");
            UIManager.Instance.DetachItemToWorld(block.boundedItem);
        }

        /// <summary>
        /// 把块穿到装备槽上。<paramref name="isTry"/> = 只预览不改状态：
        /// 装备槽永远放得下（会替换掉原来那件），但**类型和部位必须对**，
        /// 否则会出现"预览是绿的、松手才发现放不进去"。
        /// </summary>
        public bool TryEquipToSlot(EquipSlot slot, BagItemBlock block, bool isTry = false)
        {
            Assert.IsTrue(slot != null, "TryEquipToSlot：slot 是 null");
            Assert.IsTrue(block != null, "TryEquipToSlot：block 是 null");

            PickableItem item = block.boundedItem;

            if (isTry)
            {
                return item is DressItem dress && dress.ToSlot() == slot.Slot;
            }

            if (!_bag.TryEquip(item, out PickableItem replaced))
            {
                return false;
            }

            LayoutBlockToEquipSlot(block, slot);

            if (replaced != null)
            {
                // 换下来的那件：先把它在装备槽上的块收掉 —— 一件物品只能有一个块，
                // 待拾取位待会儿会为它重新建一个；不收掉就会撞上 CreateBlock 的"已经有块了"
                if (UIManager.Instance.TryGetBlock(replaced, out BagItemBlock replacedBlock))
                {
                    UIManager.Instance.DestroyBlock(replacedBlock);
                }

                // 送回世界，然后停在待拾取位上等玩家处理
                _bag.DropToWorld(replaced);
                _unpickItemSlot.OpenWithItem(replaced);
            }

            return true;
        }

        /// <summary>某一格上物品的表现块（空格子 / 还没建块时返回 null）</summary>
        public BagItemBlock GetBlockAt(Vector2Int cell)
        {
            PickableItem item = _bag.ItemAt(cell);
            if (item == null)
            {
                return null;
            }

            return UIManager.Instance.TryGetBlock(item, out BagItemBlock block) ? block : null;
        }

        #endregion

        #region 布局（容器负责"块摆在哪"）

        /// <summary>
        /// 把块的抓取格中心对齐到格心，并把它登记到这个格子上。
        /// 直接拿格子的 position 对齐：块和格子的 anchor/pivot 都不一样，手算 anchoredPosition 一定错位。
        /// </summary>
        public void LayoutBlockToCell(BagItemBlock block, Vector2Int origin)
        {
            Assert.IsTrue(block != null, "LayoutBlockToCell：block 是 null");
            Assert.IsTrue(TryGetCell(origin, out BagCell cell), $"LayoutBlockToCell：格子 {origin} 不在背包里");

            // 从仓库（隐藏格子框 + 缩放过）过来的块要先恢复成背包形态
            block.ApplyBagPresentation();
            block.CurrentSlot = cell;
            block.transform.SetParent(UIManager.Instance.DragLayer, false);
            block.transform.position = cell.transform.position;
        }

        /// <summary>把块停进装备槽中心。TODO: 槽里要不要隐藏底部格子框 / 换成槽位图标还没定</summary>
        public void LayoutBlockToEquipSlot(BagItemBlock block, EquipSlot slot)
        {
            Assert.IsTrue(block != null, "LayoutBlockToEquipSlot：block 是 null");
            Assert.IsTrue(slot != null, "LayoutBlockToEquipSlot：slot 是 null");

            block.ApplyBagPresentation();
            block.CurrentSlot = slot;
            block.transform.SetParent(UIManager.Instance.DragLayer, false);
            block.transform.position = slot.transform.position;
        }

        #endregion

        #region 指针回调

        /// <summary>点格子：右键使用该格物品（左键的移动 / 摆放全部交给 BagItemBlock 的拖拽流程）</summary>
        public void NotifyCellClicked(BagCell cell, BagItemBlock block, PointerEventData.InputButton button)
        {
            Assert.IsTrue(cell != null, "NotifyCellClicked：cell 是 null");

            if (button != PointerEventData.InputButton.Right || block == null)
            {
                return;
            }

            PickableItem item = block.boundedItem;
            if (item == null || !item.IsUseAble)
            {
                return;
            }

            if (!_bag.UseItem(item, null))
            {
                // 效果做了但东西还在（换装之类）：怎么安顿还没有定，先不处理
                return;
            }

            // 用掉了：后端已经把它摘掉并销毁本体，块交给 UIManager 回收
            UIManager.Instance.DestroyBlock(block);
        }

        #endregion

        #region 表现 / 工具

        private void ApplyVisibility(bool visible)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = visible ? 1f : 0f;

            // 关键：alpha = 0 只影响渲染，不影响射线。
            // BagPanel 是铺满全屏的（anchor 0,0 → 1,1），底图又开着 RaycastTarget，
            // 所以隐藏时必须把 blocksRaycasts 关掉 —— 否则这张看不见的挡板会把
            // 场景里所有 UI（按钮、格子、其它面板）的点击全部吃掉。
            // 打开时才恢复成挡板，这样"点背包外面 = 丢弃物品"才能生效。
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }

        /// <summary>清掉所有格子的试放预览</summary>
        public void HidePreview()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                _cells[i].ClearPreview();
            }
        }

        /// <summary>
        /// 按"抓取格落在 origin"刷试放预览：形状覆盖到的每个格子统一染绿（放得下）或红（放不下）。
        /// 越界的格子不画 —— 形状偏移允许是负数，越界那部分没有对应的 cell。
        /// </summary>
        private void PaintPlacementPreview(Vector2Int origin, BagShape shape, bool canPlace)
        {
            HidePreview();

            Color color = canPlace ? PreviewOkColor : PreviewBadColor;

            foreach (Vector2Int offset in shape.Offsets)
            {
                if (TryGetCell(origin + offset, out BagCell cell))
                {
                    cell.SetPreview(true, color);
                }
            }
        }

        private void OnBackpackEvent(BackpackEvent evt)
        {
            bool isOpen = evt.IsOpen;

            if (isOpen)
            {
                OpenContainer();
            }
            else
            {
                CloseContainer();
            }

        }

        /// <summary>
        /// 背包键（B）。事件只表示"键被按了"，到底是开还是关由容器自己判断
        /// （持有"当前开/关"的是容器，不再是输入层去读 ActionMap 猜）。
        /// </summary>
        private void OnToggleBackpackEvent(EventToggleBackpack evt)
        {
            ToggleContainer();
        }

        private bool TryGetCell(Vector2Int cell, out BagCell result)
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
