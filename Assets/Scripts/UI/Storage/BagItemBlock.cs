using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Items;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 一件物品的"表现块"，同时也是它的拖拽手柄。
    ///
    /// 一个 BagItemBlock 就是一件物品的完整外观：空物体下挂 N 个 CellFrame（按 BagShape 的
    /// "未旋转"偏移排好的纯色格子）和一张物品图；朝向由父节点整体旋转表达。它由
    /// <see cref="UIManager"/> 统一生成/销毁，平时挂在 UIManager 的拖动层下。
    ///
    /// 【拖拽流程 —— 落点由块自己判定】
    /// 1. 按下：<see cref="IItemSlot.OnPickUpItem"/> 告诉来源槽位"我走了"，然后把自己挂到拖动层；
    /// 2. 拖动中：每帧从鼠标位置打一条**穿透**射线，取第一个 <see cref="IItemSlot"/>，
    ///    调它的 <see cref="IItemSlot.TryDropItemBlock"/> 出预览。"命中的槽位 + 当前朝向"都没变
    ///    就不重算 —— 每次重算都会弄脏 Canvas；
    /// 3. 松手：射线找槽位 → <see cref="IItemSlot.DropItemBlock"/>；打到了但放不下，回退
    ///    <see cref="CurrentSlot"/>；什么都没打到、或连原处都回不去 → 送回世界并由 UIManager 销毁这一块。
    ///
    /// 块不碰任何后端数据，也不认识具体容器 —— 它只跟 IItemSlot 和 UIManager 打交道。
    /// </summary>
    [DisallowMultipleComponent]
    public class BagItemBlock : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>这个块代表哪件物品</summary>
        public PickableItem boundedItem;

        /// <summary>物品当前在哪个槽位中（拖拽时，这是它的来源，放不下要回这里）</summary>
        public IItemSlot CurrentSlot;

        /// <summary>正在被拖着</summary>
        public bool IsDragging { get; private set; }

        /// <summary>格子框那一层。仓库要整层隐藏，只留物品图</summary>
        public RectTransform CellFrames { get; private set; }

        /// <summary>物品图那一层的 RectTransform（没配图时是占位色块）。它的 rect 就是占格 bbox 的尺寸</summary>
        public RectTransform Icon { get; private set; }

        /// <summary>由 <see cref="UIManager"/> 造块时注入两层物料</summary>
        public void AttachVisuals(RectTransform cellFrames, RectTransform icon)
        {
            Assert.IsTrue(cellFrames != null && icon != null, "BagItemBlock.AttachVisuals：传进来的层是 null");
            CellFrames = cellFrames;
            Icon = icon;
        }

        /// <summary>格子框的显隐（背包要，仓库只看图不要）</summary>
        public void SetCellFramesVisible(bool visible)
        {
            Assert.IsTrue(CellFrames != null, "BagItemBlock：格子框层还没注入");
            CellFrames.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 恢复成"背包那套表现"：显示格子框、缩放归 1、朝向跟物品形状一致。
        /// 从仓库（隐藏格子框 + 缩放过）转到背包 / 装备槽 / 待拾取位之前必须来这一下。
        /// </summary>
        public void ApplyBagPresentation()
        {
            SetCellFramesVisible(true);
            transform.localScale = Vector3.one;
            SyncRotationWithItem();
        }

        /// <summary>上一次命中的槽位。换目标 / 松手时要靠它把预览收掉</summary>
        private IItemSlot _previewSlot;

        /// <summary>上一次算预览时的朝向。用来判断"目标没变、朝向也没变"</summary>
        private int _previewRotation = -1;

        private Vector2 _lastPointerPosition;
        private PointerEventData _pointerData;

        private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>();

        private int RotationSteps => boundedItem != null && boundedItem.shape != null
            ? boundedItem.shape.RotationSteps
            : 0;

        #region 拖拽

        public void OnBeginDrag(PointerEventData eventData)
        {
            Assert.IsTrue(boundedItem != null, "BagItemBlock：boundedItem 是 null");
            Assert.IsTrue(CurrentSlot != null,
                "BagItemBlock：块没有 CurrentSlot —— 容器创建块后必须把它登记到槽位上");

            // 先问来源槽位放不放人：返回 false 就当场放弃这次拖拽（IsDragging 保持 false，
            // 后面的 OnDrag / OnEndDrag 都会直接 return，块一步都不动）
            if (!CurrentSlot.OnPickUpItem(this))
            {
                return;
            }

            UIManager.Instance.NotifyBeginDrag(this);
            transform.SetParent(UIManager.Instance.DragLayer, true);

            IsDragging = true;
            SnapCenterToPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging)
            {
                return;
            }

            SnapCenterToPointer(eventData);
            RefreshPreview(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging)
            {
                return;
            }

            IsDragging = false;
            ClearPreview();
            UIManager.Instance.NotifyEndDrag(this);

            IItemSlot target = RaycastSlot(eventData.position);

            if (target == null)
            {
                // 没打到任何槽位（面板外，或者那个底盘没设成槽位）= 丢到世界
                UIManager.Instance.DestroyBlock(this);
                return;
            }

            if (target.DropItemBlock(this))
            {
                return;
            }

            // 打到了但放不下：退回原来的槽位
            if (CurrentSlot != null && CurrentSlot.DropItemBlock(this))
            {
                return;
            }

            // 连原处都回不去：丢回世界（已经丢回，这里只需要destroy block）
            UIManager.Instance.DestroyBlock(this);
        }

        /// <summary>R 键：把形状顺时针转 90°，块整体跟着转。由 UIManager 转发给正在拖的那一块</summary>
        public void RotateItem()
        {
            Assert.IsTrue(IsDragging, "RotateItem：只允许旋转正在拖拽的块");
            Assert.IsTrue(boundedItem.shape != null, $"RotateItem：{boundedItem.name} 没配 shape");

            boundedItem.shape.RotateClockwise();
            SyncRotationWithItem();

            // 形状变了，预览必须重算；鼠标不动也要立刻算，所以用上一次的指针位置
            _previewRotation = -1;
            RefreshPreview(_lastPointerPosition);
        }

        /// <summary>把块的中心对齐到鼠标指针（pivot 已经是中心，所以直接设 localPosition 即可）</summary>
        private void SnapCenterToPointer(PointerEventData eventData)
        {
            RectTransform parentRect = transform.parent as RectTransform;
            Assert.IsTrue(parentRect != null,
                "BagItemBlock：块的父节点不是 RectTransform（应当在 UIManager 的拖动层下）");

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                transform.localPosition = local;
            }
        }

        public void SyncRotationWithItem()
        {
            transform.localEulerAngles = new Vector3(0, 0, -90 * RotationSteps);
        }

        #endregion

        #region 预览 / 射线

        /// <summary>
        /// 刷新试放预览。只在"命中的槽位变了"或"朝向变了"时才动 UI：
        /// 预览是把格子上的 Image 打开/改色，逐帧重刷会把整块面板标记重建。
        /// </summary>
        private void RefreshPreview(Vector2 screenPosition)
        {
            _lastPointerPosition = screenPosition;

            IItemSlot slot = RaycastSlot(screenPosition);
            if (slot == _previewSlot && _previewRotation == RotationSteps)
            {
                return;
            }

            _previewSlot?.ClearDropPreview();

            _previewSlot = slot;
            _previewRotation = RotationSteps;

            _previewSlot?.TryDropItemBlock(this);
        }

        /// <summary>把预览整个收掉（松手时）</summary>
        private void ClearPreview()
        {
            _previewSlot?.ClearDropPreview();
            _previewSlot = null;
            _previewRotation = -1;
        }

        /// <summary>
        /// 鼠标位置下第一个有效的 IItemSlot。用 EventSystem.RaycastAll：它返回该点上**所有**命中的 UI
        /// （按前后排序），所以能穿过压在最上面的自己（物品图）、格框、面板底图，一路找到槽位。
        /// </summary>
        private IItemSlot RaycastSlot(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            Assert.IsNotNull(eventSystem, "BagItemBlock：场景里没有 EventSystem，射线打不出来");

            if (_pointerData == null)
            {
                _pointerData = new PointerEventData(eventSystem);
            }

            _pointerData.position = screenPosition;
            RaycastResults.Clear();
            eventSystem.RaycastAll(_pointerData, RaycastResults);

            for (int i = 0; i < RaycastResults.Count; i++)
            {
                GameObject hit = RaycastResults[i].gameObject;
                if (hit == null)
                {
                    continue;
                }

                // 命中的可能是槽的子物体（预览层 / 图标），往上找到它所属的槽。
                // 块自己身上没有 IItemSlot，会被自然跳过。
                IItemSlot slot = hit.GetComponentInParent<IItemSlot>();
                if (slot != null)
                {
                    return slot;
                }
            }

            return null;
        }

        #endregion
    }
}
