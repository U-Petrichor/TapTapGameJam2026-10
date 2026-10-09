using UnityEngine;

// 见 Guard.cs 顶部说明：Cockroach 在本命名空间里既是命名空间又是实体类型，必须走别名
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫变体：猎杀者。
    ///
    /// 行为：平时和基类一样慢速巡逻 + 左右扫视；一旦视野（扇形 trigger）里出现蟑螂，
    /// 就盯住它、冷却好了立刻起冲（<see cref="GuardSettings.DashSpeed"/>），
    /// 身体 trigger 碰到蟑螂就扑杀，杀完进入 <see cref="GuardSettings.DashCooldown"/> 冷却。
    ///
    /// 只覆写 <see cref="Decide"/> 与两个速度钩子，移动/避障/转向全部复用基类。
    /// </summary>
    public class KillerGuard : Guard
    {
        /// <summary>&gt; 0 表示正在冲刺</summary>
        float _dashTimer;

        /// <summary>冲刺冷却，冷却中不追杀</summary>
        float _dashCd;

        /// <summary>冲刺中锁定的目标。跟丢（死了/进巢了）就收冲</summary>
        CockroachAgent _chaseTarget;

        /// <summary>正在冲刺</summary>
        public bool IsDashing => _dashTimer > 0f;

        /// <summary>冲刺冷却剩余秒数</summary>
        public float DashCooldownRemaining => Mathf.Max(0f, _dashCd);

        protected override void TickTimers(float dt)
        {
            base.TickTimers(dt);

            if (_dashCd > 0f) _dashCd -= dt;
        }

        protected override void Decide(float dt, CockroachAgent target)
        {
            bool dashing = _dashTimer > 0f;

            // 冲刺期间盯死一个目标：中途换目标会让冲刺方向来回甩，看起来像抽搐
            if (dashing)
            {
                if (_chaseTarget == null)
                {
                    _dashTimer = 0f;        // 目标跟丢了就收
                    dashing = false;
                }
                else
                {
                    DesiredAngle = AngleTo(Position, _chaseTarget.Position);
                    _dashTimer -= dt;
                    return;
                }
            }

            if (target != null)
            {
                _chaseTarget = target;
                DesiredAngle = AngleTo(Position, target.Position);

                // 冷却好了就起冲
                if (_dashCd <= 0f)
                {
                    _dashTimer = settings.DashDuration;
                    dashing = true;
                }
            }
            else
            {
                _chaseTarget = null;
                Scan(dt);
            }

            // 贴脸了就杀（冲刺或在冷却中都不影响"撞上即死"）
            if (!dashing && _dashCd <= 0f)
            {
                TryKill();
            }
        }

        protected override float CurrentSpeed => _dashTimer > 0f ? settings.DashSpeed : settings.PatrolSpeed;

        /// <summary>
        /// 身体 trigger 里碰到蟑螂就杀，然后进冷却。
        /// 杀完顺手把它从两份名单里摘掉，免得冷却期间一直对着尸体。
        /// </summary>
        void TryKill()
        {
            CockroachAgent victim = PickNearest(CockroachesTouching);
            if (victim == null) return;

            victim.Die();       // 危险信息素爆发在 Die() 里做
            CockroachesTouching.Remove(victim);
            CockroachesInSight.Remove(victim);
            _chaseTarget = null;
            _dashTimer = 0f;
            _dashCd = settings.DashCooldown;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (!Application.isPlaying) return;

            // 冲刺中把撞击判定球也画出来，方便调数值
            Gizmos.color = IsDashing ? new Color(1f, 0.15f, 0.15f, 0.8f) : new Color(0.8f, 0.8f, 0.8f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, settings.BodyRadius);
        }
    }
}
