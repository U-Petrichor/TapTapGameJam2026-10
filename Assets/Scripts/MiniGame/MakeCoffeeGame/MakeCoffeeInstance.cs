using TapTapGameJam.Player;
namespace TapTapGameJam.MiniGame
{
    public sealed class MakeCoffeeInstance : PanelMiniGameInstance
    {
        public MakeCoffeeInstance(TapTapGameJam.Interactables.MakeCoffeePoint point, int coins, int talent) : base(point, "制作咖啡", coins, talent) { }
        protected override void OnSuccess(PlayerResources resources) => resources.AddCoffee();
    }
}
