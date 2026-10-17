using TapTapGameJam.Interactables;
using TapTapGameJam.Player;

namespace TapTapGameJam.Items.tools
{
    /// <summary>工具：药品、道具这类东西的基类。仓库里归 Tool 页</summary>
    public class ToolItem : PickableItem
    {
        public override ItemCategory Category => ItemCategory.Tool;


        public override void Interact(PlayerBasic player, InteractKey key)
        {
            if (key == InteractKey.E)
            {
                if (!CanInteract(player))
                {
                    return;
                }
                if (UseItem(player))
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                base.Interact(player, key);
            }
        }

        /// <summary>
        /// 使用物品：在地面使用或在背包中使用，ToolItem的专属逻辑
        /// 返回 true 表示成功使用，返回 false 表示使用失败。由调用方负责销毁
        /// </summary>
        public virtual bool UseItem(PlayerBasic player)
        {
            // 默认不可使用的物品直接返回失败
            return false;
        }

        
    }
}
