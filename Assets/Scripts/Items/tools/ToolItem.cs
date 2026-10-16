namespace TapTapGameJam.Items.tools
{
    /// <summary>工具：药品、道具这类东西的基类。仓库里归 Tool 页</summary>
    public class ToolItem : PickableItem
    {
        public override ItemCategory Category => ItemCategory.Tool;
    }
}
