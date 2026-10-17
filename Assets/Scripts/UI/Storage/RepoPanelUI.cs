using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 仓库界面容器：一格一件（每页格数由 <see cref="PlayerRepo.Width"/> × Height 决定，不再自己写死常量）。
    ///
    /// 【分页】按 <see cref="ItemCategory"/> 分三页（Food / Tool / Dress），每页一个 GridRoot、一组
    /// <see cref="RepoCellUI"/>；同时只显示当前页（其余 GridRoot 直接 SetActive(false)）。
    /// 物品块挂在**它那一页的 GridRoot** 下，所以切页时格子、块、射线一起跟着显隐。
    /// 切页入口是 <see cref="ShowFoodPage"/> / <see cref="ShowToolPage"/> / <see cref="ShowDressPage"/>，
    /// 直接绑给 ToggleGroup 里的 Toggle 即可。
    ///
    /// 【和 BagPanelUI 一致】
    /// - 派生自 <see cref="ItemContainer"/>：打开时为仓库持有的每件物品建块并摆好，关闭时销毁这些块；
    /// - 不记录自己持有哪些块 —— 块跟着物品走，物品转到别的容器后由那个容器负责销毁；
    /// - 布局在容器里（槽位只转发）；拖拽由 <see cref="BagItemBlock"/> 自己射线找落点。
    ///
    /// 【和 BagPanelUI 的区别】
    /// - 判的是"这一格空不空"，不管形状；物品类别还必须和当前页对上
    ///   （对不上时 <see cref="TryDropToCell"/> 返回 false 并**自动切到它该去的那一页**）；
    /// - 块在仓库里隐藏格子框、只显示物品图，并把 bbox 缩放/居中到格心。
    ///
    /// 开关走独立事件 <see cref="RepoOnOffEvent"/>（和背包的 BackpackEvent 互不影响）。
    /// </summary>
    [DisallowMultipleComponent]
    public class RepoPanelUI : ItemContainer
    {
        #region 配置常量

        /// <summary>单格边长，物品在仓库中不显示格子、只显示sprite、且尺寸自适应缩放</summary>
        private const float CellSize = 155f;

        /// <summary>格与格的间隙</summary>
        private const float CellSpacing = 5f;

        /// <summary>格心间距</summary>
        private const float CellPitch = CellSize + CellSpacing;

        private static readonly Color CellEmptyColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color PreviewOkColor = new Color(0.35f, 0.9f, 0.4f, 0.35f);
        private static readonly Color PreviewBadColor = new Color(0.95f, 0.3f, 0.3f, 0.35f);

        #endregion

        #region 序列化引用

        [Header("网格 / 物料（三页各一个 GridRoot）")]
        [Tooltip("Food 页的网格根节点。格子由代码生成在这里，列/行数取自 PlayerRepo")]
        [SerializeField] private RectTransform _foodGridRoot;
        [Tooltip("Tool 页的网格根节点")]
        [SerializeField] private RectTransform _toolGridRoot;
        [Tooltip("Dress 页的网格根节点")]
        [SerializeField] private RectTransform _dressGridRoot;

        [Header("仓库组件")]
        [Tooltip("玩家的 PlayerRepo。在 Inspector 里从场景里的 Player 预制体实例上拖过来")]
        [SerializeField] private PlayerRepo _repo;

        #endregion

        #region 运行时状态

        /// <summary>每一页的 GridRoot</summary>
        private readonly Dictionary<ItemCategory, RectTransform> _pageRoots =
            new Dictionary<ItemCategory, RectTransform>();

        /// <summary>每一页的格子</summary>
        private readonly Dictionary<ItemCategory, List<RepoCellUI>> _pageCells =
            new Dictionary<ItemCategory, List<RepoCellUI>>();

        /// <summary>当前显示的是哪一页</summary>
        private ItemCategory _currentPage;

        public ItemCategory CurrentPage => _currentPage;

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

        [Tooltip("藏起来时的位置：相对停留位置的偏移。仓库从屏幕左侧进来 → 填负 x（按面板宽度调，≈-1100,0）")]
        [SerializeField] private Vector2 _hiddenOffset = new Vector2(-1100f, 0f);

        #endregion

        #region 生命周期

        private void Awake()
        {
            Assert.IsTrue(_foodGridRoot && _toolGridRoot && _dressGridRoot,
                "RepoPanelUI：三个 GridRoot（Food / Tool / Dress）要连全");
            Assert.IsTrue(_repo, "RepoPanelUI：没连 PlayerRepo");

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
            ApplyVisibility(false);

            BuildPages();

            // 默认停在第一页（Food），并且保证只有它显示
            ShowPage(ItemCategories.All[0]);
        }

        public void OpenRepoTemp()
        {
            EventPipeline.Publish<RepoOnOffEvent>(new RepoOnOffEvent(true));
        }

        private void Start()
        {
            EventPipeline.Subscribe<RepoOnOffEvent>(OnRepoEvent);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<RepoOnOffEvent>(OnRepoEvent);
        }

        #endregion

        #region ItemContainer

        /// <summary>
        /// 打开：为仓库（三页）持有的每件物品建块并摆到它那一页的格子上，最后把自己显示出来。
        /// 可安全重复调用；状态与 ActionMap 由基类负责。
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

            foreach (ItemCategory page in ItemCategories.All)
            {
                List<RepoCellUI> cells = _pageCells[page];
                for (int i = 0; i < cells.Count; i++)
                {
                    PickableItem item = _repo.ItemAt(page, i);
                    if (item == null)
                    {
                        continue;
                    }

                    BagItemBlock block = UIManager.Instance.CreateBlock(item);
                    LayoutBlockToCell(block, cells[i]);
                }
            }

            HidePreview();
            ApplyVisibility(true);
            ShowPage(_currentPage);

            // 块已经在这一帧建好了，接着从隐藏位滑进来
            PlayEnterAnimation();
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

            ApplyVisibility(false);

            base.CloseContainer();

            PlayExitAnimation(FinishClose);
        }

        /// <summary>离场动画播完之后的收尾：把仓库（三页）持有的块交回 UIManager 销毁、清预览</summary>
        private void FinishClose()
        {
            foreach (PickableItem item in _repo.Items)
            {
                UIManager.Instance.DestroyBlock(item);
            }

            HidePreview();

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

            foreach (KeyValuePair<ItemCategory, RectTransform> kv in _pageRoots)
            {
                kv.Value.gameObject.SetActive(kv.Key == page);
            }
        }

        /// <summary>三页各建一组格子（页数、每页格数都取自 PlayerRepo）</summary>
        private void BuildPages()
        {
            _pageCells.Clear();

            foreach (ItemCategory page in ItemCategories.All)
            {
                RectTransform root = _pageRoots[page];

                // 清掉旧格子（可能来自上一次进 Play 的残留）
                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    Transform child = root.GetChild(i);
                    if (child.GetComponent<RepoCellUI>() != null)
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }

                List<RepoCellUI> cells = new List<RepoCellUI>(_repo.CellCount);
                for (int index = 0; index < _repo.CellCount; index++)
                {
                    cells.Add(CreateCell(page, root, index));
                }

                _pageCells[page] = cells;
            }
        }

        private RepoCellUI CreateCell(ItemCategory page, RectTransform root, int index)
        {
            GameObject go = new GameObject($"RepoCell_{page}_{index}", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0f, 1f); // 以网格左上角为基准
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CellSize, CellSize);
            rect.anchoredPosition = CellLocalPosition(index) + new Vector2(CellSize * 0.5f, -CellSize * 0.5f);

            Image background = EnsureImage(go, CellEmptyColor, true);
            Image preview = CreateStretchImage(rect, "Preview", PreviewOkColor, false);

            RepoCellUI cell = go.AddComponent<RepoCellUI>();
            cell.Initialize(page, index, background, preview, CellEmptyColor);
            cell.repoPanelUI = this;
            return cell;
        }

        /// <summary>格子中心相对网格左上角的位置（x 向右、y 向下，所以 y 取负）</summary>
        private Vector2 CellLocalPosition(int index)
        {
            int column = index % _repo.Width;
            int row = index / _repo.Width;
            return new Vector2(column * CellPitch, -row * CellPitch);
        }

        #endregion

        #region 容器操作（槽位转发到这里）

        /// <summary>
        /// 预览：物品类别和这一格所在的页对上、且这一格空着才能放。
        /// 对不上时**顺手切到它该去的那一页**，然后返回 false —— 这样玩家的拖拽会自然落到
        /// 同一位置的那一页格子上（各页 GridRoot 位置是重合的）。
        /// </summary>
        public bool TryDropToCell(BagItemBlock block, RepoCellUI cell)
        {
            Assert.IsTrue(block != null, "RepoPanelUI.TryDropToCell：block 是 null");
            AssertCellBelongsToThisPanel(cell);

            ItemCategory itemPage = block.boundedItem.Category;
            if (itemPage != cell.Page)
            {
                ShowPage(itemPage);
                return false;
            }

            bool canDrop = !_repo.IsCellOccupied(cell.Page, cell.Index);
            cell.SetDropPreview(canDrop ? PreviewOkColor : PreviewBadColor);
            return canDrop;
        }

        /// <summary>真放：登记到仓库 + 把块按仓库的样子塞进格子（进对应页的 GridRoot）</summary>
        public bool DropToCell(BagItemBlock block, RepoCellUI cell)
        {
            Assert.IsTrue(block != null, "RepoPanelUI.DropToCell：block 是 null");
            AssertCellBelongsToThisPanel(cell);
            Assert.IsTrue(block.boundedItem.Category == cell.Page,
                $"RepoPanelUI.DropToCell：{block.boundedItem.name} 是 {block.boundedItem.Category} 类，" +
                $"却往 {cell.Page} 页放（TryDropToCell 里应该已经切过页了）");

            if (!_repo.PutItem(cell.Page, cell.Index, block.boundedItem))
            {
                return false;
            }

            LayoutBlockToCell(block, cell);
            return true;
        }

        /// <summary>
        /// 从仓库把东西拿起来：先把块的形态恢复成背包那套（显示格子框、缩放归 1），
        /// 否则整个拖拽过程它都还是"仓库样子"；再从仓库摘掉、本体按统一规则回世界。
        /// </summary>
        public bool OnItemPickedUp(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "RepoPanelUI.OnItemPickedUp：block 是 null");

            block.ApplyBagPresentation();

            Assert.IsTrue(_repo.TakeFromRepo(block.boundedItem),
                $"RepoPanelUI.OnItemPickedUp：{block.boundedItem} 不在仓库里");

            UIManager.Instance.DetachItemToWorld(block.boundedItem);
            return true;
        }

        #endregion

        #region 布局（容器负责"块摆在哪"）

        /// <summary>
        /// 仓库布局：隐藏格子框、按标准朝向、把整块形状的 bbox 缩放并居中到格心。
        ///
        /// bbox 中心 ≠ 块的原点（块的原点是抓取格中心），所以居中这里不算坐标 ——
        /// 直接把"物品图那一层"的中心搬到格心即可（换目标/旋转都不影响）。
        /// 块挂在**它那一页的 GridRoot** 下：切页时跟着整页一起显隐，面板滑动时也一起走；
        /// 拖动中才会临时挪到拖动层。
        /// </summary>
        public void LayoutBlockToCell(BagItemBlock block, RepoCellUI cell)
        {
            Assert.IsTrue(block != null, "RepoPanelUI.LayoutBlockToCell：block 是 null");
            AssertCellBelongsToThisPanel(cell);
            Assert.IsTrue(block.Icon != null, "RepoPanelUI.LayoutBlockToCell：块没有物品图层");

            block.CurrentSlot = cell;
            block.SetCellFramesVisible(false);
            block.transform.SetParent(_pageRoots[cell.Page], false);
            block.transform.localRotation = Quaternion.identity; // 仓库按"标准朝向"看，不吃背包里的旋转

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
        private void OnRepoEvent(RepoOnOffEvent evt)
        {
            if (evt.IsOpen)
            {
                OpenContainer();
            }
            else
            {
                CloseContainer();
            }
        }

        /// <summary>清掉三页所有格子的试放预览</summary>
        public void HidePreview()
        {
            foreach (KeyValuePair<ItemCategory, List<RepoCellUI>> kv in _pageCells)
            {
                List<RepoCellUI> cells = kv.Value;
                for (int i = 0; i < cells.Count; i++)
                {
                    cells[i].ClearPreview();
                }
            }
        }

        private void AssertCellBelongsToThisPanel(RepoCellUI cell)
        {
            Assert.IsTrue(cell != null, "RepoPanelUI：cell 是 null");
            Assert.IsTrue(_pageCells.ContainsKey(cell.Page), $"RepoPanelUI：没有 {cell.Page} 页");
            Assert.IsTrue(cell.Index >= 0 && cell.Index < _pageCells[cell.Page].Count,
                $"RepoPanelUI：{cell.Page} 页的格子下标 {cell.Index} 越界");
            Assert.IsTrue(_pageCells[cell.Page][cell.Index] == cell,
                $"RepoPanelUI：{cell.Page} 页第 {cell.Index} 格不是这个面板建的（是不是连错容器了？）");
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
