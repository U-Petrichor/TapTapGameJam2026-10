using System.Collections.Generic;
using TapTapGameJam.Core;
using TapTapGameJam.Interactable;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 可拾取进入背包的物品，可使用。
    /// 物品实例自身负责向 ItemManager 注册/注销，ItemManager 只做登记与查询，不主动遍历场景。
    /// </summary>
    public class PickableItem : MonoBehaviour, IInteractable
    {
        public enum ItemLocation
        {
            /// <summary>散落在地图上，可被拾取/被蟑螂吃掉</summary>
            OnMap,
            /// <summary>已经在玩家背包里（gameObject统一挪到一个地方隐藏起来，不销毁重建）</summary>
            InBag,
            /// <summary>已装备在玩家身上（gameObject统一挪到一个地方隐藏起来，不销毁重建）</summary>
            Equipped
        }

        [System.Serializable]
        public class BagItemShape
        {
            // 背包是一个2维格子，每个物品在背包中占据特定形状（如俄罗斯方块），需要合理摆放才能存下更多东西。
            public List<Vector2Int> blockOffsets = new List<Vector2Int>();

            /// <summary>
            /// 顺时针旋转90度（修改blockOffsets）
            /// </summary>
            public void Rotate()
            {
                // TODO: 依次把 (x, y) 变成 (y, -x)，再整体归一化到最小角为(0,0)，避免负偏移
            }
        }

        [Header("背包形状")] public BagItemShape itemShape;

        [Header("显示")] [Tooltip("在背包中鼠标hover该物品时显示的提示信息")]
        [SerializeField]
        protected string _hoverPrompt = "";
        public virtual string HoverPrompt => _hoverPrompt;

        [Tooltip("是否可以在背包中被使用")]
        public bool IsUseAble { get; } = true;

        public ItemLocation Location { get; private set; } = ItemLocation.OnMap;

        public bool IsOnMap => Location == ItemLocation.OnMap;

        protected virtual void Start()
        {
            // 物品出生（或从池里被重新启用）时登记自己
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.Register(this);
            }
        }

        protected virtual void OnDisable()
        {
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.Unregister(this);
            }
        }

        public void SetLocation(ItemLocation location)
        {
            Location = location;
        }

        #region 交互

        public virtual bool CanInteract(PlayerBasic player)
        {
            return IsOnMap;
        }

        public virtual void Interact(PlayerBasic player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            if (ItemManager.Instance == null)
            {
                Debug.LogError($"[{name}]：场景里没有 ItemManager，无法拾取");
                return;
            }

            // 尝试把它放进背包，放不下就不动它（摆放算法不在这里实现）
            ItemManager.Instance.TryAddToBag(this);
            // TODO：打开交互页面，让玩家自己决定怎么摆
        }

        public virtual string InteractionPrompt => "按 E 拾取";

        /// <summary>
        /// 在背包中才能使用，具体作用由子类实现
        /// </summary>
        public virtual bool UseInBag(PlayerBasic player)
        {
            // TODO: 默认不可使用的物品直接返回失败
            return false;
        }

        #endregion
    }
}
