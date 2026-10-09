using TapTapGameJam.Items;
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
        [field: SerializeField] public PlayerInteractionManager PlayerInteractionManager { get; private set; }
        [field: SerializeField] public PlayerResources PlayerResources { get; private set; }

        [Tooltip("玩家的背包（网格 + 手 + 装备槽）。物品拾取/使用都走它")]
        [field: SerializeField] public PlayerBag PlayerBag { get; private set; }

        [Tooltip("玩家体力：冲刺消耗、受伤/中毒/负重占据上限")]
        [field: SerializeField] public PlayerHealth PlayerHealth { get; private set; }


        public IdleState Idle { get; private set; }
        public HappyIdleState HappyIdle { get; private set; }
        public MoveState Move { get; private set; }
        public DeathState Death { get; private set; }
        private void Awake()
        {
            // 序列化字段没接全时自动兜底，避免"忘了拖引用"导致一堆空引用
            if (InputManager == null) InputManager = GetComponent<PlayerInputManager>();
            if (PlayerMovement == null) PlayerMovement = GetComponent<PlayerMovement>();
            if (AnimationManager == null) AnimationManager = GetComponent<PlayerAnimationManager>();
            if (PlayerInteractionManager == null) PlayerInteractionManager = GetComponent<PlayerInteractionManager>();
            if (PlayerResources == null) PlayerResources = GetComponent<PlayerResources>();
            if (PlayerBag == null) PlayerBag = GetComponent<PlayerBag>();
            if (PlayerHealth == null) PlayerHealth = GetComponent<PlayerHealth>();

            // 初始化玩家拥有的状态机与状态实例
            StateMachine = new PlayerStateMachine();
            Idle = new IdleState(this);
            HappyIdle = new HappyIdleState(this);
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