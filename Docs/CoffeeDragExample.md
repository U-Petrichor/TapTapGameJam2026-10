# 完整手冲咖啡：脚本职责、操作流程与 Inspector 接线

本版按照完整的七步流程执行，制作期间世界倒计时继续，滴滤结束后自动获得一份咖啡并关闭小游戏。没有 FinishButton。

## 1. 脚本分工

| 文件 | 挂在哪里 | 职责 |
| --- | --- | --- |
| CoffeeMinigameManager.cs | CoffeeGame | 连接器具、配方、输入和 UI，直接开关自己的镜头，成功时发放咖啡 |
| CoffeeRecipe.cs | 不挂物体，普通 C# 类 | 检查七步顺序、计时和完成条件 |
| CoffeeDeskInteractor.cs | CoffeeGame | 统一读取鼠标：点击、持续按住、拖拽 |
| CoffeeDraggable.cs | 水壶、咖啡豆、滤杯、滤纸、咖啡粉容器 | 移动、倾倒、吸附、复位 |
| CoffeeClickable.cs | 水壶开关、研磨机操作部位 | 标记可以点击或按住的 3D 部位 |
| CoffeeDropZone.cs | 研磨机入口、杯口、滤杯内部 | 判定接收区域并提供吸附位置 |

咖啡小游戏不依赖 CameraManager。CoffeeMinigameManager 持有咖啡镜头，只启用/禁用该组件；CinemachineBrain 根据 Inspector 中的 Priority 选择镜头并完成过渡。

## 2. 七步的具体操作

| 顺序 | 操作 | 完成条件 |
| --- | --- | --- |
| 1. 加热水 | 点击热水壶开关 | 加热时长达到，自动进入取豆 |
| 2. 放咖啡豆 | 把咖啡豆拖到研磨机入口并松手 | 落在 GrinderZone 内 |
| 3. 磨咖啡豆 | 在研磨机操作部位按住鼠标左键 | 有效按住时长达到；松开/移开不增长，保留进度 |
| 4. 放滤杯 | 把滤杯拖到杯口并松手 | 落在 CupZone 内，吸附到杯口 |
| 5. 放滤纸 | 把滤纸拖到滤杯并松手 | 落在 FilterZone 内，吸附到指定位置 |
| 6. 倒咖啡粉 | 把磨好后的粉杯拖到滤纸上并松手 | 进入 FilterZone 后自动倾倒，倒粉时长达到 |
| 7. 注水过滤 | 拖起热水壶，在滤杯上方持续按住 | 有效注水达到目标，再等待滴滤结束 |

只有当前步骤允许的器具能操作。错误位置松手会返回起点。注水可以分次完成，水壶移开、松手或移到 UI 上都会停止出水，已经倒入的量保留。

研磨结束时显示一个咖啡粉容器，作为第六步可拖动的对象。咖啡粉容器可以先用小 Cube 表示，摆在研磨机旁。粉粒、蒸汽、水流等是可选表现，不做液体物理模拟。

成功后自动调用 PlayerResources.AddCoffee()，CoffeeCount 增加 1，并发布 EventCoffeeCountChange。玩家资源组件的 Inspector 中可以看到咖啡数。现有项目尚没有通用物品背包，所以先将咖啡作为玩家持有的资源记录。

## 3. 场景结构

~~~text
Main Camera                         Camera + CinemachineBrain
PlayerFollowCamera                  始终启用，Priority 较低
MiniGameManager
    CoffeeGame                      CoffeeMinigameManager + Interactor，始终启用
        CoffeeTaskCamera            固定桌前镜头，Priority 较高，组件默认禁用
        Kettle                      Collider + CoffeeDraggable
            KettleSwitch            Collider + CoffeeClickable
            WaterStream             可选的水流表现
        Beans                       Collider + CoffeeDraggable
        Grinder
            GrinderButton           Collider + CoffeeClickable
            GrinderZone             CoffeeDropZone
        Cup
            CupZone                 CoffeeDropZone
                DripperSnapPoint    滤杯放好后的位置/朝向
        Dripper                     Collider + CoffeeDraggable
            FilterZone              CoffeeDropZone
                FilterSnapPoint     滤纸放好后的位置/朝向
                GroundsPourPoint    可选，倒粉时粉杯的位置
        FilterPaper                 Collider + CoffeeDraggable
        GroundsCup                  Collider + CoffeeDraggable
CoffeeTaskCanvas
    CoffeePanel
        HintText                    TextMeshPro - Text (UI)
        ProgressBar                 可选 Slider
~~~

FilterZone 必须跟着滤杯移动，推荐放在滤杯下面。这样滤杯放到杯子上后，滤纸和注水的接收区域也跟过去。

CoffeeGame 和器具不要放在 CoffeePanel 下面，避免关闭 UI 时禁用整个制作系统。

## 4. 镜头配置

CoffeeMinigameManager 的 Brain 字段拖入 Main Camera 上的 CinemachineBrain；不填时从带 MainCamera 标签的相机查找。Coffee Camera 字段引用桌前镜头；不填时自动查找自己的子物体（包括未激活的子物体）。同一个咖啡镜头只交给一个小游戏控制器使用。

CoffeeTaskCamera 的 Tracking Target、Look At Target 留空，Position Control 和 Rotation Control 均为 None。它保持固定的桌前位置，Channel 与 Brain 匹配，关闭 Solo。

保持咖啡镜头物体及其父物体激活，只将 CinemachineCamera 组件默认禁用（Awake 也会关闭它）。玩家跟随镜头始终启用，例如 Priority 为 10，咖啡镜头为 20。脚本不会修改 Priority，也不会关闭玩家镜头。Main Camera 和 CinemachineBrain 始终保持启用。

关闭咖啡镜头后，Brain 选择剩余可用镜头中优先级最高的一个，并不记录“上一个镜头”。请保证普通游玩时只有玩家镜头和当前小游戏镜头参与竞争，小游戏之间不同时开启。镜头没有统一的父物体要求。

小游戏内部直接操作：

~~~csharp
_coffeeCamera.enabled = true;  // 进入
_coffeeCamera.enabled = false; // 完成或取消
~~~

控制器直接查询 Brain.ActiveVirtualCamera 和 Brain.IsBlending，判断咖啡镜头是否接管。鼠标射线使用 Brain.OutputCamera 对应的真实渲染相机。

开始制作前会等待镜头接管并完成 Blend。编辑时可先用 Cut，或将 Default Blend 设为 0.5 秒。镜头过渡同样占用游戏时间。

## 5. 必须连接的控制器字段

| CoffeeMinigameManager 字段 | 拖入 |
| --- | --- |
| Brain | Main Camera 上的 CinemachineBrain；可留空自动查找 |
| Coffee Camera | CoffeeTaskCamera 的 CinemachineCamera |
| Interactor | CoffeeGame 上的 CoffeeDeskInteractor |
| Kettle Switch | 热水壶开关上的 CoffeeClickable |
| Grinder Button | 研磨机操作部位上的 CoffeeClickable |
| Kettle | 热水壶上的 CoffeeDraggable |
| Beans | 咖啡豆上的 CoffeeDraggable |
| Dripper | 滤杯上的 CoffeeDraggable |
| Filter Paper | 滤纸上的 CoffeeDraggable |
| Grounds Cup | 咖啡粉容器上的 CoffeeDraggable |
| Grinder Zone | 研磨机入口的 CoffeeDropZone |
| Cup Zone | 杯口的 CoffeeDropZone |
| Filter Zone | 滤杯内部的 CoffeeDropZone |
| Coffee Panel | 要显示/隐藏的面板 |
| Hint Text | 提示文本 TMP_Text |

CoffeeDeskInteractor 的 Controller 拖入同一物体上的 CoffeeMinigameManager。Pick Mask 初次使用 Everything，Max Pick Distance 默认 20。

TaskMakeCoffee 的 Coffee Minigame 指向这个控制器。玩家身上需要已有的 PlayerResources，成功后咖啡数保存在这里。

器具 Collider 不勾选 Is Trigger。CoffeeClickable 和 CoffeeDraggable 所在物体都应有 Collider；可以在水壶同一个物体上同时挂两个脚本，实现前期点击加热、后期拖拽注水。为开关单独建子物体时，需要开关自己的 Collider，保证射线可以点中它。

物体不需要动态 Rigidbody；如果已有 Rigidbody，将 Is Kinematic 打开，避免物理系统与 Transform 拖拽争夺控制权。

CoffeeDropZone 的 Radius 使用世界单位。选中后 Scene 里的绿色圆表示接收范围。Snap Point 可不填，此时使用区域本身的 Transform；建议为滤杯和滤纸指定独立 SnapPoint，调整准确的位置、角度。

拖拽保持物体拿起时的水平高度。放置判定比较物体轴心与接收区域圆心的水平距离，因此目标区域高度与拖拽高度可以不同，成功吸附时才调整高度。

## 6. 可选表现与 UI

- Grounds Pour Point：指定倒粉时粉杯悬停的位置。粉杯自己的 Pour Euler Angles 定义倾倒角度。
- Heating Effect：水壶加热表现，只在加热期间启用。
- Water Stream：水流表现，建议作为水壶子物体跟着壶嘴移动。
- Beans In Grinder：研磨机中豆子的视觉对象。
- Grounds In Filter：滤杯中咖啡粉的视觉对象。
- Coffee Liquid：滴滤时显示杯中咖啡。
- Grinder Rotor：研磨时旋转的部件；只旋转视觉部件，避免接收区也旋转。
- Progress Bar：非交互 Slider，显示当前加热、研磨、倒粉、注水、滴滤进度。
- On Coffee Completed：完成后的额外通知；咖啡数量已经由控制器增加，不要在此重复加奖励。

可选视觉对象只用于表现，不要把水壶、滤杯等交互物体本体填进这些字段，以免关闭表现时连器具也被关闭。

CoffeeCanvas 使用 Screen Space - Overlay。文本、进度条和整屏背景的 Raycast Target 关闭，避免遮住桌面的鼠标检测。场景继续使用现有 EventSystem 和 InputSystemUIInputModule。

不创建 FinishButton。成功会自动隐藏面板并返回原镜头。Esc 可中途取消；如需要取消按钮，绑定 Exit()，取消不会发放咖啡。

## 7. 倒计时与异常退出

咖啡控制器不引用 GamePhaseManager，不修改 CouldWork、RemainingTime 或 Time.timeScale。

咖啡自身计时使用 Time.deltaTime；它和世界倒计时并行运行。世界发出 GamePhaseChangedEvent 或 EventGameOver 时，制作立即中止，器具复位、镜头恢复、输入解锁，不发放未完成奖励。

已有的 ESC 菜单输入在小游戏期间被拦截，Esc 由小游戏用于取消。当前没有增加“在咖啡中打开暂停菜单”的功能。

目前 GamePhaseManager 的白天时长是 10 秒。示例的默认有效操作时长合计 5 秒（加热 1、研磨 1、倒粉 0.5、注水 2、滴滤 0.5），还要加上拖拽、走到任务处和镜头过渡时间。若测试时还没做完就进购物阶段，这是倒计时正常到期；可以在 Inspector 调整制作时长，或在另一次游戏平衡修改中调整白天时长。

## 8. 阅读代码的顺序

1. 从 TaskMakeCoffee.Interact 看进入点：只调用 Begin(player)。
2. 查看 CoffeeMinigameManager.Begin：连接器具、锁普通玩家操作、启用自己的镜头。
3. 查看 CoffeeRecipe：七步的顺序、每个操作的合法性、时间如何累积。
4. 查看 CoffeeDeskInteractor：鼠标按下检测 Collider，按住驱动研磨或拖拽，松手调用 TryDrop。
5. 查看 Controller.TryDrop：不同步骤对应不同物体和目标区域。
6. 查看 Controller.UpdateDraggedItem：只有热水壶在滤杯区域内持续按住时，才调用 PourWater。
7. 查看 HandleStepChanged 和 CompleteCoffee：滴滤完成后自动退出，并且只加一份咖啡。

关键调用链：

~~~text
点击热水壶 → TryClick → Recipe.StartHeating
每帧 → Recipe.Tick → 加热结束 → LoadBeans
咖啡豆松手 → TryDrop → Recipe.LoadBeans
按住研磨机 → Hold → Recipe.Grind
滤杯松手 → TryDrop → Recipe.PlaceDripper
滤纸松手 → TryDrop → Recipe.PlaceFilter
粉杯松手 → TryDrop → Recipe.StartPouringGrounds
每帧 → Recipe.Tick → 倒粉完成 → PourWater
水壶持续有效拖拽 → UpdateDraggedItem → Recipe.PourWater
每帧 → Recipe.Tick → 滴滤完成 → CompleteCoffee
CompleteCoffee → 自动关闭 + PlayerResources.AddCoffee
~~~

## 9. 验证

配方的自动检查位于 Tools/CoffeeRecipeChecks，运行：

~~~powershell
dotnet run --project Tools/CoffeeRecipeChecks/CoffeeRecipeChecks.csproj
~~~

覆盖顺序限制、加热启动、研磨松手、分次注水、自动滴滤完成、重复完成保护、重新开始及无效时间参数。

Unity 中仍需连接器具/UI 后进行 Play 验证：开始/取消、每步拖错位置、注水移出区域、完整制作后自动退出且 CoffeeCount 只增加 1，以及倒计时到期中止。代码编译和配方测试不等于已完成场景中的鼠标操作验证。
