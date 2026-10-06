using System.Collections.Generic;
using System.Threading.Tasks;
using TapTapGameJam.MiniGame;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class DeliverFileQuest : MiniGameTaskCandidate
    {
        private class Deliver
        {
            public FileSource Source;
            public FileDestination Destination;
            public MiniGameTask Task;
            private PlayerResources _playerResources;
            private int _rewardCoins;
            private int _rewardTalentPoints;
            public Deliver(FileSource source,FileDestination destination,MiniGameTask task)
            {
                Source = source;
                Destination = destination;
                Task = task;

                _rewardCoins = Random.Range(25, 35);
                _rewardTalentPoints = 1;
            }
            public void Begin()
            {
                // 先声明整两个地点已经被占用
                Source.IsFree = false;
                Destination.IsFree = false;

                Source.EventFileSourceFinish += HandleSourceFinished;
                Destination.EventFileDestinationFinish += HandleDestinationFinished;

                Source.StartTask();
            }
            private void HandleSourceFinished()
            {

                // 领取阶段结束，不再监听这个起点。
                Source.EventFileSourceFinish -= HandleSourceFinished;

                // 让目的地可以交互
                Destination.StartTask();

                // 更新当前任务的指引位置
                Task.NotifyTargetChanged(Destination.transform);
                Task.NotifyStageChanged();
            }

            private void HandleDestinationFinished(PlayerBasic player)
            {
                _playerResources = player.PlayerResources;
                End(true);
            }

            public void Cancel()
            {
                End(false);
            }

            private void End(bool success)
            {
                if(success == true)
                {
                    // 任务已经完成
                    if (_playerResources == null)
                        Debug.Log($"PlayerResource=为空");
                    _playerResources.AddCoins(_rewardCoins);
                    _playerResources.AddTalentPoint(_rewardTalentPoints);
                }
                else
                {
                    // 任务可能是在中途取消，取订所有事件
                    Source.EventFileSourceFinish -= HandleSourceFinished;
                    Destination.EventFileDestinationFinish -= HandleDestinationFinished;
                }
                Source.IsFree = true;
                Destination.IsFree = true;
            }
        }
        public override bool CanSpawn => CanSpawnNewTask();

        [Tooltip("可以生成拿文件的地方")]
        [SerializeField] private List<FileSource> _sourcePool = new();
        [Tooltip("可以生成送文件目的地的地方")]
        [SerializeField] private List<FileDestination> _destinationPool = new();


        private List<Deliver> _delivers = new List<Deliver>();

        private bool CanSpawnNewTask()
        {
            Debug.Log($"step4：送文件任务判定开始");
            var availableSource = new List<FileSource>();

            foreach (var fileSource in _sourcePool)
            {
                if (fileSource != null && fileSource.CanUse)
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

            var availableDestination = new List<FileDestination>();

            foreach (var fileDestination in _destinationPool)
            {
                if (fileDestination != null && fileDestination.IsFree)
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
        public override MiniGameTask CreateTask()
        {
            var availableSource = new List<FileSource>();

            foreach (var fileSource in _sourcePool)
            {
                if (fileSource != null && fileSource.IsFree)
                {
                    availableSource.Add(fileSource);
                }
            }
            int sourceIndex = Random.Range(0, availableSource.Count);


            var availableDestination = new List<FileDestination>();

            foreach (var fileDestination in _destinationPool)
            {
                if (fileDestination != null && fileDestination.IsFree)
                {
                    availableDestination.Add(fileDestination);
                }
            }
            int destinationIndex = Random.Range(0, availableSource.Count);

            // 添加此事件

            Deliver deliver = new Deliver(
                availableSource[sourceIndex],
                availableDestination[destinationIndex],
                new MiniGameTask(availableSource[sourceIndex].transform)
                );
            _delivers.Add(deliver);
            deliver.Begin();
            
            return deliver.Task;
        }

    }

}