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

        private Rigidbody rb;
        public Vector2 MoveInput; // 玩家输入的移动方向,相较于屏幕上下左右的方向
        public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;

        public event Action OnInteractPress;
        public event Action<int> OnInteractionScroll; // 传入滚轮方向，1为向上，-1为向下


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
            OnInteractPress?.Invoke();
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
                OnInteractionScroll?.Invoke(1);
            }
            else if (scrollValue > 0f)
            {
                OnInteractionScroll?.Invoke(-1);
            }
        }
        #endregion
        #region 控制背包
        public void OnOpenBackpack(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }
            SwitchActionMap("UI");
            EventPipeline.Publish(new InventoryControlEvent());
        }
        /// <summary>
        /// 背包右上角的Exit按钮点击时调用此方法
        /// </summary>
        public void OnExitButtonClick()
        {
            SwitchActionMap("Player");
            EventPipeline.Publish(new InventoryControlEvent());
        }
        public void OnCloseBackpack(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }
            SwitchActionMap("Player");
            EventPipeline.Publish(new InventoryControlEvent());
        }
        #endregion
        #region 切换ActionMap
        public void SwitchActionMap(string actionMapName)
        {
            switch (actionMapName)
            {
                case "Player":
                    {
                        _playerInput.SwitchCurrentActionMap("Player");
                        break;
                    }
                case "UI":
                    {
                        _playerInput.SwitchCurrentActionMap("UI");
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
