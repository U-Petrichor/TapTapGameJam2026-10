using System.Collections.Generic;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class MiniGameTaskManager : MonoBehaviour
    {
        [SerializeField]private List<MiniGameTaskCandidate> _candidatePool = new();
        private bool _isDayTime=false;
        private float _spawnTime = 0f;
        private readonly List<MiniGameTask> _tasks = new();
        public IReadOnlyList<MiniGameTask> Tasks => _tasks;
        private void Update()
        {
            Debug.Log($"step1：进入循环");
            if (_isDayTime == false)
            {
                return;
            }
            _spawnTime -= Time.deltaTime;
            if (_spawnTime < 0f)
            {
                if (TrySpawnTask())
                {
                    // 如果任务生成成功
                    // 下个任务在10秒到15秒内刷新
                    _spawnTime = Random.Range(10f, 15f);
                }
                else
                {
                    // 如果生成失败，三秒后重试，一般是不会失败的
                    _spawnTime = 3f;
                }
            }

        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }
        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            if (evt.NewPhase == GamePhase.Day)
            {
                // 如果是白天，那么这个小游戏管理器就要开始重新起作用了
                foreach(var x in _tasks)
                {
                    RemoveTask(x);
                }
                _tasks.Clear();
                _isDayTime = true;
            }
            else
            {
                // 如果是商店时间或者夜晚，这个小游戏管理器就停止起作用
                _isDayTime = false;
            }
        }
        private bool TrySpawnTask()
        {
            if (_isDayTime == false)
            {
                // 还是以防万一，如果不是白天就拒绝生成任务
                Debug.Log($"step3：TrySpawnTask执行失败，居然是因为现在不是白天");
                return false;
            }

            var available = new List<MiniGameTaskCandidate>();

            foreach(var candidate in _candidatePool)
            {
                if(candidate!=null&&candidate.CanSpawn)
                {
                    available.Add(candidate);
                }
            }

            if (available.Count == 0)
            {
                // 说明目前没有任务可以生成
                Debug.Log($"step3：TrySpawnTask执行失败，因为没有可生成的任务");
                return false;
            }

            // 随机取一个
            int index = Random.Range(0, available.Count);
            MiniGameTask task = available[index].CreateTask();
            Debug.Log($"step3：TrySpawnTask执行成功");
            AddTask(task);
            return true;
        }
        private void AddTask(MiniGameTask task)
        {
            task.EventTargetChanged += HandleTaskChanged;
            task.EventStageChanged += HandleTaskChanged;
            task.EventFinished += RemoveTask;

            _tasks.Add(task);
        }
        private void RemoveTask(MiniGameTask task)
        {
            task.EventTargetChanged -= HandleTaskChanged;
            task.EventStageChanged -= HandleTaskChanged;
            task.EventFinished -= RemoveTask;

            _tasks.Remove(task);
        }
        private void HandleTaskChanged(MiniGameTask task)
        {

        }
    }

}