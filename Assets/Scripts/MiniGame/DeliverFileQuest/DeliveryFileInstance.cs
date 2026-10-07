

using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class DeliveryFileInsance : MiniGameInstanceBasic
    {
        private enum Stage
        {
            WaitingForPickup,
            WaitingForDelivery,
            Finished
        }
            
        private readonly FileSource _source;
        private readonly FileDestination _destination;
        private Stage _stage;
        private PlayerResources _playerResources;
        private bool _begun;
        

        public DeliveryFileInsance(FileSource source, FileDestination destination):base(source.transform)
        {
            _source = source;
            _destination = destination;
            GuideText = "领取文件";
            // 沿用原来的结算数值；指引与结算读取同一份奖励。
            RewardCoins = 1;
            RewardTalentPoints = 5;
        }
        public void Begin()
        {
            if (_begun || IsFinished) return;
            _begun = true;
            _stage = Stage.WaitingForPickup;
            // 先声明整两个地点已经被占用
            _source.IsFree = false;
            _destination.IsFree = false;

            _source.EventFileSourceFinish += HandleFilePickedup;
            _destination.EventFileDestinationFinish += HandleDestinationFinished;

            _source.StartTask();
        }
        private void HandleFilePickedup()
        {
            if (_stage != Stage.WaitingForPickup || IsFinished) return;
            _stage = Stage.WaitingForDelivery;
            // 文件已被拾取，取消订阅
            _source.EventFileSourceFinish -= HandleFilePickedup;

            // 让目的地可以交互
            _destination.StartTask();

            GuideText = "送达文件";
            NotifyTargetChanged(_destination.transform);
            NotifyStageChanged();
        }

        private void HandleDestinationFinished(PlayerBasic player)
        {
            if (_stage != Stage.WaitingForDelivery || IsFinished ||
                player == null || player.PlayerResources == null) return;
            _playerResources = player.PlayerResources;
            End(true);
        }

        public override void Cancel()
        {
            End(false);
        }

        private void End(bool success)
        {
            if (_stage == Stage.Finished || IsFinished) return;
            bool waitingForPickup = _stage == Stage.WaitingForPickup;
            _stage = Stage.Finished;

            // 成功和取消都解绑，避免地点复用时回调到旧任务。
            if (_source != null)
                _source.EventFileSourceFinish -= HandleFilePickedup;
            if (_destination != null)
                _destination.EventFileDestinationFinish -= HandleDestinationFinished;

            // 起点领取后可能被另一单复用，不能再重置它。
            if (_begun && waitingForPickup && _source != null) _source.ResetTask();
            if (_begun && _destination != null) _destination.ResetTask();
            if (success == true)
            {
                _playerResources.AddCoins(RewardCoins);
                _playerResources.AddTalentPoint(RewardTalentPoints);
            }
            NotifyFinished();
        }

    }
}
