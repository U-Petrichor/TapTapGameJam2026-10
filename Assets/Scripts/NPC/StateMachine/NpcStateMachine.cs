using UnityEngine;

namespace TapTapGameJam.NPC
{
    /// <summary>NPC 的通用状态机。</summary>
    public sealed class NpcStateMachine
    {
        private NpcStateBase _currentState;

        public NpcStateBase CurrentState => _currentState;

        public void ChangeState(NpcStateBase newState)
        {
            if (newState == null)
            {
                Debug.LogError("[NpcStateMachine] 目标状态为 null，忽略本次切换。");
                return;
            }

            if (ReferenceEquals(newState, _currentState)) return;

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
