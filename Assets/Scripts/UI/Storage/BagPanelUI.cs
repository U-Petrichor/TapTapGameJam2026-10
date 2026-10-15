using System;
using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
using TMPro.EditorUtilities;
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
    /// - 捡起物品打开：后端的 PendingPickupItem 在打开时建块、停在物品标记点上（还没拿在手上），提示框随之显示。
    ///
    /// 【操作】
    /// - 左键按住物品块拖动：落在格子上就试放，放不下回原位；落在格子外就是丢弃
    /// - 左键把块拖到装备槽上：穿上去；槽里原有的那件送回世界并提示玩家
    /// - R：旋转手持物（块不重建，靠父节点整体旋转）
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

        #endregion

        #region 运行时状态

        private readonly List<BagCellUI> _cells = new List<BagCellUI>();
        private readonly BagSlotUI[] _slots = new BagSlotUI[3];

        /// <summary>当前手持物（可能是从地图捡的、从背包拖出来的、从槽位脱下来的）</summary>
        private readonly HandBagItem _hand = new HandBagItem();
        
        /// <summary>背包中管理的所有block，打开背包统一创建，关闭背包统一清理</summary>
        private Dictionary<PickableItem, BagItemBlock> _blocks = new Dictionary<PickableItem, BagItemBlock>();
        
        /// <summary>待拾取的物品</summary>
        private PickableItem _unpickItem = null;

        private CanvasGroup _canvasGroup;
        private bool _visible;

        /// <summary>放下时那一次穿透射线的复用缓存，避免每次松手都分配</summary>
        private PointerEventData _pointerRaycastData;
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

        #endregion

        #region 生命周期

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            BuildGrid();
            BuildSlots();

            _itemInfoRoot?.gameObject.SetActive(false);
            ApplyVisibility();
        }

        private void Start()
        {
            Assert.IsTrue(_bag);
            EventPipeline.Subscribe<BackpackEvent>(OnBackpackEvent);
            GameManager.PlayerInputManager.EventOnRotateItemPress += OnRotateItemPressed;
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<BackpackEvent>(OnBackpackEvent);
            GameManager.PlayerInputManager.EventOnRotateItemPress -= OnRotateItemPressed;
        }

        /// <summary>
        /// 打开背包。**只监听 BackpackEvent 来触发，不要从别处调**：
        /// 捡东西那条链路是 PlayerBag.PickUp 把物品记成 PendingPickupItem 后自己发 BackpackEvent(true)，
        /// 界面在这里检查那个值，有就给它建块、停在物品标记点上（此时还不算拿在手上）。
        /// </summary>
        private void OpenBag()
        {
            if (_bag == null)
            {
                return;
            }

            _blocks.Clear();
            GenerateBlocksInBag();

            _hand.Clear();
            
            // 从地图捡起来、还没安顿的那件：给它建个块停在物品标记点上，提示框随之显示。
            // 注意这时**不算拿在手上** —— _hand 的语义是"鼠标正拿着/拖着的物品"，
            // 唯一设置它的时机是 BagItemBlock.OnBeginDrag。
            // 这里只"检查"不"消费"：PendingPickupItem 由 PlayerBag 自己维护，
            // 物品真的进了背包或有结果时它会清掉。
            PickableItem pending = _bag.PendingPickupItem;
            if (pending != null)
            {
                BagItemBlock block = GenerateBagItemBlock(pending);
                ReturnBlockToPivot(block);
                _blocks.Add(pending, block);
                SetUnpickItemInfo(pending);
            }
            else
            {
                SetUnpickItemInfo(null);
            }
        }

        /// <summary>
        /// 收起背包。清界面上的块
        /// </summary>
        private void CloseBag()
        {
            _hand.Clear();

            // 界面上的块全部销毁。不销毁的话下次 OpenBag 会为同一件物品再建一个块，
            // 旧的留在网格上重复显示、还压着射线挡拖动
            foreach (BagItemBlock block in _blocks.Values)
            {
                if (block != null)
                {
                    Destroy(block.gameObject);
                }
            }

            _blocks.Clear();

            _bag.ClearPendingPickupItem();
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

            HidePreview();
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
            cellUI.ClearPreview();
            return cellUI;
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
        /// 2. 按 shape 的"未旋转"偏移生成 CellFrame（100x100 纯色 Image），中心放在
        ///    偏移 * 格心间距 处，颜色取 shape.colorInGrid；这样 (0,0)（抓取格）天然在父节点原点；
        /// 3. 给物品图建 Image：anchor/pivot 都居中，大小 = bbox 格数 * 格心间距，
        ///    位置 = bbox 中心相对抓取格的位移，这样图正好盖住整个 bbox；
        /// 4. 按 shape.RotationSteps 整体旋转父节点（UI 里顺时针是 Z 负方向）。
        ///
        /// 第 2 步必须用"未旋转"的偏移，第 4 步再整体旋转：布局和旋转各做一次才等于最终朝向。
        /// 之前第 2 步用的是旋转后的偏移，和父节点旋转互相抵消 ——
        /// 表现上格子和物品图都停在未旋转的位置，和占格判定（按旋转后的偏移算）对不齐；
        /// 物品图的 rect 又会先按旋转后的 bbox 定尺寸，preserveAspect 于是一起缩水。
        /// </summary>
        private BagItemBlock GenerateBagItemBlock(PickableItem item)
        {
            Assert.IsTrue(item != null, "GenerateBagItemBlock：item 是 null");
            Assert.IsTrue(item.shape != null, $"GenerateBagItemBlock：{item.name} 没配 shape，放不进背包");

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

            List<Vector2Int> offsets = item.shape.UnrotatedOffsets;

            for (int i = 0; i < offsets.Count; i++)
            {
                AddCellFrame(root, offsets[i], item.shape.colorInGrid);
            }

            AddItemSprite(root, item, offsets);
            
            block.SyncRotationWithItem();

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

        /// <summary>为背包里所有物品生成块并挂到网格下并对齐格子</summary>
        private void GenerateBlocksInBag()
        {
            foreach (PlayerBag.Placement p in _bag.Placements)
            {
                BagItemBlock block = GenerateBagItemBlock(p.item);
                PlaceBlockIntoBag(block, p.origin);
                _blocks.Add(p.item, block);
            }
        }
        
        #endregion

        #region 手持物

        /// <summary>当前是否空着手</summary>
        public bool HasHandItem => _hand.IsValid;

        /// <summary>手持物对应的块</summary>
        public BagItemBlock HandBlock => _hand.Block;

        /// <summary>放不下时把东西还回原来的位置。还回去了返回 true</summary>
        private bool RestoreHandItemToOrigin()
        {
            if (!_hand.IsValid) return false;
            
            PickableItem item = _hand.Item;
            BagItemBlock block = _hand.Block;

            switch (_hand.Origin)
            {
                case HandItemOrigin.FromBag:
                    // 回原来的格子；
                    if (_bag.TryPlace(item, _hand.FromOrigin))
                    {
                        PlaceBlockIntoBag(block, _hand.FromOrigin);
                        _hand.Clear();
                        return true;
                    }
                    return false;

                case HandItemOrigin.FromSlot:
                    
                    Assert.IsTrue(item is DressItem);
                    DressItem dress = item as DressItem;
                    EquipSlot slot = dress.ToSlot();
                    
                    // 穿回原来的槽（正常一定成功，因为槽刚被我们腾空了）
                    if (_bag.TryEquip(item, out _))
                    {
                        ReturnBlockToSlot(block, slot);
                        _hand.Clear();
                        return true;
                    }

                    return false;

                case HandItemOrigin.Unpicked:
                    ReturnBlockToPivot(block);
                    _hand.Clear();
                    return true;
                
                default:
                    // FromStore / FromRepo 还没实现（SetHandItem 里也还没有地方会设这两个来源）
                    throw new ArgumentOutOfRangeException(nameof(_hand.Origin), _hand.Origin,
                        "没有实现的手持物来源");
            }
        }

        /// <summary>提示框：显示待拾取的物品，没有就收起来</summary>
        private void SetUnpickItemInfo(PickableItem item)
        {
            Assert.IsTrue(_infoTextTitle);
            Assert.IsTrue(_infoTextContent);
            Assert.IsTrue(_itemInfoRoot);
            _unpickItem = item;

            if (!item)
            {
                _itemInfoRoot.gameObject.SetActive(false);
                return;
            }

            // TODO: 物品名现在取的是 GameObject 名字，需要的话给 PickableItem 加一个显示名字段
            _infoTextTitle.text = item.name;
            _infoTextContent.text = item.HoverPrompt;
            _itemInfoRoot.gameObject.SetActive(true);
            
            ReturnBlockToPivot(_blocks[_unpickItem]);
        }

        /// <summary>R 键按下：旋转手持物</summary>
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

        /// <summary>
        /// 开始拖拽某个块。这里负责：
        /// 1. 安顿上一个还没放下的手持物；
        /// 2. 记录这件东西是从哪来的（未拾取 / 背包格子 / 装备槽），放不下时要回得去；
        /// 3. 把它从后端摘出来 —— 也就是"从背包里拿出来了"。
        /// </summary>
        public void SetHandItem(BagItemBlock block)
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

            RestoreHandItemToOrigin();

            _hand.Clear();
            _hand.Item = item;
            _hand.Block = block;

            // TODO: 加仓库和商店来源
            if (_bag.TryGetEquipSlot(item, out EquipSlot slot))
            {
                _hand.Origin = HandItemOrigin.FromSlot;
                _hand.FromSlot = slot;
                _bag.DropToWorld(item);
            }
            else if (_bag.TryGetPlacement(item, out Vector2Int origin))
            {
                _hand.Origin = HandItemOrigin.FromBag;
                _hand.FromOrigin = origin;
                _bag.DropToWorld(item);
            }
            else
            {
                _hand.Origin = HandItemOrigin.Unpicked;
            }
        }

        #endregion

        # region Block状态且切换（仅改变block位置）
        
        ///<summary> Block挪入背包中某处 </summary>
        public void PlaceBlockIntoBag(BagItemBlock block, Vector2Int origin)
        {
            Assert.IsTrue(block != null, "PlaceBlockIntoBag：block 是 null");
            Assert.IsTrue(TryGetCell(origin, out BagCellUI cell), $"PlaceBlockIntoBag：格子 {origin} 不在背包里");

            block.transform.SetParent(_gridRoot, false);

            // 块的 pivot 就是抓取格中心，格子的 position 就是格心 —— 直接对齐。
            // 不能手算坐标：块的 anchor 是 (0.5,0.5)、格子是 (0,1)，
            // 两者基准点差半个网格（256px），同一套数字拿去设 anchoredPosition/localPosition 都必然错位
            block.transform.position = cell.transform.position;
        }

        ///<summary> Block挪入unpick item的pivot处 </summary>
        public void ReturnBlockToPivot(BagItemBlock block)
        {
            block.transform.SetParent(_dragLayer, false);
            block.transform.position = _itemSpritePivot.position;
        }

        ///<summary> Block挪入某个槽位 </summary>
        public void ReturnBlockToSlot(BagItemBlock block, EquipSlot slot)
        {
            // TODO 应该以sprite形态进入槽位（隐藏底部格子 or 切换图标？）;
        }

        /// <summary>销毁某件物品的表现块。用在"后端已经不要这件东西了"的场合（例如右键用掉）</summary>
        private void DestroyBlockOf(PickableItem item)
        {
            Assert.IsTrue(item != null, "DestroyBlockOf：item 是 null");

            if (!_blocks.TryGetValue(item, out BagItemBlock block))
            {
                return;
            }

            _blocks.Remove(item);

            if (block != null)
            {
                Destroy(block.gameObject);
            }
        }
        
        
        # endregion
        
        #region 拖拽意图指令区

        /// <summary>把手持物送回世界</summary>
        private void DiscardHandItem()
        {
            if (!_hand.IsValid) return;
            PickableItem item = _hand.Item;

            _bag?.DropToWorld(item);
            _hand.Clear();

            if (!_unpickItem || item == _unpickItem)
            {
                // 标记点空着（或者本来就是这件）：丢掉的那件停在标记点上，成为待拾取物 —— 还能拖回背包
                SetUnpickItemInfo(item);
            }
            else
            {
                // 已经有别的待拾取物占了标记点：这件直接回世界，销毁它的块（字典里那条登记也一起摘掉）
                DestroyBlockOf(item);
            }
        }

        /// <summary>把手持物穿到槽位上。槽里原有的那件送回世界并提示玩家</summary>
        private bool TryEquipToSlot(BagSlotUI slot)
        {
            if (slot == null || !_hand.IsValid)
            {
                return false;
            }

            PickableItem item = _hand.Item;
            if (!_bag.TryEquip(item, out PickableItem replaced))
            {
                return false;
            }

            if (item == _unpickItem)
            {
                SetUnpickItemInfo(null);
            }
            _hand.Clear();

            if (replaced != null)
            {
                _bag.DropToWorld(replaced);
                
                // 替下来的东西，如果当前没有unpick，它就成为unpick
                if (!_unpickItem)
                {
                    SetUnpickItemInfo(replaced);
                }
                else
                {
                    DestroyBlockOf(item);
                }
            }

            return true;
        }
        
        /// <summary>
        /// 放下手持物：从 <paramref name="screenPosition"/> 打一条"穿透"的 UI 射线，
        /// 在所有命中里只取最靠前的第一个有效 <see cref="IItemSlot"/>
        /// （拖拽中的物品块、格框、面板底图都会压在同一点上，所以要能穿透着往后找），
        /// 再按槽位类型走各自的放置逻辑。什么都没命中 = 丢到世界里。
        /// </summary>
        /// <returns>是否成功放下（成功丢掉手上的东西都算）</returns>
        private bool TryDropHandItem(Vector2 screenPosition)
        {
            if (!_hand.IsValid)
            {
                return false;
            }

            if (!TryGetItemSlotUnderPointer(screenPosition, out IItemSlot slot))
            {
                DiscardHandItem();
                return true;
            }

            // 槽位种类在这儿扩展：新增仓库槽只要实现 IItemSlot 再加一个 case
            switch (slot)
            {
                case BagCellUI cell:

                    if (!_bag.TryPlace(_hand.Item, cell.Cell))
                    {
                        RestoreHandItemToOrigin();
                        return true;
                    }

                    PlaceBlockIntoBag(_hand.Block, cell.Cell);
                    _hand.Clear();
                    SetUnpickItemInfo(null);

                    return true;

                case BagSlotUI equipSlot:
                    // TODO: 穿到装备槽上。现成的 TryEquipToSlot(equipSlot) 可以直接接进来
                    return false;

                default:
                    // TODO: 仓库槽（将来的 BagSlotUI 同类）等其它 IItemSlot 的放置逻辑
                    return false;
            }
        }
        
        #endregion

        #region 指针回调（悬停/点击由 BagCellUI / BagSlotUI 转发；放下由 BagItemBlock 转发）

        public void NotifyCellHovered(BagCellUI cell, bool entered)
        {
            HandleCellHovered(cell, entered);
        }

        public void NotifyCellClicked(BagCellUI cell, PointerEventData.InputButton button)
        {
            HandleCellClicked(cell, button);
        }

        /// <summary>
        /// 鼠标松开的总入口（由 <see cref="BagItemBlock.OnEndDrag"/> 带着松开时的屏幕坐标调进来）。
        /// 只有手上真有东西才继续，落点判定全部交给 <see cref="TryDropHandItem"/>。
        /// </summary>
        public void NotifyEndDrag(Vector2 screenPosition)
        {
            if (!_hand.IsValid)
            {
                return;
            }

            TryDropHandItem(screenPosition);

            // 松手了，拖动中的试放预览一定要收掉：
            // 放下成功的分支里 _hand 已被清空，不走这一步绿框会一直留在格子上
            HidePreview();
        }

        /// <summary>
        /// 拖动中：按指针位置刷新试放预览（由 <see cref="BagItemBlock.OnDrag"/> 转发）。
        ///
        /// 这里刻意复用放下时那条穿透射线，而不是自己把屏幕坐标换算成格子：
        /// 预览画在哪个格子，松手就一定落在哪个格子。两套算法只要有一点出入
        /// （格子之间 3px 的缝、边界取整），就会出现"预览是绿的、松手却丢了"这种最坏的手感。
        /// </summary>
        public void NotifyDragMoved(Vector2 screenPosition)
        {
            if (!_hand.IsValid)
            {
                HidePreview();
                return;
            }

            // 命中装备槽 / 仓库槽时收掉格子预览 —— 那些槽位将来有自己的高亮表现，加在 else 里
            if (TryGetItemSlotUnderPointer(screenPosition, out IItemSlot slot) && slot is BagCellUI cell)
            {
                ShowPlacementPreview(cell.Cell);
            }
            else
            {
                HidePreview();
            }
        }

        /// <summary>
        /// 鼠标位置下第一个有效的 <see cref="IItemSlot"/>。
        ///
        /// 用 <see cref="EventSystem.RaycastAll"/>：它返回该点上"所有"命中的 UI（按前后排序），
        /// 正好是穿透式的；逐个往下找，第一个带 IItemSlot 的就是落点。
        /// </summary>
        private bool TryGetItemSlotUnderPointer(Vector2 screenPosition, out IItemSlot slot)
        {
            slot = null;

            // 这条链路本来就是 EventSystem 驱动起来的（OnDrag / OnEndDrag），走到这里它必然在
            EventSystem eventSystem = EventSystem.current;
            Assert.IsNotNull(eventSystem, "TryGetItemSlotUnderPointer：场景里没有 EventSystem，射线打不出来");

            if (_pointerRaycastData == null)
            {
                _pointerRaycastData = new PointerEventData(eventSystem);
            }

            _pointerRaycastData.position = screenPosition;
            _raycastResults.Clear();
            eventSystem.RaycastAll(_pointerRaycastData, _raycastResults);

            for (int i = 0; i < _raycastResults.Count; i++)
            {
                GameObject hit = _raycastResults[i].gameObject;
                if (hit == null)
                {
                    continue;
                }

                // 命中的可能是槽的子物体（预览层 / 图标），往上找到它所属的槽
                IItemSlot candidate = hit.GetComponentInParent<IItemSlot>();
                if (candidate != null)
                {
                    slot = candidate;
                    return true;
                }
            }

            return false;
        }
        
        /// <summary> 鼠标进入或离开某格，触发预览 </summary>
        private void HandleCellHovered(BagCellUI cell, bool entered)
        {
            if (!entered || !HasHandItem)
            {
                HidePreview();
                return;
            }

            // 鼠标咬住的就是抓取格，所以悬停的格子就是 origin
            ShowPlacementPreview(cell.Cell);
        }

        /// <summary>
        /// 按"抓取格落在 <paramref name="origin"/>"刷出试放预览：形状覆盖到的每个格子
        /// 统一染成绿（放得下）或红（放不下）。越界的格子不画 —— 形状的偏移允许是负数，
        /// 越界的那部分根本没有对应的 cell。
        /// </summary>
        private void ShowPlacementPreview(Vector2Int origin)
        {
            HidePreview();

            if (!_hand.IsValid || _bag == null)
            {
                return;
            }

            BagShape shape = _hand.Item.shape;
            if (shape == null)
            {
                return;
            }

            // ignore 传自己：和 TryPlace 的默认行为一致（TryPlace 内部也是 ignore = item），
            // 这样"预览是绿的，松手就一定放得下"才成立
            bool canPlace = _bag.CanPlace(_hand.Item, origin, _hand.Item);
            Color color = canPlace ? PreviewOkColor : PreviewBadColor;

            foreach (Vector2Int offset in shape.Offsets)
            {
                if (TryGetCell(origin + offset, out BagCellUI targetCell))
                {
                    targetCell.SetPreview(true, color);
                }
            }
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
                // 用掉了：后端已经把它从格子摘掉、本体也销毁了，表现块必须跟着回收。
                // 留着的话这张图会一直压在同一格上吃射线，底下的块再也点不到、拖不动
                DestroyBlockOf(item);
                return;
            }
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
