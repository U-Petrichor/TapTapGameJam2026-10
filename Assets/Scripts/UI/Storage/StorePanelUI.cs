using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 商店界面容器：一格一件商品（每页格数由 <see cref="MouseStore.Width"/> × Height 决定）。
    ///
    /// 【和 RepoPanelUI 一样】
    /// - 按 <see cref="ItemCategory"/> 分三页，每页一个 GridRoot、一组 <see cref="StoreCellUI"/>，
    ///   同时只显示当前页；块挂在**它那一页的 GridRoot** 下；
    /// - 派生自 <see cref="ItemContainer"/>：打开时给货建块、关闭时销毁，自己不记块；
    /// - 布局在容器里（隐藏格子框、按 bbox 缩放居中到格心）；出场/离场滑动的动画自己写；
    /// - 切页入口 <see cref="ShowFoodPage"/> / <see cref="ShowToolPage"/> / <see cref="ShowDressPage"/> 绑 Toggle。
    ///
    /// 【和 RepoPanelUI 的区别】
    /// - **可以放进来，但只是"归还"**：把东西拖到空着的、类别对得上的货位上，它就回到商店后端
    ///   （标记为已购买，随时能再拿走）；不做"卖店换钱"；
    /// - 拿货要"已买下"：没买下时格子会**拒绝拿取并高亮自己**，玩家点购买按钮
    ///   （<see cref="OnBuyButton"/>）或再点一次那一格买下；买下后再拖才拿得走，拿走后即售罄；
    /// - 高亮可以被"点商品格"或"拖拽被拒"点亮，同一时刻只有一格亮着。
    ///
    /// 开关走独立事件 <see cref="StoreOnOffEvent"/>（和背包 / 仓库互不影响）。
    /// </summary>
    [DisallowMultipleComponent]
    public class StorePanelUI : ItemContainer
    {
        #region 配置常量

        /// <summary>单格边长，商品在商店里不显示格子、只显示sprite、且尺寸自适应缩放</summary>
        private const float CellSize = 180f;

        /// <summary>格与格的间隙</summary>
        private const float CellSpacing = 10f;

        /// <summary>格心间距</summary>
        private const float CellPitch = CellSize + CellSpacing;

        /// <summary>默认语（欢迎 / 谢谢惠顾）的标题颜色</summary>
        private static readonly Color DefaultTitleColor = Color.black;

        #endregion

        #region 序列化引用

        [Header("网格 / 物料（三页各一个 GridRoot）")]
        [Tooltip("Food 页的网格根节点。格子由代码生成在这里，列/行数取自 MouseStore")]
        [SerializeField] private RectTransform _foodGridRoot;
        [Tooltip("Tool 页的网格根节点")]
        [SerializeField] private RectTransform _toolGridRoot;
        [Tooltip("Dress 页的网格根节点")]
        [SerializeField] private RectTransform _dressGridRoot;

        [Header("格子预制体")]
        [Tooltip("货架格的预制体（带 StoreCellUI、底色 Image、价格文字、购买提示）。每格都是它的一份实例")]
        [SerializeField] private StoreCellUI _storeCell;
        
        [Header("显示信息")]
        [Tooltip("物品标题（高亮时显示商品名）")]
        [SerializeField] private TextMeshProUGUI _showTitle;
        [Tooltip("物品描述（高亮时显示商品描述）")]
        [SerializeField] private TextMeshProUGUI _showContent;

        [Header("商店组件")]
        [Tooltip("商店后端。在 Inspector 里从场景里的 Player 预制体实例上拖过来")]
        public MouseStore mouseStore;

        #endregion

        #region 运行时状态

        /// <summary>每一页的 GridRoot</summary>
        private readonly Dictionary<ItemCategory, RectTransform> _pageRoots =
            new Dictionary<ItemCategory, RectTransform>();

        /// <summary>每一页的格子</summary>
        private readonly Dictionary<ItemCategory, List<StoreCellUI>> _pageCells =
            new Dictionary<ItemCategory, List<StoreCellUI>>();

        /// <summary>当前显示的是哪一页</summary>
        private ItemCategory _currentPage;

        public ItemCategory CurrentPage => _currentPage;

        /// <summary>当前高亮（= 正被鼠标悬停、信息显示区在展示、购买按钮要买）的那一格</summary>
        private StoreCellUI _highlightedCell;

        public StoreCellUI HighlightedCell => _highlightedCell;

        private CanvasGroup _canvasGroup;

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

        [Tooltip("藏起来时的位置：相对停留位置的偏移。按面板宽度调，默认从左侧进来")]
        [SerializeField] private Vector2 _hiddenOffset = new Vector2(-1100f, 0f);

        #endregion

        #region 生命周期

        private void Start()
        {
            Assert.IsTrue(_foodGridRoot && _toolGridRoot && _dressGridRoot,
                "StorePanelUI：三个 GridRoot（Food / Tool / Dress）要连全");
            Assert.IsTrue(_storeCell != null, "StorePanelUI：没连格子预制体 _storeCell");
            Assert.IsTrue(mouseStore, "StorePanelUI：没连 MouseStore");

            _pageRoots.Clear();
            _pageRoots[ItemCategory.Food] = _foodGridRoot;
            _pageRoots[ItemCategory.Tool] = _toolGridRoot;
            _pageRoots[ItemCategory.Dress] = _dressGridRoot;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _shownPosition = ((RectTransform)transform).anchoredPosition + _shownOffset;

            // 一开始就停在隐藏位、透明、不吃射线：不然第一次打开之前它会亮在屏幕中间
            ((RectTransform)transform).anchoredPosition = _shownPosition + _hiddenOffset;
            _canvasGroup.alpha = 0f;

            BuildPages();
            CloseContainer();

            // 默认停在第一页（Food），并且保证只有它显示
            ShowPage(ItemCategories.All[0]);
            
            EventPipeline.Subscribe<StoreOnOffEvent>(OnStoreEvent);
            EventPipeline.Subscribe<GamePhaseChangedEvent>(OnPhaseChanged);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<StoreOnOffEvent>(OnStoreEvent);
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(OnPhaseChanged);
        }

        #endregion

        #region ItemContainer

        /// <summary>
        /// 打开：为三页货架上还摆着的每件货建块并摆到它那一页的格子上，最后把自己显示出来。
        /// 可安全重复调用；状态与 ActionMap 由基类负责。
        /// </summary>
        public override void OpenContainer()
        {
            if (IsOpen)
            {
                return;
            }

            Debug.Log("商店打开");
            
            // 上一次离场动画还没播完：先把它的收尾（销毁块）补掉，否则下面 CreateBlock 会撞"已经有块了"
            FinishPendingClose();

            base.OpenContainer();

            foreach (ItemCategory page in ItemCategories.All)
            {
                List<StoreCellUI> cells = _pageCells[page];
                for (int i = 0; i < cells.Count; i++)
                {
                    MouseStore.Goods goods = mouseStore.GoodsAt(page, i);
                    if (goods == null || goods.item == null)
                    {
                        continue; // 空格位 / 已售罄
                    }

                    BagItemBlock block = UIManager.Instance.CreateBlock(goods.item);
                    LayoutBlockToCell(block, cells[i]);
                }
            }

            ApplyVisibility(true);
            RefreshAllCell();
            ShowWellcomeInfo();
            ShowPage(_currentPage);

            // 块已经在这一帧建好了，接着从隐藏位滑进来
            PlayEnterAnimation();
        }

        /// <summary>
        /// 关闭：立刻停掉拖拽、断输入、翻状态，然后播离场动画。
        /// 块的销毁通过回调传进动画协程 —— 放在这里的话货会在面板滑出去之前先凭空消失。
        /// </summary>
        public override void CloseContainer()
        {
            if (!IsOpen)
            {
                return;
            }

            Debug.Log("商店关闭");
            
            // 拖到一半就关：这块已经不属于任何容器了，按约定直接销毁
            UIManager.Instance.DestroyDraggingBlock();

            ApplyVisibility(false);

            base.CloseContainer();

            PlayExitAnimation(FinishClose);
        }

        /// <summary>离场动画播完之后的收尾：把货架上的块交回 UIManager 销毁、清掉高亮</summary>
        private void FinishClose()
        {
            foreach (PickableItem item in mouseStore.Items)
            {
                UIManager.Instance.DestroyBlock(item);
            }

            ClearHighlight();

            // 滑完了才真的藏起来（滑动期间要看得见）
            _canvasGroup.alpha = 0f;
        }

        #region 出场 / 离场动画（实现）

        /// <summary>从隐藏位滑到停留位。打开时调（块已经在这一帧建好了）</summary>
        private void PlayEnterAnimation()
        {
            StopAnimation();

            ((RectTransform)transform).anchoredPosition = _shownPosition + _hiddenOffset;
            _anim = StartCoroutine(Animate(_shownPosition, null));
        }

        /// <summary>从当前位置滑回隐藏位，滑完调 onDone（收尾从自己的回调里进去）</summary>
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

        #endregion

        #region 分页 / 网格构建

        /// <summary>切到 Food 页（绑给 ToggleGroup 里的 Toggle）</summary>
        public void ShowFoodPage()
        {
            ShowPage(ItemCategory.Food);
        }

        /// <summary>切到 Tool 页（绑给 ToggleGroup 里的 Toggle）</summary>
        public void ShowToolPage()
        {
            ShowPage(ItemCategory.Tool);
        }

        /// <summary>切到 Dress 页（绑给 ToggleGroup 里的 Toggle）</summary>
        public void ShowDressPage()
        {
            ShowPage(ItemCategory.Dress);
        }

        /// <summary>只显示这一页：其余 GridRoot 整块 SetActive(false)，格子、块、射线一起收掉</summary>
        private void ShowPage(ItemCategory page)
        {
            _currentPage = page;

            // 高亮的那一格可能在别的页上，切页就清掉，免得购买按钮买到一个看不见的东西
            ClearHighlight();

            foreach (KeyValuePair<ItemCategory, RectTransform> kv in _pageRoots)
            {
                kv.Value.gameObject.SetActive(kv.Key == page);
            }
        }

        /// <summary>三页各建一组格子（每页格数取自 MouseStore）</summary>
        private void BuildPages()
        {
            _pageCells.Clear();

            foreach (ItemCategory page in ItemCategories.All)
            {
                RectTransform root = _pageRoots[page];

                // 清掉旧格子（可能来自上一次进 Play 的残留）。
                // 用 Destroy 而不是同步销毁：运行时同步销毁会让正在被检视的对象变成死对象
                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    Transform child = root.GetChild(i);
                    if (child.GetComponent<StoreCellUI>() != null)
                    {
                        Destroy(child.gameObject);
                    }
                }

                List<StoreCellUI> cells = new List<StoreCellUI>(mouseStore.CellCount);
                for (int index = 0; index < mouseStore.CellCount; index++)
                {
                    cells.Add(CreateCell(page, root, index));
                }

                _pageCells[page] = cells;
            }
        }

        private StoreCellUI CreateCell(ItemCategory page, RectTransform root, int index)
        {
            Assert.IsTrue(_storeCell != null, "StorePanelUI：没连格子预制体 _storeCell");

            StoreCellUI cell = Instantiate(_storeCell, root, false);
            cell.name = $"StoreCell_{page}_{index}";

            RectTransform rect = (RectTransform)cell.transform;
            rect.anchorMin = new Vector2(0f, 1f); // 以网格左上角为基准
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CellSize, CellSize);
            rect.anchoredPosition = CellLocalPosition(index) + new Vector2(CellSize * 0.5f, -CellSize * 0.5f);

            // 先连容器再 Initialize：格子要靠 storePanelUI 实时取货
            cell.storePanelUI = this;
            cell.Initialize(page, index);
            return cell;
        }

        /// <summary>某一页第 <paramref name="index"/> 个货位上现在摆的是什么（实时向后端要，刷新也不会失效）</summary>
        public MouseStore.Goods GoodsAt(ItemCategory page, int index)
        {
            return mouseStore != null ? mouseStore.GoodsAt(page, index) : null;
        }

        /// <summary>格子中心相对网格左上角的位置（x 向右、y 向下，所以 y 取负）</summary>
        private Vector2 CellLocalPosition(int index)
        {
            int column = index % mouseStore.Width;
            int row = index / mouseStore.Width;
            return new Vector2(column * CellPitch, -row * CellPitch);
        }

        #endregion

        #region 容器操作（槽位转发到这里）

        /// <summary>
        /// 点一下商品格 = **选中它**（全商店唯一、互斥）。空格位 / 已售罄的格子点不出高亮来。
        /// 购买不走这里 —— 玩家的购买按钮调 <see cref="TryBuyHighlighted"/>。
        /// </summary>
        public void OnClickCell(StoreCellUI cell)
        {
            AssertCellBelongsToThisPanel(cell);

            if (cell.Goods == null || cell.Goods.item == null)
            {
                ClearHighlight();
                return;
            }

            SetHighlight(cell);
        }

        /// <summary>买某一格。成功返回 true，并把所有格子的价格 / 提示刷一遍</summary>
        private bool TryBuyCell(StoreCellUI cell)
        {
            MouseStore.Goods goods = cell.Goods;
            if (goods == null || goods.item == null)
            {
                return false; // 空格位
            }

            if (!mouseStore.TryBuy(goods))
            {
                ShowNoMoneyInfo();
                return false; // 钱不够 / 已经买过 / 已售罄：什么都不改
            }

            RefreshAllCell();
            ShowBuyInfo();
            return true;
        }

        /// <summary>
        /// 预览：把东西拖到货架上是"归还"。空货位 + 类别对得上才收。
        /// 类别对不上时**顺手切到它该去的那一页**再返回 false（和仓库一个套路）。
        /// </summary>
        public bool TryDropToCell(BagItemBlock block, StoreCellUI cell)
        {
            Assert.IsTrue(block != null, "StorePanelUI.TryDropToCell：block 是 null");
            AssertCellBelongsToThisPanel(cell);

            ItemCategory itemPage = block.boundedItem.Category;
            if (itemPage != cell.Page)
            {
                ShowPage(itemPage);
                return false;
            }

            MouseStore.Goods goods = mouseStore.GoodsAt(cell.Page, cell.Index);
            return goods == null || goods.item == null; // 空货位才收（不做交换）
        }

        /// <summary>
        /// 真放（归还）：登记进货架第 index 个货位并标成已购买，本体挪进商店暂存区，
        /// 块按商店样式摆进格子。之后就归商店管了（关店销毁块、再开店重建）。
        /// </summary>
        public bool DropToCell(BagItemBlock block, StoreCellUI cell)
        {
            Assert.IsTrue(block != null, "StorePanelUI.DropToCell：block 是 null");
            AssertCellBelongsToThisPanel(cell);
            Assert.IsTrue(block.boundedItem.Category == cell.Page,
                $"StorePanelUI.DropToCell：{block.boundedItem.name} 是 {block.boundedItem.Category} 类，" +
                $"却往 {cell.Page} 页放（TryDropToCell 里应该已经切过页了）");

            if (!mouseStore.TryPutItem(cell.Page, cell.Index, block.boundedItem))
            {
                return false;
            }

            LayoutBlockToCell(block, cell);
            cell.Refresh();
            return true;
        }

        /// <summary>
        /// 从货架上拿货：**只有已经买下的才放行**。没买下就当场拒绝，块一步都不动
        /// （买是"选中 + 购买按钮"的事，拖拽不替玩家买单，也不改高亮）。
        /// </summary>
        public bool OnItemPickedUp(StoreCellUI cell, BagItemBlock block)
        {
            AssertCellBelongsToThisPanel(cell);
            Assert.IsTrue(block != null, "StorePanelUI.OnItemPickedUp：block 是 null");
            Assert.IsTrue(ReferenceEquals(block.CurrentSlot, cell),
                "StorePanelUI.OnItemPickedUp：块记着的槽位不是这个格子（是不是 CurrentSlot 被写坏了？）");

            MouseStore.Goods goods = cell.Goods;
            if (goods == null || goods.item == null)
            {
                return false; // 空格位：本来就没东西可拿
            }

            if (!goods.purchased)
            {
                return false; // 没买下不许拿
            }

            Assert.IsTrue(mouseStore.TryTakeOut(goods, out PickableItem item),
                $"StorePanelUI.OnItemPickedUp：{goods.item} 取不出来（没买下 / 已售罄）");
            Assert.IsTrue(item == block.boundedItem,
                $"StorePanelUI.OnItemPickedUp：取出来的不是这一格的那件（格子是 {item}，块是 {block.boundedItem}）");

            // 从商店出去之后按背包形态拖（显示格子框、缩放归 1），本体按统一规则回世界
            block.ApplyBagPresentation();
            ClearHighlight();
            cell.Refresh(); // 这一格已经售罄：价格 / 绿色都要回到"空货位"的样子
            UIManager.Instance.DetachItemToWorld(item);
            return true;
        }

        /// <summary>
        /// 尝试买下**当前选中（高亮）**的那件 —— 给购买按钮绑这个。
        /// 成功返回 true（价格 / 底色会跟着刷）；没选中 / 空格位 / 钱不够 / 已经买过 / 已售罄 → false，什么都不发生。
        /// </summary>
        public bool TryBuyHighlighted()
        {
            if (_highlightedCell == null)
            {
                return false;
            }

            return TryBuyCell(_highlightedCell);
        }

        /// <summary>按钮入口的 void 版本（UnityEvent 直接绑这个也行）</summary>
        public void OnBuyButton()
        {
            TryBuyHighlighted();
        }

        private void RefreshAllCell()
        {
            foreach (KeyValuePair<ItemCategory, List<StoreCellUI>> kv in _pageCells)
            {
                List<StoreCellUI> cells = kv.Value;
                for (int i = 0; i < cells.Count; i++)
                {
                    cells[i].Refresh();
                }
            }
        }

        #region 选中高亮 / 显示信息

        /// <summary>
        /// 选中某一格：全商店**同时只有一格亮着**（互斥）。表现有两条：
        /// 1. 那一格的块**恢复成背包形态**（显示格子框、缩放归 1）—— 也就是"放大显示网格"，
        ///    能看清这件货真实占几格；取消选中时再摆回商店样式；
        /// 2. 把它的标题 / 描述刷到显示区。
        /// </summary>
        private void SetHighlight(StoreCellUI cell)
        {
            if (ReferenceEquals(_highlightedCell, cell))
            {
                return;
            }

            if (_highlightedCell != null)
            {
                _highlightedCell.SetHighlighted(false);
                RestoreCellBlock(_highlightedCell);
            }

            _highlightedCell = cell;

            if (_highlightedCell == null)
            {
                ShowWellcomeInfo();
                return;
            }

            _highlightedCell.SetHighlighted(true);
            ShowInfo(_highlightedCell);

            if (TryGetBlockOf(_highlightedCell, out BagItemBlock block))
            {
                block.ApplyBagPresentation();
            }
        }

        /// <summary>把某一格的块摆回商店样式（取消选中用）；没货 / 没块就跳过</summary>
        private void RestoreCellBlock(StoreCellUI cell)
        {
            if (cell != null && TryGetBlockOf(cell, out BagItemBlock block))
            {
                LayoutBlockToCell(block, cell);
            }
        }

        /// <summary>把某一格的商品信息刷到显示区（标题用分类代表色）；空格位就回默认语</summary>
        private void ShowInfo(StoreCellUI cell)
        {
            PickableItem item = cell != null && cell.Goods != null ? cell.Goods.item : null;
            if (item == null)
            {
                ShowWellcomeInfo();
                return;
            }

            ShowInfo(item.ItemTitle, item.ItemDesc, UIManager.Instance.CategoryColor(item.Category));
        }

        private void ShowWellcomeInfo()
        {
            ShowInfo("欢迎光临", "", DefaultTitleColor);
        }
        
        private void ShowBuyInfo()
        {
            ShowInfo("谢谢惠顾", "请收好", DefaultTitleColor);
        }
        
        private void ShowNoMoneyInfo()
        {
            ShowInfo("钱不够哦", "快去上班吧", Color.red);
        }
        
        private void ShowInfo(string title, string content, Color titleColor)
        {
            Assert.IsTrue(_showTitle != null && _showContent != null,
                "StorePanelUI：显示信息的标题 / 内容没连");

            _showTitle.text = title;
            _showContent.text = content;

            // 标题配色和待拾取位一致：有商品时用分类代表色，默认语用黑色
            _showTitle.color = titleColor;
        }

        /// <summary>这一格上那件货的块（没货 / 已售罄 / 没建块 → false）</summary>
        private bool TryGetBlockOf(StoreCellUI cell, out BagItemBlock block)
        {
            MouseStore.Goods goods = cell.Goods;
            block = null;

            return goods != null
                   && goods.item != null
                   && UIManager.Instance.TryGetBlock(goods.item, out block);
        }

        /// <summary>退出所有高亮（切页 / 关店 / 点到空格时用）：灭掉当前那格、块摆回商店样式、回默认语</summary>
        private void ClearHighlight()
        {
            if (_highlightedCell != null)
            {
                _highlightedCell.SetHighlighted(false);
                RestoreCellBlock(_highlightedCell);
                _highlightedCell = null;
            }

            ShowWellcomeInfo();
        }

        #endregion

        #endregion

        #region 布局（容器负责"块摆在哪"）

        /// <summary>
        /// 商店布局：隐藏格子框、按标准朝向、把整块形状的 bbox 缩放并居中到格心。
        ///
        /// bbox 中心 ≠ 块的原点（块的原点是抓取格中心），所以居中这里不算坐标 ——
        /// 直接把"物品图那一层"的中心搬到格心即可（换目标/旋转都不影响）。
        /// 块挂在**格子里**（<paramref name="cell"/> 下面）：跟着格子一起显隐、一起被切页带走，
        /// 并且排到第一个子物体 —— UI 后画的盖前面，排在文字前面才会被两个文字盖住。
        /// 拖动中才会临时挪到拖动层。
        /// </summary>
        public void LayoutBlockToCell(BagItemBlock block, StoreCellUI cell)
        {
            Assert.IsTrue(block != null, "StorePanelUI.LayoutBlockToCell：block 是 null");
            AssertCellBelongsToThisPanel(cell);
            Assert.IsTrue(block.Icon != null, "StorePanelUI.LayoutBlockToCell：块没有物品图层");

            block.CurrentSlot = cell;
            block.SetCellFramesVisible(false);

            // 挂在格子里，并排到第一位（画在价格 / 购买提示两个文字下面）
            block.transform.SetParent(cell.transform, false);
            block.transform.SetAsFirstSibling();
            block.transform.localRotation = Quaternion.identity; // 商店按"标准朝向"看

            // 先归位再量尺寸：bbox 的长边塞满格子，短边按比例
            block.transform.localScale = Vector3.one;
            Vector2 iconSize = block.Icon.rect.size;
            float fit = CellSize / Mathf.Max(iconSize.x, iconSize.y);
            block.transform.localScale = Vector3.one * fit;

            // 居中：把物品图的中心搬到格心
            block.transform.position += cell.transform.position - block.Icon.position;
        }

        #endregion

        #region 表现 / 工具

        /// <summary>
        /// 可交互性（射线 / 点选）立刻生效；alpha 只在"显示"时立刻拉满 ——
        /// 隐藏的 alpha 要等离场动画播完（<see cref="FinishClose"/> 里），不然看不到它滑出去。
        /// </summary>
        private void ApplyVisibility(bool visible)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;

            if (visible)
            {
                _canvasGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// 事件只负责触发：开关的全部动作都在 <see cref="OpenContainer"/> / <see cref="CloseContainer"/> 里，
        /// 去重也在那两个方法里（重复的事件不会重复建块 / 重复销毁）。
        /// </summary>
        private void OnStoreEvent(StoreOnOffEvent evt)
        {
            if (evt.IsOpen)
            {
                Debug.Log("StorePanelUI.StoreOnOffEvent");
                OpenContainer();
            }
            else
            {
                CloseContainer();
            }
        }

        private void OnPhaseChanged(GamePhaseChangedEvent evt)
        {
            if (evt.NewPhase == GamePhase.Shopping)
            {
                // 每天刷新
                mouseStore.RefreshStore();
            }
        }

        private void AssertCellBelongsToThisPanel(StoreCellUI cell)
        {
            Assert.IsTrue(cell != null, "StorePanelUI：cell 是 null");
            Assert.IsTrue(_pageCells.ContainsKey(cell.Page), $"StorePanelUI：没有 {cell.Page} 页");
            Assert.IsTrue(cell.Index >= 0 && cell.Index < _pageCells[cell.Page].Count,
                $"StorePanelUI：{cell.Page} 页的格子下标 {cell.Index} 越界");
            Assert.IsTrue(_pageCells[cell.Page][cell.Index] == cell,
                $"StorePanelUI：{cell.Page} 页第 {cell.Index} 格不是这个面板建的（是不是连错容器了？）");
        }

        #endregion
    }
}
