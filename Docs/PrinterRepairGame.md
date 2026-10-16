# 打印机修复：敲击与时机反馈

场景中的 PrintRepairGame 使用原有空格时机判定。每次有效按键（包括按错时机）都播放 Hand 平滑敲向 HandFall、短暂停留及快速收手的动画。

- 正确时机：敲下时只显示 Flash，快速淡出，内部正确次数加一。
- 错误时机：敲下时只显示 FlashWrongTime，快速淡出，不增加或清空内部正确次数。
- 不再显示进度文字或进度条。正确次数达到 Required Hits（当前为 5）后，等待最后一击的动画播完，直接完成任务。

连续按键会保存每次判定结果并依次播放动画。播放时不重新读取指针位置，避免先按错、后按对时错误闪光被覆盖。长按空格仍不会自动连击。退出、禁用或重开清空待播动画与待结算回调，隐藏两种闪光，恢复手的姿态。

## 场景绑定与可调项

GameScene 已绑定新建的 HandFall、FlashWrongTime、Flash 和 Printer。HandFall 仅用作隐藏的目标姿态参考。两种闪光默认隐藏且不接收射线。

TimingBar 是固定轨道，SuccessZone 是判定区域，Pointer 是左右移动的指示物。保留更换后的 Sprite；Pointer 移动和复位只控制横向归一化位置，保留场景中设置的纵向锚点和 Y 偏移（当前为 -76）。

Inspector 可调整：

- Required Hits：隐藏的完成目标，默认 5 次正确命中。
- Pointer Speed、Success Width：移动速度和有效区域宽度。
- Hand Down Duration：下压时间，默认 0.1 秒。
- Hand Hold Duration：接触姿态停留，默认 0.03 秒。
- Hand Return Duration：收手时间，默认 0.12 秒。
- Flash Duration：两种闪光的淡出时间，默认 0.08 秒。

调整 Hand 决定初始姿态，调整 HandFall 决定敲击姿态，分别调整 Flash 与 FlashWrongTime 决定闪光位置、大小及颜色。动画使用四元数插值，避免 Euler 角度的整圈数导致多圈旋转。

运行时默认配置优先使用美术版 PrintRepairGame。默认占位布局也不再生成进度控件，已有旧占位布局中的 Progress、ProgressText 会被隐藏。

## 验证

- `dotnet run --project Tools/PrinterRepairChecks/PrinterRepairChecks.csproj`：独立检查错误不计数、累计五次正确后完成、完成及取消后不再接受输入、判定边界和指针往返运动。
- `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/MiniGameChecks/Compile-UnityScripts.ps1`：使用 Unity DLL 编译项目脚本。
- 在 Unity 连接可用时，运行 `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/MiniGameChecks/Invoke-UnityCommand.ps1 -CodePath Tools/MiniGameChecks/PrinterAnimationChecks.cs`：检查两种闪光互斥、错误时机也敲手、交错输入按原结果排队、最后一击延迟结算、退出取消，以及 Pointer 纵向位置保持。该检查仅创建并销毁临时对象，不修改场景原物体。
- Play 模式确认新 Sprite 对齐、指针运动范围、两种闪光位置及敲击手感。

本轮独立判定检查和全项目脚本编译已通过；Unity 连接探测未响应，尚未执行 Play 模式动画检查。

### 错误闪光可见性修复

两种闪光按各自 Image 引用独立缓存颜色，补绑或替换 FlashWrongTime 不再沿用未初始化的零透明度。未保存新字段时可按根物体下的 FlashWrongTime 名称补全引用。零透明度的初始状态恢复到可见基准，闪光显示时启用 Image 并提升到当前 UI 前层。首次到达敲击姿态时保留一帧满亮度，防止低帧率越过整个闪光窗口。验证脚本已增加补绑、零透明度、组件禁用、前层显示和低帧率情况；这些动画检查仍需要可用的 Unity 连接执行。
