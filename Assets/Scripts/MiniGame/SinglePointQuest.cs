using TapTapGameJam.Events;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public abstract class SinglePointQuest : MiniGameCandidate
    {
        [SerializeField, Min(0)] protected int _rewardCoins = 30;
        [SerializeField, Min(0)] protected int _rewardTalentPoints = 1;
        private PanelMiniGameInstance _currentTask;
        protected abstract PanelTaskPoint Point { get; }
        protected abstract PanelMiniGameInstance CreateInstance();
        public override bool CanSpawn => isActiveAndEnabled && _currentTask == null && Point != null && Point.CanStartTask;
        public void ConfigureRewards(int coins, int talent) { _rewardCoins = Mathf.Max(0, coins); _rewardTalentPoints = Mathf.Max(0, talent); }

        protected virtual void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.Subscribe<EventGameOver>(HandleGameOver);
        }
        protected virtual void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.UnSubscribe<EventGameOver>(HandleGameOver);
            CancelTask();
        }
        public override MiniGameInstanceBasic CreateTask()
        {
            if (!CanSpawn) return null;
            var instance = CreateInstance();
            _currentTask = instance;
            instance.EventFinished += HandleEnded;
            if (!instance.Begin()) { CancelTask(); return null; }
            return instance;
        }
        public void CancelTask() => _currentTask?.Cancel();
        private void HandleEnded(MiniGameInstanceBasic task)
        {
            task.EventFinished -= HandleEnded;
            if (_currentTask == task) _currentTask = null;
        }
        private void HandlePhaseChanged(GamePhaseChangedEvent evt) => CancelTask();
        private void HandleGameOver(EventGameOver evt) => CancelTask();
    }
}
