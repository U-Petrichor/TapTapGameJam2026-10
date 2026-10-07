using TapTapGameJam.Events;
using TapTapGameJam.Interactable;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class CleanMopWater : AbstractInteractable
    {
        public override string InteractionPrompt => "清理污水";
        private PlayerResources _playerResources;

        public override void Interact(PlayerBasic player)
        {
            _playerResources = player.PlayerResources;
            EventPipeline.Publish<EventOpenMiniGamePanel>(new EventOpenMiniGamePanel(0,OnFinish));
        }
        private void OnFinish(bool isFinish)
        {
            if (isFinish)
            {
                // 如果完成就给玩家结算钱
                _playerResources.AddCoins(Random.Range(25, 35));
                ClearAllRegister();
            }
            {
                // 如果没完成就什么都不做
            }
        }
        
    }

}