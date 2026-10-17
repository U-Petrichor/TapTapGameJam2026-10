using TapTapGameJam.Audio;
using System;
using System.Collections.Generic;
using UnityEngine;
using TapTapGameJam.Interactables;

namespace TapTapGameJam.Player
{
    [RequireComponent(typeof(PlayerBasic))]
    [RequireComponent(typeof(PlayerInputManager))]
    public class PlayerInteractionManager : MonoBehaviour
    {
        [field: SerializeField] private PlayerBasic _player;
        [field: SerializeField] private PlayerInputManager _inputManager;
        private readonly List<Interactable> _candidates = new();
        public IReadOnlyList<Interactable> Candidates => _candidates;
        public bool HasInteractionTargets => _candidates.Count > 0;
        public string CurrentInteractionPrompt => _currentTarget != null ? _currentTarget.InteractionPrompt : string.Empty;
        private Interactable _currentTarget;
        private bool _isUIVisible;

        // 新 UI 只接收显示、隐藏和文字通知，不维护交互对象列表。
        public event Action EventShowInteractionUI;
        public event Action EventHideInteractionUI;
        public event Action<string> EventInteractionPromptChanged;

        // 保留旧事件以兼容仍使用旧选择 UI 的场景。
        public event Action<Interactable> EventTargetChanged;
        public event Action<Interactable> EventObjectAdded;
        public event Action<Interactable> EventObjectDeleted;

        private void Awake()
        {
            if (_player == null) _player = GetComponent<PlayerBasic>();
            if (_inputManager == null) _inputManager = GetComponent<PlayerInputManager>();
        }

        private void OnEnable()
        {
            // 订阅交互按键
            _inputManager.EventOnInteractPress += HandleInteract;
            // 订阅 Q 切换交互目标
            _inputManager.EventOnInteractionSwitchPress += HandleSwitchInteraction;
        }
        private void OnDisable()
        {
            _inputManager.EventOnInteractPress -= HandleInteract;
            _inputManager.EventOnInteractionSwitchPress -= HandleSwitchInteraction;
        }

        private void Update()
        {
            // 玩家一动就强制刷新：最近的候选成为 InteractPending，正在进行的交互被打断。
            if (_inputManager != null && _inputManager.HasMoveInput)
            {
                RefreshInteraction(true);
            }
        }

        public void Register(Interactable interactable)
        {
            if (interactable == null || _candidates.Contains(interactable)) return;

            _candidates.Add(interactable);
            EventObjectAdded?.Invoke(interactable);

            // 只是别的物体进了范围：焦点没换人就不该打断手里的持续交互
            RefreshInteraction(false);
        }

        public void Unregister(Interactable interactable)
        {
            int deletedIndex = _candidates.IndexOf(interactable);
            if (deletedIndex < 0) return;

            _candidates.RemoveAt(deletedIndex);
            EventObjectDeleted?.Invoke(interactable);

            // 离开候选列表的对象不能再停在 Pending / Interacting，否则它自己的持续逻辑会一直跑下去。
            if (interactable != null) interactable.InteractState = InteractState.InteractIdle;

            // 同理：别人离开时如果焦点还是原来那个，它正在进行的交互不该被中断
            RefreshInteraction(false);
        }

        /// <summary>
        /// 重算候选与聚焦：距离玩家最近的候选变成 InteractPending，其余回到 InteractIdle。
        /// 刷新一定会覆盖 Q 手动切换的结果；
        /// breakInteracting = true（玩家移动）时无条件打断正在进行的交互；
        /// false（别的物体注册/注销）时只有焦点真的换了对象才算打断。
        /// </summary>
        public void RefreshInteraction(bool breakInteracting)
        {
            RemoveDestroyedCandidates();

            if (_candidates.Count == 0)
            {
                SetFocus(null, breakInteracting);
                if (_isUIVisible)
                {
                    _isUIVisible = false;
                    EventHideInteractionUI?.Invoke();
                }
                return;
            }

            SetFocus(FindNearest(), breakInteracting);
            if (!_isUIVisible)
            {
                _isUIVisible = true;
                EventShowInteractionUI?.Invoke();
            }
        }

        /// <summary>
        /// 设置聚焦对象：它变 InteractPending，其余候选回到 InteractIdle。
        /// 焦点换人时，原来那个会走"非焦点"分支被打回 InteractIdle，也就是被中断了。
        /// </summary>
        private void SetFocus(Interactable target, bool breakInteracting)
        {
            // Unity 的 == 重载会把已销毁对象判成 null，先把这个"假 null"清掉，后面才好比较。
            if (_currentTarget == null) _currentTarget = null;

            foreach (Interactable candidate in _candidates)
            {
                if (candidate == null) continue;

                if (candidate != target)
                {
                    candidate.InteractState = InteractState.InteractIdle;
                    continue;
                }

                // 焦点对象正在持续交互，且这次刷新不要求打断，就让它继续跑
                if (!breakInteracting && candidate.InteractState == InteractState.Interacting) continue;

                candidate.InteractState = InteractState.InteractPending;
            }

            if (_currentTarget == target) return;

            _currentTarget = target;
            EventInteractionPromptChanged?.Invoke(CurrentInteractionPrompt);
            EventTargetChanged?.Invoke(_currentTarget);
        }

        public void HandleInteract(InteractKey key)
        {
            if (_currentTarget == null) return; // 没有聚焦对象，什么都不做

            if (!_currentTarget.CanInteract(_player))
            {
                Sfx.Play(SoundId.Error, this);
                Debug.LogError($"当前目标{_currentTarget.InteractionPrompt}无法交互");
                return;
            }

            // 先切成 Interacting 再交给对象执行：它在这一帧里就能读到自己的状态
            _currentTarget.InteractState = InteractState.Interacting;
            int feedback = Sfx.FeedbackSequence;
            _currentTarget.Interact(_player, key);
            if (feedback == Sfx.FeedbackSequence && !(_currentTarget is TapTapGameJam.Items.BlackBox))
                Sfx.Play(SoundId.Click, this);
        }

        public void HandleSwitchInteraction()
        {
            if (_candidates.Count <= 1) return;

            // Q 按距离从近到远依次聚焦（次近、更远…），最远的下一次回到最近的。
            // 这个聚焦是临时的，下一次刷新（移动 / 注册 / 注销）会自动覆盖回"最近"。
            Interactable next = FindNextNearest(_currentTarget);
            if (next != null && next != _currentTarget) { SetFocus(next, true); Sfx.Play(SoundId.Switch, this); }
        }

        /// <summary>让特殊交互流程（例如手持巢穴）立即成为当前聚焦对象，效果等同一次 Q 切换。</summary>
        public bool SelectInteractionTarget(Interactable interactable)
        {
            if (interactable == null || _candidates.IndexOf(interactable) < 0) return false;

            SetFocus(interactable, true);
            return true;
        }

        /// <summary>丢弃已经销毁、但没来得及 Unregister 的候选（例如食物被吃空后 Destroy）。</summary>
        private void RemoveDestroyedCandidates()
        {
            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                if (_candidates[i] == null) _candidates.RemoveAt(i);
            }
        }

        private Interactable FindNearest()
        {
            Interactable nearest = null;
            float nearestSqr = float.MaxValue;
            foreach (Interactable candidate in _candidates)
            {
                if (candidate == null) continue;
                float sqr = SqrDistanceTo(candidate);
                if (nearest == null || sqr < nearestSqr)
                {
                    nearest = candidate;
                    nearestSqr = sqr;
                }
            }
            return nearest;
        }

        /// <summary>比当前聚焦更远的最近一个（次近 → 更远）；已经没有更远的就回到最近的那个。</summary>
        private Interactable FindNextNearest(Interactable current)
        {
            float currentSqr = current != null ? SqrDistanceTo(current) : float.MaxValue;
            Interactable next = null;
            float nextSqr = float.MaxValue;

            foreach (Interactable candidate in _candidates)
            {
                if (candidate == null || candidate == current) continue;
                float sqr = SqrDistanceTo(candidate);
                if (sqr <= currentSqr) continue; // 更近或同距的都跳过
                if (next == null || sqr < nextSqr)
                {
                    next = candidate;
                    nextSqr = sqr;
                }
            }

            return next != null ? next : FindNearest();
        }

        /// <summary>只比水平距离，避免轴心高度更高的物体被误判成更远。</summary>
        private float SqrDistanceTo(Interactable target)
        {
            Vector3 self = transform.position;
            Vector3 targetPosition = target.transform.position;
            float dx = self.x - targetPosition.x;
            float dz = self.z - targetPosition.z;
            return dx * dx + dz * dz;
        }
    }
}
