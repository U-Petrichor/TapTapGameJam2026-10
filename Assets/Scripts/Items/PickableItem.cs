using TapTapGameJam.Core;
using TapTapGameJam.Interactable;
using TapTapGameJam.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 可拾取进入背包的物品，可使用。
    /// 物品实例自身负责向 ItemManager 注册/注销，ItemManager 只做登记与查询，不主动遍历场景。
    ///
    /// 位置状态说明：
    /// - OnMap：躺在地图上，可被拾取 / 被蟑螂吃掉。
    /// - InBag：已经属于玩家背包（含"手上正拿着、还没放进格子"这一状态，靠 PlayerBag.HandItem 区分）。
    /// - Equipped：已装备在玩家身上。
    /// 进背包 / 装备时本体不销毁，只是被 PlayerBag 挪到暂存区隐藏（见 PlayerBag.MoveToBagRoot），
    /// 所以食物剩余营养之类的运行时状态都会被完整保留。
    /// </summary>
    public class PickableItem : MonoBehaviour, IInteractable
    {
        public enum ItemLocation
        {
            /// <summary>散落在地图上，可被拾取/被蟑螂吃掉</summary>
            OnMap,
            /// <summary>已经在玩家背包里（gameObject 被挪到暂存区隐藏，不销毁重建）</summary>
            InBag,
            /// <summary>已装备在玩家身上（gameObject 被挪到暂存区隐藏，不销毁重建）</summary>
            Equipped
        }

        [Header("背包形状")]
        [FormerlySerializedAs("itemShape")]
        [Tooltip("在背包中占据的格子形状。(0,0) 是鼠标咬住的抓取格，旋转与占格判定都在 BagShape 里")]
        public BagShape shape = new BagShape();

        [Header("显示")]
        [Tooltip("在背包格子里的图标。留空则由 BagPanelUI 用同色方块兜底，不影响摆放逻辑")]
        [SerializeField] protected Sprite _bagIcon;
        public virtual Sprite BagIcon => _bagIcon;

        [Tooltip("在背包中鼠标 hover 该物品时显示的提示信息")]
        [SerializeField] protected string _hoverPrompt = "";
        public virtual string HoverPrompt => _hoverPrompt;

        [Tooltip("是否可以在背包中被使用（右键）。默认可用，子类可覆写 IsUseAble 关闭")]
        [SerializeField] protected bool _isUseAble = true;
        public virtual bool IsUseAble => _isUseAble;

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

        protected virtual void OnDestroy()
        {
            // 只有真正被销毁时才注销。
            // 注意：不能放在 OnDisable —— 进背包/装备时本体只是 SetActive(false)，
            // 如果在那里注销，物品就会从 ItemManager 的名单里消失。
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

            PlayerBag bag = player != null ? player.PlayerBag : null;
            if (bag == null)
            {
                // 场景里没有 PlayerBag（例如还没接线的测试场景）。
                // 这里不做任何状态修改，只报错——把物品藏起来却没人记录它，等于凭空消失。
                Debug.LogError($"[{name}]：玩家身上没有 PlayerBag，无法拾取。请在 Player 预制体上挂 PlayerBag 组件");
                return;
            }

            // 捡起来交给背包：是否自动穿戴、是否打开摆放界面都由 PlayerBag 决定
            bag.PickUp(this);
        }

        public virtual string InteractionPrompt => "按 E 拾取";

        /// <summary>
        /// 在背包中才能使用（右键），具体作用由子类实现。
        /// 返回 true 表示"这件东西用完了"，调用方（PlayerBag.UseItem）会把它从背包摘掉并销毁；
        /// 返回 false 表示"效果做了但东西还在"（例如饰品换装），调用方会把它交回玩家手上。
        /// </summary>
        public virtual bool UseInBag(PlayerBasic player)
        {
            // 默认不可使用的物品直接返回失败
            return false;
        }

        #endregion
    }
}
