using TapTapGameJam.Player;
namespace TapTapGameJam.MiniGame
{
    public sealed class CoffeeInstance : PanelMiniGameInstance
    {
        public CoffeeInstance(TapTapGameJam.Interactable.TaskMakeCoffee point, int coins, int talent) : base(point, "制作咖啡", coins, talent) { }
        protected override void OnSuccess(PlayerResources resources) => resources.AddCoffee();
    }
}
