using System;
using TapTapGameJam.Core;
using TapTapGameJam.Player;
using UnityEngine;
using UnityEngine.Assertions;

// Cockroach 在本命名空间里既是命名空间又是实体类型，直接写会被当成命名空间（CS0118），统一走别名
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫变体：猎杀者。
    /// - 蟑螂进视野就追，冲过去撞死；贴到身上的也直接吃掉（不用冲刺）。
    /// - 玩家进视野优先追玩家，只有冲刺撞到玩家才造成伤害，一次冲刺最多命中一次。
    ///
    /// 它只实现 <see cref="DecideIntent"/>（和几个数值），避障、区域约束、回出生点全在 <see cref="Guard"/> 里。
    /// 攻击判定用距离而不是碰撞回调：碰撞事件的时序跟着物理帧走，会出现"贴到脸上了却没死"。
    /// </summary>
    public class KillerGuard : Guard
    {
        [Serializable]
        public class KillerGuardSettings
        {
            [Header("冲刺击杀")]
            [Tooltip("冲刺时判定命中蟑螂的额外半径（叠在 BodyRadius 上）")]
            public float KillReach = 0.35f;

            [Header("玩家")]
            [Tooltip("冲刺时判定命中玩家的额外半径（叠在 BodyRadius 上）")]
            public float PlayerHitReach = 0.6f;

            [Tooltip("一次冲刺对玩家造成的伤害")]
            public float PlayerDamage = 25f;

            [Tooltip("玩家和蟑螂同时在视野里时，优先追玩家")]
            public bool PreferPlayer = true;
        }

        /// <summary>
        /// 追击目标。蟑螂会中途被别的守卫咬死或钻进巢里，玩家则一直存在；
        /// 用一个结构表达"两种目标"，追击和命中的流程就能共用。
        /// </summary>
        private readonly struct ChaseTarget
        {
            public readonly bool IsPlayer;
            public readonly CockroachAgent Cockroach;
            public readonly Vector2 Position;

            private ChaseTarget(bool isPlayer, CockroachAgent cockroach, Vector2 position)
            {
                IsPlayer = isPlayer;
                Cockroach = cockroach;
                Position = position;
            }

            public static ChaseTarget ForPlayer(Vector2 position) => new ChaseTarget(true, null, position);

            public static ChaseTarget ForCockroach(CockroachAgent cockroach)
                => new ChaseTarget(false, cockroach, cockroach.Position);
        }

        [Header("猎杀者数值")]
        public KillerGuardSettings killer = new KillerGuardSettings();

        /// <summary>冲刺锁定的目标；null = 没在冲刺</summary>
        private ChaseTarget? _dashTarget;

        private float _dashTimer;
        private float _dashCooldown;

        /// <summary>是否正在冲刺（冲刺才有对玩家的伤害）</summary>
        public bool IsDashing => _dashTarget.HasValue;

        protected override void Start()
        {
            base.Start();

            // 基类没能把守卫搭起来（错误已经报过），这里不用再往下走
            if (!IsReady)
            {
                return;
            }

            Assert.IsNotNull(GameManager.Player, "猎杀者守卫要追玩家，但 GameManager 没指到 PlayerBasic");
        }

        #region 决策

        protected override GuardIntent DecideIntent(float dt)
        {
            // 自己撞上来的蟑螂不用冲刺也能咬死
            TryMeleeCockroach();

            // 1. 冲刺中：只盯锁定的那一个目标，中途换目标会让方向来回甩
            if (TryTickDash())
            {
                return new GuardIntent(GuardStance.Engage, AngleTo(Position, _dashTarget.Value.Position),
                    settings.DashSpeed, settings.TurnSpeed);
            }

            // 2. 不在冲刺：选目标，冷却好了就开冲
            if (!TryPickChaseTarget(out ChaseTarget target))
            {
                return PatrolIntent(dt);
            }

            if (_dashCooldown <= 0f)
            {
                BeginDash(target);
            }

            float speed = _dashTarget.HasValue ? settings.DashSpeed : settings.PatrolSpeed;
            return new GuardIntent(GuardStance.Engage, AngleTo(Position, target.Position), speed, settings.TurnSpeed);
        }

        protected override void TickTimers(float dt)
        {
            if (_dashCooldown > 0f)
            {
                _dashCooldown = Mathf.Max(0f, _dashCooldown - dt);
            }

            if (!_dashTarget.HasValue)
            {
                return;
            }

            _dashTimer -= dt;
            if (_dashTimer <= 0f)
            {
                FinishDash();
            }
        }

        protected override void OnNightActiveChanged(bool active)
        {
            if (!active)
            {
                // 白天不该留着一段没跑完的冲刺，下一个夜晚从干净状态开始
                _dashTarget = null;
                _dashTimer = 0f;
            }
        }

        #endregion

        #region 追击目标

        /// <summary>
        /// 选一个追击目标。玩家可见就优先玩家（除非关掉 PreferPlayer，此时按距离比），
        /// 否则取视野里最近的蟑螂。
        /// </summary>
        private bool TryPickChaseTarget(out ChaseTarget target)
        {
            Vector2 position = Position;

            bool playerVisible = TryGetPlayerPosition(out Vector2 playerPosition) && CanSee(playerPosition);
            CockroachAgent cockroach = PickNearestCockroach(position, settings.VisionRange, includeTouching: true);

            bool preferPlayer = playerVisible &&
                                (killer.PreferPlayer
                                 || cockroach == null
                                 || (playerPosition - position).sqrMagnitude <= (cockroach.Position - position).sqrMagnitude);

            if (preferPlayer)
            {
                target = ChaseTarget.ForPlayer(playerPosition);
                return true;
            }

            if (cockroach != null)
            {
                target = ChaseTarget.ForCockroach(cockroach);
                return true;
            }

            target = default;
            return false;
        }

        /// <summary>
        /// 推进当前冲刺，返回 true = 还在冲刺（调用方应继续盯住目标）。
        /// 目标没了就收冲进冷却，不会一直空转。
        /// </summary>
        private bool TryTickDash()
        {
            if (!_dashTarget.HasValue)
            {
                return false;
            }

            if (!TryRefreshDashTarget(out ChaseTarget target))
            {
                FinishDash();
                return false;
            }

            TryDashAttack(target);
            return _dashTarget.HasValue;
        }

        /// <summary>刷新锁定目标的位置；目标没了返回 false</summary>
        private bool TryRefreshDashTarget(out ChaseTarget target)
        {
            Assert.IsTrue(_dashTarget.HasValue);

            ChaseTarget current = _dashTarget.Value;

            if (current.IsPlayer)
            {
                if (!TryGetPlayerPosition(out Vector2 playerPosition))
                {
                    target = current;
                    return false;
                }

                target = ChaseTarget.ForPlayer(playerPosition);
            }
            else
            {
                // 冲刺途中允许追出视野 / 追出区域，只看它还活没活着
                if (!IsTargetable(current.Cockroach))
                {
                    target = current;
                    return false;
                }

                target = ChaseTarget.ForCockroach(current.Cockroach);
            }

            _dashTarget = target;
            return true;
        }

        private void BeginDash(ChaseTarget target)
        {
            Assert.IsTrue(_dashCooldown <= 0f, "冲刺冷却还没好就开冲了");
            Assert.IsFalse(_dashTarget.HasValue, "冲刺还没结束又开了一次");

            _dashTarget = target;
            _dashTimer = settings.DashDuration;
        }

        /// <summary>结束冲刺并进冷却。冲刺命中、目标丢失、被卡住、时间到都走这里</summary>
        private void FinishDash()
        {
            if (!_dashTarget.HasValue)
            {
                return;
            }

            _dashTarget = null;
            _dashTimer = 0f;
            _dashCooldown = settings.DashCooldown;
        }

        #endregion

        #region 攻击判定

        /// <summary>冲刺攻击：只有冲刺才会调到这里，所以对玩家的伤害一次冲刺最多一次</summary>
        private void TryDashAttack(ChaseTarget target)
        {
            float reach = settings.BodyRadius + (target.IsPlayer ? killer.PlayerHitReach : killer.KillReach);
            if ((target.Position - Position).sqrMagnitude > reach * reach)
            {
                return;
            }

            if (target.IsPlayer)
            {
                HitPlayer();
            }
            else
            {
                KillCockroach(target.Cockroach);
            }
        }

        /// <summary>贴脸的蟑螂直接咬死：不需要冲刺，也不吃冲刺冷却</summary>
        private void TryMeleeCockroach()
        {
            CockroachAgent victim = PickNearestCockroach(Position, settings.BodyRadius + killer.KillReach, includeTouching: true);
            if (victim != null)
            {
                KillCockroach(victim);
            }
        }

        private void KillCockroach(CockroachAgent victim)
        {
            if (!IsTargetable(victim))
            {
                return;
            }

            victim.Die();       // 危险信息素爆发在 Die() 里做

            CockroachesInSight.Remove(victim);
            CockroachesTouching.Remove(victim);

            // 咬死的正是冲刺锁定的目标：这次冲刺已经达成目的，收掉进冷却
            if (_dashTarget.HasValue && !_dashTarget.Value.IsPlayer && _dashTarget.Value.Cockroach == victim)
            {
                FinishDash();
            }
        }

        private void HitPlayer()
        {
            PlayerHealth health = ResolvePlayerHealth();
            Assert.IsNotNull(health, "猎杀者守卫要伤害玩家，但 PlayerBasic 上没有 PlayerHealth");
            if (health == null)
            {
                return;
            }

            health.AddDamage(killer.PlayerDamage);
            FinishDash();       // 一次冲刺只给一次伤害
        }

        private static PlayerHealth ResolvePlayerHealth()
        {
            PlayerBasic player = GameManager.Player;
            return player == null ? null : player.PlayerHealth;
        }

        private static bool TryGetPlayerPosition(out Vector2 position)
        {
            PlayerBasic player = GameManager.Player;
            if (player == null)
            {
                position = default;
                return false;
            }

            position = CockroachAgent.FlatOf(player.transform.position);
            return true;
        }

        #endregion

        #region 卡住

        protected override void OnMovementStalled()
        {
            // 冲不动了（撞在桌子角 / 墙里）就别把整段冲刺耗在推墙上
            FinishDash();
            base.OnMovementStalled();
        }

        #endregion

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (!Application.isPlaying)
            {
                return;
            }

            Gizmos.color = IsDashing ? new Color(1f, 0.15f, 0.15f, 0.8f) : new Color(0.8f, 0.8f, 0.8f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, settings.BodyRadius + killer.KillReach);

            if (!_dashTarget.HasValue)
            {
                return;
            }

            Vector2 dashTarget = _dashTarget.Value.Position;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, new Vector3(dashTarget.x, transform.position.y, dashTarget.y));
        }
    }
}
