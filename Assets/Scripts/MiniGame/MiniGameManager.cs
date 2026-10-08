using System;
using System.Collections.Generic;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TapTapGameJam.MiniGame
{
    /// <summary>
    /// MiniGameManager负责统筹管理所有的MiniGame，每新增一个任务，只需要实现抽象类MiniGameCandidate并且注册在下方的_candidatePool当中即可
    /// </summary>
    public class MiniGameManager : MonoBehaviour
    {
        [SerializeField]private List<MiniGameCandidate> _candidatePool = new();
        private readonly List<MiniGameInstanceBasic> _tasks = new(); // 记录当前已经生成的任务
        public IReadOnlyList<MiniGameInstanceBasic> Tasks => _tasks;
        public event Action<MiniGameInstanceBasic> TaskAdded;
        public event Action<MiniGameInstanceBasic> TaskRemoved;
        public void RegisterCandidate(MiniGameCandidate candidate)
        {
            _candidatePool.RemoveAll(item => item == null);
            if (candidate != null && !_candidatePool.Contains(candidate)) _candidatePool.Add(candidate);
            if (_isDayTime) EnsureDailyActivities();
        }

        // 小游戏目前只在白天刷新，所以需要_isDayTime去控制，游戏阶段为其他时间段时停止
        private bool _isDayTime=false;

        [Header("刷新时间配置")]
        [Tooltip("当本次刷新成功刷新时，距离下次刷新的基准值")]
        [SerializeField] private float _spawnTimeOnSuccess=12f;
        [Tooltip("刷新成功时，刷新时间相较于基准值的最大浮动值")]
        [SerializeField] private float _MaxDiffTime=2f;
        [Tooltip("刷新失败时，距离下次刷新的时间差值")]
        [SerializeField] private float _spawnTimeOnDefeat=1f;
        private float _spawnTime = 0f; // 刷新时间

        private void Update()
        {
            if (_isDayTime == false)
            {
                return;
            }
            EnsureDailyActivities();
            _spawnTime -= Time.deltaTime;
            if (_spawnTime < 0f)
            {
                if (TrySpawnTask())
                {
                    // 如果任务生成成功
                    _spawnTime = _spawnTimeOnSuccess + Random.Range(-_MaxDiffTime, _MaxDiffTime);
                }
                else
                {
                    _spawnTime = _spawnTimeOnDefeat;
                }
            }

        }

        private void OnEnable()
        {
            // 订阅此事件是为了切换_isDayTime，除了白天，其他时间段不刷新任务
            // 同时，从DayTime切换为其他时间段时，也要清空当前任务列表
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged); 
            EventPipeline.Subscribe<EventGameOver>(HandleGameOver);
        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.UnSubscribe<EventGameOver>(HandleGameOver);
            _isDayTime = false;
            CancelAll();
        }
        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            if (evt.NewPhase == GamePhase.Day)
            {
                bool enteringDay = !_isDayTime;
                // 如果是白天，那么这个小游戏管理器就要开始重新起作用了
                CancelAll();
                _spawnTime = 0f;
                _isDayTime = true;
                if (enteringDay)
                    foreach (var candidate in _candidatePool)
                        if (candidate != null) candidate.BeginNewDay();
                EnsureDailyActivities();
            }
            else
            {
                // 如果是商店时间或者夜晚，这个小游戏管理器就停止起作用
                _isDayTime = false;
                CancelAll();
            }
        }
        private bool TrySpawnTask()
        {
            if (_isDayTime == false)
            {
                // 以防万一，如果不是白天就拒绝生成任务
                return false;
            }

            var available = new List<MiniGameCandidate>();

            foreach(var candidate in _candidatePool)
            {
                if(candidate!=null&&!candidate.IsDailyActivity&&candidate.isActiveAndEnabled&&candidate.CanSpawn)
                {
                    available.Add(candidate);
                }
            }

            if (available.Count == 0)
            {
                Debug.Log($"[MiniGameManager]：没有可生成的任务");
                return false;
            }

            // 随机取一个
            int index = Random.Range(0, available.Count);
            MiniGameInstanceBasic task = available[index].CreateTask();
            if (task == null)
                return false;
            AddTask(task);
            return true;
        }
        private void AddTask(MiniGameInstanceBasic task)
        {
            if (task == null || task.IsFinished || _tasks.Contains(task)) return;
            task.EventFinished += RemoveTask;

            _tasks.Add(task);
            TaskAdded?.Invoke(task);
        }
        private void EnsureDailyActivities()
        {
            foreach (var candidate in _candidatePool)
                if (candidate != null && candidate.IsDailyActivity && candidate.isActiveAndEnabled && candidate.CanSpawn)
                    AddTask(candidate.CreateTask());
        }
        private void RemoveTask(MiniGameInstanceBasic task)
        {
            if (!_tasks.Remove(task)) return;
            task.EventFinished -= RemoveTask;
            TaskRemoved?.Invoke(task);
        }

        private void CancelAll()
        {
            foreach (var task in _tasks.ToArray())
            {
                task.Cancel();
                // 兼容尚未在 Cancel 中发送结束通知的实例。
                RemoveTask(task);
            }
        }
        private void HandleGameOver(EventGameOver evt) { _isDayTime = false; CancelAll(); }
    }

}
