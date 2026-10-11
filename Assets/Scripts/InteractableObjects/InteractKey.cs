namespace TapTapGameJam.Interactables
{
    /// <summary>
    /// 触发交互的按键。Interactable 用它决定这一次走哪套交互逻辑（例如 E 捡起、F 吃掉）。
    /// None 表示这次触发不是键盘交互键（例如以后接手柄），走各 Interactable 自己的默认逻辑。
    /// </summary>
    public enum InteractKey
    {
        None = 0,
        E = 1,
        F = 2,
    }
}
