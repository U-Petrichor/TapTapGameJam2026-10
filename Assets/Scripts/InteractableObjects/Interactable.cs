using System.Collections.Generic;
using TapTapGameJam.Player;
using UnityEngine;


namespace TapTapGameJam.Interactables
{

    public enum InteractState
    {
        InteractIdle,       // 等待交互状态
        InteractPending,    // 已经聚焦，下一次按键即将交互
        Interacting         // 持续交互状态
    }

    public abstract class Interactable : MonoBehaviour
    {
        public abstract string InteractionPrompt { get; }
        [SerializeField, Tooltip("左下角的完整任务提示图；不配置时不显示任务提示。")]
        private Sprite _interactionPromptSprite;
        public Sprite InteractionPromptSprite => _interactionPromptSprite;

        /// <summary>
        /// 是否处于就绪状态（已被选中，准备交互），由 PlayerInteractionManager 维护，只有当前选中的对象为 true
        /// </summary>
        // public bool IsPendingInteract { get; set; } = false;
        public InteractState InteractState { get; set; } = InteractState.InteractIdle;

        protected Dictionary<PlayerInteractionManager, HashSet<Collider>> _registered = new Dictionary<PlayerInteractionManager, HashSet<Collider>>();

        /// <summary>
        /// 玩家是否可以与当前物品交互，需要在子类中重写，完善逻辑，默认可交互
        /// </summary>
        public virtual bool CanInteract(PlayerBasic player)
        {
            return true;
        }

        /// <summary>
        /// 真正执行交互。key 是玩家这次按下的交互键，子类可以据此实现不同的交互（例如 E 拾取、F 吃掉）。
        /// 用于一次性物品：玩家交互之后，会从所有已注册单位中取消注册
        /// </summary>
        public abstract void Interact(PlayerBasic player, InteractKey key);
        /// <summary>
        /// 主动从所有已注册的 PlayerInteractionManager 里注销自己。
        ///
        /// 除了 <see cref="OnTriggerExit"/>，**被隐藏起来时也必须调用它**：
        /// Unity 不会为被禁用的碰撞体触发 OnTriggerExit，物体被 SetActive(false)
        /// 收起来（例如收进背包暂存区）后如果还留在候选列表里，它会一直霸住焦点，
        /// 而这时它的 CanInteract 往往已经是 false —— 玩家按交互键只会看到"无法交互"。
        ///
        /// 它会把 <see cref="_registered"/> 的账一起清掉，所以之后重新启用时
        /// OnTriggerEnter 能正常把注册重新建起来（只调 manager.Unregister 做不到这点）。
        /// </summary>
        public void ClearAllRegister()
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
        public virtual void OnTriggerEnter(Collider other)
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
        public virtual void OnTriggerExit(Collider other)
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
