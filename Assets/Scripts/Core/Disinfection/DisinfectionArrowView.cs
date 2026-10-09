using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TapTapGameJam.UI;

namespace TapTapGameJam.Core.Disinfection
{
    /// <summary>
    /// Arrow view used by disinfection. It reuses the existing Arrow prefab and
    /// the same screen-edge geometry as the mini-game guidance system.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class DisinfectionArrowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _countdown;
        [SerializeField] private RectTransform _arrowImage;
        [SerializeField] private float _angleOffset = -90f;

        private DisinfectionRoom _room;
        private RectTransform _rect;
        private CanvasGroup _group;
        private Canvas _canvas;
        private bool _visible;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _canvas = GetComponentInParent<Canvas>();
            _title = _title != null ? _title : transform.Find("TaskName")?.GetComponent<TMP_Text>();
            _countdown = _countdown != null ? _countdown : transform.Find("TaskReward")?.GetComponent<TMP_Text>();
            _arrowImage = _arrowImage != null ? _arrowImage : transform.Find("ArrowImage") as RectTransform;

            _group.interactable = false;
            _group.blocksRaycasts = false;
            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            SetVisible(false);
        }

        public void Bind(DisinfectionRoom room, Color color)
        {
            _room = room;
            if (_title != null)
            {
                _title.color = color;
            }

            if (_countdown != null)
            {
                _countdown.color = color;
            }

            Image image = _arrowImage != null ? _arrowImage.GetComponent<Image>() : null;
            if (image != null)
            {
                image.color = color;
            }
        }

        public void UpdatePosition(RectTransform area, Camera camera, float padding, float seconds)
        {
            if (_room == null || area == null || camera == null)
            {
                SetVisible(false);
                return;
            }

            if (_title != null)
            {
                _title.text = $"消杀区域\n{_room.DisplayName}";
            }

            if (_countdown != null)
            {
                _countdown.text = $"剩余 {Mathf.Max(0, Mathf.CeilToInt(seconds))} 秒";
            }

            Vector3 world = _room.IndicatorTarget.position;
            Vector3 viewport = camera.WorldToViewportPoint(world);
            float inset = _visible ? 0.015f : 0f;
            if (viewport.z > 0f && viewport.x >= inset && viewport.x <= 1f - inset &&
                viewport.y >= inset && viewport.y <= 1f - inset)
            {
                SetVisible(false);
                return;
            }

            Vector2 direction;
            if (viewport.z > 0f)
            {
                Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? _canvas.worldCamera
                    : null;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area, camera.WorldToScreenPoint(world), uiCamera, out Vector2 projected))
                {
                    SetVisible(false);
                    return;
                }

                direction = projected - area.rect.center;
            }
            else
            {
                Vector3 local = camera.transform.InverseTransformPoint(world);
                direction = new Vector2(
                    local.x * camera.projectionMatrix.m00 * area.rect.width,
                    local.y * camera.projectionMatrix.m11 * area.rect.height);
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.down;
            }

            float halfWidth = area.rect.width * 0.5f - _rect.rect.width * 0.5f - padding;
            float halfHeight = area.rect.height * 0.5f - _rect.rect.height * 0.5f - padding;
            if (!TaskIndicatorGeometry.TryGetEdgePosition(
                direction.x, direction.y, halfWidth, halfHeight, out float x, out float y))
            {
                SetVisible(false);
                return;
            }

            _rect.anchoredPosition = new Vector2(x, y);
            if (_arrowImage != null)
            {
                _arrowImage.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + _angleOffset);
            }

            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_group != null)
            {
                _group.alpha = visible ? 1f : 0f;
            }
        }
    }
}
