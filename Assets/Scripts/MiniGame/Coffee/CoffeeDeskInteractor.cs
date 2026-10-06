using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TapTapGameJam.Coffee
{
    // 一张桌子一个交互器，负责点击、按住和拖拽三种输入。
    public class CoffeeDeskInteractor : MonoBehaviour
    {
        [SerializeField] private CoffeeMinigameManager _controller;
        [SerializeField] private LayerMask _pickMask = ~0;
        [SerializeField, Min(0.1f)] private float _maxPickDistance = 20f;

        private CoffeeDraggable _draggedItem;
        private CoffeeClickable _heldTarget;
        private Plane _dragPlane;
        private Vector3 _grabOffset;
        private bool _acceptInput;

        public void SetInputEnabled(bool value)
        {
            _acceptInput = value;
            if (!value) CancelInteraction();
        }

        private bool IsOverUI() => EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();

        private void Update()
        {
            if (!_acceptInput || _controller == null || !_controller.CanOperate ||
                _controller.PointerCamera == null || Mouse.current == null)
            {
                CancelInteraction();
                return;
            }

            Ray ray = _controller.PointerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            bool overUI = IsOverUI();

            if (Mouse.current.leftButton.wasPressedThisFrame && !overUI)
                TryBeginInteraction(ray);

            if (_heldTarget != null)
            {
                bool holding = Mouse.current.leftButton.isPressed && !overUI &&
                    Physics.Raycast(ray, out RaycastHit holdHit, _maxPickDistance,
                        _pickMask, QueryTriggerInteraction.Ignore) &&
                    holdHit.collider.GetComponentInParent<CoffeeClickable>() == _heldTarget;
                _controller.Hold(_heldTarget, holding ? Time.deltaTime : 0f);
                if (!Mouse.current.leftButton.isPressed) _heldTarget = null;
            }

            if (_draggedItem == null) return;

            if (_dragPlane.Raycast(ray, out float distance))
                _draggedItem.MoveTo(ray.GetPoint(distance) + _grabOffset);

            // 水壶在滤杯区域内持续按住时注水；移开、松手、移到 UI 上都会停止。
            _controller.UpdateDraggedItem(_draggedItem,
                Mouse.current.leftButton.isPressed && !overUI ? Time.deltaTime : 0f);
            if (_draggedItem == null) return; // 注水完成可能取消当前拖拽。

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                // 先清除手里的引用，再通知流程；流程可能立刻切状态或自动退出。
                CoffeeDraggable released = _draggedItem;
                _draggedItem = null;
                bool accepted = !overUI && _controller.TryDrop(released);
                if (!accepted) released.ResetItem();
            }
            else if (!Mouse.current.leftButton.isPressed)
                CancelInteraction();
        }

        private void TryBeginInteraction(Ray ray)
        {
            if (_draggedItem != null || _heldTarget != null) return;
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxPickDistance,
                    _pickMask, QueryTriggerInteraction.Ignore)) return;

            CoffeeClickable clickable = hit.collider.GetComponentInParent<CoffeeClickable>();
            if (clickable != null)
            {
                if (_controller.TryClick(clickable)) return;
                if (_controller.CanHold(clickable))
                {
                    _heldTarget = clickable;
                    return;
                }
            }

            CoffeeDraggable item = hit.collider.GetComponentInParent<CoffeeDraggable>();
            if (!_controller.CanDrag(item)) return;

            _dragPlane = new Plane(Vector3.up, item.transform.position);
            if (!_dragPlane.Raycast(ray, out float distance)) return;
            _draggedItem = item;
            _grabOffset = item.transform.position - ray.GetPoint(distance);
        }

        public void CancelInteraction()
        {
            if (_draggedItem != null) _draggedItem.ResetItem();
            _draggedItem = null;
            _heldTarget = null;
            if (_controller != null) _controller.StopInteractionEffects();
        }

        private void OnDisable() => CancelInteraction();
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelInteraction();
        }
    }
}
