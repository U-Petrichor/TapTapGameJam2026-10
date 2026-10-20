using System;
using System.Collections;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.MiniGame;
using UnityEngine;

namespace TapTapGameJam.Tutorial
{
    /// <summary>
    /// 保证第一天第一个任务是“擦污渍”：先暂停随机刷新，等老板提示关闭后生成指定的 Clean 任务。
    /// </summary>
    public sealed class TutorialTaskBootstrap : MonoBehaviour
    {
        [SerializeField] private MiniGameManager manager;
        [SerializeField] private CleanQuest cleanQuest;
        [SerializeField] private CleanPoint firstPoint;
        private GamePhaseManager phaseManager;

        private void Awake()
        {
            if (manager == null) manager = FindFirstObjectByType<MiniGameManager>();
            if (cleanQuest == null) cleanQuest = FindFirstObjectByType<CleanQuest>();
            if (firstPoint == null) firstPoint = FindPreferredCleanPoint();
            phaseManager = FindFirstObjectByType<GamePhaseManager>();

            // GamePhaseManager publishes the first Day event before this component's Start
            // coroutine has waited for DayIndex. Pause in Awake so daily tasks cannot appear
            // ahead of the tutorial's forced first cleaning task.
            if (manager != null && !PlayerProfile.HasSeen(TutorialStepIds.Day1TaskGuide))
                manager.SetTutorialSpawningPaused(true);
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<EventTutorialSignal>(HandleSignal);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventTutorialSignal>(HandleSignal);
        }

        private IEnumerator Start()
        {
            if (manager == null)
                yield break;

            if (PlayerProfile.HasSeen(TutorialStepIds.Day1TaskGuide))
            {
                ReleaseSpawning();
                yield break;
            }

            // 等 GamePhaseManager 恢复存档/进入第一天后再判断，避免在 DayIndex 还是 0 时误判，
            // 也避免在 Day 事件触发前生成任务、随后被 HandlePhaseChanged 的 CancelAll 清掉。
            float deadline = Time.unscaledTime + 5f;
            while (phaseManager != null && phaseManager.DayIndex < 1 && Time.unscaledTime < deadline)
                yield return null;

            if (phaseManager == null || phaseManager.DayIndex < 1)
            {
                ReleaseSpawning();
                yield break;
            }

            if (phaseManager.DayIndex > 1)
            {
                // 老存档从中途继续：不要再卡住第一天的任务流程。
                ReleaseSpawning();
                yield break;
            }

            // 玩家在“首次移动”弹窗关闭后、任务生成前退出过：下次进场景补生成。
            if (PlayerProfile.HasSeen(TutorialStepIds.Day1FirstMove))
                SpawnFirstTask();
        }

        private void HandleSignal(EventTutorialSignal evt)
        {
            if (evt.Key == TutorialSignals.TaskSpawnFirst) SpawnFirstTask();
            else if (evt.Key == TutorialSignals.TaskRelease) ReleaseSpawning();
        }

        private void SpawnFirstTask()
        {
            if (manager == null) manager = FindFirstObjectByType<MiniGameManager>();
            if (cleanQuest == null) cleanQuest = FindFirstObjectByType<CleanQuest>();
            if (firstPoint == null) firstPoint = FindPreferredCleanPoint();

            if (manager == null || cleanQuest == null || firstPoint == null)
            {
                Debug.LogWarning("[Tutorial] 找不到首个清洁任务所需的引用，取消教程任务限制。", this);
                ReleaseSpawning();
                return;
            }

            MiniGameInstanceBasic task = cleanQuest.CreateTaskAt(firstPoint);
            if (task == null)
            {
                Debug.LogWarning("[Tutorial] 首个清洁任务创建失败，回退到随机刷新。", this);
                ReleaseSpawning();
                return;
            }

            if (manager.AddExternalTask(task) == null)
            {
                Debug.LogWarning("[Tutorial] 首个清洁任务未能登记到 MiniGameManager。", this);
            }
        }

        private void ReleaseSpawning()
        {
            if (manager != null) manager.SetTutorialSpawningPaused(false);
        }

        private static CleanPoint FindPreferredCleanPoint()
        {
            CleanPoint[] points = FindObjectsByType<CleanPoint>(FindObjectsSortMode.None);
            CleanPoint fallback = null;

            foreach (CleanPoint point in points)
            {
                if (point == null) continue;
                if (fallback == null) fallback = point;
                if (point.name.IndexOf("MopWater", StringComparison.OrdinalIgnoreCase) >= 0)
                    return point;
            }

            return fallback;
        }
    }
}
