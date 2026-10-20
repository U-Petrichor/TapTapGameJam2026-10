using System;
using System.Collections.Generic;
using TapTapGameJam.Events;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    /// <summary>
    /// Common lifecycle for a panel task that can choose one point from a pool.
    /// It deliberately does not assume that a task has only one point.
    /// </summary>
    public abstract class PanelMiniGameQuest : MiniGameCandidate
    {
        [SerializeField, Min(0)] protected int _rewardCoins = 30;
        [SerializeField, Min(0)] protected int _rewardTalentPoints = 1;
        [SerializeField, Min(1)] private int _maxActiveTasks = 1;

        private readonly List<PanelMiniGameInstance> _activeTasks = new();

        public int MaxActiveTasks
        {
            get => Mathf.Max(1, _maxActiveTasks);
            set => _maxActiveTasks = Mathf.Max(1, value);
        }

        public int ActiveTaskCount => _activeTasks.Count;
        public IReadOnlyList<PanelMiniGameInstance> ActiveTasks => _activeTasks;

        protected abstract IEnumerable<PanelTaskPoint> GetPoints();
        protected abstract PanelMiniGameInstance CreateInstance(PanelTaskPoint point);

        public override bool CanSpawn =>
            isActiveAndEnabled &&
            _activeTasks.Count < MaxActiveTasks &&
            FindAvailablePoint() != null;

        public void ConfigureRewards(int coins, int talent)
        {
            _rewardCoins = Mathf.Max(0, coins);
            _rewardTalentPoints = Mathf.Max(0, talent);
        }

        public override MiniGameInstanceBasic CreateTask()
        {
            if (!isActiveAndEnabled || _activeTasks.Count >= MaxActiveTasks)
                return null;

            return CreateTaskAt(FindAvailablePoint());
        }

        /// <summary>
        /// 教程用：在指定点位创建任务。点位不可用、或该类任务已达上限时返回 null。
        /// </summary>
        public PanelMiniGameInstance CreateTaskAt(PanelTaskPoint point)
        {
            if (!isActiveAndEnabled || _activeTasks.Count >= MaxActiveTasks)
                return null;
            if (point == null || !point.CanStartTask)
                return null;

            PanelMiniGameInstance instance = CreateInstance(point);
            if (instance == null)
                return null;

            _activeTasks.Add(instance);
            instance.EventFinished += HandleEnded;
            if (instance.Begin())
                return instance;

            // Begin normally emits EventFinished when it cannot acquire the
            // point. Keep this fallback for custom instances that do not.
            RemoveActive(instance);
            return null;
        }

        protected virtual void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.Subscribe<EventGameOver>(HandleGameOver);
        }

        protected virtual void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.UnSubscribe<EventGameOver>(HandleGameOver);
            CancelAll();
        }

        protected void CancelAll()
        {
            foreach (var task in _activeTasks.ToArray())
                task.Cancel();
            _activeTasks.Clear();
        }

        private PanelTaskPoint FindAvailablePoint()
        {
            var seen = new HashSet<PanelTaskPoint>();
            var points = GetPoints();
            if (points == null)
                return null;

            foreach (var point in points)
            {
                if (point != null && seen.Add(point) && point.CanStartTask)
                    return point;
            }

            return null;
        }

        private void HandleEnded(MiniGameInstanceBasic task)
        {
            task.EventFinished -= HandleEnded;
            if (task is PanelMiniGameInstance panelTask)
                RemoveActive(panelTask);
        }

        private void RemoveActive(PanelMiniGameInstance task)
        {
            _activeTasks.Remove(task);
            task.EventFinished -= HandleEnded;
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt) => CancelAll();
        private void HandleGameOver(EventGameOver evt) => CancelAll();
    }
}
