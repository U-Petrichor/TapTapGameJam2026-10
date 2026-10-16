# MiniGame 任务系统结构总结与清理审计

这份文档记录当前任务系统的结构、已经稳定的设计，以及下一轮代码清理的候选项。此次只做审计，没有删除新的代码。

## 当前任务流

```text
MiniGameManager
    └─ MiniGameCandidate      决定任务现在能不能生成，并创建一次任务实例
        └─ MiniGameInstanceBasic  保存目标位置、奖励、成功/取消生命周期
            └─ PanelMiniGameInstance  把世界交互点和面板小游戏连接起来

PanelTaskPoint
    └─ 玩家与世界中的任务点交互
        └─ EventOpenMiniGamePanel
            └─ MiniGamePanelUI      管理弹窗、输入、关闭和当前小游戏
                └─ MiniGameContent   运行具体的 UI 玩法
```

这套分层的优点是：任务生成、世界交互、面板生命周期和小游戏规则各自有边界。一个新小游戏通常只需要实现一个任务候选、一个实例、一个世界点位和一个 `MiniGameContent`。

## 目录职责

| 目录 | 当前职责 |
| --- | --- |
| `MiniGame/` | 任务管理器、候选任务、实例基类、面板基类和默认 UI 配置 |
| `MiniGame/CleanGame/` | 清洁任务与擦除规则 |
| `MiniGame/DeliverFileGame/` | 送文件任务与两个世界交互点 |
| `MiniGame/FixPrinterGame/` | 打印机 QTE、任务和编辑器配置 |
| `MiniGame/ParcelCutGame/` | 拆快递随机胶带路径玩法、任务和编辑器配置 |
| `MiniGame/MakeCoffeeGame/` | 新咖啡 UI 玩法、配方和器具交互 |
| `MiniGame/PourDogFoodGame/` | 倒狗粮玩法、任务和晃动规则 |
| `MiniGame/SortFileGame/` | 分拣玩法、文件卡片、任务和编辑器配置 |
| `MiniGame/StockTradeGame/Common/` | 炒股任务入口、价格生成、交易回合和坐标轴 |
| `MiniGame/StockTradeGame/V2/` | 当前唯一保留的点线版炒股 UI |
| `MiniGame/TaskGuidance/` | 任务箭头和屏幕边缘定位；其中几何算法也被消毒房间箭头使用 |
| `MiniGame/Editor/` | 全套小游戏的场景配置和 Play 模式检查 |

## 可以保留的核心代码

这些代码目前都有明确职责或被多个小游戏复用，暂时不建议删除：

- `MiniGameManager`、`MiniGameCandidate`、`MiniGameInstanceBasic`：任务生成和任务实例生命周期。
- `PanelTaskPoint`、`PanelMiniGameInstance`、`PanelMiniGameQuest`：世界点位、面板打开和点位池任务的公共桥接。
- `MiniGamePanelUI`、`MiniGameContent`：弹窗生命周期和 UI 玩法入口。
- `MiniGameDefaultSetup`：运行时补齐默认面板；编辑器配置后的场景仍然可以依靠它恢复引用。
- 每个游戏的 `Quest`、`Instance`、`Point` 和 `Round`：它们分别承担任务、奖励、世界点位和规则状态。
- `TaskIndicatorGeometry`：除了 `TaskIndicatorUI`，还被 `DisinfectionArrowView` 使用，不能只按文件夹位置判断为多余。
- `DeliveryFileInstance`：送文件实例，负责起点、终点和携带文件的生命周期。

## 第一批清理候选

### 咖啡小游戏的旧 3D 交互链

当前 `GameScene` 使用的是 `CoffeeGame` 和 `CoffeeRecipe`。旧的 `CoffeeMinigameManager` 与 `CoffeeDeskInteractor` 已从运行代码删除；`CoffeeClickable`、`CoffeeDraggable`、`CoffeeDropZone` 仅作为旧场景资产保留，主场景不再依赖它们。

它们仍然被 `Assets/_Recovery/0.unity` 等恢复材料引用，也被 `Docs/CoffeeDragExample.md` 记录。因此它们属于“可迁移到 Legacy 或待确认删除”，不是现在可以无条件删除的代码。

清理条件是：确认这些旧场景不再用于试玩、回滚或教学；之后可以把这些脚本和文档移到 `RecoverySnapshots`，再从 `Assets/Scripts/MiniGame/MakeCoffeeGame` 删除。

### 炒股编辑器配置入口

`StockPointVersionSetup` 现在只负责调用 `MiniGameDefaultSetup.EnsureStock`。由于 V1 已删除、V2 已经是唯一版本，这个菜单可以继续保留作为“一键修复场景配置”，也可以把它合并进 `MiniGameSuiteSetup`，减少一个编辑器入口。

`EnsureStock` 也是只被这个编辑器工具和测试使用。如果团队不再需要单独配置炒股面板，可以合并后删除这个方法；这属于整理重复入口，不是玩法代码清理。

### 默认配置器的体积

`MiniGameDefaultSetup` 同时负责清洁、炒股、咖啡、打印机、任务点注册和通用 UI 创建。它当前能工作，但随着小游戏增加会继续变长。

下一阶段可以拆成：

- `MiniGamePanelDefaultSetup`：公共面板和注册。
- `CleanGameSetup`、`StockGameSetup`、`CoffeeGameSetup` 等每个游戏一个配置器。
- `MiniGameDefaultSetup` 只做分发。

这个改动主要改善可读性，不建议和删除遗留代码同时进行。

## 暂时不要做的清理

- 不要因为脚本没有直接被 C# 调用就删除它；Unity 场景和预制体可能通过 `.meta` GUID 保存引用。
- 不要先改公共基类的命名空间；当前部分脚本位于 `MiniGame`，但命名空间仍是 `TapTapGameJam.UI`，改 namespace 会影响序列化组件类型。
- 送文件实例已经统一为 `DeliveryFileInstance`，不再保留错误拼写。
- 不要删除 `MiniGameSuiteSmokeChecks`；它虽然不是运行时代码，但承担跨小游戏的输入、UI、物理和结算回归检查。

## 这次任务系统的经验总结

1. **候选和实例分开**：候选决定“能不能生成”，实例代表“一次已经生成的任务”。这样每日任务、随机任务和取消都能使用同一套管理器。
2. **点位池和条件分开**：Quest 维护同类型点位池，点位通过 `ITaskSpawnCondition` 检查门、日期或前置任务；条件对象自己维护状态并提供只读原因，增加限制不需要修改具体 Quest。
3. **世界点位和面板玩法分开**：`PanelTaskPoint` 处理玩家在世界中的交互，`MiniGameContent` 处理弹窗内的玩法，避免小游戏逻辑直接控制世界任务池。
4. **奖励只在实例完成时结算**：实例统一防止重复完成、重复奖励和旧回调污染新一轮任务。
5. **纯规则尽量脱离 Unity**：`CoffeeRecipe`、`SortFileRound`、`PrinterRepairRound`、`WipeMask`、`StockRound` 等可以在 .NET 工具中测试，反馈比只依靠 Play 模式更快。
6. **编辑器配置和运行时兜底是两套职责**：编辑器工具负责把场景保存成可检查的状态，`MiniGameDefaultSetup` 负责运行时补齐缺失对象；两者都存在时要保持配置方向一致。
7. **遗留代码要先确认资产边界**：只要旧场景还引用脚本，它就不是单纯的“没被当前主场景使用”，而是一个需要产品决定的兼容资产。

建议下一轮清理顺序是：先确认恢复材料是否还需要旧咖啡辅助组件；之后再拆分 `MiniGameDefaultSetup`，最后统一 namespace 等结构性改动。
