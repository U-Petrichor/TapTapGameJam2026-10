using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Core;
using TapTapGameJam.Items;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 背包 / 仓库 / 商店这些界面共用的设施：所有 <see cref="BagItemBlock"/> 的生成与销毁、
    /// 拖动层、以及"鼠标正拖着的那一块"。
    ///
    /// 它是 PickableItem → BagItemBlock 的**唯一登记处**。容器不维护自己的块列表：
    /// 打开时按自己持有的物品申请块，关闭时按自己持有的物品请求销毁；物品在容器之间转移时
    /// 一行代码都不用写 —— 块跟着物品走，最后持有那件物品的容器负责销毁它。
    ///
    /// 【尺寸】格子尺寸放在这里是因为"背包格子的间距"和"块内格子框的间距"必须完全一致，
    /// 两边各留一份迟早会对不齐。仓库将来要自己的尺寸时，把这几项变成参数即可。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIManager : MonoBehaviour
    {
        private static UIManager _instance;

        /// <summary>
        /// 场景里唯一的 UIManager。
        ///
        /// 没挂、或者挂在"启动时处于关闭状态"的物体上时（Awake 不会跑），这里直接抛一条能照着做的错，
        /// 免得调用方在很远的地方报一个看不出原因的 NullReference。
        /// </summary>
        public static UIManager Instance
        {
            get
            {
                Assert.IsNotNull(_instance,
                    "场景里找不到 UIManager：请给一个启动时激活的物体挂上 UIManager 组件，" +
                    "并连好 DragLayer（拖动层）和 PlayerBag");
                return _instance;
            }
        }

        #region 序列化引用

        [Header("格子尺寸（背包用；仓库将来要另一套）")]
        [Tooltip("单格边长")]
        [SerializeField] private float _cellSize = 100f;

        [Tooltip("格与格的间隙")]
        [SerializeField] private float _cellSpacing = 3f;

        [Header("引用")]
        [Tooltip("拖动中的块临时挂到这里，保证它在最上层")]
        [SerializeField] private RectTransform _dragLayer;

        [Tooltip("物品送回世界时用的后端。UI 侧只有这一处要落到世界上")]
        [SerializeField] private PlayerBag _bag;

        #endregion

        #region 对外

        public RectTransform DragLayer => _dragLayer;

        public float CellSize => _cellSize;

        /// <summary>格心间距（块内相邻格的距离也是它）</summary>
        public float CellPitch => _cellSize + _cellSpacing;

        /// <summary>格子中心相对网格左上角的位置（x 向右、y 向下，所以 y 取负）。格子布局和块内格子框都用它</summary>
        public Vector2 CellLocalPosition(Vector2Int cell)
        {
            return new Vector2(cell.x * CellPitch, -cell.y * CellPitch);
        }

        /// <summary>鼠标正拖着的那一块。R 键旋转、拖拽中关容器都靠它</summary>
        public BagItemBlock CurrentDraggingBlock { get; private set; }

        #endregion

        #region 生命周期

        private void Awake()
        {
            Assert.IsNull(_instance, "UIManager：场景里出现了两个实例");
            _instance = this;

            Assert.IsTrue(_dragLayer, "UIManager：没连 DragLayer");
            Assert.IsTrue(_bag, "UIManager：没连 PlayerBag（送回世界要用）");

            EnsureDragLayerOnTop();
        }

        /// <summary>
        /// 拖动层必须是"最上面那一层"。
        ///
        /// 容器面板是同一个 Canvas 下的兄弟节点，谁在上面靠层级顺序决定 —— 仓库面板就排在背包面板
        /// 后面，块会被它整个盖住（看起来像"物品放进去就没了"）。而且拖动层原来套在背包面板的
        /// CanvasGroup 里：背包一关（alpha=0 / blocksRaycasts=false），拖动层上的块会跟着隐形、
        /// 还收不到射线，仓库单独打开时什么都动不了。
        ///
        /// 所以这里把拖动层挪到根 Canvas 的最后一个子物体。想改成在场景里手动摆好也行，
        /// 把这次调用删掉即可（等价）。
        /// </summary>
        private void EnsureDragLayerOnTop()
        {
            Canvas canvas = _dragLayer.GetComponentInParent<Canvas>();
            Assert.IsTrue(canvas != null, "UIManager：DragLayer 不在任何 Canvas 下");

            _dragLayer.SetParent(canvas.rootCanvas.transform, false);
            _dragLayer.SetAsLastSibling();
        }

        private void Start()
        {
            // 和面板一样放到 Start：GameManager.PlayerInputManager 在那之前不一定就绪
            GameManager.PlayerInputManager.EventOnRotateItemPress += OnRotateItemPressed;
        }

        private void OnDisable()
        {
            GameManager.PlayerInputManager.EventOnRotateItemPress -= OnRotateItemPressed;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnRotateItemPressed()
        {
            if (CurrentDraggingBlock == null)
            {
                return;
            }

            CurrentDraggingBlock.RotateItem();
        }

        #endregion

        #region 块的生成 / 销毁

        private readonly Dictionary<PickableItem, BagItemBlock> _blocks =
            new Dictionary<PickableItem, BagItemBlock>();

        /// <summary>
        /// 为一件物品建表现块并登记。1 件物品 1 个块，重复创建说明容器没清干净，直接 Assert。
        /// </summary>
        public BagItemBlock CreateBlock(PickableItem item)
        {
            Assert.IsTrue(item != null, "CreateBlock：item 是 null");
            Assert.IsTrue(item.shape != null, $"CreateBlock：{item.name} 没配 shape，摆不进格子");
            Assert.IsFalse(_blocks.ContainsKey(item),
                $"CreateBlock：{item.name} 已经有块了（块与物品 1:1，重复创建说明上一次没销毁）");

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

            // 先在"未旋转"的偏移上把块排好，朝向由父节点旋转表达（见 BagShape.UnrotatedOffsets）
            List<Vector2Int> offsets = item.shape.UnrotatedOffsets;

            // 格子框单独放一层：仓库要把整层隐藏、只留物品图
            GameObject framesRoot = new GameObject("CellFrames", typeof(RectTransform));
            RectTransform framesRect = (RectTransform)framesRoot.transform;
            framesRect.SetParent(root, false);
            framesRect.anchorMin = new Vector2(0.5f, 0.5f);
            framesRect.anchorMax = new Vector2(0.5f, 0.5f);
            framesRect.pivot = new Vector2(0.5f, 0.5f);
            framesRect.sizeDelta = Vector2.zero;
            framesRect.anchoredPosition = Vector2.zero;

            for (int i = 0; i < offsets.Count; i++)
            {
                AddCellFrame(framesRect, offsets[i], item.shape.colorInGrid);
            }

            RectTransform icon = AddItemSprite(root, item, offsets);
            block.AttachVisuals(framesRect, icon);
            block.SyncRotationWithItem();

            _blocks.Add(item, block);
            return block;
        }

        /// <summary>销毁某件物品的块。容器关闭、右键用掉物品时走这里</summary>
        public void DestroyBlock(PickableItem item)
        {
            Assert.IsTrue(item != null, "DestroyBlock：item 是 null");
            Assert.IsTrue(_blocks.ContainsKey(item),
                $"DestroyBlock：{item.name} 没有对应的块（容器持有的物品必须有块）");

            BagItemBlock block = _blocks[item];
            _blocks.Remove(item);
            DestroyBlock(block);
        }

        /// <summary>销毁某个块（登记项一并摘掉）</summary>
        public void DestroyBlock(BagItemBlock block)
        {
            if (block == null)
            {
                return;
            }

            if (block.boundedItem != null)
            {
                _blocks.Remove(block.boundedItem);
            }

            if (CurrentDraggingBlock == block)
            {
                CurrentDraggingBlock = null;
            }

            Destroy(block.gameObject);
        }

        public bool TryGetBlock(PickableItem item, out BagItemBlock block)
        {
            block = null;
            return item != null && _blocks.TryGetValue(item, out block);
        }

        /// <summary>
        /// 物品被从容器里拿起来（按下开始拖）：本体回到世界。
        /// "拿起 = 脱离原容器"是 UI 侧的统一规则，所以各容器的 OnPickUpItem 都调这里；
        /// 拖动期间物品就算 OnMap（老规矩，先不管隐藏），放不下时块会带着 CurrentSlot 回退。
        /// </summary>
        public void DetachItemToWorld(PickableItem item)
        {
            Assert.IsTrue(item != null, "DetachItemToWorld：item 是 null");
            _bag.DropToWorld(item);
        }

        /// <summary>把块对应的物品送回世界，然后销毁块（拖到面板外松手 / 落在拒绝的槽位上时走这里）</summary>
        public void ReturnBlockToWorld(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "ReturnBlockToWorld：block 是 null");

            PickableItem item = block.boundedItem;
            if (item != null)
            {
                _bag.DropToWorld(item);
            }

            DestroyBlock(block);
        }

        /// <summary>拖拽到一半容器被关掉：这一块已经不属于任何容器了，直接销毁</summary>
        public void DestroyDraggingBlock()
        {
            if (CurrentDraggingBlock == null)
            {
                return;
            }

            DestroyBlock(CurrentDraggingBlock);
        }

        #endregion

        #region 拖拽

        /// <summary>开始拖拽：记下当前块，并把它挂到拖动层（层级统一在这里管，块自己不认识 DragLayer）</summary>
        public void NotifyBeginDrag(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "NotifyBeginDrag：block 是 null");
            Assert.IsTrue(CurrentDraggingBlock == null, "NotifyBeginDrag：上一块还在拖，同一时间只允许拖一块");

            CurrentDraggingBlock = block;
            block.transform.SetParent(_dragLayer, false);
        }

        public void NotifyEndDrag(BagItemBlock block)
        {
            if (CurrentDraggingBlock == block)
            {
                CurrentDraggingBlock = null;
            }
        }

        #endregion

        #region 块内物料

        private void AddCellFrame(Transform parent, Vector2Int offset, Color color)
        {
            GameObject go = new GameObject($"CellFrame_{offset.x}_{offset.y}", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(_cellSize, _cellSize);
            rect.anchoredPosition = CellLocalPosition(offset);

            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false; // 点选留给物品图，格子框不参与射线
        }

        /// <summary>
        /// 建"物品图"那一层并返回它（没配图时用形状色画一个占位块 —— 仓库只显示这一层，
        /// 没有它物品在仓库里就完全看不见）。返回的 rect 就是占格 bbox 的尺寸，仓库靠它缩放/居中。
        /// </summary>
        private RectTransform AddItemSprite(Transform parent, PickableItem item, List<Vector2Int> offsets)
        {
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
            if (item.BagIcon != null)
            {
                image.sprite = item.BagIcon;
                image.preserveAspect = true;
            }
            else
            {
                // 还没配图：用形状色兜底，至少看得见、也拖得动
                image.color = item.shape.colorInGrid;
                image.preserveAspect = false;
            }

            // 拖拽的射线必须打中物品图（块的空物体和格子框都不参与射线）
            image.raycastTarget = true;
            return rect;
        }

        #endregion
    }
}
