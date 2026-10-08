namespace TapTapGameJam.MiniGame
{
    // 保留原脚本 GUID，让已有清理点继续使用同一组件。
    public class CleanMopWater : PanelTaskPoint
    {
        public override string InteractionPrompt => "清理垃圾";
        protected override int DefaultGameIndex => 0;
    }
}