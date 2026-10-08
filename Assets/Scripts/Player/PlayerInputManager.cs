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
        private bool _isBackpackOpen = false; // 当前是否打开了背包
        private Rigidbody rb;
        public Vector2 MoveInput; // 玩家输入的移动方向,相较于屏幕上下左右的方向
        public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;

        public event Action EventOnInteractPress;
        public event Action EventOnInteractionSwitchPress; // 按 Q 切换到下一个交互目标
        public event Action<int> EventOnCameraScroll; // 传入滚轮方向，1为远离，-1为靠近
        public event Action<int> EventOnCameraRotate; // [ 为 +1，] 为 -1，每次旋转 45 度

        private InputAction _rotateCameraLeft;
        private InputAction _rotateCameraRight;

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
            }
        }

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
        #endregion
        #region 控制背包
        public void SwitchBackpackPanel(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }
            if (_isBackpackOpen)
            {
                // 如果ESC菜单打开着，就切换回玩家操作模式
                SwitchActionMap("DayPlayer");
                EventPipeline.Publish(new BackpackEvent(_isBackpackOpen));
                _isBackpackOpen = !_isBackpackOpen;
            }
            else
            {
                SwitchActionMap("Backpack");
                EventPipeline.Publish(new BackpackEvent(_isBackpackOpen));
                _isBackpackOpen = !_isBackpackOpen;
            }
        }
        public void SwitchBackpackPanel()
        {
            SwitchActionMap("DayPlayer");
            EventPipeline.Publish(new BackpackEvent());
        }
        public void OnCloseBackpack(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }
            SwitchActionMap("DayPlayer");
            EventPipeline.Publish(new BackpackEvent());
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
            // 这是打开时的UI按钮调用的函数，直接切换回玩家操作模式
            SwitchActionMap("DayPlayer");
            EventPipeline.Publish(new ESCMenuEvent(_isESCMenuOpen));
            _isESCMenuOpen = !_isESCMenuOpen;
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
