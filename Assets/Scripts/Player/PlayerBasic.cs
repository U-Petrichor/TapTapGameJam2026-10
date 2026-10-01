using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Player
{
    public class PlayerBasic : MonoBehaviour
    {
        public PlayerStateMachine StateMachine { get; private set; }
        [field: SerializeField] public PlayerInputManager InputManager { get; private set; }
        [field: SerializeField] public PlayerMovement PlayerMovement { get; private set; }
        [field: SerializeField] public PlayerAnimationManager AnimationManager { get; private set; }


        public IdleState Idle { get; private set; }
        public MoveState Move { get; private set; }
        public DeathState Death { get; private set; }
        private void Awake()
        {


            // 初始化玩家拥有的状态机与状态实例
            StateMachine = new PlayerStateMachine();
            Idle = new IdleState(this);
            Move = new MoveState(this);
            Death = new DeathState(this);
        }
        private void Start()
        {
            StateMachine.ChangeState(Idle);
        }
        private void Update()
        {
            StateMachine.Update();
        }
    }
}