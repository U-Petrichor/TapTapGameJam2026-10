using TapTapGameJam.Audio;


using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class DeliveryFileInstance : MiniGameInstanceBasic
    {
        private enum Stage
        {
            WaitingForPickup,
            WaitingForDelivery,
            Finished
        }
            
        private readonly FileSourcePoint _source;
        private readonly FileDestinationPoint _destination;
        private PlayerBasic _carrier;
        private UnityEngine.GameObject _carriedFile;
        private Stage _stage;
        private bool _begun;
        

        public DeliveryFileInstance(FileSourcePoint source, FileDestinationPoint destination, int coins = 1, int talent = 5)
            : base(source.transform, coins, talent)
        {
            _source = source;
            _destination = destination;
            GuideText = "领取文件";
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
        private void HandleFilePickedup(PlayerBasic player)
        {
            if (_stage != Stage.WaitingForPickup || IsFinished || player == null) return;
            _stage = Stage.WaitingForDelivery;
            _carrier = player;
            _carriedFile = player.CarryController != null
                ? player.CarryController.CurrentObject as UnityEngine.GameObject
                : null;
            // 文件已被拾取，取消订阅
            Sfx.At(SoundId.Delivery, _source.transform.position, player);
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
            Sfx.At(SoundId.Delivery, _destination.transform.position, player);
            Complete(player.PlayerResources);
        }

        protected override void Release()
        {
            bool waitingForPickup = _stage == Stage.WaitingForPickup;
            _stage = Stage.Finished;

            // 成功和取消都解绑，避免地点复用时回调到旧任务。
            if (_source != null)
                _source.EventFileSourceFinish -= HandleFilePickedup;
            if (_destination != null)
                _destination.EventFileDestinationFinish -= HandleDestinationFinished;

            if (_carrier != null && _carrier.CarryController != null &&
                _carrier.CarryController.CurrentType == PlayerCarryController.CarryType.File)
            {
                _carrier.CarryController.EndCarry();
            }

            if (_carriedFile != null)
            {
                UnityEngine.Object.Destroy(_carriedFile);
                _carriedFile = null;
            }

            // 起点领取后可能被另一单复用，不能再重置它。
            if (_begun && waitingForPickup && _source != null) _source.ResetTask();
            if (_begun && _destination != null) _destination.ResetTask();
        }

    }
}
