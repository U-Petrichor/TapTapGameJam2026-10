# 游戏结局

一局游戏以哪种方式结束 → 切回 `MainMenu` → 全屏播一张结局图 → 点击继续回到主菜单。

## 数据流

```
GameScene:  触发点 → GameEndingDirector.Trigger(ending)
              └ 暂停（0.1 倍速）→ 延迟 → 黑场淡入 → EndingState.Set(ending) → LoadScene("MainMenu")

MainMenu:   EndingScreenUI.Awake
              └ EndingState.Consume()
                  ├ 没有 → 自己关掉，主菜单正常显示（ESC 退回主菜单不会误弹）
                  └ 有   → 关主菜单 → 全黑 → 淡出露出结局图 → 3 秒 → 「点击继续」→ 点击 → 回主菜单
```

**只在内存里传递，不写盘**：重开游戏直接进主菜单，不会又弹一次结局图。

## 五个结局与触发点

| 结局 | 触发位置 | 延迟 | 判据 |
| --- | --- | --- | --- |
| 踩死 `Crushed` | `PlayerHealth.HandleDefeat` | 1s | 死因 = 受伤（只有猎杀者保安造成） |
| 喷死 `Sprayed` | `PlayerHealth.HandleDefeat` | 1s | 死因 = 中毒（只有保洁喷雾造成） |
| 过劳死 `Overwork` | `EventNewDay` | 4s | `DayIndex >= 10` 且没触发占领 |
| 区域消杀 `Disinfection` | `DisinfectionManager.ExecuteDisinfection` | 2s | 消杀前 > 0 且消杀后 == 0 |
| 占领 `Occupation` | `EventNewDay` | 0s | `HasWon` 或 数量 == `victoryPopulation` 或 玩家有「群颚破壁」 |

占领优先于过劳死：两者都在"进入白天"这一刻判，顺序写死在 `HandleNewDay` 里。

## 已定的设计细节

**1. 所有结局统一暂停，但是 0.1 倍速而不是冻死。**
`GamePauseManager.SetGameEnding(true)` → `Time.timeScale = 0.1`。理由是结局前那段延迟是给玩家
"看清发生了什么"的（被踩、被喷、成片消杀），彻底冻住反而看不出戏。该值在
`GameEndingDirector.EndingTimeScale` 里集中定义。

**2. 播报不会被暂停。**
广播视频用的是 `VideoTimeUpdateMode.UnscaledGameTime`，改 `timeScale` 不影响它。
同理，结局流程里所有计时都用 `Time.unscaledDeltaTime` —— 用 `WaitForSeconds` 会慢十倍。

**3. 黑场过渡跨场景。**
GameScene 这边在延迟结束后用代码建一块全屏黑 `Image`（sortingOrder 32760）淡入；
MainMenu 那边的结局屏开局就是全黑，再淡出露出图片。所以中间不会闪白。
不在 GameScene 里 authored 黑幕，是为了不动那个 23MB 的场景文件 —— 这是本局最后 0.5 秒的一次性开销。

**4. 消杀结局必须等一帧再判。**
蟑螂的 `Die()` 走 `Destroy`（帧末才真正销毁），巢穴名单要到 `OnDisable` 才摘除，
所以消杀结束那一帧查 `CockroachTotalCount()` 拿到的还是消杀前的数字。
`CheckDisinfectionEnding` 里 `yield return null` 之后再判。

**5. 占领判定带上 `HasWon`。**
清晨会先清掉没归巢的蟑螂，只看当前数量的话，"夜里明明已经达标"会被判成没赢。
`CockroachManager.HasWon` 在夜里达标的那一瞬间就置位了，用它兜底。

**6. 结局不动存档。**
回主菜单后点"继续"仍会从最近一个检查点重来（等于重试这一局）。

## 资源

- 结局图：`Assets/UI/结局/*.PNG`（3840×2160）。导入设置是 **`textureType: 0`（普通贴图）**，
  所以 MainMenu 里用 **`RawImage.texture`** 直接引用，**不需要改成 Sprite**。
- 结局屏：直接 authored 在 `MainMenu.unity` 的根 `Canvas` 下（`EndingScreen` → `Picture` / `Black`），
  是画布最后一个子物体，所以盖在主菜单上面。
- 「点击继续」：场景里没 authored `_hint` 的话，运行时用 `_hintFont`（`fusion-pixel-12px …`）建一个；
  要精确控制位置/样式的话，自己在 `_hintParent` 下 authored 一个 TMP 文本并拖到 `_hint` 上。

## 排查

| 现象 | 原因 |
| --- | --- |
| 进不去结局，Console 报"场景里没有 GameEndingDirector" | `GameScene` 的 `GameManager` 物体上要挂这个组件 |
| 弹的是黑屏 | `EndingScreenUI._pictures` 里没配到那个结局，会有 warning |
| 结局屏不出现，但主菜单正常 | `EndingState` 没被写入（触发点没走到） |
| 结局图一闪就没了 | `Black` 不是 `Picture` 的后一个兄弟节点（同级顺序决定谁盖住谁） |
