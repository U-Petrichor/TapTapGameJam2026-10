using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 背包里的一个格子。只负责"显示自己处于什么状态"和"把指针事件转发给 BagPanelUI"，
    /// 摆放规则、预览合法性都由 BagPanelUI 问 PlayerBag 决定。
    ///
    /// 层级：Background（可着色）→ Marker（物品图标）→ Preview（预览叠色，raycastTarget=false）
    /// </summary>
    [DisallowMultipleComponent]
    public class BagCellUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Vector2Int Cell { get; private set; }

        private Image _background;
        private Image _marker;
        private Image _preview;

        private Color _baseColor = Color.white;
        private Color _previewColor;
        private Sprite _markerSprite;
        private Color _markerColor;
        private bool _hasPreview;
        private bool _hasMarker;
        private bool _isHovered;

        public void Initialize(Vector2Int cell, Image background, Image marker, Image preview, Color baseColor)
        {
            Cell = cell;
            _background = background;
            _marker = marker;
            _preview = preview;
            _baseColor = baseColor;
            Apply();
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

        /// <summary>设置/清除物品图标。值没变就不碰 UI</summary>
        public void SetMarker(Sprite sprite, Color color)
        {
            // 注意：sprite 为 null 时也要按 _hasMarker 判断，不能只看 sprite 引用
            bool hasMarker = sprite != null;
            if (hasMarker == _hasMarker && (!hasMarker || (_markerSprite == sprite && _markerColor == color)))
            {
                return;
            }

            _hasMarker = hasMarker;
            _markerSprite = sprite;
            _markerColor = color;

            if (_marker != null)
            {
                _marker.sprite = sprite;
                _marker.color = color;
                _marker.enabled = hasMarker;
            }

            Apply();
        }

        /// <summary>设置/清除试放预览颜色（半透明叠色）</summary>
        public void SetPreview(bool active, Color color)
        {
            _hasPreview = active;
            _previewColor = color;

            if (_preview != null)
            {
                _preview.enabled = active;
                _preview.color = color;
            }

            Apply();
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
            BagPanelUI.NotifyCellHovered(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            Apply();
            BagPanelUI.NotifyCellHovered(this, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            BagPanelUI.NotifyCellClicked(this, eventData.button);
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
