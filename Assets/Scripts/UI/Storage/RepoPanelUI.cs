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
    /// 仓库界面容器：一格一件（格数由 <see cref="PlayerRepo.Width"/> × Height 决定，不再自己写死常量）。
    ///
    /// 【和 BagPanelUI 一致】
    /// - 实现 <see cref="IItemContainer"/>：打开时为仓库持有的每件物品建块并摆好，关闭时销毁这些块；
    /// - 不记录自己持有哪些块 —— 块跟着物品走，物品转到别的容器后由那个容器负责销毁；
    /// - 布局在容器里（槽位只转发）；拖拽由 <see cref="BagItemBlock"/> 自己射线找落点。
    ///
    /// 【和 BagPanelUI 的区别】
    /// - 判的是"这一格空不空"，不管形状；
    /// - 块在仓库里隐藏格子框、只显示物品图，并把 bbox 缩放/居中到格心。
    ///
    /// 开关走独立事件 <see cref="RepoOnOffEvent"/>（和背包的 BackpackEvent 互不影响）。
    /// </summary>
    [DisallowMultipleComponent]
    public class RepoPanelUI : MonoBehaviour, IItemContainer
    {
        #region 配置常量

        /// <summary>单格边长，物品在仓库中不显示格子、只显示sprite、且尺寸自适应缩放</summary>
        private const float CellSize = 150f;

        /// <summary>格与格的间隙</summary>
        private const float CellSpacing = 3f;

        /// <summary>格心间距</summary>
        private const float CellPitch = CellSize + CellSpacing;

        private static readonly Color CellEmptyColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color PreviewOkColor = new Color(0.35f, 0.9f, 0.4f, 0.35f);
        private static readonly Color PreviewBadColor = new Color(0.95f, 0.3f, 0.3f, 0.35f);

        #endregion

        #region 序列化引用

        [Header("网格 / 物料")]
        [Tooltip("仓库网格根节点，格子由代码生成在这里。列/行数取自 PlayerRepo，不要在面板上另写一套")]
        [SerializeField] private RectTransform _gridRoot;

        [Header("仓库组件")]
        [Tooltip("玩家的 PlayerRepo。在 Inspector 里从场景里的 Player 预制体实例上拖过来")]
        [SerializeField] private PlayerRepo _repo;

        #endregion

        #region 运行时状态

        private readonly List<RepoCellUI> _cells = new List<RepoCellUI>();

        private CanvasGroup _canvasGroup;
        private bool _visible;

        #endregion

        #region 生命周期

        private void Awake()
        {
            Assert.IsTrue(_gridRoot, "RepoPanelUI：没连 _gridRoot");
            Assert.IsTrue(_repo, "RepoPanelUI：没连 PlayerRepo");

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            BuildGrid();
            ApplyVisibility();
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

        #region IItemContainer

        public void OpenContainer()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                PickableItem item = _repo.ItemAt(i);
                if (item == null)
                {
                    continue;
                }

                BagItemBlock block = UIManager.Instance.CreateBlock(item);
                LayoutBlockToCell(block, _cells[i]);
            }
        }

        public void CloseContainer()
        {
            // 拖到一半就关：这块已经不属于任何容器了，按约定直接销毁
            UIManager.Instance.DestroyDraggingBlock();

            foreach (PickableItem item in _repo.Items)
            {
                UIManager.Instance.DestroyBlock(item);
            }

            HidePreview();
        }

        #endregion

        #region 网格构建

        private void BuildGrid()
        {
            _cells.Clear();

            // 清掉旧格子（可能来自上一次进 Play 的残留）
            for (int i = _gridRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _gridRoot.GetChild(i);
                if (child.GetComponent<RepoCellUI>() != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            for (int index = 0; index < _repo.CellCount; index++)
            {
                _cells.Add(CreateCell(index));
            }
        }

        private RepoCellUI CreateCell(int index)
        {
            GameObject go = new GameObject($"RepoCell_{index}", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(_gridRoot, false);
            rect.anchorMin = new Vector2(0f, 1f); // 以网格左上角为基准
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CellSize, CellSize);
            rect.anchoredPosition = CellLocalPosition(index) + new Vector2(CellSize * 0.5f, -CellSize * 0.5f);

            Image background = EnsureImage(go, CellEmptyColor, true);
            Image preview = CreateStretchImage(rect, "Preview", PreviewOkColor, false);

            RepoCellUI cell = go.AddComponent<RepoCellUI>();
            cell.Initialize(index, background, preview, CellEmptyColor);
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

        /// <summary>预览：这一格空着就能放</summary>
        public bool TryDropToCell(BagItemBlock block, RepoCellUI cell)
        {
            Assert.IsTrue(block != null, "RepoPanelUI.TryDropToCell：block 是 null");
            AssertCellBelongsToThisPanel(cell);

            bool canDrop = !_repo.IsCellOccupied(cell.Index);
            cell.SetDropPreview(canDrop ? PreviewOkColor : PreviewBadColor);
            return canDrop;
        }

        /// <summary>真放：登记到仓库 + 把块按仓库的样子塞进格子</summary>
        public bool DropToCell(BagItemBlock block, RepoCellUI cell)
        {
            Assert.IsTrue(block != null, "RepoPanelUI.DropToCell：block 是 null");
            AssertCellBelongsToThisPanel(cell);

            if (!_repo.PutItem(cell.Index, block.boundedItem))
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
        public void OnItemPickedUp(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "RepoPanelUI.OnItemPickedUp：block 是 null");

            block.ApplyBagPresentation();

            Assert.IsTrue(_repo.TakeFromRepo(block.boundedItem),
                $"RepoPanelUI.OnItemPickedUp：{block.boundedItem} 不在仓库里");

            UIManager.Instance.DetachItemToWorld(block.boundedItem);
        }

        #endregion

        #region 布局（容器负责"块摆在哪"）

        /// <summary>
        /// 仓库布局：隐藏格子框、按标准朝向、把整块形状的 bbox 缩放并居中到格心。
        ///
        /// bbox 中心 ≠ 块的原点（块的原点是抓取格中心），所以居中这里不算坐标 ——
        /// 直接把"物品图那一层"的中心搬到格心即可（换目标/旋转都不影响）。
        /// </summary>
        public void LayoutBlockToCell(BagItemBlock block, RepoCellUI cell)
        {
            Assert.IsTrue(block != null, "RepoPanelUI.LayoutBlockToCell：block 是 null");
            AssertCellBelongsToThisPanel(cell);
            Assert.IsTrue(block.Icon != null, "RepoPanelUI.LayoutBlockToCell：块没有物品图层");

            block.CurrentSlot = cell;
            block.SetCellFramesVisible(false);
            block.transform.SetParent(UIManager.Instance.DragLayer, false);
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

        private void ApplyVisibility()
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = _visible ? 1f : 0f;

            // 和背包同理：alpha = 0 不影响射线，隐藏时必须关掉 blocksRaycasts，
            // 否则这张看不见的面板会把别的 UI 的点击吃掉
            _canvasGroup.blocksRaycasts = _visible;
            _canvasGroup.interactable = _visible;
        }

        private void OnRepoEvent(RepoOnOffEvent evt)
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
                OpenContainer();
            }
            else
            {
                CloseContainer();
            }

            ApplyVisibility();
        }

        /// <summary>清掉所有格子的试放预览</summary>
        public void HidePreview()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                _cells[i].ClearPreview();
            }
        }

        private void AssertCellBelongsToThisPanel(RepoCellUI cell)
        {
            Assert.IsTrue(cell != null, "RepoPanelUI：cell 是 null");
            Assert.IsTrue(cell.Index >= 0 && cell.Index < _cells.Count, $"RepoPanelUI：格子下标 {cell.Index} 越界");
            Assert.IsTrue(_cells[cell.Index] == cell,
                $"RepoPanelUI：格子 {cell.Index} 不是这个面板建的（是不是连错容器了？）");
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
