using TapTapGameJam.Player;

namespace TapTapGameJam.MiniGame
{
    public abstract class PanelMiniGameInstance : MiniGameInstanceBasic
    {
        private readonly PanelTaskPoint _point;
        private bool _begun;

        protected PanelMiniGameInstance(PanelTaskPoint point, string text, int coins, int talent)
            : base(point.transform, coins, talent)
        {
            _point = point;
            GuideText = text;
        }

        public bool Begin()
        {
            if (_begun || IsFinished || _point == null) return false;
            _begun = true;
            _point.EventCompleted += HandleCompleted;
            _point.EventTaskCancelled += Cancel;
            if (_point.StartTask(this)) return true;
            // 未取得点位时不能取消别的实例拥有的点位。
            _begun = false;
            Cancel();
            return false;
        }

        private void HandleCompleted(PlayerBasic player)
        {
            if (player != null && player.PlayerResources != null) Complete(player.PlayerResources);
            else Cancel();
        }

        protected override void Release()
        {
            if (_point == null) return;
            _point.EventCompleted -= HandleCompleted;
            _point.EventTaskCancelled -= Cancel;
            if (_begun) _point.CancelTask(this);
        }
    }
}
