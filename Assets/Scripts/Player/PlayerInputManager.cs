using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.UI;
using System;

namespace TapTapGameJam.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerInputManager : MonoBehaviour
    {
        [Tooltip("放入Unity自带的PlayerInput系统，用于InputActionMap之间切换，防止不同场景的按键冲突")]
        [SerializeField] private PlayerInput _playerInput;

        private PlayerBasic _player;
        private bool _isESCMenuOpen = false; // 当前是否打开了ESC菜单
        private Rigidbody rb;
        public Vector2 MoveInput; // 玩家输入的移动方向,相较于屏幕上下左右的方向
        public bool WorldInputBlocked { get; set; }
        public bool HasMoveInput => !WorldInputBlocked && MoveInput.sqrMagnitude > 0.0001f;

        /// <summary>交互键按下，参数是这一次按的是哪个键（E / F）</summary>
        public event Action<InteractKey> EventOnInteractPress;
        public event Action EventOnInteractionSwitchPress; // 按 Q 切换到下一个交互目标
        public event Action<int> EventOnCameraScroll; // 传入滚轮方向，1为远离，-1为靠近
        public event Action<int> EventOnCameraRotate; // [ 为 +1，] 为 -1，每次旋转 45 度
        public event Action EventOnRotateItemPress;

        private InputAction _rotateCameraLeft;
        private InputAction _rotateCameraRight;
        private InputAction _sprintAction;

        /// <summary>冲刺键（Shift）按下/松开，参数 true = 按下</summary>
        public event Action<bool> EventOnSprintChanged;

        private void OnEnable()
        {
            EventPipeline.Subscribe<EventCloseMiniGamePanel>(OnCloseMiniGame);
        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventCloseMiniGamePanel>(OnCloseMiniGame);
        }

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            _player = GetComponent<PlayerBasic>();
            // Start 时 PlayerInput 已完成初始化，使用它为当前玩家创建的 actions。
            if (_playerInput != null && _playerInput.actions != null)
            {
                // 通过固定 ID 查找 CameraClockwise / CameraAntiClockwise，重命名动作不影响连接。
                _rotateCameraLeft = _playerInput.actions.FindAction("9f97b44a-5cb2-4719-ba84-723d489da067");
                _rotateCameraRight = _playerInput.actions.FindAction("fcb1663d-5bba-4b07-8639-fe019e2a7f76");
                // 冲刺：按名字找。找不到说明 .inputactions 里没这个 action，ReadSprintHeld 会返回 null
                _sprintAction = _playerInput.actions.FindAction("Sprint");
            }
        }

        /// <summary>
        /// 冲刺键当前是否按住。
        /// 返回 null 表示"这个 action 不存在"（或者还没初始化），调用方应该退回直接读键盘；
        /// 返回 true/false 表示以 action 的状态为准，即使玩家没在 ActionEvents 里挂回调也能读。
        /// </summary>
        public bool? ReadSprintHeld()
        {
            if (WorldInputBlocked) return false;
            if (_sprintAction == null)
            {
                return null;
            }

            return _sprintAction.IsPressed();
        }

        /// <summary>
        /// UI 面板（背包 / 仓库 / 待拾取位…）统一用的 ActionMap：
        /// 只要有一个受监控的容器开着就用它，全关了才回 <see cref="DefaultActionMapName"/>。
        /// 切换由 <see cref="TapTapGameJam.UI.UIManager"/> 负责，这里只提供名字。
        /// </summary>
        public const string UIActionMapName = "Backpack";

        /// <summary>没有 UI 面板开着时的默认（走位 / 交互）ActionMap</summary>
        public const string DefaultActionMapName = "DayPlayer";

        private void Update()
        {
            if (WorldInputBlocked) return;
            // 跟随 ActionMap 的启停，打开背包或 ESC 菜单时不响应相机旋转。
            if (_rotateCameraLeft != null && _rotateCameraLeft.enabled
                && _playerInput.currentActionMap == _rotateCameraLeft.actionMap
                && _rotateCameraLeft.WasPerformedThisFrame())
                EventOnCameraRotate?.Invoke(1);

            if (_rotateCameraRight != null && _rotateCameraRight.enabled
                && _playerInput.currentActionMap == _rotateCameraRight.actionMap
                && _rotateCameraRight.WasPerformedThisFrame())
                EventOnCameraRotate?.Invoke(-1);
        }
        #region 打开和关闭MiniGame的时候切换ActionMap
        private void OnCloseMiniGame(EventCloseMiniGamePanel evt)
        {
            // TODO: 这里和 ESC 一样是硬编码切回游戏：如果那时还有受监控的容器开着，
            // 应该改成让 UIManager 重算（UIManager.Instance.RefreshActionMap()）
            SwitchActionMap(DefaultActionMapName);
        }
        #endregion
        #region 玩家移动

        /// <summary>
        /// 根据玩家的输入，更新InputVector
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            if (WorldInputBlocked) { MoveInput = Vector2.zero; return; }
            MoveInput = context.canceled
                ? Vector2.zero
                : Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f);
        }
        #endregion

        #region 玩家交互
        /// <summary>
        /// E / F 都绑在 Interact 这一个 action 上，这里把本次按下的是哪个键一起广播出去。
        /// </summary>
        public void OnInteract(InputAction.CallbackContext context)
        {
            if (WorldInputBlocked) return;
            if (!context.performed)
            {
                return;
            }
            EventOnInteractPress?.Invoke(ResolveInteractKey(context));
        }

        /// <summary>
        /// 反查本次触发用的是哪个键：一个 action 挂多个绑定时，context.control 就是真正被按下的那个控件。
        /// 认不出来的（例如以后接手柄）返回 None，交给 Interactable 走默认逻辑。
        /// </summary>
        private static InteractKey ResolveInteractKey(InputAction.CallbackContext context)
        {
            if (context.control is KeyControl keyControl)
            {
                switch (keyControl.keyCode)
                {
                    case Key.E:
                        return InteractKey.E;
                    case Key.F:
                        return InteractKey.F;
                }
            }

            return InteractKey.None;
        }
        public void OnMouseScroll(InputAction.CallbackContext context)
        {
            if (WorldInputBlocked) return;
            if (!context.performed)
            {
                return;
            }
            float scrollValue = context.ReadValue<float>();

            if (scrollValue < 0f)
            {
                EventOnCameraScroll?.Invoke(1);
            }
            else if (scrollValue > 0f)
            {
                EventOnCameraScroll?.Invoke(-1);
            }
        }
        public void OnSwitchInteraction(InputAction.CallbackContext context)
        {
            if (WorldInputBlocked) return;
            if (!context.performed) return;

            EventOnInteractionSwitchPress?.Invoke();
        }

        /// <summary>
        /// 冲刺键。需要在 Player 预制体的 PlayerInput → ActionEvents 里把 Sprint 挂到这个方法上；
        /// 没挂也不影响，PlayerHealth 会退回直接读键盘。
        /// </summary>
        public void OnSprint(InputAction.CallbackContext context)
        {
            if (WorldInputBlocked) return;
            if (context.performed)
            {
                EventOnSprintChanged?.Invoke(true);
            }
            else if (context.canceled)
            {
                EventOnSprintChanged?.Invoke(false);
            }
        }
        #endregion
        #region 控制背包
        /// <summary>
        /// 按 B。这里**只发事件**：到底是开还是关由容器自己判断（持有"当前开/关"的是容器），
        /// ActionMap 由 UIManager 按"有没有受监控容器开着"统一处理。
        /// 注意：Player 预制体里 `Player/Backpack[/Keyboard/b]` 绑定的就是这个名字，
        /// 不要随手改名，否则预制体上的 UnityEvent 会变成 Missing。
        /// </summary>
        public void OnToggleBackpack(InputAction.CallbackContext context)
        {
            if (WorldInputBlocked) return;
            if (!context.performed)
            {
                return;
            }

            EventPipeline.Publish(new EventToggleBackpack());
        }

        public void OnRotateItem(InputAction.CallbackContext context)
        {
            if (WorldInputBlocked) return;
            if (!context.performed)
            {
                return;
            }

            EventOnRotateItemPress?.Invoke();
        }

        /// <summary>
        /// 请求打开背包：只发事件（UI 与暂停都靠这个事件驱动）。
        /// 重复调用是安全的 —— 容器自己用 IsOpen 去重，ActionMap 由 UIManager 统一处理。
        /// </summary>
        public void OpenBackpack()
        {
            if (WorldInputBlocked) return;
            EventPipeline.Publish(new BackpackEvent(true));
        }

        /// <summary>请求关闭背包：只发事件</summary>
        public void CloseBackpack()
        {
            EventPipeline.Publish(new BackpackEvent(false));
        }

        /// <summary>
        /// Backpack ActionMap 下的 ESC：退出所有 UI —— 直接找 UIManager 关掉全部受监控容器
        /// （容器的开关会自动把 ActionMap 和暂停收回到 DayPlayer）。
        /// </summary>
        public void OnCloseBackpack(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            UIManager.Instance.CloseAllContainer();
        }
        #endregion
        #region 打开ESC菜单
        public void SwitchESCMenu(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }
            if (_isESCMenuOpen)
            {
                // 如果ESC菜单打开着，就切换回玩家操作模式
                // TODO: 如果那时还有受监控的容器开着，这里应该走 UIManager.Instance.RefreshActionMap()
                SwitchActionMap(DefaultActionMapName);
                _isESCMenuOpen = !_isESCMenuOpen;
                // 发送事件，告诉其他系统现在ESC是关闭状态
                EventPipeline.Publish(new ESCMenuEvent(_isESCMenuOpen));

            }
            else
            {
                SwitchActionMap("ESCMenu");
                _isESCMenuOpen = !_isESCMenuOpen;
                // 发送事件，告诉其他系统现在ESC是打开状态，
                EventPipeline.Publish(new ESCMenuEvent(_isESCMenuOpen));
            }
        }
        public void SwitchESCMenu()
        {
            // 这是打开时的UI按钮调用的函数，直接切换回玩家操作模式。
            // 注意要发"目标状态"：原来先发旧值再翻转，订阅方会收到反的状态。
            // TODO: 同上，有容器开着的话该走 UIManager.Instance.RefreshActionMap()
            SwitchActionMap(DefaultActionMapName);
            _isESCMenuOpen = false;
            EventPipeline.Publish(new ESCMenuEvent(false));
        }

        /// <summary>
        /// Player 预制体上 `Player/ESC[/Keyboard/escape]` 绑定的名字。
        /// 该 action 已废弃（现在的 ESC 入口是 `SwitchESCMenu`），保留空实现只为避免
        /// UnityEvent 出现 Missing 报警。如果预制体里已经改绑，可以删掉这个方法。
        /// </summary>
        public void OnESCPanelOpen(InputAction.CallbackContext context)
        {
        }
        #endregion
        #region 切换ActionMap
        public void SwitchActionMap(string actionMapName)
        {
            Debug.Log($"切换为ActionMap：{actionMapName}");
            switch (actionMapName)
            {
                case DefaultActionMapName:
                    {
                        _playerInput.SwitchCurrentActionMap(DefaultActionMapName);
                        break;
                    }
                case "MiniGame":
                case "MiniGameInstanceBasic":
                    {
                        _playerInput.SwitchCurrentActionMap("MiniGame");
                        break;
                    }
                case "ESCMenu":
                    {
                        _playerInput.SwitchCurrentActionMap("ESCMenu");
                        break;
                    }
                case UIActionMapName:
                    {
                        _playerInput.SwitchCurrentActionMap(UIActionMapName);
                        break;
                    }
                case "TalentTree":
                    {
                        _playerInput.SwitchCurrentActionMap("TalentTree");
                        break;
                    }
                default:
                    {
                        Debug.LogError($"InputManager：出现意料之外的ActionMapName，请检查");
                        break;
                    }
            }
        }
        #endregion
    }
}
