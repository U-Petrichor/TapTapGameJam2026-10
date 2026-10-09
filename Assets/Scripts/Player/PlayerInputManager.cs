using UnityEngine;
using UnityEngine.InputSystem;
using TapTapGameJam.Events;
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
        public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;

        public event Action EventOnInteractPress;
        public event Action EventOnInteractionSwitchPress; // 按 Q 切换到下一个交互目标
        public event Action<int> EventOnCameraScroll; // 传入滚轮方向，1为远离，-1为靠近
        public event Action<int> EventOnCameraRotate; // [ 为 +1，] 为 -1，每次旋转 45 度

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
            if (_sprintAction == null)
            {
                return null;
            }

            return _sprintAction.IsPressed();
        }

        /// <summary>
        /// 背包是否处于打开状态。
        /// 不看自己缓存的 bool，而是看当前 ActionMap —— 这样和 ActionMap 的切换永远一致，
        /// 不会被"某次没走到赋值"搞脏。
        /// </summary>
        public bool IsBackpackOpen
        {
            get
            {
                return _playerInput != null
                       && _playerInput.actions != null
                       && _playerInput.currentActionMap != null
                       && _playerInput.currentActionMap.name == BackpackActionMapName;
            }
        }

        private const string BackpackActionMapName = "Backpack";

        private void Update()
        {
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
            SwitchActionMap("DayPlayer");
        }
        #endregion
        #region 玩家移动

        /// <summary>
        /// 根据玩家的输入，更新InputVector
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            MoveInput = context.canceled
                ? Vector2.zero
                : Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f);
        }
        #endregion

        #region 玩家交互
        public void OnInteract(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }
            EventOnInteractPress?.Invoke();
        }
        public void OnMouseScroll(InputAction.CallbackContext context)
        {
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
            if (!context.performed) return;

            EventOnInteractionSwitchPress?.Invoke();
        }

        /// <summary>
        /// 冲刺键。需要在 Player 预制体的 PlayerInput → ActionEvents 里把 Sprint 挂到这个方法上；
        /// 没挂也不影响，PlayerHealth 会退回直接读键盘。
        /// </summary>
        public void OnSprint(InputAction.CallbackContext context)
        {
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
        /// 按 B 打开背包。
        /// 注意：Player 预制体里 `Player/Backpack[/Keyboard/b]` 绑定的就是这个名字，
        /// 不要随手改名，否则预制体上的 UnityEvent 会变成 Missing。
        /// </summary>
        public void OnOpenBackpack(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            if (IsBackpackOpen)
            {
                CloseBackpack();
            }
            else
            {
                OpenBackpack();
            }
        }

        /// <summary>给 UI 按钮/代码调用的重载（没有 InputAction 上下文时用）</summary>
        public void OnOpenBackpack()
        {
            if (IsBackpackOpen)
            {
                CloseBackpack();
            }
            else
            {
                OpenBackpack();
            }
        }

        /// <summary>打开背包：切 ActionMap + 发事件（UI 与暂停都靠这个事件驱动）</summary>
        public void OpenBackpack()
        {
            if (IsBackpackOpen)
            {
                // 已经开着就不要再发一次事件，否则 UI 会重复刷新、暂停计数也会被重复置位
                return;
            }

            SwitchActionMap(BackpackActionMapName);
            EventPipeline.Publish(new BackpackEvent(true));
        }

        /// <summary>关闭背包</summary>
        public void CloseBackpack()
        {
            if (!IsBackpackOpen)
            {
                return;
            }

            SwitchActionMap("DayPlayer");
            EventPipeline.Publish(new BackpackEvent(false));
        }

        /// <summary>旧名字，保留兼容旧绑定</summary>
        public void SwitchBackpackPanel(InputAction.CallbackContext context)
        {
            OnOpenBackpack(context);
        }

        /// <summary>旧名字（无参重载），UI 按钮调用时用来切换</summary>
        public void SwitchBackpackPanel()
        {
            OnOpenBackpack();
        }

        /// <summary>Backpack ActionMap 下的 ESC：关闭背包</summary>
        public void OnCloseBackpack(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            CloseBackpack();
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
                SwitchActionMap("DayPlayer");
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
            SwitchActionMap("DayPlayer");
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
                case "DayPlayer":
                    {
                        _playerInput.SwitchCurrentActionMap("DayPlayer");
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
                case "Backpack":
                    {
                        _playerInput.SwitchCurrentActionMap("Backpack");
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
