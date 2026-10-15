namespace TapTapGameJam.UI
{
    /// <summary>
    /// 一个界面容器：自己管内容物。
    ///
    /// 【约定】
    /// 1. 打开时：为容器内**每一件**物品向 <see cref="UIManager"/> 申请块，然后自己负责**定位**
    ///    （布局逻辑在容器里，不在槽位里），并把块登记到对应槽位上（写 CurrentSlot）。
    ///    注意"给已有物品摆位置"不要走 <see cref="IItemSlot.DropItemBlock"/> —— 那是"放入 + 容量检查"
    ///    的逻辑，重跑一次可能直接失败（例如 PlayerBag.TryEquip 对"已经穿着的那件"返回 false）。
    /// 2. 关闭时：把自己持有的每一件物品对应的块交给 <see cref="UIManager"/> 销毁。
    ///
    /// 容器**不需要**维护"我持有哪些块"，也不需要写任何物品转移的代码：块跟着物品走，
    /// 谁最后持有那件物品，谁在关闭时销毁它。
    /// </summary>
    public interface IItemContainer
    {
        void OpenContainer();

        void CloseContainer();
    }
}
