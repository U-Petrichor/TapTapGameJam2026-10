using TapTapGameJam.Events;

namespace TapTapGameJam.Tutorial
{
    /// <summary>
    /// 业务代码触发教程检查的统一入口。没有教程管理器时发布事件也是安全的。
    /// </summary>
    public static class TutorialSignal
    {
        public static void Send(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            EventPipeline.Publish(new EventTutorialSignal(key));
        }
    }
}
