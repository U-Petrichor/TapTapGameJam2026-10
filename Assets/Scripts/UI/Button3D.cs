using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 通用 3D 按钮：挂在一个带 Collider 的物体上，就能接收鼠标 hover / 按下 / 松开。
    ///
    /// 用的是新输入系统（UnityEngine.InputSystem），因为项目的 activeInputHandler = 1（只启用新输入），
    /// 旧版的 OnMouseDown / OnMouseEnter 在这套设置下不会触发。
    ///
    /// 用法：挂上组件 → 把要执行的方法拖到 Hover Enter / Hover Exit / Press / Release 上。
    /// 代码里也能直接加监听：
    ///     button.OnPress.AddListener(nest.CycleCommand);
    /// </summary>
    [DisallowMultipleComponent]
    public class Button3D : MonoBehaviour
    {
        public enum MouseButton
        {
            Left,
            Right,
            Middle
        }

        [Header("射线检测")]
        [Tooltip("检测用的相机。留空则用 Camera.main")]
        [SerializeField]
        private Camera _camera;

        [Tooltip("只检测这些层上的按钮。默认 Everything")]
        [SerializeField]
        private LayerMask _layerMask = ~0;

        [Tooltip("射线能打到的最远距离")]
        [SerializeField]
        private float _maxDistance = 500f;

        [Tooltip("鼠标指着 UI 时不触发。开着可以避免点界面时误触场景里的按钮")]
        [SerializeField]
        private bool _ignoreWhenPointerOverUI = true;

        [Tooltip("打开后每到 hover 变化 / 按下松开就输出一条日志，用来排查为什么没反应")]
        [SerializeField]
        private bool _debugLog = false;

        [Header("按键")]
        [SerializeField]
        private MouseButton _mouseButton = MouseButton.Left;

        [Header("回调")]
        [Tooltip("鼠标移上来时")]
        public UnityEvent OnHoverEnter;

        [Tooltip("鼠标移开时")]
        public UnityEvent OnHoverExit;

        [Tooltip("在本按钮上按下时")]
        public UnityEvent OnPress;

        [Tooltip("松开时（只要是在本按钮上按下的就会触发）")]
        public UnityEvent OnRelease;

        /// <summary>鼠标当前是否停在本按钮上</summary>
        public bool IsHovered { get; private set; }

        /// <summary>本按钮是否正被按住</summary>
        public bool IsPressed { get; private set; }

        private Camera Cam
        {
            get
            {
                if (_camera == null)
                {
                    _camera = Camera.main;
                }

                return _camera;
            }
        }

        private void OnDisable()
        {
            // 组件在 hover 状态下被关掉时补一个 Exit，避免对方的状态永远停在"被指着"
            if (IsHovered)
            {
                IsHovered = false;
                OnHoverExit?.Invoke();
            }

            IsPressed = false;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                // 没有鼠标设备（纯触屏 / 手柄）时什么都不做
                return;
            }

            Camera cam = Cam;

            bool overUI = _ignoreWhenPointerOverUI && IsPointerOverUI();
            bool isOver = !overUI && IsPointerOverThis(mouse.position.ReadValue(), cam);

            if (isOver != IsHovered)
            {
                IsHovered = isOver;
                LogHoverChanged(IsHovered);

                if (IsHovered)
                {
                    OnHoverEnter?.Invoke();
                }
                else
                {
                    OnHoverExit?.Invoke();
                }
            }

            if (!IsHovered)
            {
                LogNotHovered();
            }

            bool isDown = !overUI && GetButton(mouse).isPressed;

            if (isDown && !IsPressed && IsHovered)
            {
                IsPressed = true;
                OnPress?.Invoke();
                LogPressed();
            }
            else if (!isDown && IsPressed)
            {
                IsPressed = false;
                OnRelease?.Invoke();
            }
        }

        /// <summary>
        /// 内部自检：当按钮没反应时，先看被动检测（别人打的射线命中了谁），
        /// 再看主动检测（自己打出去的射线命中了谁），两者不一致就说明有东西挡在前面。
        /// </summary>
        private void LogNotHovered()
        {
            if (!_debugLog || Time.frameCount % 30 != 0)
            {
                return;
            }

            // 被动：别人打在同一个位置上的射线命中了什么
            EventSystem eventSystem = EventSystem.current;
            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = Mouse.current.position.ReadValue()
            };
            System.Collections.Generic.List<RaycastResult> uiHits =
                new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, uiHits);

            string uiName = uiHits.Count > 0
                ? $"{uiHits[0].gameObject.name}(depth {uiHits[0].depth})"
                : "无";

            // 主动：Physics 射线把 UI 挡在后面的东西也照出来
            Ray ray = Cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            string physicsName = Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _layerMask)
                ? $"{hit.transform.name} (layer {hit.transform.gameObject.layer})"
                : "无";

            Debug.Log($"[Button3D:{name}] 没被指到 | 鼠标={Mouse.current.position.ReadValue()} " +
                      $"相机={(Cam != null ? Cam.name : "null")} | UI命中={uiName} | Physics命中={physicsName}");
        }

        private void LogPressed()
        {
            if (_debugLog)
            {
                Debug.Log($"[Button3D:{name}] 按下，触发 OnPress");
            }
        }

        private void LogHoverChanged(bool entered)
        {
            if (_debugLog)
            {
                Debug.Log($"[Button3D:{name}] hover {(entered ? "进入" : "离开")}");
            }
        }

        /// <summary>
        /// 屏幕点是否落在本按钮上。公开出来是为了让别的系统能复用同一套判定
        /// （例如巢穴要区分"点到了哪个巢穴"）。
        /// </summary>
        public bool IsPointerOverThis(Vector2 screenPosition, Camera cam)
        {
            if (cam == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _layerMask))
            {
                return false;
            }

            // 命中的可能是子物体上的 Collider，所以要把整棵层级都算进来：
            // 本组件挂在被命中的物体上、或它的任意祖先/后代上，都算点到了本按钮
            Transform hitTransform = hit.transform;
            return hitTransform == transform
                   || hitTransform.IsChildOf(transform)
                   || transform.IsChildOf(hitTransform);
        }

        /// <summary>
        /// 鼠标指向的世界坐标，没指到任何东西时返回 false。
        /// 给需要知道"点在场景哪儿"的逻辑用。
        /// </summary>
        public bool TryGetPointerWorld(out Vector3 worldPoint)
        {
            Mouse mouse = Mouse.current;
            Camera cam = Cam;

            worldPoint = default;

            if (mouse == null || cam == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _layerMask))
            {
                worldPoint = hit.point;
                return true;
            }

            return false;
        }

        private bool IsPointerOverUI()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        /// <summary>
        /// 取本组件配置的鼠标键对应的按钮控件
        /// </summary>
        private ButtonControl GetButton(Mouse mouse)
        {
            switch (_mouseButton)
            {
                case MouseButton.Right:
                    return mouse.rightButton;
                case MouseButton.Middle:
                    return mouse.middleButton;
                default:
                    return mouse.leftButton;
            }
        }
    }
}
