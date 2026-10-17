using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(Image))]
    public class SortFileCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private TMP_Text _departmentText;
        private SortFileGame _owner;
        private RectTransform _rect;
        private CanvasGroup _group;
        private RectTransform _homeParent;
        private Vector2 _homePosition;
        private Vector3 _homeScale;
        private Quaternion _homeRotation;
        private Vector2 _grabOffset;
        private bool _dragging;
        private int _pointerId;

        public int Index { get; private set; }
        public RectTransform Rect => _rect;
        public Vector3 HomeWorldPosition => _homeParent.TransformPoint(_homeParent.rect.center + _homePosition);

        public void Initialize(SortFileGame owner, int index, SortFileDepartment department, Color color, Sprite artwork = null)
        {
            _owner = owner;
            Index = index;
            _rect = GetComponent<RectTransform>();
            _group = GetComponent<CanvasGroup>();
            _homeParent = (RectTransform)_rect.parent;
            _homePosition = _rect.anchoredPosition;
            _homeScale = _rect.localScale;
            _homeRotation = _rect.localRotation;
            if (_departmentText == null) _departmentText = GetComponentInChildren<TMP_Text>(true);
            if (_departmentText != null)
            {
                _departmentText.text = SortFileRound.DepartmentName(department) + "文件";
                _departmentText.raycastTarget = false;
                // 文件素材已自带部门文字，避免与旧标签重叠。
                _departmentText.gameObject.SetActive(artwork == null);
            }
            var image = GetComponent<Image>();
            image.color = artwork != null ? Color.white : color;
            if (artwork != null)
            {
                image.sprite = artwork;
                image.preserveAspect = true;
            }
            SetDraggable(false);
        }

        public void SetDraggable(bool enabled)
        {
            if (_group == null) return;
            _group.interactable = enabled;
            _group.blocksRaycasts = enabled;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_owner == null || _dragging || eventData.button != PointerEventData.InputButton.Left) return;
            if (!_owner.TryBeginDrag(this)) return;
            _rect.SetParent(_owner.DragRoot, true);
            _rect.SetAsLastSibling();
            if (!GetPointer(eventData, out Vector2 pointer))
            {
                _owner.AbortDrag();
                return;
            }
            _grabOffset = (Vector2)_rect.localPosition - pointer;
            _pointerId = eventData.pointerId;
            _dragging = true;
            SetDraggable(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData.pointerId != _pointerId || _owner == null) return;
            if (!GetPointer(eventData, out Vector2 pointer)) return;
            Vector2 position = pointer + _grabOffset;
            Rect bounds = _owner.DragRoot.rect;
            position.x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
            position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
            _rect.localPosition = new Vector3(position.x, position.y, _rect.localPosition.z);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData.pointerId != _pointerId) return;
            _dragging = false;
            _owner?.Drop(this, eventData.pressEventCamera);
        }

        private bool GetPointer(PointerEventData eventData, out Vector2 pointer) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _owner.DragRoot, eventData.position, eventData.pressEventCamera, out pointer);

        public void RestoreHome()
        {
            _dragging = false;
            _rect.SetParent(_homeParent, false);
            _rect.anchoredPosition = _homePosition;
            _rect.localScale = _homeScale;
            _rect.localRotation = _homeRotation;
            _rect.SetAsLastSibling();
        }

        public void PlaceIn(RectTransform area, Vector2 position)
        {
            _dragging = false;
            _rect.SetParent(area, false);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = position;
            _rect.localScale = _homeScale * 0.8f;
            _rect.localRotation = Quaternion.identity;
            _rect.SetAsLastSibling();
            SetDraggable(false);
        }
    }
}
