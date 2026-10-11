using System.Collections.Generic;
using TapTapGameJam.MiniGame;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class DeliverFileQuest : MiniGameCandidate
    {
        [SerializeField, Min(0)] private int _rewardCoins = 1;
        [SerializeField, Min(0)] private int _rewardTalentPoints = 5;
        [SerializeField, Min(1)] private int _maxActiveTasks = 1;
        private readonly List<MiniGameInstanceBasic> _instances = new();

        public int MaxActiveTasks
        {
            get => Mathf.Max(1, _maxActiveTasks);
            set => _maxActiveTasks = Mathf.Max(1, value);
        }

        private void OnEnable()
        {
            TapTapGameJam.Events.EventPipeline.Subscribe<TapTapGameJam.Events.GamePhaseChangedEvent>(HandlePhase);
            TapTapGameJam.Events.EventPipeline.Subscribe<TapTapGameJam.Events.EventGameOver>(HandleGameOver);
        }
        private void OnDisable()
        {
            TapTapGameJam.Events.EventPipeline.UnSubscribe<TapTapGameJam.Events.GamePhaseChangedEvent>(HandlePhase);
            TapTapGameJam.Events.EventPipeline.UnSubscribe<TapTapGameJam.Events.EventGameOver>(HandleGameOver);
            CancelAll();
        }
        private void CancelAll() { foreach (var task in _instances.ToArray()) task.Cancel(); }
        private void HandleEnded(MiniGameInstanceBasic task) { task.EventFinished -= HandleEnded; _instances.Remove(task); }
        private void HandlePhase(TapTapGameJam.Events.GamePhaseChangedEvent evt) => CancelAll();
        private void HandleGameOver(TapTapGameJam.Events.EventGameOver evt) => CancelAll();
        
        public override bool CanSpawn => _instances.Count < MaxActiveTasks && CanSpawnNewTask();

        [Tooltip("可以生成拿文件的地方")]
        [SerializeField] private List<FileSourcePoint> _sourcePool = new();
        [Tooltip("可以生成送文件目的地的地方")]
        [SerializeField] private List<FileDestinationPoint> _destinationPool = new();



        private bool CanSpawnNewTask()
        {
            Debug.Log($"step4：送文件任务判定开始");
            var availableSource = new List<FileSourcePoint>();

            foreach (var fileSource in _sourcePool)
            {
                if (fileSource != null && fileSource.isActiveAndEnabled && fileSource.CanUse)
                {
                    availableSource.Add(fileSource);
                }
            }

            if (availableSource.Count == 0)
            {
                Debug.Log($"step4：送文件任务判定失败，没有可用的任务接取点");
                // 说明目前没有任务接取点可以生成
                return false;
            }

            var availableDestination = new List<FileDestinationPoint>();

            foreach (var fileDestination in _destinationPool)
            {
                if (fileDestination != null && fileDestination.isActiveAndEnabled && fileDestination.CanUse)
                {
                    availableDestination.Add(fileDestination);
                }
            }

            if (availableDestination.Count == 0)
            {
                Debug.Log($"step4：送文件任务判定失败，没有可用的任务提交点");
                // 说明目前没有任务提交点可以生成
                return false;
            }
            return true;
        }
        public override MiniGameInstanceBasic CreateTask()
        {
            if (_instances.Count >= MaxActiveTasks) return null;
            var availableSource = new List<FileSourcePoint>();

            foreach (var fileSource in _sourcePool)
            {
                if (fileSource != null && fileSource.isActiveAndEnabled && fileSource.CanUse)
                {
                    availableSource.Add(fileSource);
                }
            }


            var availableDestination = new List<FileDestinationPoint>();

            foreach (var fileDestination in _destinationPool)
            {
                if (fileDestination != null && fileDestination.isActiveAndEnabled && fileDestination.CanUse)
                {
                    availableDestination.Add(fileDestination);
                }
            }
            if (availableSource.Count == 0 || availableDestination.Count == 0) return null;
            int sourceIndex = Random.Range(0, availableSource.Count);
            int destinationIndex = Random.Range(0, availableDestination.Count);

            // 添加此事件

            DeliveryFileInstance instance = new DeliveryFileInstance(
                availableSource[sourceIndex],
                availableDestination[destinationIndex], _rewardCoins, _rewardTalentPoints
                );
            _instances.Add(instance);
            instance.EventFinished += HandleEnded;
            instance.Begin();
            
            return instance;
        }

    }

}
