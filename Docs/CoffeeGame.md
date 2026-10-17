# 五阶段咖啡小游戏配置

当前场景使用 `Assets/Resources/MiniGame/CoffeeGame.prefab`，已在 GameScene 的 ContentRoot 下实例化为 MakeCoffeeGame，并绑定公共面板的 MakeCoffee 类型。旧三步界面改名为 LegacyCoffeeGame 并停用；运行时默认配置优先沿用已绑定的新界面，空面板则加载同一个预制体。

## 操作顺序

1. p1：拖动豆袋上方的 CoffeeBeans，在研磨机上方的 BeansDropZone 中松手。未命中则复位，成功后豆子隐藏，豆袋保留。
2. p2：按住 Grinder 研磨，默认 1.5 秒。手柄绕圆形连接处顺时针旋转；松手或离开研磨机区域时，进度和角度一起暂停，继续按住会从停下的角度接着研磨。研磨完成后手柄保持最终角度，不能再次研磨；新一轮任务恢复初始姿态。
3. p3：研磨完成后，CoffeeGround 出现在 groundCoffeeCup 上。拖动整个盛粉杯，在咖啡机的 GroundsDropZone 中松手。粉堆随杯子一起移动；成功后空杯回到研磨机下，咖啡机滤杯处显示 PlacedCoffeeGround；放错位置则盛粉杯连同粉堆复位。
4. p4：拖住 HotWater，让倾倒姿态下的 KettleSpout 进入 WaterPourZone，停住即可持续注水，默认 2 秒。移开或松手暂停，保留进度，水壶松手后复位。
5. p5：注水满后显示完成状态，默认保留 0.45 秒，再通过原有 MakeCoffeeInstance 结算任务奖励。关闭、失焦、暂停或取消任务都会停止当前操作；关闭后重新打开从放豆开始。

五张阶段图位于顶部。当前阶段原色，后续变暗，完成阶段使用绿色色调。底部进度条表示整个制作过程，研磨和注水阶段同时显示当前操作百分比。

## 更换素材和调整区域

展开场景 ContentRoot 下的新 MakeCoffeeGame，或者打开 CoffeeGame 预制体：

| 物体 | 用途与调整方式 |
| --- | --- |
| Grinder | 研磨机本体 Image，也是固定的长按判定区；可调整尺寸，保持 Preserve Aspect |
| Grinder / GrinderHandleFrame / GrinderHandle | 独立手柄 RawImage，画布与本体自动保持相同比例；Pivot 对准圆形连接处，不接收射线，旋转不会移动长按判定区 |
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

Grinder Handle 绑定独立手柄，Grind Degrees Per Second 控制每秒旋转角度（默认 240）。研磨机与手柄来自 `Assets/Model/MiniGame/png(1)/png/` 的同名 PNG，按共同的 589×668 画布叠合，与“研磨机带手柄.psd”一致。`Tools/MiniGames/更新咖啡研磨机与旋转手柄` 可重新绑定并保存预制体及场景，保留场景的研磨机位置与尺寸。

素材来自 `Assets/Model/MiniGame/New Folder`。本次将使用到的 11 张 PNG 配置为 Sprite Single，保留原有 GUID。场景和预制体均已绑定，无需手工制作单独器具预制体。

## 验证

- `dotnet run --project Tools/CoffeeRecipeChecks/CoffeeRecipeChecks.csproj`：独立验证步骤顺序、放豆前禁止研磨、按住处理、暂停恢复、非法时间步、取消后停止与完成只通知一次。
- `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/MiniGameChecks/Compile-UnityScripts.ps1`：使用项目的 Unity DLL 编译全部脚本。
- Play 模式验证鼠标拖放、研磨区域、壶嘴判定、粉杯父子移动、五阶段显隐和最终任务奖励。自动化场景冒烟检查已更新为先拖豆再研磨，并等待最后的完成显示结束。

本轮已通过独立流程检查、全脚本编译及预制体引用/素材导入设置/场景映射的静态检查，尚未验证 Play 模式的实际画面和输入。

手柄更新已通过 Unity 编译及隔离 UI 验证：拼装比例与转轴、按住旋转、反复松手恢复、移出区域暂停、丢失松手事件与失焦处理、最后一帧按剩余时间旋转、研磨完成后锁定、退出保留角度及新回合复位；已渲染检查拼装画面。
