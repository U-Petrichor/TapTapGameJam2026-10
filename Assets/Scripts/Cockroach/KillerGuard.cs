using UnityEngine;

// Cockroach 在本命名空间里既是命名空间又是实体类型，直接写会被当成命名空间（CS0118），统一走别名
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫变体：猎杀者。看见蟑螂就盯住并冲刺，靠近就扑杀，杀完进冷却。
    /// 只覆写 <see cref="Decide"/> 和速度钩子。
    /// </summary>
    public class KillerGuard : Guard
    {
        /// <summary>距离兜底时的宽容度，盖住蟑螂自身的碰撞半径</summary>
        const float KillReach = 0.35f;

        /// <summary>&gt; 0 表示冲刺中</summary>
        float _dashTimer;

        /// <summary>冲刺冷却</summary>
        float _dashCd;

        /// <summary>冲刺锁定的目标，跟丢就收冲</summary>
        CockroachAgent _chaseTarget;

        public bool IsDashing => _dashTimer > 0f;

        protected override void TickTimers(float dt)
        {
            if (_dashCd > 0f) _dashCd -= dt;
        }

        protected override void Decide(float dt, CockroachAgent target)
        {
            if (_dashTimer > 0f)
            {
                if (_chaseTarget == null)
                {
                    _dashTimer = 0f;                // 目标跟丢了就收
                }
                else
                {
                    // 冲刺期间盯死同一个目标：中途换目标会让方向来回甩
                    DesiredAngle = AngleTo(Position, _chaseTarget.Position);
                    _dashTimer -= dt;
                    if (_dashCd <= 0f) TryKill();
                    return;
                }
            }

            if (target != null)
            {
                _chaseTarget = target;
                DesiredAngle = AngleTo(Position, target.Position);

                if (_dashCd <= 0f) _dashTimer = settings.DashDuration;
            }
            else
            {
                _chaseTarget = null;
                Scan(dt);
            }

            if (_dashCd <= 0f) TryKill();
        }

        protected override float CurrentSpeed => _dashTimer > 0f ? settings.DashSpeed : settings.PatrolSpeed;

        /// <summary>
        /// 扑杀。身体 trigger 的 overlap 和距离判定两条路都试，谁先命中算谁，
        /// 免得因为物理事件时序问题出现"贴到脸上了还不死"。
        /// </summary>
        void TryKill()
        {
            // 不能写 ?? ：Unity 的"已销毁"对象不等于 C# null，?? 不会兜底
            CockroachAgent victim = PickNearest(CockroachesTouching);
            if (victim == null) victim = PickNearestInRange(settings.BodyRadius + KillReach);
            if (victim == null) return;

            victim.Die();       // 危险信息素爆发在 Die() 里做
            CockroachesTouching.Remove(victim);
            CockroachesInSight.Remove(victim);
            _chaseTarget = null;
            _dashTimer = 0f;
            _dashCd = settings.DashCooldown;
        }

        CockroachAgent PickNearestInRange(float range)
        {
            Vector2 pos = Position;
            float bestSqr = range * range;
            CockroachAgent best = null;

            foreach (CockroachAgent c in CockroachesInSight)
            {
                if (c == null || c.IsInside) continue;

                float d = (c.Position - pos).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = c;
                }
            }

            return best;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (!Application.isPlaying) return;

            Gizmos.color = IsDashing ? new Color(1f, 0.15f, 0.15f, 0.8f) : new Color(0.8f, 0.8f, 0.8f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, settings.BodyRadius);
        }
    }
}
