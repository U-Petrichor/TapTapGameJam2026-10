using System.Collections.Generic;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Interactable
{
    public class TaskMakeCoffee : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => $"按 E 制作咖啡";
        private Dictionary<PlayerInteractionManager, HashSet<Collider>> _registered = new Dictionary<PlayerInteractionManager, HashSet<Collider>>();

        /// <summary>
        /// Item非首次实例化时，继承先前拥有的额外属性，在掉落物中继续保持
        /// </summary>


        #region 玩家交互
        public bool CanInteract(PlayerBasic player)
        {
            // TODO 这里可以添加一些条件，例如后续玩家如果没有收到这个任务就不能制作咖啡
            return true;
        }

        public void Interact(PlayerBasic player)
        {
            if (!CanInteract(player))
                return;

            // TODO 下面要添加制作咖啡时，打开小游戏界面的窗口
        }

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
        #endregion
    }
}
