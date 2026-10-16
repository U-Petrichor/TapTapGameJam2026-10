namespace TapTapGameJam.MiniGame
{
    public sealed class ParcelCutInstance : PanelMiniGameInstance
    {
        public ParcelCutInstance(ParcelCutPoint point, int coins, int talent)
            : base(point, "拆快递", coins, talent) { }
    }
}
