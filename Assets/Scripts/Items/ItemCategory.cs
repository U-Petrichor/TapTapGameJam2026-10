namespace TapTapGameJam.Items
{
    /// <summary>
    /// 物品大类，主要给仓库 / 商店分页用。
    ///
    /// 一件物品属于哪一类，**由它是哪种 <see cref="PickableItem"/> 子类决定**
    /// （见 <see cref="PickableItem.Category"/>）：FoodItem → Food，ToolItem → Tool，DressItem → Dress。
    /// 枚举顺序 = 界面上的分页顺序。
    /// </summary>
    public enum ItemCategory
    {
        Food = 0,
        Tool = 1,
        Dress = 2,
    }
}
