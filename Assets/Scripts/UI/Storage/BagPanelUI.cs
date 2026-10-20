using TapTapGameJam.Audio;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
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
        [SerializeField] private UnpickItemContainer _unpickItemContainer;

        
        [Header("背包组件")]
        [Tooltip("玩家的 PlayerBag。在 Inspector 里从场景里的 Player 预制体实例上拖过来")]
        [SerializeField] private PlayerBag _bag;

        #endregion

        #region 运行时状态

        private readonly List<BagCell> _cells = new List<BagCell>();

        /// <summary>停留位置 = 场景里摆好的位置 + _shownOffset</summary>
        private Vector2 _shownPosition;

        private Coroutine _anim;

        /// <summary>离场动画还没播完（收尾还没跑）</summary>
        private bool _closePending;

        #endregion

        #region 出场 / 离场动画

        [Header("出场 / 离场动画")]
        [Tooltip("动画时长（秒，真实时间，不受暂停影响）。填 0 = 不播动画、直接到位")]
        [SerializeField] private float _animDuration = 0.25f;

        [Tooltip("停留位置：相对场景里摆好的位置偏移，一般留 (0,0)")]
        [SerializeField] private Vector2 _shownOffset = Vector2.zero;

        [Tooltip("藏起来时的位置：相对停留位置的偏移。背包从屏幕右侧进来 → 填正 x（面板宽 850，所以 ≈900,0）")]
        [SerializeField] private Vector2 _hiddenOffset = new Vector2(900f, 0f);

        #endregion

        #region 生命周期

        private void Awake()
        {
            Assert.IsTrue(_gridRoot, "BagPanelUI：没连 _gridRoot");
            Assert.IsTrue(_slotHead && _slotBody && _slotLegs,
                "BagPanelUI：三个装备槽没连全（字段类型已从 Image 变成 EquipSlot，需要在场景里重新拖）");
            Assert.IsTrue(_unpickItemContainer, "BagPanelUI：没连 _unpickItemSlot");
            Assert.IsTrue(_bag, "BagPanelUI：没连 PlayerBag");

            _shownPosition = ((RectTransform)transform).anchoredPosition + _shownOffset;

            // 一开始就缩在角落（留 ToggleButton 在外面），但**不隐藏**：
            // 背包面板故意不碰 alpha / blocksRaycasts / interactable ——
            // 一旦把 alpha 抹成 0 或者 blocksRaycasts 关掉，面板上的拉开按钮就跟着看不见 / 点不到了。
            // 隐藏的部分靠 _hiddenOffset 缩到屏幕外（按面板宽度和按钮宽度调）。
            ((RectTransform)transform).anchoredPosition = _shownPosition + _hiddenOffset;
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

            // 上一次离场动画还没播完：先把它的收尾（销毁块）补掉，否则下面 CreateBlock 会撞"已经有块了"
            FinishPendingClose();

            base.OpenContainer();

            foreach (PlayerBag.Placement p in _bag.Placements)
            {
                BagItemBlock block = UIManager.Instance.CreateBlock(p.item);
                LayoutBlockToCell(block, p.origin);
            }

            OpenEquippedBlock(_slotHead);
            OpenEquippedBlock(_slotBody);
            OpenEquippedBlock(_slotLegs);

            HidePreview();
            _toggleButton.transform.localEulerAngles = new Vector3(0, 0, 0);

            // 块已经在这一帧建好了，接着从隐藏位滑进来
            PlayEnterAnimation();

            PickableItem pending = _bag.PendingPickupItem;
            if (pending != null)
            {
                _unpickItemContainer.OpenWithItem(pending);
            }
            else
            {
                _unpickItemContainer.CloseContainer();
            }
        }

        /// <summary>
        /// 关闭：立刻停掉拖拽、断输入、翻状态，然后播离场动画。
        /// 块的销毁通过回调传进动画协程 —— 放在这里的话物品会在面板滑出去之前先凭空消失。
        /// </summary>
        public override void CloseContainer()
        {
            if (!IsOpen)
            {
                return;
            }

            // 拖到一半就关：这块已经不属于任何容器了，按约定直接销毁
            UIManager.Instance.DestroyDraggingBlock();

            _toggleButton.transform.localEulerAngles = new Vector3(0, 0, 180);

            _unpickItemContainer.CloseContainer();
            base.CloseContainer();

            PlayExitAnimation(FinishClose);
        }

        /// <summary>离场动画播完之后的收尾：把块交回 UIManager 销毁，清掉 pending 和预览</summary>
        private void FinishClose()
        {
            foreach (PlayerBag.Placement p in _bag.Placements)
            {
                UIManager.Instance.DestroyBlock(p.item);
            }

            DestroyEquippedBlock(_slotHead);
            DestroyEquippedBlock(_slotBody);
            DestroyEquippedBlock(_slotLegs);

            _bag.ClearPendingPickupItem();
            HidePreview();

            // 注意：这里**不隐藏面板** —— 背包只是缩进去，拉开按钮要留在屏幕外面（见 Awake 的注释）
        }

        #region 出场 / 离场动画（实现）

        /// <summary>从隐藏位滑到停留位。打开时调（块已经在这一帧建好了）</summary>
        private void PlayEnterAnimation()
        {
            StopAnimation();

            ((RectTransform)transform).anchoredPosition = _shownPosition + _hiddenOffset;
            _anim = StartCoroutine(Animate(_shownPosition, null));
        }

        /// <summary>从当前位置滑回隐藏位，滑完调 onDone（子类自己的收尾从这里进去）</summary>
        private void PlayExitAnimation(Action onDone)
        {
            StopAnimation();

            _closePending = true;
            _anim = StartCoroutine(Animate(_shownPosition + _hiddenOffset, () =>
            {
                _closePending = false;
                onDone?.Invoke();
            }));
        }

        /// <summary>先快后慢（三次缓出）地滑到 to。用真实时间，游戏暂停时也能播完</summary>
        private IEnumerator Animate(Vector2 to, Action onDone)
        {
            RectTransform rect = (RectTransform)transform;
            Vector2 from = rect.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < _animDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float k = Mathf.Clamp01(elapsed / _animDuration);
                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, 1f - Mathf.Pow(1f - k, 3f));

                yield return null;
            }

            rect.anchoredPosition = to;
            _anim = null;
            onDone?.Invoke();
        }

        /// <summary>上一次离场还没播完就把收尾补上（重开时用），免得块的销毁被跳过</summary>
        private void FinishPendingClose()
        {
            if (!_closePending)
            {
                return;
            }

            StopAnimation();
            _closePending = false;
            FinishClose();
        }

        private void StopAnimation()
        {
            if (_anim == null)
            {
                return;
            }

            StopCoroutine(_anim);
            _anim = null;
        }

        #endregion

        /// <summary>部位用槽位自己记的那个（slot.Slot，Inspector 里指定的），不按字段顺序假设</summary>
        private void OpenEquippedBlock(EquipSlot slot)
        {
            // 先清掉槽上可能残留的图：空槽也要回到"没有物品"的样子
            slot.SetShownItem(null);

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

            // 清掉旧格子（可能来自上一次进 Play 的残留）。
            // 用 Destroy 而不是 DestroyImmediate：运行时同步销毁会把"正在被检视/仍被引用的对象"
            // 当场变成死对象，Editor 里就会出现 SerializedObjectNotCreatable / MissingReference 那一堆报错
            for (int i = _gridRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _gridRoot.GetChild(i);
                if (child.GetComponent<BagCell>() != null)
                {
                    Destroy(child.gameObject);
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
            Image preview = CreateStretchImage(rect, "Preview", Color.clear, false);

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
            PaintPlacementPreview(origin, item.shape, canPlace, item.Category);
            return canPlace;
        }

        /// <summary>真放：登记到后端 + 把块对齐到格子上</summary>
        public bool DropToCell(BagItemBlock block, Vector2Int origin)
        {
            Assert.IsTrue(block != null, "DropToCell：block 是 null");

            if (!_bag.TryPlace(block.boundedItem, origin))
            {
                Sfx.Play(SoundId.Error, this);
                return false;
            }

            LayoutBlockToCell(block, origin);
            Sfx.Play(SoundId.MoveItem, this);
            return true;
        }

        /// <summary>
        /// 块被从槽里拿起来（按下开始拖）：把它从原来的占格里摘掉、本体回到世界。
        /// 背包格和装备槽是同一件事：PlayerBag.ReturnToWorld 会顺手把 Placement / _equipped 清掉。
        /// </summary>
        public bool OnItemPickedUp(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "OnItemPickedUp：block 是 null");
            UIManager.Instance.DetachItemToWorld(block.boundedItem);
            return true;
        }

        /// <summary>
        /// 把块穿到装备槽上。<paramref name="isTry"/> = 只预览不改状态。
        /// 两种情况都先过**部位判定**：必须是 <see cref="DressItem"/> 且部位正好是这个槽
        /// （以前只靠 PlayerBag.TryEquip 自己找部位，往脚部槽丢帽子也会被穿到头上去）。
        /// </summary>
        public bool TryEquipToSlot(EquipSlot slot, BagItemBlock block, bool isTry = false)
        {
            Assert.IsTrue(slot != null, "TryEquipToSlot：slot 是 null");
            Assert.IsTrue(block != null, "TryEquipToSlot：block 是 null");

            PickableItem item = block.boundedItem;
            bool fits = item is DressItem dress && dress.ToSlot() == slot.Slot;

            if (isTry)
            {
                // 对得上：把试放那件的图贴到槽上（松手或换目标时 ClearDropPreview 会收掉）
                if (fits)
                {
                    slot.SetDropPreview(item);
                }

                return fits;
            }

            if (!fits)
            {
                return false; // 类型 / 部位不对：这一槽不收（状态一律不动）
            }

            // 拖的就是这一槽里已有的那件：当"放回原处"处理，别让 TryEquip 的"同一件"分支把它判死
            if (_bag.GetEquipped(slot.Slot) == item)
            {
                LayoutBlockToEquipSlot(block, slot);
                return true;
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
                _unpickItemContainer.OpenWithItem(replaced);
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
        ///
        /// 块挂在**容器自己**下面（不是拖动层）：这样面板滑进滑出时物品跟着一起走，
        /// 而且容器关闭（CanvasGroup 全关）的时候它的射线也一起没了。拖动中才临时挪到拖动层。
        /// </summary>
        public void LayoutBlockToCell(BagItemBlock block, Vector2Int origin)
        {
            Assert.IsTrue(block != null, "LayoutBlockToCell：block 是 null");
            bool foundCell = TryGetCell(origin, out BagCell cell);
            Assert.IsTrue(foundCell, $"LayoutBlockToCell：格子 {origin} 不在背包里");

            // 从仓库（隐藏格子框 + 缩放过）过来的块要先恢复成背包形态
            block.ApplyBagPresentation();
            block.CurrentSlot = cell;
            block.transform.SetParent(transform, false);
            block.transform.position = cell.transform.position;
        }

        /// <summary>
        /// 把块停进装备槽中心，并让它进**代理模式**：块不画出来（alpha 0）但射线照旧挡着 ——
        /// 所以按在槽上就是在按这块，拖拽直接落到块自己身上（<see cref="EquipSlot"/> 不用接拖拽/点击）。
        /// 物品图由槽自己贴在自己的 Image 上（见 <see cref="EquipSlot.SetShownItem"/>）。
        /// </summary>
        public void LayoutBlockToEquipSlot(BagItemBlock block, EquipSlot slot)
        {
            Assert.IsTrue(block != null, "LayoutBlockToEquipSlot：block 是 null");
            Assert.IsTrue(slot != null, "LayoutBlockToEquipSlot：slot 是 null");

            block.ApplyBagPresentation();
            block.CurrentSlot = slot;
            block.transform.SetParent(transform, false);
            block.transform.position = slot.transform.position;

            slot.SetShownItem(block.boundedItem);
            block.SetProxyHidden(true);

            // 代理模式下把块缩到槽的大小：它挡射线的范围正好是这一格，不会盖到旁边的槽上
            // （多格装扮的 bbox 可能比槽宽）。拖起来时 EquipSlot.OnPickUpItem 会把大小恢复成背包形态。
            RectTransform slotRect = (RectTransform)slot.transform;
            Vector2 iconSize = block.Icon.rect.size;
            float fit = Mathf.Min(slotRect.rect.width, slotRect.rect.height) / Mathf.Max(iconSize.x, iconSize.y);
            block.transform.localScale = Vector3.one * fit;
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

            if (!_bag.UseItem(item, GameManager.Player)) return;
            
            // 用掉了：后端已经把它摘掉并销毁本体，块交给 UIManager 回收
            UIManager.Instance.DestroyBlock(block);
        }

        #endregion

        #region 表现 / 工具

        // 背包**不做**显隐（alpha / blocksRaycasts / interactable）：
        // 它只缩进屏幕外、把 ToggleButton 留在外面，所以那三样必须一直保持"可见 + 可点"。
        // 需要真正隐藏的容器（仓库 / 待拾取位）各自去管自己的 CanvasGroup。

        /// <summary>清掉所有格子的试放预览</summary>
        public void HidePreview()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                _cells[i].ClearPreview();
            }
        }

        /// <summary>
        /// 按"抓取格落在 origin"刷试放预览：形状覆盖到的每个格子统一染色 ——
        /// 放得下用**物品分类的代表色**（UIManager 里配的那套），放不下用统一的红色。
        /// 越界的格子不画：形状偏移允许是负数，越界那部分没有对应的 cell。
        /// </summary>
        private void PaintPlacementPreview(Vector2Int origin, BagShape shape, bool canPlace, ItemCategory category)
        {
            HidePreview();

            Color color = canPlace ? UIManager.Instance.CategoryPreviewColor(category) : PreviewBadColor;

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
