using System;
using UnityEngine;
using static UnityEngine.CullingGroup;

namespace TapTapGameJam.MiniGame
{
    public class MiniGameInstanceBasic
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string GuideText;
        public int RewardCoins;
        public int RewardTalentPoints;
        public bool IsFinished { get; private set; }
        public Transform TargetPosition;
        public event Action<MiniGameInstanceBasic> EventTargetChanged;
        public event Action<MiniGameInstanceBasic> EventStageChanged;
        public event Action<MiniGameInstanceBasic> EventFinished;
        public MiniGameInstanceBasic(Transform guideTarget)
        {
            TargetPosition = guideTarget;
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
            if (IsFinished) return;
            IsFinished = true;
            TargetPosition = null;
            EventFinished?.Invoke(this);
        }

        public virtual void Cancel()
        {
            NotifyFinished();
        }

    }

}
