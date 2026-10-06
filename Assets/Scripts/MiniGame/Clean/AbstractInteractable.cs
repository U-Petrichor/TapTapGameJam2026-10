using System.Collections.Generic;
using TapTapGameJam.Interactable;
using TapTapGameJam.Player;
using UnityEngine;


namespace TapTapGameJam.Interactable
{

    public abstract class AbstractInteractable : MonoBehaviour, IInteractable
    {
        public abstract string InteractionPrompt { get; }

        private Dictionary<PlayerInteractionManager, HashSet<Collider>> _registered = new Dictionary<PlayerInteractionManager, HashSet<Collider>>();

        /// <summary>
        /// 玩家是否可以与当前物品交互，需要在子类中重写，完善逻辑，默认可交互
        /// </summary>
        public virtual bool CanInteract(PlayerBasic player)
        {
            return true;
        }

        /// <summary>
        /// 用于一次性物品：玩家交互之后，会从所有已注册单位中取消注册
        /// </summary>
        public abstract void Interact(PlayerBasic player);
        protected void ClearAllRegister()
        {
            foreach (var x in _registered)
            {
                x.Key.Unregister(this);
            }
            _registered.Clear();
        }

        /// <summary>
        /// 检测进入的单位Tag是否为Player，是则注册自身
        /// </summary>
        public void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                // 玩家进入范围
                PlayerInteractionManager interactionManager = other.GetComponentInParent<PlayerInteractionManager>();
                if (!_registered.TryGetValue(interactionManager, out var colliders))
                {
                    // 尝试获取值失败→这是第一个collider
                    colliders = new HashSet<Collider>();
                    _registered.Add(interactionManager, colliders);
                    interactionManager.Register(this);
                }
                colliders.Add(other);
            }
        }
        /// <summary>
        /// 检测离开的单位Tag是否为Player，是则取消注册自身
        /// </summary>
        public void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                // 玩家离开范围
                PlayerInteractionManager interactionManager = other.GetComponentInParent<PlayerInteractionManager>();
                if (_registered.TryGetValue(interactionManager, out var colliders))
                {
                    colliders.Remove(other);
                    if (colliders.Count == 0)
                    {
                        // 说明是最后一个，那么就取消注册并且删除这个键
                        interactionManager.Unregister(this);
                        _registered.Remove(interactionManager);
                    }
                }
            }
        }
    }

}
