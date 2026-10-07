# 倒狗粮小游戏配置

玩法已经写入 `PourDogFoodGame`：按住狗粮袋拖到盆上方，左右反向晃动达到指定距离才增加粮食；停止移动、单向拖动、在盆外晃动都不会增加进度。松手后袋子回到初始位置，已倒入的粮食保留；重新打开小游戏清空进度。

公共 `MiniGamePanelUI` 已接上 Index 映射，弹出动画结束调用 `Begin`，通过返回 `true`、关闭返回 `false`，收回动画结束重置并隐藏内容。代码尚未替你搭建和绑定场景中的狗粮 UI，以下设置完成后才能在 Play 模式使用。

## 场景层级

```text
DayMiniGamePanel                       MiniGamePanelUI + CanvasGroup
├── Background
├── ContentRoot                       调整到背景内部的内容区域，不要保留默认 100×100
│   └── PourDogFoodGame                挂 PourDogFoodGame，RectTransform 铺满 ContentRoot
│       ├── Bowl                      Image，显示盆
│       ├── FoodFill                  Image，可选，显示逐渐增加的粮食
│       ├── BowlFront                 Image，可选，盆前沿遮挡粮食
│       ├── PourArea                  空 RectTransform，位于盆口上方
│       ├── FoodBag                   Image，狗粮袋，可以被拖动
│       │   ├── PourOrigin            空 RectTransform，摆在袋口
│       │   └── PourEffect            可选粮食流图片
│       └── ProgressBar               可选 Slider
└── ExitButton
```

所有装饰图片取消 `Raycast Target`；只有 FoodBag 的 Image 开启它。袋子不要挂 Button、EventTrigger 或其他拖拽脚本，让根物体接收拖拽事件。Canvas 需要 GraphicRaycaster，场景需要 EventSystem 和项目正在使用的 InputSystemUIInputModule。

## 素材与绑定

项目素材目录：`Assets/Model/MiniGame/MiniGameConponent/小游戏组件/倒狗粮-摇晃狗粮袋子倒进碗里面`。

- `1.png` 是空盆，`2.png` 是粮袋，`5.png` 是盆的前沿图；参考图可用来摆放。
- 当前这些 PNG 还是普通 Texture。需要在 Import Settings 将 Texture Type 改成 Sprite (2D and UI)，Sprite Mode 选 Single，点击 Apply，再拖到 Image 的 Source Image。
- `FoodBag` 必须直接放在尺寸合适的小游戏根 RectTransform 下；代码使用它的父区域限制拖动范围。不要用 LayoutGroup 或 Animator 同时驱动袋子位置。
- `PourOrigin` 必须是 FoodBag 的子物体，摆在倾倒时粮食离开袋子的位置。
- `PourArea` 是逻辑接粮区域，无需 Image，也不需要 Collider。建议从约 220×140 的区域开始调，摆在盆口上方，使袋口左右晃动时始终留在区域内。
- `FoodFill` 可用一张粮食堆图片，Image Type 设置为 Filled、Fill Method 设置 Vertical、Fill Origin 设置 Bottom。没有粮食图片时，先只用 ProgressBar 展示进度即可。
- `ProgressBar` 的 Interactable 取消勾选；显示值由 normalizedValue 控制。
- `PourEffect` 是可选粮食流图片，默认禁用且不接收射线，每次有效晃动短暂显示；它不是模拟真实颗粒的物理系统。

选中 PourDogFoodGame 根物体，在脚本中拖入 Food Bag、Pour Origin、Pour Area；Food Fill、Progress Bar、Pour Effect 可以留空。

默认 Shake Distance = 20（UI 单位），Food Per Shake = 0.1（每次有效反向晃动增加 10%），Pour Angle = -25。素材 `2.png` 自带倾斜，最终角度可在 Play 模式按画面效果调节。

## 接入公共 Panel

选中 DayMiniGamePanel，在 Mini Games 列表中新增一项：Index 填未占用编号，例如 1，Content 拖入 PourDogFoodGame 的组件。编号不要重复。保持 Panel 本身启用，子内容由它控制显隐。不要将尚未实现、仍抛出 NotImplementedException 的内容用于测试。

交互物体发送：

```csharp
EventPipeline.Publish(new EventSwitchMiniGamePanel(true, 1, OnFinish));

private void OnFinish(bool success)
{
    if (!success) return;
    // 在这里推进喂狗任务或结算奖励。
}
```

原有 CleanMopWater 发送的是 Index 0；它不会自动打开 Index 1 的喂狗游戏，需要由对应交互物体发送新编号。

Panel 会发送 EventPlayMiniGame，使现有资源栏和交互提示隐藏/恢复；这个事件目前不等于锁住玩家移动，也不会暂停世界时间。如希望玩家喂狗期间站在原地，需要另行接入玩家输入管理。

## 接入随机任务生成器

在固定的喂狗物体上保留 PourDogFood，再添加 PourDogFoodQuest。Quest 的 Pour Dog Food 字段拖入这个交互组件（同物体也会自动查找），再把 Quest 拖进 MiniGameTaskManager 的 Candidate Pool。不要把 UI 中的 PourDogFoodGame 或交互组件 PourDogFood 拖进任务池。

PourDogFood 的 Detect Collider 需要绑定检测用 Trigger Collider，Mini Game Index 保持与 Panel 相同的 1。初始化时检测器自动关闭，随机生成任务后才开启。Quest 同时只保存一个当前任务，未完成时不能重复生成。窗口中途关闭保留任务，成功后取消注册、关闭检测、通知任务管理器移除并发放奖励；随后可以再次随机生成。默认奖励为 30 金币、1 天赋点，可在 Quest 的 Inspector 中修改，设为 0 可不奖励该资源。

阶段切换或禁用组件会取消旧任务，不发奖，并关闭对应窗口。使用现有 NotifyFinished 事件将取消的任务从管理器中移除；该事件本身没有成功/取消结果字段，奖励仍只在成功回调里处理。

额外修复了 MiniGameTaskManager 新一天清理列表时的遍历问题，并允许 CreateTask 失败返回 null；Panel 忙碌拒绝打开时会返回 false，让交互能够重试。DeliverFileQuest 相关脚本未修改。

任务生命周期检查：`dotnet run --project Tools/PourDogFoodQuestChecks/PourDogFoodQuestChecks.csproj`。这些检查运行真实的任务/交互代码，使用最小替身代替 Unity 对象、资源和触发器注册，不替代 Play 模式中的物理/UI 验证。

## 验证

在 Play 模式确认：打开后空盆；拖到盆外左右晃动不增长；盆上方停住不增长；在盆上方左右往返增长；松手保留进度并复位袋子；达到 100% 只返回一次成功；关闭后返回一次取消；重新打开清空进度。还要检查不同分辨率的鼠标拖动，以及收回动画期间不能重复打开。

独立逻辑检查命令：`dotnet run --project Tools/DogFoodShakeChecks/DogFoodShakeChecks.csproj`。
