# 炒股点线版试玩

本次新增了一个独立的 `V2/StockPointGame` / `V2/StockPointChart` 版本，参考用户提供的 `index.html`：开局只显示开盘点，之后每确认一个报价就增加一个点并连接一条线；达到 13 个点后继续压缩横轴，让历史点和右侧唯一待吐点始终留在图表内。

## 与原版的关系

- 原版 `StockGame`、`StockChart` 保留，仍然使用开局 12 个历史报价和连续绘制。
- 新版复用 `StockPriceGenerator`、`StockRound` 的价格生成、买卖、单股持仓、结算和每日次数规则。
- `StockRound` 新增可选的初始点数量，默认仍为 12；点线版传入 1，不影响原版和原有验证。
- 新版不显示连续插值线头。点确认时显示新点，并把它与上一个已确认点连接；待吐报价只在内部预计算，不在图像上显示位置、圆点或文字。
- 新版只绘制水平方向网格线，删除竖直网格线。

## 在 Unity 中切换

打开 `GameScene`，等待编译完成，在菜单选择：

- **Tools → MiniGames → 使用炒股点线版**：在现有 `TradeStocks` 面板上添加并启用点线版组件。
- **Tools → MiniGames → 恢复炒股连续版**：关闭点线版，恢复原 `StockGame` / `StockChart`。

切换命令只修改当前场景中的组件启用状态和面板注册，不会自动保存场景。确认策划试玩版本后再保存 `GameScene`。

## 横向布局规则

点线版内部会为下一报价预留一个时间槽，但这个未知报价不会绘制出来：

- 开局 1 个确认点，预留 12 个间隔，开盘点在左侧。
- 确认点数量达到 13 个时，最后一个已确认点接近右侧，右侧保留空白时间槽。
- 之后分母随确认点数量增加，所有历史点按比例压缩；新报价确认后才在右侧空白处出现。

这与参考页面的“`max(12, points.length - 1)`”布局意图一致，但额外为 Unity 版本保留了唯一待吐点的位置。

## 验证

- 核心旧脚本快照：`RecoverySnapshots/20261009-stock-core-before-pointline/`。
- 全项目 Unity 引用编译通过，0 错误；新增点线图表只产生一个构造函数警告已修正，剩余为项目原有未使用字段警告。
- 原有逻辑检查仍可运行；当前工作区在本次改动前已有 `StockQuest.DailyPlayLimit = 999`，与“每天一次”的旧检查断言冲突，因此综合工具在每日次数场景提前失败。这是工作区已有改动，不由点线版引入。


## 当前目录

- V1 连续绘制版位于 `Assets/Scripts/MiniGame/StockTradeGame/V1/`。
- V2 点线版位于 `Assets/Scripts/MiniGame/StockTradeGame/V2/`。
- 共用交易与任务代码位于 `Assets/Scripts/MiniGame/StockTradeGame/Common/`。
