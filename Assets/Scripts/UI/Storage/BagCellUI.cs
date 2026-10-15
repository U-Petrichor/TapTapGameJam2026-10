using TapTapGameJam.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 背包里的一个格子。只负责"显示自己处于什么状态"和"把指针事件转发给 BagPanelUI"。
    ///
    /// 格子本身不画物品 —— 物品由 <see cref="TapTapGameJam.Items.BagItemBlock"/> 整体表现，
    /// 这里只有底色和试放预览两层：Background → Preview（raycastTarget=false）。
    /// </summary>
    [DisallowMultipleComponent]
    public class BagCellUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IItemSlot
    {
        public Vector2Int Cell { get; private set; }

        private Image _background;
        private Image _preview;

        private Color _baseColor = Color.white;
        private Color _previewColor;
        private bool _hasPreview;
        private bool _isHovered;

        public BagPanelUI bagPanelUI;

        public void Initialize(Vector2Int cell, Image background, Image preview, Color baseColor)
        {
            Cell = cell;
            _background = background;
            _preview = preview;
            _baseColor = baseColor;
            _hasPreview = false;
            _previewColor = Color.clear;
            Apply();
            ApplyPreview();
        }

        /// <summary>设置格子底色的基准色（空 / 已占）。值没变就不碰 UI，避免每帧触发 Canvas 重建</summary>
        public void SetBaseColor(Color color)
        {
            if (color == _baseColor)
            {
                return;
            }

            _baseColor = color;
            Apply();
        }

        /// <summary>设置/清除试放预览颜色（半透明叠色）</summary>
        public void SetPreview(bool active, Color color)
        {
            if (_hasPreview == active && (!active || _previewColor == color))
            {
                return;
            }

            _hasPreview = active;
            _previewColor = color;
            ApplyPreview();
        }

        /// <summary>把缓存里的预览状态写进 Image。缓存是唯一真相，Preview 只在这里被写</summary>
        private void ApplyPreview()
        {
            if (_preview == null)
            {
                return;
            }

            _preview.enabled = _hasPreview;
            _preview.color = _previewColor;
        }

        /// <summary>清空所有预览（每次重算前调用）</summary>
        public void ClearPreview()
        {
            SetPreview(false, Color.clear);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            Apply();
            bagPanelUI.NotifyCellHovered(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            Apply();
            bagPanelUI.NotifyCellHovered(this, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            bagPanelUI.NotifyCellClicked(this, eventData.button);
        }

        private void Apply()
        {
            if (_background == null)
            {
                return;
            }

            // 悬停时把底色提亮一点，作为"鼠标在哪一格"的反馈。
            // 用 Lerp 而不是直接乘：乘会把 alpha 也一起放大，半透明底会变浑。
            _background.color = _isHovered ? Color.Lerp(_baseColor, Color.white, 0.3f) : _baseColor;
        }
    }
}
