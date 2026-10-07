using System.Collections.Generic;
using System.Threading.Tasks;
using TapTapGameJam.MiniGame;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class DeliverFileQuest : MiniGameCandidate
    {
        
        public override bool CanSpawn => CanSpawnNewTask();

        [Tooltip("可以生成拿文件的地方")]
        [SerializeField] private List<FileSource> _sourcePool = new();
        [Tooltip("可以生成送文件目的地的地方")]
        [SerializeField] private List<FileDestination> _destinationPool = new();



        private bool CanSpawnNewTask()
        {
            Debug.Log($"step4：送文件任务判定开始");
            var availableSource = new List<FileSource>();

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

            var availableDestination = new List<FileDestination>();

            foreach (var fileDestination in _destinationPool)
            {
                if (fileDestination != null && fileDestination.isActiveAndEnabled && fileDestination.IsFree)
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
            var availableSource = new List<FileSource>();

            foreach (var fileSource in _sourcePool)
            {
                if (fileSource != null && fileSource.isActiveAndEnabled && fileSource.CanUse)
                {
                    availableSource.Add(fileSource);
                }
            }


            var availableDestination = new List<FileDestination>();

            foreach (var fileDestination in _destinationPool)
            {
                if (fileDestination != null && fileDestination.isActiveAndEnabled && fileDestination.IsFree)
                {
                    availableDestination.Add(fileDestination);
                }
            }
            if (availableSource.Count == 0 || availableDestination.Count == 0) return null;
            int sourceIndex = Random.Range(0, availableSource.Count);
            int destinationIndex = Random.Range(0, availableDestination.Count);

            // 添加此事件

            DeliveryFileInsance instance = new DeliveryFileInsance(
                availableSource[sourceIndex],
                availableDestination[destinationIndex]
                );
            instance.Begin();
            
            return instance;
        }

    }

}
