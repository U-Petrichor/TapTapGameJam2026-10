# 五阶段咖啡小游戏配置

当前场景使用 `Assets/Resources/MiniGame/CoffeeGame.prefab`，已在 GameScene 的 ContentRoot 下实例化为 MakeCoffeeGame，并绑定公共面板的 MakeCoffee 类型。旧三步界面改名为 LegacyCoffeeGame 并停用；运行时默认配置优先沿用已绑定的新界面，空面板则加载同一个预制体。

## 操作顺序

1. p1：拖动豆袋上方的 CoffeeBeans，在研磨机上方的 BeansDropZone 中松手。未命中则复位，成功后豆子隐藏，豆袋保留。
2. p2：按住 Grinder 研磨，默认 1.5 秒。松手或离开研磨机区域暂停进度，继续按住可接着研磨。
3. p3：研磨完成后，CoffeeGround 出现在 groundCoffeeCup 上。拖动整个盛粉杯，在咖啡机的 GroundsDropZone 中松手。粉堆随杯子一起移动；成功后空杯回到研磨机下，咖啡机滤杯处显示 PlacedCoffeeGround；放错位置则盛粉杯连同粉堆复位。
4. p4：拖住 HotWater，让倾倒姿态下的 KettleSpout 进入 WaterPourZone，停住即可持续注水，默认 2 秒。移开或松手暂停，保留进度，水壶松手后复位。
5. p5：注水满后显示完成状态，默认保留 0.45 秒，再通过原有 MakeCoffeeInstance 结算任务奖励。关闭、失焦、暂停或取消任务都会停止当前操作；关闭后重新打开从放豆开始。

五张阶段图位于顶部。当前阶段原色，后续变暗，完成阶段使用绿色色调。底部进度条表示整个制作过程，研磨和注水阶段同时显示当前操作百分比。

## 更换素材和调整区域

展开场景 ContentRoot 下的新 MakeCoffeeGame，或者打开 CoffeeGame 预制体：

| 物体 | 用途与调整方式 |
| --- | --- |
| Grinder | 目前为简单 Image 占位。替换 Source Image、设 Color 为白色并调整尺寸；换图后隐藏或删除其 GrinderPlaceholderLabel 即可 |
| Grinder / BeansDropZone | 透明放豆判定区，可单独移动、调整宽高；不需要 Image、Collider 或 Raycast Target |
| groundCoffeeCup / CoffeeGround | 杯子和粉堆为父子关系，可分别调整图案大小及粉堆相对位置。粉堆受流程控制显隐 |
| FilterClamp | 咖啡机图片，修改图片本身不影响独立区域绑定 |
| FilterClamp / GroundsDropZone | 盛粉杯中心落入此矩形后，松手即算放粉；可单独调整至滤杯位置 |
| FilterClamp / WaterPourZone | 壶嘴的持续注水判定区，可单独调整至滤杯上方 |
| FilterClamp / PlacedCoffeeGround | 放粉后的粉堆显示位置，可移动至咖啡机的滤杯内 |
| FilterClamp / WaterEffect | 注水时显示的简单水流，可按壶嘴与滤杯位置调整 |
| HotWater / KettleSpout | 壶嘴判定点，跟随水壶移动与旋转，可在壶的局部空间中调整 |
| p1～p5 | 顶部阶段图，可调整大小和位置，绑定顺序对应配方阶段 |

CoffeeGame 组件中的 Beans Zone、Cup Zone、Pour Zone 分别绑定上述三个判定区域。Grounds 绑定整个盛粉杯，Ground Visual 绑定它的粉堆子物体。只有 CoffeeBeans、groundCoffeeCup 和 HotWater 的图片以及 Grinder 接收射线；装饰、粉堆及阶段图不接收射线。

三个步骤参数为 Grind Seconds、Water Seconds 和 Completion Delay。Kettle Pour Angle 控制注水姿态，判定使用这个姿态下的壶嘴位置，避免画面与判定不一致。调整在 Edit 模式进行可保存；Play 模式里的普通 Inspector 改动不会自动保留。

素材来自 `Assets/Model/MiniGame/New Folder`。本次将使用到的 11 张 PNG 配置为 Sprite Single，保留原有 GUID。场景和预制体均已绑定，无需手工制作单独器具预制体。

## 验证

- `dotnet run --project Tools/CoffeeRecipeChecks/CoffeeRecipeChecks.csproj`：独立验证步骤顺序、放豆前禁止研磨、按住处理、暂停恢复、非法时间步、取消后停止与完成只通知一次。
- `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/MiniGameChecks/Compile-UnityScripts.ps1`：使用项目的 Unity DLL 编译全部脚本。
- Play 模式验证鼠标拖放、研磨区域、壶嘴判定、粉杯父子移动、五阶段显隐和最终任务奖励。自动化场景冒烟检查已更新为先拖豆再研磨，并等待最后的完成显示结束。

本轮已通过独立流程检查、全脚本编译及预制体引用/素材导入设置/场景映射的静态检查，尚未验证 Play 模式的实际画面和输入。
