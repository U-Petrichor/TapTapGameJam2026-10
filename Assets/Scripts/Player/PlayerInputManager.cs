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
        private bool _isCtrlPressed = false; // 当前是否按下了Ctrl键
        private Rigidbody rb;
        public Vector2 MoveInput; // 玩家输入的移动方向,相较于屏幕上下左右的方向
        public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;

        public event Action EventOnInteractPress;
        public event Action<int> EventOnInteractionScroll; // 传入滚轮方向，1为向上，-1为向下
        public event Action<int> EventOnCameraScroll; // 传入滚轮方向，1为靠近，-1为远离


        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            _player = GetComponent<PlayerBasic>();
        }

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
            // 传入值，鼠标往下是-120，往上是+120
            // 但是PlayerInteractionManager中，List的索引越大，在UI层面是越靠下的，所以往下滚，UI应该往下，索引应当增加；往上滚，UI应该往上，索引应当减少
            // 于是我们这里得反过来
            if (!context.performed)
            {
                return;
            }
            float scrollValue = context.ReadValue<float>();

            if (scrollValue < 0f)
            {
                if (_isCtrlPressed == true)
                {
                    EventOnInteractionScroll?.Invoke(1);
                }
                else
                {
                    EventOnCameraScroll?.Invoke(1);
                }
            }
            else if (scrollValue > 0f)
            {
                if(_isCtrlPressed == true)
                {
                    EventOnInteractionScroll?.Invoke(-1);
                }
                else
                {
                    EventOnCameraScroll?.Invoke(-1);
                }
            }
        }
        public void OnCtrlPressed(InputAction.CallbackContext context)
        {
            if (context.performed || context.canceled)
            {
                _isCtrlPressed = context.ReadValueAsButton();
            }
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
            switch (actionMapName)
            {
                case "DayPlayer":
                    {
                        _playerInput.SwitchCurrentActionMap("DayPlayer");
                        break;
                    }
                case "NightPlayer":
                    {
                        _playerInput.SwitchCurrentActionMap("NightPlayer");
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
