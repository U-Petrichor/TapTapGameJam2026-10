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

    /// <summary><see cref="ItemCategory"/> 的配套常量</summary>
    public static class ItemCategories
    {
        /// <summary>所有分页，顺序 = 枚举顺序 = 界面上的分页顺序（背包仓库 / 商店分页都读它）。只读，别改</summary>
        public static readonly ItemCategory[] All =
            (ItemCategory[])System.Enum.GetValues(typeof(ItemCategory));
    }
}
