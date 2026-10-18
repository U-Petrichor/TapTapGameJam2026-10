# 分拣文件小游戏

实现文件：

- `Assets/Scripts/MiniGame/SortFileGame/SortFileGame.cs`：UI 生命周期、生成文件堆、接收区域判断、归位动画及完成回调。
- `SortFileCard.cs`：文件拖动、归位和部门文字。
- `SortFileRound.cs`：四部门枚举和不依赖 Unity 的栈顶分类规则。
- `Assets/Scripts/MiniGame/SortFileGame/SortFilePoint.cs`：固定工作台交互，使用 `EventOpenMiniGamePanel(2, callback)` 打开，使用 `EventCloseMiniGamePanel` 取消窗口。
- `SortFileQuest.cs`：单点任务候选，完成后通知管理器并结算奖励。

## 场景与素材（2026-10-18）

GameScene 已保存文件堆、拖动层、禁用的文件模板、四个部门和公共面板 `MiniGameKind.SortFile` 的映射。旧的一键 Setup 菜单已移除；现在直接在 Inspector 调整并保存绑定，运行时不再补建布局。

素材统一使用 `Assets/Model/MiniGame/SortFile` 中的 JPG，按 Sprite Single 导入：人事、行政、技术、设计文件分别对应同部门文件夹。微信命名的紫色文件夹 `微信图片_20261018184624_150_37.jpg` 是设计部门。文件图片自带名称，因此隐藏模板上的重复文字；部门计数位于文件夹下方。

新增工作台时，手动配置 SortFilePoint 的检测 Collider、SortFileQuest 的点位列表及管理器的 Candidate Pool。场景现有的任务绑定已保留，不依赖自动初始化。

## 玩法规则

每次 Begin 默认生成 12 份文件，四个部门各 3 份并随机打乱；Files Per Department 可以调整每部门数量，关闭 Shuffle 后使用固定部门顺序。

只有栈顶文件接收拖动。按住文件拖动，松手后用**文件中心位置**判断投放区域。部门正确则吸附到对应区域并叠放、增加计数，再开放下一份；部门错误、区域外或同时落入重叠区域时返回中央栈顶，不增加计数。拖动和归位期间不允许拿其他文件。

所有文件正确归类后只通知一次完成。世界交互收到成功结果后关闭检测、取消注册，并由 Quest 通知任务管理器移除任务，默认奖励 30 金币及 1 天赋点，奖励可在 Inspector 修改。

中途关闭窗口保留世界任务；重新打开重新生成文件堆。阶段切换或禁用任务/工作台则取消整个任务、不发奖并关闭窗口。旧任务和旧尝试回调不能完成后来的任务或重试。

## 自定义 UI 绑定

SortFileGame 的 Departments 列表恰好配置四项，对应 HumanResources / Administration / Technical / Design，每项绑定独立 RectTransform 接收区域、对应 File Sprite、文件颜色及可选的计数文字。区域不要互相重叠；区域本身不需要 Collider 或 DropHandler。

File Stack Root 是中央文件堆；Drag Root 建议铺满整个小游戏并与其保持相同缩放；File Template 是**默认禁用**的 SortFileCard 模板（场景对象或 Prefab），带 Image、CanvasGroup 和部门文字。文件 RectTransform 使用居中锚点及中心 Pivot。不要让 LayoutGroup/Animator 同时驱动拖动中的文件。Image 保持 Raycast Target 开启，文件文字和其他装饰关闭 Raycast Target。Canvas 需要 GraphicRaycaster、场景需要正常工作的 EventSystem UI 输入模块。

默认 Move Duration 0.18 秒，Stack Offset 控制中央堆叠错位，Sorted Offset 控制部门中的堆叠错位。脚本使用 unscaledDeltaTime 驱动归位动画。

## 验证范围

`dotnet run --project Tools/SortFileChecks/SortFileChecks.csproj` 运行 13 项真实分拣规则与任务回调检查。任务测试只替代 Unity 对象、物理注册和资源依赖，不覆盖原生 UI 射线、触发器、动画或对象销毁。

Play 模式还应检查：只有栈顶能拖动；错误和区域外投放复位；归位期间不能拿其他文件；正确文件留在对应部门；最后一份之后收回弹窗并只发一次奖励；关闭重试；换天取消；不同窗口尺寸下的拖动坐标。

当前代码跟随项目新拆分的 EventOpenMiniGamePanel / EventCloseMiniGamePanel 接口。公共 Panel 必须完成对应事件订阅，不能再读取打开事件上的旧 IsStart 字段；游戏完成和关闭时也应恢复玩家操作。正在调整的公共事件/输入代码未由这个小游戏实现覆盖。
