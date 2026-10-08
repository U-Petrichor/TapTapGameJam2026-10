# 当前任务系统结构

结构概览见 task-system-overview.png，可编辑版本见 task-system-overview.svg。内容依据当前项目源代码整理。

## 核心分工

| 角色 | 基类或组件 | 职责 |
| --- | --- | --- |
| 总管 | MiniGameManager | 维护候选池、当前实例列表；白天随机生成普通任务；立即开放每日活动；结束阶段时取消任务。 |
| 生成规则 | MiniGameCandidate / SinglePointQuest | 具体 Quest 判断 CanSpawn、配置奖励、创建实例；Quest 是可重复使用的场景组件。 |
| 一次任务 | MiniGameInstanceBasic / PanelMiniGameInstance | 记录 Id、状态、目标、奖励；协调点位；统一完成和取消。实例是普通 C# 对象，不是面板或场景预制体。 |
| 场景入口 | PanelTaskPoint | Collider 检测、玩家交互、点位占用和回调版本；具体 Point 设置交互文字与小游戏编号。 |
| 面板总管 | MiniGamePanelUI | 接收打开事件；绑定本次玩家；按 Index 打开内容；处理关闭、输入模式、结果回调。 |
| 具体玩法 | MiniGameContent | 具体 Game 处理鼠标、空格、视觉反馈和完成条件，遵循 Begin / End / ResetView。 |
| 位置指引 | TaskIndicatorUI / Arrow | 订阅任务增删、目标和阶段变化；每帧读取 TargetPosition 的当前世界位置，更新屏幕边缘箭头。 |
| 玩家资源 | PlayerResources | 保存金币、天赋点、咖啡等实际资源。 |

奖励值在具体 Quest 上配置，创建实例时传入，并由普通任务实例统一结算。奖励数值不同无需复制一份玩法代码；创建新的实例类型主要是为了不同任务行为或附加奖励。

## 现有任务对应关系

| 任务 | Quest | Instance | 场景入口 | 面板玩法 |
| --- | --- | --- | --- | --- |
| 清理垃圾 | CleanQuest | CleanInstance | CleanMopWater | CleanGame |
| 分拣文件 | SortFileQuest | SortFileInstance | SortFile | SortFileGame |
| 倒狗粮 | PourDogFoodQuest | PourDogFoodInstance | PourDogFood | PourDogFoodGame |
| 制作咖啡 | CoffeeQuest | CoffeeInstance | TaskMakeCoffee | CoffeeGame |
| 修复打印机 | PrinterRepairQuest | PrinterRepairInstance | PrinterRepairPoint | PrinterRepairGame |
| 模拟炒股 | StockQuest | StockInstance | StockTradePoint | StockGame |
| 送文件 | DeliverFileQuest | DeliveryFileInsance | FileSource + FileDestination | 直接在场景交互 |

前五项共用 SinglePointQuest 和 PanelMiniGameInstance。送文件实例类名保留了项目已有的 DeliveryFileInsance 拼写。

## 一次打印机任务的流程

1. 白天，管理器从可生成候选中选择 PrinterRepairQuest。
2. Quest 创建 PrinterRepairInstance，传入交互点和奖励；实例启用交互点。
3. 管理器登记实例，发送 TaskAdded，指引 UI 创建箭头。
4. 玩家在 PrinterRepairPoint 交互，发出 EventOpenMiniGamePanel。
5. MiniGamePanelUI 按 Index 5 打开 PrinterRepairGame，绑定玩家并调用 Begin。
6. 指针持续移动，玩家按空格，命中黄色区累计成功；达到五次通知面板完成。
7. 面板先 End，再回调 Point；Point 发出 EventCompleted，实例调用 Complete。
8. MiniGameInstanceBasic 标记结束并执行 Release，发放配置的金币和天赋点，发送 EventFinished。
9. 管理器移除实例并发送 TaskRemoved，箭头移除；点位和候选可用于后续任务。

提前关闭仅结束当前玩法尝试，普通任务保留，再次进入重置玩法；阶段切换、游戏结束或取消会结束任务且不发普通完成奖励。结束状态会阻止旧回调和重复奖励。

## 特殊流程

- 炒股属于每日活动：白天固定开放，不进入随机池；StockQuest 持有每日三次额度，成功打开面板扣一次，提前退出也计次。StockInstance 在尚有次数时跨单局保留入口和指引，每局五个价格点。交易直接使用玩家金币，关闭或白天结束按当前价格清仓；次数耗尽或离开白天时活动结束，下一天恢复。
- 送文件属于两阶段场景任务：领取后实例把 TargetPosition 从起点切到目的地，箭头随之更新；送达后使用同一套统一结算。
- 咖啡的额外产出通过 CoffeeInstance.OnSuccess 增加一份咖啡，基础金币和天赋仍由基类发放。

## 设计上的名称

整体可以描述为“职责分离、基于抽象基类与事件驱动的任务框架”。这是项目架构的描述，内部使用以下模式：

- 工厂方法（Factory Method）：管理器调用 MiniGameCandidate.CreateTask，由具体 Quest 决定创建哪种实例；SinglePointQuest.CreateInstance 也是这个扩展点。
- 模板方法（Template Method）：基类规定创建、完成、取消和结算的流程，通过 CreateInstance、Release、OnSuccess 等抽象方法或钩子扩展任务差异。
- 观察者 / 发布订阅（Observer / Pub-Sub）：TaskAdded、TaskRemoved、EventFinished、目标变化以及 EventPipeline 阶段事件让各组件通过通知协作。

抽象基类与多态是支撑这些模式的语言机制；职责分离让新增玩法通常只需补齐具体 Quest、Instance、Point、Game，然后配置面板编号和注册候选。
