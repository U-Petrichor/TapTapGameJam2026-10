using UnityEngine.Assertions;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using UnityEngine;

namespace TapTapGameJam.Player
{
    /// <summary>
    /// 玩家动画：白天（人形）/ 夜晚（蟑螂）两套模型各自有自己的 Animator，
    /// 阶段切换时这里负责**只激活一套模型**，并把 <see cref="Animator"/> 换成那套身上的那个。
    ///
    /// 注意 <see cref="Animator"/> 是 Play* 系列唯一的操作对象，所以它必须始终指向"当前激活的那套"；
    /// 换模型后还要把上一次请求的状态补播一次 —— 状态机只在 Enter 里调一次 PlayXxx，
    /// 换完不补播新模型就定在默认姿势上了。
    /// </summary>
    public class PlayerAnimationManager : MonoBehaviour
    {
        [Header("白天 / 夜晚 两套模型")]
        [Tooltip("白天（含购物阶段）显示的模型根物体")]
        [SerializeField] private GameObject _humanModel;

        [Tooltip("夜晚显示的模型根物体")]
        [SerializeField] private GameObject _nightModel;

        [field: SerializeField] public Animator Animator { get; private set; }

        /// <summary>
        /// 夜晚（蟑螂）模型根物体的 Transform。
        /// 给"挂在骨骼上的东西"（<see cref="PlayerDressView"/>）找挂点用：
        /// 它白天是 inactive 的，但那不影响按名字遍历骨骼层级。
        /// </summary>
        public Transform NightModelRoot => _nightModel != null ? _nightModel.transform : null;

        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int HappyIdleStateHash = Animator.StringToHash("Base Layer.HappyIdle");
        private static readonly int MoveStateHash = Animator.StringToHash("Base Layer.Move");
        private static readonly int CarryIdleStateHash = Animator.StringToHash("Base Layer.CarryIdle");
        private static readonly int CarryAndMoveStateHash = Animator.StringToHash("Base Layer.CarryAndMove");
        private static readonly int FileCarryIdleStateHash = Animator.StringToHash("Base Layer.FileCarryIdle");
        private static readonly int FileCarryAndMoveStateHash = Animator.StringToHash("Base Layer.FileCarryAndMove");
        private static readonly int NestCarryIdleStateHash = Animator.StringToHash("Base Layer.NestCarryIdle");
        private static readonly int NestCarryAndMoveStateHash = Animator.StringToHash("Base Layer.NestCarryAndMove");

        public bool IsHappyIdleFinished = false;

        /// <summary>最后一次请求的状态。换 Animator 之后要拿它在新的 Animator 上补播</summary>
        private int _lastStateHash = -1;

        #region 生命周期

        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }

        private void Start()
        {
            // 阶段事件可能在我们订阅之前就发过了（比如一进场景就是夜晚），所以开局按当前阶段对齐一次
            ApplyPhase(GameManager.IsNight);
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            ApplyPhase(evt.NewPhase == GamePhase.Night);
        }

        #endregion

        #region 模型切换

        /// <summary>白天 / 夜晚切模型：只激活一套，并把 Animator 指到那一套身上</summary>
        public void ApplyPhase(bool night)
        {
            GameObject on = night ? _nightModel : _humanModel;
            GameObject off = night ? _humanModel : _nightModel;

            Assert.IsTrue(on != null && off != null,
                $"{name}：两套模型都要在 Inspector 里连上（白天模型 / 夜晚模型）");

            SwitchModel(on, off);
        }

        private void SwitchModel(GameObject on, GameObject off)
        {
            if (off.activeSelf)
            {
                off.SetActive(false);
            }

            if (!on.activeSelf)
            {
                on.SetActive(true);
            }

            // Animator 可能挂在模型根上，也可能挂在子物体上
            Animator animator = on.GetComponentInChildren<Animator>(true);
            Assert.IsTrue(animator != null, $"{name}：{on.name} 身上找不到 Animator");
            Animator = animator;

            // 补播上一次请求的状态：状态机只在 Enter 里调一次 PlayXxx，
            // 不补的话新模型会停在默认姿势，直到下一次状态切换
            if (_lastStateHash != -1)
            {
                Animator.CrossFadeInFixedTime(_lastStateHash, 0f, 0, 0f);
            }
        }

        #endregion

        #region 播放

        public void PlayIdle()
        {
            PlayState(IdleStateHash, 0.3f);
        }
        public void PlayHappyIdle()
        {
            // TODO: 蟑螂还是会报错
            // PlayState(HappyIdleStateHash, 0.15f);
        }
        public void PlayMove()
        {
            PlayState(MoveStateHash, 0.15f);
        }

        public void PlayCarryIdle(PlayerCarryController.CarryType carryType)
        {
            int preferred = carryType == PlayerCarryController.CarryType.File
                ? FileCarryIdleStateHash
                : carryType == PlayerCarryController.CarryType.CockroachNest
                    ? NestCarryIdleStateHash
                    : CarryIdleStateHash;
            PlayCarryState(preferred, CarryIdleStateHash);
        }

        public void PlayCarryAndMove(PlayerCarryController.CarryType carryType)
        {
            int preferred = carryType == PlayerCarryController.CarryType.File
                ? FileCarryAndMoveStateHash
                : carryType == PlayerCarryController.CarryType.CockroachNest
                    ? NestCarryAndMoveStateHash
                    : CarryAndMoveStateHash;
            PlayCarryState(preferred, CarryAndMoveStateHash);
        }

        private void PlayCarryState(int preferredHash, int fallbackHash)
        {
            // 两套模型的动作集不一定一样：优先播专用的，没有就退回通用的
            int stateHash = Animator != null && Animator.HasState(0, preferredHash) ? preferredHash : fallbackHash;
            PlayState(stateHash, 0.15f);
        }

        /// <summary>
        /// 统一播放入口：先记下"最后请求的状态"，再写到当前 Animator 上。
        /// 记下来是为了切模型时能补播；而这里可能比模型切换先跑
        /// （两个组件的 Start 顺序不保证），所以没 Animator 时只记录、不报错。
        /// </summary>
        private void PlayState(int stateHash, float duration)
        {
            _lastStateHash = stateHash;

            if (Animator != null)
            {
                Animator.CrossFadeInFixedTime(stateHash, duration, 0, 0f);
            }
        }

        public void PlayDeath()
        {

        }
        public void HappyIdleEnd()
        {
            IsHappyIdleFinished = true;
        }

        #endregion
    }
}
