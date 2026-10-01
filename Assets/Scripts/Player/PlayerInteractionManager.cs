using System;
using System.Collections.Generic;
using UnityEngine;
using TapTapGameJam.Interactable;

namespace TapTapGameJam.Player
{
    [RequireComponent(typeof(PlayerBasic))]
    [RequireComponent(typeof(PlayerInputManager))]
    public class PlayerInteractionManager : MonoBehaviour
    {
        [field: SerializeField] private PlayerBasic _player;
        [field: SerializeField] private PlayerInputManager _inputManager;
        private List<IInteractable> _candidates = new();
        public IReadOnlyList<IInteractable> Candidates => _candidates;
        private IInteractable _currentTarget;
        private int _currentTargetIndex = 0;

        public event Action<IInteractable> EventTargetChanged;
        public event Action<IInteractable> EventObjectAdded;
        public event Action<IInteractable> EventObjectDeleted;

        private void OnEnable()
        {
            // 订阅交互按键
            _inputManager.OnInteractPress += HandleInteract;
            // 订阅鼠标滚轮滑动
            _inputManager.OnInteractionScroll += HandleMouseScroll;
        }
        public void Register(IInteractable interactable)
        {
            Debug.Log($"在注册了");
            // 1. 加入队列
            _candidates.Add(interactable);
            EventObjectAdded?.Invoke(interactable);
            // 2. 如果这个是第一个，则额外配置
            if (_candidates.Count == 1)
            {
                _currentTargetIndex = 0; // 索引归0
                SetCurrentTarget(interactable); // 当前指向的单位改为这个
            }
        }
        public void Unregister(IInteractable interactable)
        {
            // 事先记录要删除的这个所处的索引位置
            int deletedIndex = _candidates.IndexOf(interactable);
            // 删除的是不是当前指向的目标
            bool isCurrentTarget = interactable == _currentTarget;

            // 先什么都不管，先删除
            _candidates.Remove(interactable);
            EventObjectDeleted?.Invoke(interactable);

            // 如果删完后为空
            if (_candidates.Count == 0)
            {
                _currentTarget = null;
                _currentTargetIndex = 0;
                EventTargetChanged?.Invoke(null);
                return;
            }

            // 如果被删的那个在当前指向的目标之前，那么Index应该减少，但是指向相同的物品
            // 如果在之后，就没有必要变动
            if (deletedIndex < _currentTargetIndex)
            {
                _currentTargetIndex -= 1;
                return;
            }


            // 如果删除的是当前指向的目标，注意之前已经让UI删除了，所以在这个分支需要告诉UI一个新的可指向对象
            if (isCurrentTarget)
            {
                // 并且原先指向的是最后一个
                if (_currentTargetIndex == _candidates.Count)
                {
                    _currentTargetIndex -= 1;
                }
                // else 删除的是当前指向的，但不是最后一个，那么Index不变，但是指向的物品变了
            }
            // 如果删除的不是指向的目标，还要分在之前和在之后
            SetCurrentTarget(_candidates[_currentTargetIndex]);


        }
        private void SetCurrentTarget(IInteractable target)
        {
            if (target == null)
            {
                Debug.LogError($"PlayerInteractionManager：传入的当前指向对象为null");
                return;
            }
            if (_currentTarget == target)
            {
                return;
            }

            _currentTarget = target;
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
        public void HandleMouseScroll(int direction)
        {
            if (_candidates.Count == 0) return;
            else
            {
                _currentTargetIndex = (_currentTargetIndex + direction + _candidates.Count) % _candidates.Count;
                SetCurrentTarget(_candidates[_currentTargetIndex]);
            }
        }
    }
}

