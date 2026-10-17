using UnityEngine;

namespace TapTapGameJam.Player
{
    /// <summary>接收白天模型的动画事件，并转发给玩家根物体上的动画管理器。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class DayTimeAnimationManager : MonoBehaviour
    {
        [SerializeField] private PlayerAnimationManager _playerAnimationManager;

        private void Awake()
        {
            if (_playerAnimationManager == null)
            {
                _playerAnimationManager = GetComponentInParent<PlayerAnimationManager>();
            }
        }

        public void HappyIdleEnd()
        {
            _playerAnimationManager.HappyIdleEnd();
        }
    }
}
