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
        public string CurrentInteractionPrompt => _currentTarget?.InteractionPrompt ?? string.Empty;
        private Interactable _currentTarget;
        private int _currentTargetIndex = 0;

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
        public void Register(Interactable interactable)
        {
            if (interactable == null || _candidates.Contains(interactable)) return;

            // 1. 加入队列
            _candidates.Add(interactable);
            EventObjectAdded?.Invoke(interactable);
            // 2. 如果这个是第一个，则额外配置
            if (_candidates.Count == 1)
            {
                _currentTargetIndex = 0; // 索引归0
                SetCurrentTarget(interactable); // 当前指向的单位改为这个
                EventShowInteractionUI?.Invoke();
            }
        }
        public void Unregister(Interactable interactable)
        {
            // 事先记录要删除的这个所处的索引位置
            int deletedIndex = _candidates.IndexOf(interactable);
            if (deletedIndex < 0) return;
            // 删除的是不是当前指向的目标
            bool isCurrentTarget = interactable == _currentTarget;

            // 先从候选列表移除，再更新当前目标和 UI。
            _candidates.RemoveAt(deletedIndex);
            EventObjectDeleted?.Invoke(interactable);

            // 如果删完后为空
            if (_candidates.Count == 0)
            {
                _currentTargetIndex = 0;
                SetCurrentTarget(null);
                EventHideInteractionUI?.Invoke();
                return;
            }

            // 删除当前目标之前的对象时，保持目标不变，只修正索引。
            if (deletedIndex < _currentTargetIndex)
            {
                _currentTargetIndex -= 1;
                return;
            }
            // 当前目标消失后选择原位置的下一个；原目标在末尾则选择前一个。
            if (isCurrentTarget)
            {
                _currentTargetIndex = Math.Min(_currentTargetIndex, _candidates.Count - 1);
                SetCurrentTarget(_candidates[_currentTargetIndex]);
            }
        }
        private void SetCurrentTarget(Interactable target)
        {
            if (_currentTarget == target)
            {
                return;
            }

            // 只有当前选中的对象处于待交互状态
            if (_currentTarget != null) _currentTarget.IsPendingInteract = false;
            _currentTarget = target;
            if (_currentTarget != null) _currentTarget.IsPendingInteract = true;

            EventInteractionPromptChanged?.Invoke(CurrentInteractionPrompt);
            EventTargetChanged?.Invoke(_currentTarget);
        }
        public void HandleInteract()
        {
            if (_currentTarget != null)
            {
                if (_currentTarget.CanInteract(_player))
                {
                    _currentTarget.Interact(_player);
                }
                else
                {
                    Debug.LogError($"当前目标{_currentTarget.InteractionPrompt}无法交互");
                }
            }
            // else分支为无可交互对象，什么都不做
        }
        public void HandleSwitchInteraction()
        {
            if (_candidates.Count <= 1) return;

            // Q 每次只向下切换一个，到末尾后回到第一个。
            _currentTargetIndex = (_currentTargetIndex + 1) % _candidates.Count;
            SetCurrentTarget(_candidates[_currentTargetIndex]);
        }

        /// <summary>让特殊交互流程（例如手持巢穴）立即成为当前 E 目标。</summary>
        public bool SelectInteractionTarget(Interactable interactable)
        {
            int index = _candidates.IndexOf(interactable);
            if (index < 0)
            {
                return false;
            }

            _currentTargetIndex = index;
            SetCurrentTarget(interactable);
            return true;
        }
    }
}
