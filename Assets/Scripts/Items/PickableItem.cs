using TapTapGameJam.Audio;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 可拾取进入背包的物品，可使用。
    /// 物品<b>不往任何全局名单里登记</b>；只有食物会往 <see cref="FoodManager"/> 的空间索引里登记自己
    /// （见 <see cref="FoodItem"/>），因为只有"找食物"需要按位置查。
    ///
    /// 位置状态说明：
    /// - OnMap：躺在地图上，可被拾取 / 被蟑螂吃掉。
    /// - InBag：已经属于玩家背包（本体被 PlayerBag 挪到暂存区隐藏）。
    /// - Equipped：已装备在玩家身上（本体同样在暂存区隐藏）。
    /// 进背包 / 装备时本体不销毁，只是被 PlayerBag 挪到暂存区隐藏（见 PlayerBag.MoveToBagStorage），
    /// 所以食物剩余营养之类的运行时状态都会被完整保留。
    /// </summary>
    public class PickableItem : Interactable
    {
        public enum ItemLocation
        {
            /// <summary>散落在地图上，可被拾取/被蟑螂吃掉</summary>
            OnMap,
            /// <summary>已经在玩家背包里（gameObject 被挪到暂存区隐藏，不销毁重建）</summary>
            InBag,
            /// <summary>已装备在玩家身上（gameObject 被挪到暂存区隐藏，不销毁重建）</summary>
            Equipped,
            /// <summary>在商店中</summary>
            InStore,
            /// <summary>在仓库中</summary>
            InRepo
        }

        [Header("背包形状")]
        [FormerlySerializedAs("itemShape")]
        [Tooltip("在背包中占据的格子形状。(0,0) 是鼠标咬住的抓取格，旋转与占格判定都在 BagShape 里")]
        public BagShape shape;

        [Header("显示")]
        [Tooltip("在背包格子里的图标。留空则由 BagPanelUI 用同色方块兜底，不影响摆放逻辑")]
        [SerializeField] protected Sprite _bagIcon;
        public virtual Sprite BagIcon => _bagIcon;

        [Tooltip("物品稀有度：0 = 普通，1 = 稀有。黑盒按它分池抽取（见 LootRoll.IsRare）")]
        [SerializeField] protected int _specialLevel = 0;
        public virtual int SpecialLevel => _specialLevel;
        
        [Tooltip("物品名称")]
        [SerializeField] protected string _itemTitle = "";
        public virtual string ItemTitle => _itemTitle;
        
        [Tooltip("物品描述信息")]
        [SerializeField] protected string _itemDesc = "";
        public virtual string ItemDesc => _itemDesc;

        /// <summary>
        /// 这件物品属于哪个大类（仓库 / 商店分页用）。
        /// 由子类决定：FoodItem → Food，ToolItem → Tool，DressItem → Dress；
        /// 直接挂在 PickableItem 上的东西兜底算 Tool。
        /// </summary>
        public virtual ItemCategory Category => ItemCategory.Tool;

        public ItemLocation Location { get; private set; } = ItemLocation.OnMap;

        public bool IsOnMap => Location == ItemLocation.OnMap;

        // 注意：物品<b>不再往任何全局名单里登记</b>。
        // 原先 PickableItem 会把自己登记进 ItemManager，但那个名单实际只有"找食物"一个消费方，
        // 现在食物改用 FoodManager 的空间索引（见 FoodItem / FoodManager），
        // 工具 / 装扮 / 黑盒没有任何地方需要"全量枚举"，这套登记就整个去掉了。

        public void SetLocation(ItemLocation location)
        {
            Location = location;
        }

        #region 交互

        public override bool CanInteract(PlayerBasic player)
        {
            return GameManager.IsNight && IsOnMap;
        }

        public override void Interact(PlayerBasic player, InteractKey key)
        {
            Debug.Log("Try pickup");
            
            if (!CanInteract(player))
            {
                return;
            }

            PlayerBag bag = player != null ? player.PlayerBag : null;
            if (bag == null)
            {
                Debug.LogError($"[{name}]：玩家身上没有 PlayerBag，无法拾取。请在 Player 预制体上挂 PlayerBag 组件");
                return;
            }

            Vector3 pickupPosition = transform.position;
            if (bag.PickUp(this)) Sfx.At(SoundId.Pickup, pickupPosition, player, SoundBus.Interface);
            else Sfx.Play(SoundId.Error, player);
        }

        public override string InteractionPrompt => "按 E 拾取";

        #endregion
    }
}
