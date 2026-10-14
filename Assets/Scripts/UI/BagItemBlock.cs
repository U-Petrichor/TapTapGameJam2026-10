using System;
using TapTapGameJam.Items;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 背包里一件物品的"表现块"，同时也是它的拖拽手柄。
    ///
    /// 一个 BagItemBlock 就是一件物品的完整外观：空物体下挂 N 个 CellFrame（按 BagShape 当前朝向的
    /// 偏移排好的纯色格子）和一张物品图。它是整体旋转、整体搬运的。
    ///
    /// 【拖拽流程】
    /// 1. 在块上按下鼠标（射线要打得中，所以物品图那层开了 Raycast Target）→ 进入拖拽，
    ///    同时通知面板 <see cref="BagPanelUI.BeginBlockDrag"/>：
    ///    面板会把上一件手持物安顿好，并记录这一件是从哪来的（未拾取 / 背包格子 / 装备槽）；
    /// 2. 拖拽过程中块的中心跟着鼠标，并通知面板刷新试放预览（绿/红）；
    /// 3. 松开鼠标 → 通知面板 <see cref="BagPanelUI.EndBlockDrag"/>，由面板决定：
    ///    穿到槽位上 / 放进背包 / 回原位 / 丢到世界。
    ///
    /// 块自己不碰后端数据，也不决定归属 —— 它只是个"手柄 + 外观"。
    /// </summary>
    [DisallowMultipleComponent]
    public class BagItemBlock : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>这个块代表哪件物品</summary>
        public PickableItem boundedItem;

        /// <summary>面板。由生成块的 BagPanelUI 注入</summary>
        public BagPanelUI Panel { get; set; }

        /// <summary>正在被拖着</summary>
        public bool IsDragging { get; private set; }
        
        #region 悬停

        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log("OnPointerEnter");
            if (!IsDragging)
            {
                Panel?.NotifyBlockHovered(this, true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!IsDragging)
            {
                Panel?.NotifyBlockHovered(this, false);
            }
        }

        #endregion

        #region 拖拽

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Panel == null || boundedItem == null)
            {
                return;
            }

            Panel.BeginBlockDrag(this);

            IsDragging = true;

            // 按下这一瞬间就把块的中心挪到鼠标处。
            // 本来块还在原来的格子上，这里会看到一下"跳"，是故意的：
            // 之后整个拖拽过程鼠标都咬在块的抓取格中心上，位置关系不会再变。
            SnapCenterToPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging)
            {
                return;
            }

            SnapCenterToPointer(eventData);

            // 通知面板：鼠标在哪一格、能不能放。面板负责把预览刷成绿/红
            Panel?.NotifyBlockDragging(this, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging)
            {
                return;
            }

            IsDragging = false;
            Panel?.OnBlockDragFinished(this);
            Panel?.EndBlockDrag(this, eventData.position);
        }

        /// <summary>
        /// 兜底结束拖拽。用在"左键已经松开，但 IsDragging 还挂着"的场合（松开事件没收到）：
        /// 不复位的话这个块会永远不再响应 OnPointerEnter，表现就是"图看得见但点不到"。
        /// </summary>
        public void ForceEndDrag(Vector2 screenPosition)
        {
            if (!IsDragging)
            {
                return;
            }

            IsDragging = false;
            Panel?.OnBlockDragFinished(this);
            Panel?.EndBlockDrag(this, screenPosition);
        }

        /// <summary>把块的中心对齐到鼠标指针（pivot 已经是中心，所以直接设 localPosition 即可）</summary>
        private void SnapCenterToPointer(PointerEventData eventData)
        {
            RectTransform parentRect = transform.parent as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                transform.localPosition = local;
            }
        }

        public void SyncRotationWithItem()
        {
            transform.localEulerAngles = new Vector3(0, 0, -90 * boundedItem.shape.RotationSteps);
        }

        #endregion
    }
}
