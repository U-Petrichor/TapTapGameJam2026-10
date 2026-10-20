using TapTapGameJam.Audio;
using TapTapGameJam.Buffs;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TapTapGameJam.Player
{
    /// <summary>
    /// 玩家体力系统。这里只负责逻辑与数值，不含任何 UI；体力条由 PlayerHealthUI 每帧读这里的属性绘制。
    ///
    /// 核心概念：
    /// - <see cref="MaxHealth"/>：体力上限的基准值（初始 100）。
    /// - <see cref="Damage"/> / <see cref="Poison"/> / <see cref="Heavy"/>：三种"占据上限"的减益。
    ///   三者之和就是被吃掉的上限 —— 受伤、中毒、负重不会扣当前体力，
    ///   而是把你能恢复到的天花板往下压。
    /// - <see cref="PowerTotal"/> = MaxHealth - Damage - Poison - Heavy，可用上限。
    /// - <see cref="PowerAvailable"/>：当前真正能拿来冲刺的体力，正常时按 <see cref="_powerRecoverPerSecond"/> 恢复。
    /// - 体力见底后必须恢复到 <see cref="_sprintRecoverThreshold"/> 之上才能再次冲刺，
    ///   否则会在 0 附近反复开关（抖）。
    ///
    /// 体力条五段的数值来源（顺序固定为 heavy → power_front → power_back → poison → damage）：
    ///   heavy        = Heavy
    ///   power_front  = PowerAvailable
    ///   power_back   = PowerTotal
    ///   poison       = Poison
    ///   damage       = Damage
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("体力上限")]
        [Tooltip("体力上限的基准值（初始 100）。PowerTotal = MaxHealth - Damage - Poison - Heavy")]
        [SerializeField, Min(1f)] private float _maxHealth = 100f;

        [Header("占据上限的减益（受伤 / 中毒 / 负重）")]
        [Tooltip("受伤：占据对应点数的体力上限")]
        [SerializeField, Min(0f)] private float _damage = 0f;
        [Tooltip("中毒：占据对应点数的体力上限")]
        [SerializeField, Min(0f)] private float _poison = 0f;
        [Tooltip("负重：由背包占用格数换算而来，每帧从 PlayerBag 拉取，不用手填")]
        [SerializeField, Min(0f)] private float _heavy = 0f;

        [Header("负重换算（背包占用格数 → 负重）")]
        [Tooltip("背包占用格数超过这个值，超出的每一格都算负重")]
        [SerializeField, Min(0)] private int _freeBagCells = 8;
        [Tooltip("每超出一格增加的负重")]
        [SerializeField, Min(0f)] private float _heavyPerExtraCell = 2f;

        [Header("体力恢复 / 冲刺消耗")]
        [Tooltip("当前可用体力")]
        [SerializeField, Min(0f)] private float _powerAvailable = 100f;
        [Tooltip("不冲刺时每秒恢复的体力")]
        [SerializeField, Min(0f)] private float _powerRecoverPerSecond = 20f;
        [Tooltip("冲刺时每秒消耗的体力")]
        [SerializeField, Min(0f)] private float _sprintCostPerSecond = 5f;
        [Tooltip("体力见底后，要恢复到可用上限的百分之多少才能再次冲刺（防止脚抖）")]
        [SerializeField, Range(0f, 1f)] private float _sprintRecoverThreshold = 0.1f;

        private bool _sprintHeld;
        private bool _isSprinting;
        private bool _needsRecoverToSprint;
        private bool _isDefeated;

        /// <summary>
        /// 最后把可用上限压低的到底是哪一类减益。只用来决定玩家死亡时播哪条播报，
        /// 不参与任何数值计算。
        /// </summary>
        private enum CeilingCause
        {
            Unknown,
            /// <summary>受伤 —— 目前唯一的来源是猎杀者保安打玩家</summary>
            GuardAttack,
            /// <summary>中毒 —— 目前唯一的来源是保洁的喷雾</summary>
            Poison,
            /// <summary>负重 —— 背太重把上限吃空，没有对应播报</summary>
            Heavy,
        }

        private CeilingCause _lastCeilingCause = CeilingCause.Unknown;

        private PlayerBag _bag;
        private PlayerInputManager _input;
        private PlayerMovement _movement;

        private PlayerBuffs _buffs;
        private bool _buffsResolved;

        /// <summary>
        /// 玩家身上的 Buff 宿主。惰性取：PlayerBuffs 是 PlayerBasic 在 Awake 里补上的，
        /// 同一物体上各组件 Awake 的先后顺序不保证，所以取不到就下一次再试。
        /// </summary>
        private PlayerBuffs Buffs
        {
            get
            {
                if (!_buffsResolved)
                {
                    _buffs = GetComponent<PlayerBuffs>();
                    _buffsResolved = _buffs != null;
                }

                return _buffs;
            }
        }

        /// <summary>冲刺每秒消耗的倍率（「体力损耗减免」）。没有 PlayerBuffs 时是 1。</summary>
        private float SprintCostFactor
        {
            get
            {
                PlayerBuffs buffs = Buffs;
                return buffs == null ? 1f : buffs.GetFactor(BuffKey.StaminaCostReduction);
            }
        }

        /// <summary>体力恢复速度的倍率。没有 PlayerBuffs 时是 1。</summary>
        private float RecoverFactor
        {
            get
            {
                PlayerBuffs buffs = Buffs;
                return buffs == null ? 1f : buffs.GetFactor(BuffKey.StaminaRecoveryPercent);
            }
        }

        #region 只读属性（UI 与外部逻辑都读这里）

        /// <summary>体力上限基准值</summary>
        public float MaxHealth => _maxHealth;

        /// <summary>
        /// 实际生效的体力上限 = 基准值 × 「体力上限」Buff + 固定值加成。
        /// Buff 只乘在读取点上，<see cref="_maxHealth"/> 永远是基准值。
        /// </summary>
        private float BuffedMaxHealth
        {
            get
            {
                float max = _maxHealth;
                PlayerBuffs buffs = Buffs;
                if (buffs == null)
                {
                    return max;
                }

                max = buffs.Apply(BuffKey.StaminaMaxPercent, max);
                return max + buffs.GetFlat(BuffKey.StaminaMaxFlat);
            }
        }

        /// <summary>受伤占据的上限</summary>
        public float Damage => _damage;

        /// <summary>中毒占据的上限</summary>
        public float Poison => _poison;

        /// <summary>负重占据的上限</summary>
        public float Heavy => _heavy;

        /// <summary>是否已经受伤</summary>
        public bool HasDamaged => _damage > 0.001f;
        public bool HasPoison => _poison > 0.001f;
        public bool IsFullPower => _powerAvailable + 0.001f > PowerTotal;
        
        /// <summary>可用体力上限（体力条背景 power_back 的值）。降到 0 视为失败</summary>
        public float PowerTotal => Mathf.Max(0f, BuffedMaxHealth - _damage - _poison - _heavy);

        /// <summary>当前可用体力（体力条前景 power_front 的值）</summary>
        public float PowerAvailable => Mathf.Clamp(_powerAvailable, 0f, PowerTotal);

        /// <summary>PowerAvailable / PowerTotal，UI 画条的填充比例</summary>
        public float PowerPercent => PowerTotal > 0.0001f ? Mathf.Clamp01(PowerAvailable / PowerTotal) : 0f;

        /// <summary>当前是否正在冲刺</summary>
        public bool IsSprinting => _isSprinting;

        /// <summary>可用上限归零，游戏失败</summary>
        public bool IsDefeated => _isDefeated;

        #endregion

        private void Awake()
        {
            _bag = GetComponent<PlayerBag>();
            _input = GetComponent<PlayerInputManager>();
            _movement = GetComponent<PlayerMovement>();
            _powerAvailable = Mathf.Clamp(_powerAvailable, 0f, PowerTotal);
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<BuffsRebuiltEvent>(HandleBuffsRebuilt);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<BuffsRebuiltEvent>(HandleBuffsRebuilt);
        }

        /// <summary>
        /// Buff 变了 → 可用上限可能跟着变（体力上限、负重、受伤/中毒减免都不改动已存量，
        /// 但上限本身会动），当前体力不能超过新上限。
        /// </summary>
        private void HandleBuffsRebuilt(BuffsRebuiltEvent evt) => ClampAvailableToTotal();

        private void Update()
        {
            // 背包占用格数每帧拉一次：负重是"背包状态"的函数，不适合让两边各存一份
            RefreshHeavyFromBag();

            _sprintHeld = ReadSprintHeld();
            bool wasSprinting = _isSprinting;
            UpdatePower(Time.deltaTime);
            if (!wasSprinting && _isSprinting && Time.timeScale > 0f &&
                TryGetComponent<PlayerBasic>(out var player) && !player.IsHuman)
                Sfx.At(SoundId.Sprint, transform.position, this);

            // 冲刺只在这里落地：PlayerMovement 只负责按 IsSprinting 改速度
            if (_movement != null)
            {
                _movement.IsSprinting = _isSprinting;
            }

            if (PowerTotal <= 0f)
            {
                HandleDefeat();
            }
        }

        #region 减益接口

        /// <summary>
        /// 受伤：增加受伤占据的上限。
        /// 「受伤减免」乘在<b>入口</b>而不是存储上——这样已经受的伤不会被后来的天赋追溯减免，
        /// 也不会因为 buff 中途变化而让 %Damage 自己跳。
        /// </summary>
        public void AddDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            PlayerBuffs buffs = Buffs;
            if (buffs != null)
            {
                amount = buffs.Apply(BuffKey.PlayerDamageReduction, amount);
            }

            if (amount <= 0f)
            {
                return;
            }

            _damage += amount;
            _lastCeilingCause = CeilingCause.GuardAttack;
            Sfx.Play(SoundId.Hit, this);
            ClampAvailableToTotal();
        }

        /// <summary>治疗：减少受伤占据的上限</summary>
        public void ReduceDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            float previous = _damage;
            _damage = Mathf.Max(0f, _damage - amount);
            if (previous > 0f && _damage <= 0f) Sfx.Play(SoundId.Recovery, this);
        }

        /// <summary>中毒：增加中毒占据的上限。「中毒影响减免」乘在入口，理由同 <see cref="AddDamage"/>。</summary>
        public void AddPoison(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            PlayerBuffs buffs = Buffs;
            if (buffs != null)
            {
                amount = buffs.Apply(BuffKey.PoisonImpactReduction, amount);
            }

            if (amount <= 0f)
            {
                return;
            }

            _poison += amount;
            _lastCeilingCause = CeilingCause.Poison;
            Sfx.Play(SoundId.Hit, this);
            ClampAvailableToTotal();
        }

        /// <summary>解毒：减少中毒占据的上限</summary>
        public void ReducePoison(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            float previous = _poison;
            _poison = Mathf.Max(0f, _poison - amount);
            if (previous > 0f && _poison <= 0f) Sfx.Play(SoundId.Recovery, this);
        }
        
        /// <summary>手动恢复体力</summary>
        public void AddPower(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _powerAvailable = Mathf.Min(PowerTotal, _powerAvailable + amount);
            Sfx.Play(SoundId.Recovery, this);
        }

        /// <summary>直接改负重（正常情况下负重由背包自动换算，这个接口留给特殊玩法用）</summary>
        public void SetHeavy(float value)
        {
            _heavy = Mathf.Max(0f, value);
            ClampAvailableToTotal();
        }

        #endregion

        #region 体力

        /// <summary>外部直接扣体力（例如受到攻击）。返回实际扣掉的值</summary>
        public float DrainPower(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float before = _powerAvailable;
            _powerAvailable = Mathf.Max(0f, _powerAvailable - amount);
            if (_powerAvailable <= 0f)
            {
                _needsRecoverToSprint = true;
            }

            return before - _powerAvailable;
        }

        /// <summary>回满当前可用体力（睡一觉、吃药之类）</summary>
        public void RefillPower()
        {
            if (_powerAvailable < PowerTotal) Sfx.Play(SoundId.Recovery, this);
            _powerAvailable = PowerTotal;
            _needsRecoverToSprint = false;
        }

        /// <summary>背包占用格数 → 负重。「负重减免」乘在换算结果上：同样格数产生更少负重。</summary>
        private void RefreshHeavyFromBag()
        {
            if (_bag == null)
            {
                return;
            }

            int extraCells = _bag.OccupiedCellCount - _freeBagCells;
            float heavy = extraCells > 0 ? extraCells * _heavyPerExtraCell : 0f;

            if (heavy > 0f)
            {
                PlayerBuffs buffs = Buffs;
                if (buffs != null)
                {
                    heavy = buffs.Apply(BuffKey.BagWeightReduction, heavy);
                }
            }

            if (!Mathf.Approximately(heavy, _heavy))
            {
                // 只有负重"变大"才算它压低了上限；每帧重算不该把别的死因盖掉
                if (heavy > _heavy)
                {
                    _lastCeilingCause = CeilingCause.Heavy;
                }

                _heavy = heavy;
                ClampAvailableToTotal();
            }
        }

        /// <summary>
        /// 上限被压下来时，当前体力不能超过新的上限。
        /// 如果这一下直接把当前体力顶到 0（例如背太重、中毒加深），
        /// 就等价于"体力见底"，同样要进入必须恢复到阈值才能再冲刺的锁定状态。
        /// </summary>
        private void ClampAvailableToTotal()
        {
            _powerAvailable = Mathf.Clamp(_powerAvailable, 0f, PowerTotal);
            if (_powerAvailable <= 0f)
            {
                _needsRecoverToSprint = true;
            }
        }

        private void UpdatePower(float deltaTime)
        {
            // 能不能冲刺：上限没归零 & 不是"刚见底还没恢复够"的状态 & 当前确实有体力可用
            bool canSprint = PowerTotal > 0f
                             && !_needsRecoverToSprint
                             && PowerAvailable > 0f;

            _isSprinting = _sprintHeld && canSprint;

            if (_isSprinting)
            {
                _powerAvailable = Mathf.Max(0f, _powerAvailable - _sprintCostPerSecond * SprintCostFactor * deltaTime);
                if (_powerAvailable <= 0f)
                {
                    // 见底：立刻停冲刺，并且必须恢复到阈值以上才能再次冲刺
                    _needsRecoverToSprint = true;
                    _isSprinting = false;
                }
            }
            else
            {
                _powerAvailable = Mathf.Min(PowerTotal,
                    _powerAvailable + _powerRecoverPerSecond * RecoverFactor * deltaTime);
            }

            // 恢复到阈值以上，解除"见底锁定"
            if (_needsRecoverToSprint && PowerAvailable >= PowerTotal * _sprintRecoverThreshold)
            {
                _needsRecoverToSprint = false;
            }
        }

        /// <summary>
        /// 冲刺键是否按下。优先用 PlayerInputManager 的 Sprint action（挂在 ActionEvents 里），
        /// 没挂上就退回直接读键盘，避免"忘了接线 → 疾跑静默失效"。
        /// </summary>
        private bool ReadSprintHeld()
        {
            if (_input != null)
            {
                bool? fromAction = _input.ReadSprintHeld();
                if (fromAction.HasValue)
                {
                    return fromAction.Value;
                }
            }

            return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        }

        private void HandleDefeat()
        {
            if (_isDefeated)
            {
                return;
            }

            _isDefeated = true;
            Sfx.Play(SoundId.Defeat, this);
            _isSprinting = false;

            // 广播「玩家死了」。具体触发什么（倒下动画 / 结算 / 结束本局）还没定，
            // 订阅方接 EventPlayerDied 即可，这边不写任何后续逻辑。
            EventPipeline.Publish(new EventPlayerDied());

            // 播报 + 结局：按"最后把上限压空的那一类减益"选哪一条。
            // 只有受伤（保安）和中毒（保洁的喷雾）有对应视频和结局，负重压死就都不触发。
            switch (_lastCeilingCause)
            {
                case CeilingCause.GuardAttack:
                    BroadcastUI.Broadcast(BroadcastKind.GuardKill);
                    GameEndingDirector.Trigger(GameEnding.Crushed);
                    break;
                case CeilingCause.Poison:
                    BroadcastUI.Broadcast(BroadcastKind.CleanerPoison);
                    GameEndingDirector.Trigger(GameEnding.Sprayed);
                    break;
            }

            // TODO: 失败流程（倒下动画 / 弹结算界面 / 通知 GameManager 结束本局）
            Debug.LogWarning("[PlayerHealth]：可用体力上限降到 0，游戏失败（失败流程待实现）");
        }

        #endregion
    }
}
