using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Types;
using TalentTree;
using UnityEngine;

namespace TapTapGameJam.Player
{
    [RequireComponent(typeof(PlayerCarryController))]
    public class PlayerBasic : MonoBehaviour
    {
        public PlayerStateMachine StateMachine { get; private set; }
        [field: SerializeField] public PlayerInputManager InputManager { get; private set; }
        [field: SerializeField] public PlayerMovement PlayerMovement { get; private set; }
        [field: SerializeField] public PlayerAnimationManager AnimationManager { get; private set; }
        [field: SerializeField] public PlayerInteractionManager PlayerInteractionManager { get; private set; }
        [field: SerializeField] public PlayerResources PlayerResources { get; private set; }
        [field: SerializeField] public PlayerTalentTree TalentTree { get; private set; }

        [Tooltip("人形玩家的独立携带状态，不与夜晚 PlayerBag 共用")]
        [field: SerializeField] public PlayerCarryController CarryController { get; private set; }

        [Tooltip("夜晚蟑螂形态的背包数据（网格 + 手 + 装备槽），不参与人形携带状态")]
        [field: SerializeField] public PlayerBag PlayerBag { get; private set; }

        [Tooltip("玩家体力：冲刺消耗、受伤/中毒/负重占据上限")]
        [field: SerializeField] public PlayerHealth PlayerHealth { get; private set; }


        public IdleState Idle { get; private set; }
        public HappyIdleState HappyIdle { get; private set; }
        public MoveState Move { get; private set; }
        public CarryIdleState CarryIdle { get; private set; }
        public CarryAndMoveState CarryAndMove { get; private set; }
        public DeathState Death { get; private set; }

        public bool IsCarry => CarryController != null && CarryController.IsCarry;
        public PlayerCarryController.CarryType CarryType =>
            CarryController != null
                ? CarryController.CurrentType
                : PlayerCarryController.CarryType.None;
        private void Awake()
        {
            // 序列化字段没接全时自动兜底，避免"忘了拖引用"导致一堆空引用
            if (InputManager == null) InputManager = GetComponent<PlayerInputManager>();
            if (PlayerMovement == null) PlayerMovement = GetComponent<PlayerMovement>();
            if (AnimationManager == null) AnimationManager = GetComponent<PlayerAnimationManager>();
            if (PlayerInteractionManager == null) PlayerInteractionManager = GetComponent<PlayerInteractionManager>();
            if (PlayerResources == null) PlayerResources = GetComponent<PlayerResources>();
            if (TalentTree == null) TalentTree = GetComponent<PlayerTalentTree>();
            if (CarryController == null) CarryController = GetComponent<PlayerCarryController>();
            if (PlayerBag == null) PlayerBag = GetComponent<PlayerBag>();
            if (PlayerHealth == null) PlayerHealth = GetComponent<PlayerHealth>();

            // 初始化玩家拥有的状态机与状态实例
            StateMachine = new PlayerStateMachine();
            Idle = new IdleState(this);
            HappyIdle = new HappyIdleState(this);
            Move = new MoveState(this);
            CarryIdle = new CarryIdleState(this);
            CarryAndMove = new CarryAndMoveState(this);
            Death = new DeathState(this);

            if (CarryController != null)
            {
                CarryController.EventCarryChanged += HandleCarryChanged;
            }
        }

        private void OnDestroy()
        {
            if (CarryController != null)
            {
                CarryController.EventCarryChanged -= HandleCarryChanged;
            }
        }

        private void Start()
        {
            StateMachine.ChangeState(Idle);
            SyncCarryState();
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandleGamePhaseChangedEvent);
        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandleGamePhaseChangedEvent);
        }
        private void Update()
        {
            SyncCarryState();
            StateMachine.Update();
        }

        private void HandleCarryChanged(PlayerCarryController.CarryType _)
        {
            SyncCarryState();
        }

        /// <summary>
        /// 只处理普通状态与携带状态的边界；普通 Idle/Move 之间的切换仍由各自状态负责。
        /// </summary>
        public void SyncCarryState()
        {
            if (StateMachine == null || StateMachine.CurrentState == null)
            {
                return;
            }

            bool moving = InputManager != null && InputManager.HasMoveInput;
            NpcStateBase current = StateMachine.CurrentState;

            if (IsCarry)
            {
                if (current == Idle || current == Move || current == HappyIdle)
                {
                    StateMachine.ChangeState(moving ? CarryAndMove : CarryIdle);
                }
            }
            else if (current == CarryIdle || current == CarryAndMove)
            {
                StateMachine.ChangeState(moving ? Move : Idle);
            }
        }

        private void HandleGamePhaseChangedEvent(GamePhaseChangedEvent evt)
        {
            ;
            // if (evt.NewPhase == GamePhase.Day)
            // {
            //     // 如果接下来要到来的是白天，那么将PlayerTag切换为Player
            //     gameObject.tag = ("Player");
            // }
            // else if(evt.NewPhase == GamePhase.Shopping|| evt.NewPhase == GamePhase.Night)
            // {
            //     // 如果接下来要到来的是商店或者是夜晚，则切换为Cockroach
            //     gameObject.tag = ("Cockroach");
            // }
        }
    }
}
