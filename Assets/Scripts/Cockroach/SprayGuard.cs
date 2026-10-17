using System;
using System.Collections.Generic;
using TapTapGameJam.Core;
using TapTapGameJam.Player;
using UnityEngine;
using UnityEngine.Assertions;

// Cockroach 在本命名空间里既是命名空间又是实体类型，直接写会被当成命名空间（CS0118），统一走别名
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫变体：喷雾。
    /// 发现目标（蟑螂，或者玩家——默认优先玩家）后<b>不追击</b>：原地停下、抬转向速度把视角扭到目标上，
    /// 蓄势 <see cref="SprayGuardSettings.WindupDuration"/> 秒，然后在<b>当前朝向</b>喷出一锥形药雾
    /// （范围和视野完全一致），杀死锥内所有没被墙挡住的蟑螂；玩家站在雾里会中毒。
    ///
    /// 蓄势是"承诺"：目标中途死了 / 钻回巢 / 跑出视野，它仍会朝最后已知位置喷完——
    /// 这正是玩家可以利用的 0.5 秒闪避窗口。
    ///
    /// 全程保持 <see cref="GuardStance.Patrol"/> 立场（不追击、不出区域），
    /// 所以基类的区域约束、回程、巡逻路线都不用为它特殊处理。
    /// </summary>
    public class SprayGuard : Guard
    {
        [Serializable]
        public class SprayGuardSettings
        {
            [Header("蓄势 / 释放")]
            [Tooltip("发现目标后到喷雾打出去的时间（秒）。这段时间原地转向，也是玩家的闪避窗口")]
            public float WindupDuration = 0.5f;

            [Tooltip("蓄势期间的转向倍率（相对 GuardSettings.TurnSpeed）")]
            public float AimTurnMultiplier = 2.5f;

            [Tooltip("两次喷雾之间的冷却（秒），从喷出去那一刻开始算")]
            public float Cooldown = 6f;

            [Header("杀伤")]
            [Tooltip("玩家站在雾里的中毒量（占据体力上限）。<= 0 表示喷雾不伤玩家")]
            public float PlayerPoison = 15f;

            [Tooltip("玩家和蟑螂同时出现在视野里时，优先喷玩家")]
            public bool PreferPlayer = true;

            [Header("雾壳")]
            [Tooltip("雾壳的竖直厚度（世界单位）。薄一点更像贴地弥漫的雾")]
            public float ShellHeight = 0.9f;
        }

        [Header("喷雾守卫数值")]
        public SprayGuardSettings spray = new SprayGuardSettings();

        [Header("喷雾表现")]
        [Tooltip("雾壳的浓度、时间轴、预警灯光。运行时调也能立刻看到效果")]
        public GuardSprayView.Settings visual = new GuardSprayView.Settings();

        /// <summary>蓄势 → 释放；冷却只是个计时器，期间照常沿路线巡逻</summary>
        private enum SprayPhase
        {
            None,
            Aiming,
            Spraying
        }

        /// <summary>这一帧要杀的蟑螂。缓存成字段，避免每次喷雾都分配</summary>
        private static readonly List<CockroachAgent> Victims = new List<CockroachAgent>();

        /// <summary>
        /// 蓄势锁定的目标。玩家一直存在（位置每帧变），蟑螂可能中途死掉 / 钻回巢，
        /// 所以用"是不是玩家"区分，而不是靠引用是不是 null。
        /// </summary>
        private readonly struct SprayTarget
        {
            public readonly bool IsPlayer;
            public readonly CockroachAgent Cockroach;
            public readonly Vector2 Position;

            private SprayTarget(bool isPlayer, CockroachAgent cockroach, Vector2 position)
            {
                IsPlayer = isPlayer;
                Cockroach = cockroach;
                Position = position;
            }

            public static SprayTarget ForPlayer(Vector2 position) => new SprayTarget(true, null, position);

            public static SprayTarget ForCockroach(CockroachAgent cockroach)
                => new SprayTarget(false, cockroach, cockroach.Position);
        }

        private GuardSprayView _view;
        private SprayPhase _phase;
        private float _phaseTimer;
        private float _cooldown;

        /// <summary>蓄势锁定的目标。它没了之后只保留 <see cref="_aimPoint"/>，照样喷完</summary>
        private SprayTarget? _aimTarget;
        private Vector2 _aimPoint;

        public bool IsAiming => _phase == SprayPhase.Aiming;

        public bool IsSpraying => _phase == SprayPhase.Spraying;

        /// <summary>还在冷却里（不能起手）</summary>
        public bool IsCoolingDown => _cooldown > 0f;

        #region 生命周期

        protected override void Awake()
        {
            base.Awake();

            // 视野 / 行动区域没搭起来的话，错误已经报过了，别再建表现层
            if (!IsReady)
            {
                return;
            }

            BuildView();
        }

        private void BuildView()
        {
            var viewObject = new GameObject("SprayCone");
            viewObject.layer = gameObject.layer;

            Transform viewTransform = viewObject.transform;
            viewTransform.SetParent(transform, false);

            _view = viewObject.AddComponent<GuardSprayView>();

            // 喷嘴位置换算到守卫本地坐标：vision 挂在更深的子节点上也能对准
            Vector3 nozzle = transform.InverseTransformPoint(VisionLight.transform.position);
            _view.Bind(VisionLight, nozzle, settings.VisionRange, settings.VisionCone, spray.ShellHeight, visual);
            _view.Hide();
        }

        protected override void OnNightActiveChanged(bool active)
        {
            if (active)
            {
                return;
            }

            // 白天不该留着半截喷雾状态
            _phase = SprayPhase.None;
            _phaseTimer = 0f;
            _cooldown = 0f;
            _aimTarget = null;
            _view.Hide();
        }

        #endregion

        #region 决策

        protected override void TickTimers(float dt)
        {
            if (_cooldown > 0f)
            {
                _cooldown = Mathf.Max(0f, _cooldown - dt);
            }

            switch (_phase)
            {
                case SprayPhase.Aiming:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                    {
                        ReleaseSpray();
                    }

                    break;

                case SprayPhase.Spraying:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                    {
                        _phase = SprayPhase.None;
                        _view.Hide();
                    }

                    break;
            }
        }

        protected override GuardIntent DecideIntent(float dt)
        {
            switch (_phase)
            {
                case SprayPhase.Aiming:
                    TrackAimTarget();
                    return AimIntent();

                case SprayPhase.Spraying:
                    // 喷的时候定住不动，方向就是喷出去的那一下的朝向
                    return new GuardIntent(GuardStance.Patrol, _angle, 0f, settings.TurnSpeed);
            }

            // 不在喷雾流程里：巡逻；冷却好了就看有没有目标
            if (_cooldown <= 0f && TryPickSprayTarget(out SprayTarget target))
            {
                BeginAim(target);
                return AimIntent();
            }

            return PatrolIntent(dt);
        }

        /// <summary>原地瞄准：不动，只扭头</summary>
        private GuardIntent AimIntent()
        {
            return new GuardIntent(GuardStance.Patrol, AngleTo(Position, _aimPoint), 0f,
                settings.TurnSpeed * spray.AimTurnMultiplier);
        }

        /// <summary>
        /// 原地瞄准 / 喷雾时不避障：避障是给"走路"用的，不是给"转头"用的。
        /// 不然贴着墙时守卫会被自己的避障逻辑拧开，喷向墙里。
        /// </summary>
        protected override float ApplyAvoidance(Vector2 position, float desired)
        {
            return _phase == SprayPhase.None ? base.ApplyAvoidance(position, desired) : desired;
        }

        /// <summary>
        /// 选一个喷雾目标：玩家在视野里就优先玩家，否则取锥里最近的蟑螂。
        /// 两者的"能看见"判据完全一致：距离 + 半角 + 网格视线。
        /// </summary>
        private bool TryPickSprayTarget(out SprayTarget target)
        {
            Vector2 position = Position;

            bool playerVisible = TryGetPlayerPosition(out Vector2 playerPosition) && CanSee(playerPosition);
            CockroachAgent cockroach = PickNearestCockroach(position, settings.VisionRange, includeTouching: false);

            bool preferPlayer = playerVisible &&
                                (spray.PreferPlayer
                                 || cockroach == null
                                 || (playerPosition - position).sqrMagnitude <=
                                 (cockroach.Position - position).sqrMagnitude);

            if (preferPlayer)
            {
                target = SprayTarget.ForPlayer(playerPosition);
                return true;
            }

            if (cockroach != null)
            {
                target = SprayTarget.ForCockroach(cockroach);
                return true;
            }

            target = default;
            return false;
        }

        private void BeginAim(SprayTarget target)
        {
            _phase = SprayPhase.Aiming;
            _phaseTimer = Mathf.Max(0f, spray.WindupDuration);
            _aimTarget = target;
            _aimPoint = target.Position;

            _view.ShowAiming();
        }

        /// <summary>
        /// 目标还在视野里就继续跟（玩家会跑，蟑螂也会动）；没了就保持最后位置，照样喷完。
        /// </summary>
        private void TrackAimTarget()
        {
            if (!_aimTarget.HasValue)
            {
                return;
            }

            SprayTarget target = _aimTarget.Value;

            if (target.IsPlayer)
            {
                if (TryGetPlayerPosition(out Vector2 playerPosition) && CanSee(playerPosition))
                {
                    _aimTarget = SprayTarget.ForPlayer(playerPosition);
                    _aimPoint = playerPosition;
                }

                return;
            }

            if (IsTargetable(target.Cockroach) && CanSee(target.Cockroach.Position))
            {
                _aimTarget = SprayTarget.ForCockroach(target.Cockroach);
                _aimPoint = target.Cockroach.Position;
            }
        }

        #endregion

        #region 喷雾结算

        private void ReleaseSpray()
        {
            _phase = SprayPhase.Spraying;
            _phaseTimer = _view.PlaySpray();

            KillCockroachesInCone();
            PoisonPlayerInCone();

            _cooldown = Mathf.Max(0f, spray.Cooldown);
            _aimTarget = null;
        }

        /// <summary>
        /// 锥形范围 == 视野范围，所以直接用视野 trigger 的名单当候选，
        /// 再用 <see cref="Guard.CanSee"/>（距离 + 半角 + 网格视线）复核一遍：
        /// 锥外的不杀，隔着墙的也不杀。
        /// </summary>
        private void KillCockroachesInCone()
        {
            Victims.Clear();

            foreach (CockroachAgent cockroach in CockroachesInSight)
            {
                if (!IsTargetable(cockroach))
                {
                    continue;
                }

                if (!CanSee(cockroach.Position))
                {
                    continue;
                }

                Victims.Add(cockroach);
            }

            // 先收名单再杀：Die() 里会 Destroy，避免边遍历边改集合
            for (int i = 0; i < Victims.Count; i++)
            {
                Victims[i].Die();       // 危险信息素爆发在 Die() 里做
            }

            Victims.Clear();
        }

        private void PoisonPlayerInCone()
        {
            // 0 = 这版喷雾不伤玩家（配置语义，不是兜底）
            if (spray.PlayerPoison <= 0f)
            {
                return;
            }

            PlayerBasic player = GameManager.Player;
            if (player == null)
            {
                return;
            }

            if (!CanSee(CockroachAgent.FlatOf(player.transform.position)))
            {
                return;
            }

            PlayerHealth health = player.PlayerHealth;
            Assert.IsNotNull(health, "喷雾要毒玩家，但 PlayerBasic 上没有 PlayerHealth");
            if (health == null)
            {
                return;
            }

            health.AddPoison(spray.PlayerPoison);
        }

        #endregion

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (!Application.isPlaying || _phase != SprayPhase.Aiming)
            {
                return;
            }

            // 蓄势中：把锁定的落点画出来，方便看它到底在瞄哪
            Gizmos.color = new Color(0.6f, 1f, 0.4f, 0.9f);
            Vector3 aim = new Vector3(_aimPoint.x, transform.position.y + 0.1f, _aimPoint.y);
            Gizmos.DrawWireSphere(aim, 0.15f);
            Gizmos.DrawLine(transform.position, aim);
        }
    }
}
