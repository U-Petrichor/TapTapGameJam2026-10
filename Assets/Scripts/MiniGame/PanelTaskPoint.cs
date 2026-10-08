using System;
using TapTapGameJam.Events;
using TapTapGameJam.Interactable;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    // 场景交互入口：维护点位占用，不负责奖励或小游戏玩法。
    public abstract class PanelTaskPoint : MiniGameInteractable
    {
        [SerializeField] private Collider _detectCollider;
        [SerializeField, Min(0)] private int _miniGameIndex;
        private bool _hasTask;
        private bool _isPlaying;
        private int _version;
        private PlayerBasic _player;
        private bool _attemptStarted;

        protected abstract int DefaultGameIndex { get; }
        protected virtual bool FinishOnClose => false;
        protected virtual bool KeepAvailableAfterAttempt => false;
        public bool CanStartTask => isActiveAndEnabled && _detectCollider != null && !_hasTask;
        public event Action<PlayerBasic> EventCompleted;
        public event Action EventTaskCancelled;
        public event Action EventAttemptStarted;
        public event Action<bool> EventAttemptFinished;

        public void ConfigureGameIndex(int index)
        {
            _miniGameIndex = index;
            if (_detectCollider == null) _detectCollider = GetComponent<Collider>();
        }

        protected virtual void Reset()
        {
            _miniGameIndex = DefaultGameIndex;
            _detectCollider = GetComponent<Collider>();
        }

        protected virtual void Awake()
        {
            if (_detectCollider == null) _detectCollider = GetComponent<Collider>();
            if (_detectCollider != null) _detectCollider.enabled = false;
            else Debug.LogError($"[{GetType().Name}] 请配置检测 Collider。", this);
        }

        public bool StartTask()
        {
            if (!CanStartTask) return false;
            _version++;
            _hasTask = true;
            _detectCollider.enabled = true;
            return true;
        }

        public override bool CanInteract(PlayerBasic player) =>
            isActiveAndEnabled && _hasTask && !_isPlaying && player != null && player.PlayerResources != null;

        public override void Interact(PlayerBasic player)
        {
            if (!CanInteract(player)) return;
            _player = player;
            _isPlaying = true;
            _attemptStarted = false;
            int version = ++_version;
            EventPipeline.Publish(new EventOpenMiniGamePanel(_miniGameIndex,
                success => OnFinish(version, success), player, this, () =>
                {
                    if (this == null || !_hasTask || !_isPlaying || version != _version || _attemptStarted) return;
                    _attemptStarted = true;
                    EventAttemptStarted?.Invoke();
                }));
        }

        private void OnFinish(int version, bool success)
        {
            if (this == null || !_hasTask || !_isPlaying || version != _version) return;
            PlayerBasic player = _player;
            bool started = _attemptStarted;
            _attemptStarted = false;
            _player = null;
            _isPlaying = false;
            if (success)
            {
                if (KeepAvailableAfterAttempt) _version++;
                else ClosePoint();
                EventCompleted?.Invoke(player);
            }
            else if (KeepAvailableAfterAttempt) _version++;
            else if (FinishOnClose) CancelTask();
            if (started) EventAttemptFinished?.Invoke(success);
        }

        public void CancelTask()
        {
            bool hadTask = _hasTask;
            bool wasPlaying = _isPlaying;
            ClosePoint();
            if (wasPlaying) EventPipeline.Publish(new EventRequestCloseMiniGamePanel(this));
            if (hadTask) EventTaskCancelled?.Invoke();
        }

        private void ClosePoint()
        {
            _version++;
            _hasTask = false;
            _isPlaying = false;
            _player = null;
            ClearAllRegister();
            if (_detectCollider != null) _detectCollider.enabled = false;
        }

        protected virtual void OnDisable() => CancelTask();
    }
}
