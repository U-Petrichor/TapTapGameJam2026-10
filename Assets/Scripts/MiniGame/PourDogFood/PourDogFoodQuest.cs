using TapTapGameJam.Events;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    // 只有一个喂狗点，同时最多生成一个任务。
    public class PourDogFoodQuest : MiniGameCandidate
    {
        [SerializeField] private PourDogFood _pourDogFood;
        [SerializeField, Min(0)] private int _rewardCoins = 30;
        [SerializeField, Min(0)] private int _rewardTalentPoints = 1;
        private MiniGameInstanceBasic _currentTask;

        public override bool CanSpawn => isActiveAndEnabled && _currentTask == null
            && _pourDogFood != null && _pourDogFood.CanStartTask;

        private void Awake()
        {
            // 两个组件挂在同一个物体时，可以自动找到交互组件。
            if (_pourDogFood == null) _pourDogFood = GetComponent<PourDogFood>();
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            CancelTask();
        }

        public override MiniGameInstanceBasic CreateTask()
        {
            if (!CanSpawn)
                return null;

            // 指引位置始终是同一个狗粮盆，不需要选择起点或终点。
            _currentTask = new MiniGameInstanceBasic(_pourDogFood.transform)
            {
                GuideText = "倒狗粮",
                RewardCoins = _rewardCoins,
                RewardTalentPoints = _rewardTalentPoints
            };
            _currentTask.EventFinished += HandleInstanceEnded;
            _pourDogFood.EventPourDogFoodFinish += HandleFinished;
            _pourDogFood.EventTaskCancelled += HandleCancelled;
            if (!_pourDogFood.StartTask())
            {
                ReleaseTask();
                return null;
            }
            return _currentTask;
        }

        private void HandleFinished(PlayerBasic player)
        {
            MiniGameInstanceBasic task = ReleaseTask();
            if (task == null) return;

            // 先释放占用并通知管理器，再发放奖励。
            task.NotifyFinished();
            if (player != null && player.PlayerResources != null)
            {
                player.PlayerResources.AddCoins(task.RewardCoins);
                player.PlayerResources.AddTalentPoint(task.RewardTalentPoints);
            }
        }

        private void HandleCancelled()
        {
            // 交互物体被禁用时，也要从任务管理器的列表中移除。
            MiniGameInstanceBasic task = ReleaseTask();
            task?.NotifyFinished();
        }

        public void CancelTask()
        {
            MiniGameInstanceBasic task = ReleaseTask();
            if (task == null) return;
            if (_pourDogFood != null) _pourDogFood.CancelTask();
            task.NotifyFinished();
        }

        private MiniGameInstanceBasic ReleaseTask()
        {
            MiniGameInstanceBasic task = _currentTask;
            _currentTask = null;
            if (task != null) task.EventFinished -= HandleInstanceEnded;
            if (_pourDogFood != null)
            {
                _pourDogFood.EventPourDogFoodFinish -= HandleFinished;
                _pourDogFood.EventTaskCancelled -= HandleCancelled;
            }
            return task;
        }

        private void HandleInstanceEnded(MiniGameInstanceBasic task)
        {
            if (task == _currentTask) CancelTask();
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            // 阶段切换清理旧任务，下一天重新参与随机生成。
            CancelTask();
        }
    }
}
