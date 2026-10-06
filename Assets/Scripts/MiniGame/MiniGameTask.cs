using System;
using UnityEngine;
using static UnityEngine.CullingGroup;

namespace TapTapGameJam.MiniGame
{
    public class MiniGameTask
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        private Transform _targetPosition;
        public event Action<MiniGameTask> EventTargetChanged;
        public event Action<MiniGameTask> EventStageChanged;
        public event Action<MiniGameTask> EventFinished;
        public MiniGameTask(Transform guideTarget)
        {
            _targetPosition = guideTarget;
        }
        public void NotifyTargetChanged(Transform transform)
        {
            _targetPosition = transform;
            EventTargetChanged?.Invoke(this);
        }

        public void NotifyStageChanged()
        {
            EventStageChanged?.Invoke(this);
        }

        public void NotifyFinished()
        {
            EventFinished?.Invoke(this);
        }

    }

}