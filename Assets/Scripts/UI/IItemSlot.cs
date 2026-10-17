namespace TapTapGameJam.UI
{
    /// <summary>
    /// 一个"落点"：拖动中的 <see cref="BagItemBlock"/> 射线打到它头上，由它转发给自己所属的容器。
    /// 背包格子 / 装备槽 / 待拾取位 / 以后的仓库格都是槽位实现。
    ///
    /// 【契约，实现者必须遵守】
    /// - <see cref="TryDropItemBlock"/> 是**纯查询**：只回答"放得下吗"，可以顺手刷自己的预览表现，
    ///   但不许改任何状态，也不许写 <see cref="BagItemBlock.CurrentSlot"/>。
    /// - <see cref="DropItemBlock"/> 是**真放**：放得进才返回 true。成功时必须让容器登记这件物品、
    ///   给块定位（布局），并把 <see cref="BagItemBlock.CurrentSlot"/> 指到自己。
    ///   还**必须能"放回它原来待着的那个槽位"** —— 拖拽失败要靠这条路回退。
    /// - <see cref="OnPickUpItem"/> 在**按下开始拖拽**时被调用：块要离开这个槽位了。
    ///   返回 **false = 禁止取出**（块当场放弃这次拖拽，一步都不动），
    ///   例如商店里没买下的商品就是靠这个拦下来的；允许取出时才去清自己的占用。
    ///   不许销毁块，也不许改 <see cref="BagItemBlock.CurrentSlot"/>，块还要靠它回退。
    /// - <see cref="ClearDropPreview"/> 在块的命中目标变化、以及拖拽结束时被调用，用来收掉试放预览。
    /// </summary>
    public interface IItemSlot
    {
        /// <summary>尝试把一个block放入这个槽位（非正式，仅用于预览，不应影响状态）</summary>
        bool TryDropItemBlock(BagItemBlock block);

        /// <summary>把一个block放入这个槽位</summary>
        bool DropItemBlock(BagItemBlock block);

        /// <summary>从这个槽位拿起物品。返回 false = 不允许取出</summary>
        bool OnPickUpItem(BagItemBlock block);

        /// <summary>把这个槽位刷出来的试放预览收掉</summary>
        void ClearDropPreview();
    }
}
