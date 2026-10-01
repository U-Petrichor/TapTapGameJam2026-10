using UnityEngine;

namespace TapTapGameJam.Player
{
    public class PlayerStateMachine
    {
        private PlayerStateBase _currentState;

        public PlayerStateBase CurrentState => _currentState;

        public void ChangeState(PlayerStateBase newState)
        {
            if (newState == null)
            {
                Debug.LogError($"[MonsterAIStateMachine] 目标状态为null，已忽略本次切换");
                return;
            }
            if (ReferenceEquals(newState, _currentState))
            {
                // 切到自己会让 Exit/Enter 空跑一遍，动画会被反复打断
                return;
            }

            _currentState?.Exit();
            _currentState = newState;
            _currentState.Enter();
        }

        public void Update()
        {
            _currentState?.Update();
        }
    }
}
