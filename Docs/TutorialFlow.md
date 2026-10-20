# 新手教程流程

## 1. 结构

- 持久化：`PlayerProfileData.seenTutorialSteps`（`profile.json`，整个安装一次）。
- 步骤表：`Assets/Resources/Tutorial/TutorialSequence.asset`（ScriptableObject）。
  首次在 Editor 打开项目时由 `Assets/Editor/TutorialSequenceAssetCreator.cs` 生成；
  资产缺失时运行时会使用 `TutorialDefaultContent` 的内存副本兜底。
- 调度：`TutorialDirector`（GameScene，运行时由 `TutorialBootstrap` 自动创建）。
- 显示：`TutorialDialogueView` 绑定场景已有的 `GameScene/Tutorial` Canvas：
  - `Player/TextArea`：主角自白
  - `Boss/TextArea`：老板
  - 没有“系统”文字面板；系统行是纯高亮。
- 高亮：`TutorialHighlightView` 抽象基类，由外部高亮组件继承。
- 首个任务：`TutorialTaskBootstrap` 在第一天暂停随机刷新，老板提示关闭后强制生成指定 CleanPoint 的清洁任务。

## 2. 一次“第一次事件”怎么走

```
业务代码 TutorialSignal.Send("move.first")
    -> EventPipeline
    -> TutorialDirector 记录信号并 Evaluate
    -> 满足 trigger + requires 的步骤入队
    -> 暂停/切 ActionMap/阻断输入，TutorialDialogueView.Show
    -> 玩家点击/E/Space/Enter/Esc
    -> PlayerProfile.MarkSeen(step.id) 立即写 profile.json
    -> 执行 emitOnClose，处理 after: 链和阻塞回调
```

- `trigger` 是信号名，或 `after:上一步ID`。
- `requires` 是前置步骤列表；前置没看时不会出现。
- `markOnShow` 默认 false：关闭时才记录。强退未确认的弹窗下次会再出现。
- `emitOnClose` 用于“关闭后生成首个任务 / 放开随机刷新”这类流程。

## 3. 步骤与信号

| 步骤 ID | 信号 | 说明 |
|---|---|---|
| `day1.intro` | `day.start` | 第一天开始，等开场视频结束 |
| `day1.first_move` | `move.first` | 首次移动；关闭后生成首个清洁任务 |
| `day1.task_guide` | `task.added` | 任务/产卵点/E/Q 教学；关闭后恢复随机刷新 |
| `day1.first_task_done` | `task.first_completed` | 首次完成任务 |
| `day1.coins_highlight` | `after:day1.first_task_done` | 高亮金币栏（不暂停） |
| `day1.countdown_concept` | `after:day1.coins_highlight` | 老板提示倒计时 |
| `day1.countdown_highlight` | `after:day1.countdown_concept` | 高亮倒计时（不暂停） |
| `day1.complex_task` | `task.complex_completed` | 咖啡/股票等复杂任务完成 |
| `day1.nest_marked` | `nest.first_marked` | 白天标记产卵点 |
| `day1.settlement` | `day.settled` | 结算面板确认后 |
| `shopping.home` | `home.entered` | 商店房间淡入完成 |
| `shopping.talent` | `talent.opened` | 打开天赋树 |
| `shopping.warehouse` | `warehouse.opened` | 打开仓库 |
| `night.first` | `night.started` | 夜晚过渡完成 |
| `night.first_egg` | `nest.first_egg` | 首次产卵 |
| `night.first_hatch` | `nest.first_hatch` | 首次孵化 |
| `night.workflow` | `nest.first_command` | 首次切换巢穴指令 |
| `night.search` | `blackbox.first_touched` | 首次碰到搜索箱 |

信号常量在 `TutorialSignals`，步骤 ID 在 `TutorialStepIds`。

## 4. 加一条新教程

1. 在 `TutorialSignals` 加信号名（或在业务代码里直接发已有信号）。
2. 在触发点加一行 `TutorialSignal.Send(TutorialSignals.Xxx);`。
3. 在 `TutorialDefaultContent` 加默认步骤（可选，便于重置资产）。
4. 在 `TutorialSequence.asset` 里加条目；或重新执行 `Tools/Tutorial/Create or Reset Tutorial Sequence`。
5. 用 `Tools/Tutorial/Validate Sequence` 检查 ID、trigger、after/requires 引用。

## 5. 运行细节

- 弹窗期间 `Time.timeScale = 0`，`GamePauseManager` 用 `_tutorialOpen` 参与暂停判断。
- 输入：`PlayerInputManager.SetTutorialBlocked(true)` + 临时切到 `ESCMenu` ActionMap；关闭后恢复原 ActionMap。
- 点击任意位置 / E / Space / Enter / Esc 推进。
- `Tutorial` Canvas 显示时 SortingOrder 临时提到 210，关闭恢复原值（75）。
- 夜间 `TaskInteractionPromptUI` 对 `CockNest` / `BlackBox` / `PickableItem` 放行 E 提示。
- 产卵点预制体需要在 `Interactable._interactionPromptSprite` 配“按 E 产卵”的图片。

## 6. 内容修正

- “Q 键切换任务”统一改为“Q 键切换可交互物”（Q 的实际行为是切换附近交互目标）。
- 首孵是 30 秒计时，不消耗营养；文案写成“第一窝会自动孵化，之后孵化需要营养”。
- 夜间不能搬巢；文案写成“白天可以把产卵点搬到想要的位置”。
- 炒股当前只结算金币、不发怒气点；文案用“复杂任务可以累计怒气值”，如要给股票加怒气需另改奖励逻辑。

## 7. 测试

1. 删除 `profile.json` / `.bak`（或 `Tools/Tutorial/Reset Tutorial Progress`）后开新游戏。
2. 检查完整首日顺序、商店/夜晚步骤、每个步骤只出现一次。
3. 弹窗期间按 Esc 只关弹窗，不打开暂停菜单。
4. 在天赋树/仓库/小游戏上触发教程，关闭后 ActionMap 恢复。
5. 强退未确认的弹窗，下次启动应再次出现。
6. 重启/继续/开新游戏，已看步骤不再出现。
