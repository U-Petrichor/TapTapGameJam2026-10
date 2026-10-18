using TapTapGameJam.Audio;
using System.Collections.Generic;
using TalentTree.Effects;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using UnityEngine;

namespace TalentTree
{
    /// <summary>玩家在当前游戏运行中的天赋进度与操作入口。</summary>
    [RequireComponent(typeof(PlayerResources))]
    public class PlayerTalentTree : MonoBehaviour, ITalentLevelSource
    {
        [Tooltip("绑定配置好的天赋树")]
        [SerializeField] private TalentTreeAsset _talentTreeAsset;
        [Tooltip("玩家天赋点资源所在文件")]
        [SerializeField] private PlayerResources _playerResources;
        [Tooltip("当前进程")]
        [SerializeField] private TalentProgress _progress = new();

        private TalentEffectResolver _effectResolver;

        public TalentTreeAsset TalentTreeAsset => _talentTreeAsset;
        public PlayerResources PlayerResources => _playerResources;
        public TalentEffectResolver Effects => _effectResolver ??= new TalentEffectResolver(this);

        #region ITalentLevelSource

        /// <summary>给 <see cref="TalentEffectResolver"/> 用的等级来源。等价于 <see cref="TalentTreeAsset"/>。</summary>
        public TalentTreeAsset Asset => _talentTreeAsset;

        #endregion

        private void Awake()
        {
            if (_playerResources == null) 
                _playerResources = GetComponent<PlayerResources>();
            if (_progress == null) 
                _progress = new TalentProgress();
            _progress.RebuildCache();

            // 必须是 ??=，不能无条件 new：
            // 同一物体上组件的 Awake 顺序不保证，PlayerBuffs 可能先跑到并已经拿走了这个
            // resolver 的引用。这里再新建一个的话，PlayerBuffs 手里的那个就没人 Invalidate 了，
            // 它会一直返回「第一次算出来的效果」——表现就是「天赋点了没效果」。
            _effectResolver ??= new TalentEffectResolver(this);
        }

        public int GetLevel(string talentId) => GetLevel(_talentTreeAsset?.FindTalent(talentId));

        public int GetLevel(TalentData talent) => _progress?.GetLevel(talent) ?? 0;

        public TalentOperationResult TryUpgrade(string talentId)
            => TryUpgrade(_talentTreeAsset?.FindTalent(talentId));

        public bool CanUnlock(string talentId, out string reason)
            => CanUnlock(_talentTreeAsset?.FindTalent(talentId), out reason);

        public bool CanUnlock(TalentData talent, out string reason)
            => CanUnlock(talent, out _, out reason);

        private bool CanUnlock(TalentData talent, out TalentOperationStatus status, out string reason)
        {
            status = TalentOperationStatus.Success;
            reason = string.Empty;
            if (talent == null)
            {
                status = TalentOperationStatus.TalentNotFound;
                reason = "找不到天赋。";
                return false;
            }

            if (GetLevel(talent) > 0)
            {
                status = TalentOperationStatus.AlreadyUnlocked;
                reason = "该天赋已经解锁。";
                return false;
            }

            return CanUpgrade(talent, out status, out reason);
        }

        public TalentOperationResult TryUnlock(string talentId)
            => TryUnlock(_talentTreeAsset?.FindTalent(talentId));

        public TalentOperationResult TryUnlock(TalentData talent)
        {
            if (!CanUnlock(talent, out TalentOperationStatus status, out string reason))
                return TalentOperationResult.Fail(status, reason);

            TalentLevelData levelData = talent.GetLevelData(1);
            int cost = levelData?.TalentPointCost ?? 0;
            if (cost > 0 && (_playerResources == null || !_playerResources.TrySpendTalentPoint(cost)))
                return TalentOperationResult.Fail(TalentOperationStatus.InsufficientTalentPoints, "天赋点不足。");

            _progress.SetLevel(talent, 1);
            // 必须在发事件之前失效：订阅方（PlayerBuffs）收到事件就会立刻重算效果
            Effects.Invalidate();
            EventPipeline.Publish(new TalentLevelChangedEvent(talent, 0, 1));
            Sfx.Play(SoundId.TalentUpgrade, this, 1);
            return TalentOperationResult.Success(0, 1, -cost);
        }

        public TalentOperationResult TryUpgrade(TalentData talent)
        {
            if (!CanUpgrade(talent, out TalentOperationStatus status, out string reason))
                return TalentOperationResult.Fail(status, reason);

            int oldLevel = GetLevel(talent);
            int targetLevel = oldLevel + 1;
            TalentLevelData levelData = talent.GetLevelData(targetLevel);

            int cost = levelData.TalentPointCost;
            if (cost > 0 && (_playerResources == null || !_playerResources.TrySpendTalentPoint(cost)))
                return TalentOperationResult.Fail(TalentOperationStatus.InsufficientTalentPoints, "天赋点不足。");

            _progress.SetLevel(talent, targetLevel);
            Effects.Invalidate();
            EventPipeline.Publish(new TalentLevelChangedEvent(talent, oldLevel, targetLevel));
            Sfx.Play(SoundId.TalentUpgrade, this, targetLevel);
            return TalentOperationResult.Success(oldLevel, targetLevel, -cost);
        }

        public bool CanRemove(string talentId, out string reason)
            => CanRemove(_talentTreeAsset?.FindTalent(talentId), out reason);

        public bool CanRemove(TalentData talent, out string reason)
        {
            reason = string.Empty;
            if (talent == null)
            {
                reason = "找不到天赋。";
                return false;
            }

            if (GetLevel(talent) <= 0)
            {
                reason = "该天赋尚未解锁。";
                return false;
            }

            if (!TalentGraphEvaluator.CanRemove(_talentTreeAsset, this, talent, out reason)) return false;
            return !HasActiveRequirementReference(talent, out reason);
        }

        public TalentOperationResult TryRemove(string talentId)
            => TryRemove(_talentTreeAsset?.FindTalent(talentId));

        public TalentOperationResult TryRemove(TalentData talent)
        {
            if (!CanRemove(talent, out string reason))
            {
                TalentOperationStatus status = talent == null
                    ? TalentOperationStatus.TalentNotFound
                    : GetLevel(talent) <= 0
                        ? TalentOperationStatus.NotUnlocked
                        : TalentOperationStatus.RemovalBlockedByDependency;
                return TalentOperationResult.Fail(status, reason);
            }

            int oldLevel = GetLevel(talent);
            int refund = SpentPointsFor(talent, oldLevel);
            _progress.SetLevel(talent, 0);
            Effects.Invalidate();
            if (refund > 0) _playerResources?.AddTalentPoint(refund);
            EventPipeline.Publish(new TalentLevelChangedEvent(talent, oldLevel, 0));
            Sfx.Play(SoundId.TalentRefund, this);
            return TalentOperationResult.Success(oldLevel, 0, refund);
        }

        public int TryResetAll()
        {
            if (_progress == null) return 0;
            var entries = new List<(TalentData Talent, int Level)>(_progress.Enumerate());
            if (entries.Count == 0) return 0;

            int refund = 0;
            foreach ((TalentData talent, int level) in entries)
                refund += SpentPointsFor(talent, level);

            foreach ((TalentData talent, _) in entries)
                _progress.SetLevel(talent, 0);
            Effects.Invalidate();

            if (refund > 0) _playerResources?.AddTalentPoint(refund);
            foreach ((TalentData talent, int level) in entries)
                EventPipeline.Publish(new TalentLevelChangedEvent(talent, level, 0));
            Sfx.Play(SoundId.TalentRefund, this);
            return refund;
        }

        public bool CanUpgrade(string talentId, out string reason)
            => CanUpgrade(_talentTreeAsset?.FindTalent(talentId), out _, out reason);

        public bool CanUpgrade(TalentData talent, out string reason)
            => CanUpgrade(talent, out _, out reason);

        private bool CanUpgrade(TalentData talent, out TalentOperationStatus status, out string reason)
        {
            status = TalentOperationStatus.Success;
            reason = string.Empty;
            if (talent == null)
            {
                status = TalentOperationStatus.TalentNotFound;
                reason = "找不到天赋。";
                return false;
            }

            int oldLevel = GetLevel(talent);
            int targetLevel = oldLevel + 1;
            if (targetLevel > talent.MaxLevel)
            {
                status = TalentOperationStatus.AlreadyMaxLevel;
                reason = "天赋已经达到最高等级。";
                return false;
            }

            if (oldLevel == 0 && !TalentGraphEvaluator.CanUnlock(_talentTreeAsset, this, talent, out reason))
            {
                status = TalentOperationStatus.GraphRuleNotMet;
                return false;
            }

            TalentLevelData levelData = talent.GetLevelData(targetLevel);
            if (!TalentRequirementEvaluator.AreMet(levelData, this, out reason))
            {
                status = TalentOperationStatus.RequirementNotMet;
                return false;
            }

            int cost = levelData?.TalentPointCost ?? 0;
            if (cost > 0 && (_playerResources == null || _playerResources.TalentPoint < cost))
            {
                status = TalentOperationStatus.InsufficientTalentPoints;
                reason = "天赋点不足。";
                return false;
            }
            return true;
        }

        private bool HasActiveRequirementReference(TalentData talent, out string reason)
        {
            reason = string.Empty;
            if (_talentTreeAsset == null) return false;

            foreach (TalentNodeData node in _talentTreeAsset.Nodes)
            {
                TalentData dependent = node?.Data;
                if (dependent == null || dependent == talent) continue;
                int currentLevel = GetLevel(dependent);
                if (currentLevel <= 0) continue;

                TalentLevelData currentData = dependent.GetLevelData(currentLevel);
                if (currentData?.Requirements == null) continue;
                foreach (TalentRequirement requirement in currentData.Requirements)
                {
                    if (requirement == null || !requirement.References(talent, out int minimumLevel) || minimumLevel <= 0) continue;
                    reason = $"无法解除：天赋「{dependent.DisplayName}」需要「{talent.DisplayName}」达到 {minimumLevel} 级。";
                    return true;
                }
            }

            return false;
        }

        private static int SpentPointsFor(TalentData talent, int level)
        {
            if (talent == null || level <= 0) return 0;
            int total = 0;
            int maxLevel = Mathf.Min(level, talent.MaxLevel);
            for (int current = 1; current <= maxLevel; current++)
                total += talent.GetLevelData(current)?.TalentPointCost ?? 0;
            return total;
        }
    }
}
