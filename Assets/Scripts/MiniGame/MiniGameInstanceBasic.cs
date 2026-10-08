using System;
using UnityEngine;
using TapTapGameJam.Player;

namespace TapTapGameJam.MiniGame
{
    public abstract class MiniGameInstanceBasic
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string GuideText;
        public int RewardCoins { get; }
        public int RewardTalentPoints { get; }
        public bool IsFinished { get; private set; }
        public bool WasSuccessful { get; private set; }
        public virtual bool IsDailyActivity => false;
        public virtual string AvailabilityText => "白天开放";
        public Transform TargetPosition;
        public event Action<MiniGameInstanceBasic> EventTargetChanged;
        public event Action<MiniGameInstanceBasic> EventStageChanged;
        public event Action<MiniGameInstanceBasic> EventFinished;
        protected MiniGameInstanceBasic(Transform guideTarget, int rewardCoins = 0, int rewardTalentPoints = 0)
        {
            TargetPosition = guideTarget;
            RewardCoins = Math.Max(0, rewardCoins);
            RewardTalentPoints = Math.Max(0, rewardTalentPoints);
        }
        public void NotifyTargetChanged(Transform transform)
        {
            if (IsFinished) return;
            TargetPosition = transform;
            EventTargetChanged?.Invoke(this);
        }

        public void NotifyStageChanged()
        {
            if (IsFinished) return;
            EventStageChanged?.Invoke(this);
        }

        public void NotifyFinished()
        {
            Cancel();
        }

        public void Cancel() => Finish(false, null);

        protected void Complete(PlayerResources resources)
        {
            if (resources != null) Finish(true, resources);
        }

        private void Finish(bool success, PlayerResources resources)
        {
            if (IsFinished) return;
            // 清理期间的回调和奖励事件都不能再次结算当前实例。
            IsFinished = true;
            WasSuccessful = success;
            TargetPosition = null;
            try
            {
                Release();
                if (success)
                {
                    if (RewardCoins > 0) resources.AddCoins(RewardCoins);
                    if (RewardTalentPoints > 0) resources.AddTalentPoint(RewardTalentPoints);
                    OnSuccess(resources);
                }
            }
            finally { EventFinished?.Invoke(this); }
        }

        protected virtual void OnSuccess(PlayerResources resources) { }
        protected abstract void Release();
    }

}
