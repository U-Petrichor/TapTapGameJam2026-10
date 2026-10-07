using System;
using TapTapGameJam.Events;
using TapTapGameJam.Interactable;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class PourDogFood : AbstractInteractable
    {
        public override string InteractionPrompt => "倒狗粮";
        [SerializeField] private Collider _detectCollider;
        [SerializeField, Min(0)] private int _miniGameIndex = 1;
        private bool _hasTask = false;
        private bool _isPlaying = false;
        private int _taskVersion = 0;
        private PlayerBasic _player;

        public bool CanStartTask => isActiveAndEnabled && _detectCollider != null && !_hasTask;
        public event Action<PlayerBasic> EventPourDogFoodFinish;
        public event Action EventTaskCancelled;

        private void Awake()
        {
            if (_detectCollider == null)
            {
                Debug.LogError($"[PourDogFood]:没有配置DetectCollider");
            }
            else
            {
                // 尚未生成任务时，玩家不能与这个点交互。
                _detectCollider.enabled = false;
            }
        }

        public bool StartTask()
        {
            if (!CanStartTask)
                return false;

            _taskVersion++;
            _hasTask = true;
            _isPlaying = false;
            _player = null;
            _detectCollider.enabled = true;
            return true;
        }

        public override bool CanInteract(PlayerBasic player)
        {
            // 必须先生成任务；小游戏打开期间不重复接收交互。
            return isActiveAndEnabled && _hasTask && !_isPlaying && player != null;
        }

        public override void Interact(PlayerBasic player)
        {
            if (!CanInteract(player))
                return;

            _player = player;
            _isPlaying = true;
            int taskVersion = _taskVersion;
            // 只负责打开对应小游戏，是否完成由UI判断并回调。
            EventPipeline.Publish<EventOpenMiniGamePanel>(
                new EventOpenMiniGamePanel(_miniGameIndex,
                    isFinish => OnFinish(taskVersion, isFinish)));
        }

        private void OnFinish(int taskVersion, bool isFinish)
        {
            // 旧任务的回调不能完成后来新生成的任务。
            if (this == null || !_hasTask || !_isPlaying || taskVersion != _taskVersion)
                return;

            PlayerBasic player = _player;
            _player = null;
            _isPlaying = false;

            // 关闭窗口只结束这次尝试，任务仍然保留。
            if (!isFinish)
                return;

            _hasTask = false;
            _taskVersion++;
            CloseDetector();
            EventPourDogFoodFinish?.Invoke(player);
        }

        public void CancelTask()
        {
            bool hadTask = _hasTask;
            bool wasPlaying = _isPlaying;
            _hasTask = false;
            _isPlaying = false;
            _player = null;
            _taskVersion++;
            CloseDetector();

            if (wasPlaying)
            {
                // 先清掉任务状态，再关闭窗口，返回的旧回调会被忽略。
                EventPipeline.Publish<EventCloseMiniGamePanel>(new EventCloseMiniGamePanel());
            }
            if (hadTask) EventTaskCancelled?.Invoke();
        }

        private void CloseDetector()
        {
            ClearAllRegister();
            if (_detectCollider != null) _detectCollider.enabled = false;
        }

        private void OnDisable()
        {
            CancelTask();
        }
    }
}
