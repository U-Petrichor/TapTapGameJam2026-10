namespace TapTapGameJam.Tutorial
{
    /// <summary>
    /// 业务代码发给教程系统的事件名。集中在这里，避免各处写裸字符串。
    /// </summary>
    public static class TutorialSignals
    {
        public const string DayStart = "day.start";
        public const string MoveFirst = "move.first";
        public const string TaskAdded = "task.added";
        public const string TaskFirstCompleted = "task.first_completed";
        public const string TaskComplexCompleted = "task.complex_completed";
        public const string NestFirstMarked = "nest.first_marked";
        public const string DaySettled = "day.settled";
        public const string HomeEntered = "home.entered";
        public const string TalentOpened = "talent.opened";
        public const string WarehouseOpened = "warehouse.opened";
        public const string NightStarted = "night.started";
        public const string NestFirstEgg = "nest.first_egg";
        public const string NestFirstHatch = "nest.first_hatch";
        public const string NestFirstCommand = "nest.first_command";
        public const string BlackBoxTouched = "blackbox.first_touched";

        // 教程自己用来控制“第一个任务”的流程信号。
        public const string TaskHold = "tutorial.task.hold";
        public const string TaskSpawnFirst = "tutorial.task.spawn_first";
        public const string TaskRelease = "tutorial.task.release";
    }
}
