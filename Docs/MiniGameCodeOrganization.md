# 小游戏代码目录

小游戏按玩法统一归入 `Assets/Scripts/MiniGame`。同一个游戏的世界交互、任务、回合规则、界面和绘图组件放在同一个目录，编辑器工具放在该游戏的 `Editor` 子目录。

## 归类结果

| 目录 | 职责与本次迁入脚本 |
| --- | --- |
| `MiniGame/` | 公共管理器、任务与交互基类；迁入 `MiniGameContent.cs`、`MiniGamePanelUI.cs`、`MiniGameDefaultSetup.cs` |
| `MiniGame/CleanGame/` | 清洁任务；迁入 `CleanGame.cs`、`WipeMask.cs` |
| `MiniGame/MakeCoffeeGame/` | 咖啡任务、配方和操作；迁入 `CoffeeGame.cs` |
| `MiniGame/PourDogFoodGame/` | 倒狗粮任务；迁入 `PourDogFoodGame.cs`、`DogFoodShakeProgress.cs` |
| `MiniGame/SortFileGame/` | 分拣文件任务；迁入 `SortFileGame.cs`、`SortFileCard.cs`、`SortFileRound.cs`；配置工具迁入 `Editor/SortFileGameSetup.cs` |
| `MiniGame/FixPrinterGame/` | 修理打印机任务；迁入 `PrinterRepairGame.cs`、`PrinterRepairRound.cs`；配置工具迁入 `Editor/PrinterRepairSetup.cs` |
| `MiniGame/StockTradeGame/Common/` | 两个版本共用的交易、任务入口、价格生成和动态坐标轴：`StockRound.cs`、`StockPriceGenerator.cs`、`StockAxisRange.cs`、`StockQuest.cs`、`StockInstance.cs`、`StockTradePoint.cs` |
| `MiniGame/StockTradeGame/V1/` | 旧连续绘制版：`StockGame.cs`、`StockChart.cs`、`StockPlotGraphic.cs`、`StockChartView.cs`、`StockCurve.cs` |
| `MiniGame/StockTradeGame/V2/` | 新点线版：`StockPointGame.cs`、`StockPointChart.cs`、`StockPointPlotGraphic.cs` |
| `MiniGame/StockTradeGame/Common/Editor/` | 两个版本的切换配置：`StockPointVersionSetup.cs` |
| `MiniGame/DeliverFileGame/` | 送文件任务、取件与送达交互；沿用已有目录 |
| `MiniGame/TaskGuidance/` | 多种小游戏共用的任务指引；迁入 `Arrow.cs`、`TaskIndicatorUI.cs`、`TaskIndicatorGeometry.cs` |
| `MiniGame/Editor/` | 共用配置与编辑器验证；迁入 `MiniGameSuiteSetup.cs`、`MiniGameSuiteSmokeChecks.cs` |

`Assets/Scripts/UI` 继续承载通用菜单、HUD、世界 UI 和 `Button3D`。场景预制体匹配工具继续位于 `UI/Editor`。原 `UI/MiniGameContents` 目录清空后移除。

## 迁移约定

本次只整理目录，其中炒股脚本进一步按 V1、V2 和 Common 拆分。原有 28 个小游戏脚本迁移记录保持不变；本次炒股目录移动 15 个脚本。每个脚本随原 `.meta` 一起迁移，保留 GUID、类名、命名空间与序列化字段。迁入脚本仍使用原有的 `TapTapGameJam.UI` 等命名空间；已有场景和预制体继续使用原脚本引用。

新增小游戏沿用现有 `XxxGame` 目录命名，公共代码放在根目录或按共用职责分组。所有编辑器代码都放在名为 `Editor` 的目录中，保持与运行时代码的编译边界。

`Tools` 下六个验证工程已更新源码路径；纯逻辑验证明确列出编译文件，以便游戏目录同时容纳 Unity 界面组件。Unity 生成的 IDE 工程通过编辑器重新同步。

## 2026-10-09 验证记录

- 28 个脚本的源码及 `.meta` 文件 SHA-256 与迁移前一致；对应 GUID 在 Assets 中均唯一。
- Unity 中 28 个 GUID 均解析到新路径，对应类型位于原运行时或编辑器程序集，场景内 10 个迁移组件引用有效。
- 六套现有检查通过：小游戏综合 53 项、分拣文件 13 项、倒狗粮任务 7 项、晃动倒粮 5 项、咖啡配方 9 项、任务指引 6 项，共 93 项。
- 全项目脚本引用 Unity 程序集编译通过，0 错误；保留原有的 4 个未使用字段警告。
- GameScene 磁盘文件未改变，编辑器中原有未保存修改仍保留。迁移前已有的 `CockroachManager` 和 `InteractionSelectorUI` 各一个 Missing Script，迁移后对象与数量一致。

此次为目录迁移，验证覆盖编译、逻辑检查及引用完整性；游玩和输入操作未重新测试。
