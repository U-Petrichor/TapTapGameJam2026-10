# 弧形液体进度条

安装后位置：`HUDCanvas/ResourcesUI/Cockroach/ProgressBar/LiquidFill`。

如果尚未安装，在 Unity 编译完成后点击菜单 **Tools → UI → Install Cockroach Liquid Progress**。这会为 GameScene 创建材质与 LiquidFill 子物体，并在保存前备份场景。已存在 LiquidFill 时会复用，避免重复创建。

保留原来的 `ProgressBar` 图片作为白色空管，`LiquidFill` 是独立、可编辑的子物体。背景、文字、蟑螂插图以及原来的 RectTransform 均由场景控制，运行时不重排布局。

## 第一次预览

1. 在 Hierarchy 选中 `LiquidFill`，查看 `Arc Liquid Progress` 组件。
2. 拖动 **Progress**（0～1），或点击组件底部的 0%、25%、55%、100%。编辑模式即可看到波浪。
3. 勾选 **Demo Loop** 自动往返演示填充；关闭后回到手动进度。Demo Duration 是单程秒数。
4. Liquid Color 控制液体颜色，Highlight Color 控制亮边。Wave Amplitude 控制前沿翻涌幅度，Flow Speed 控制流动速度，Bubble Strength 控制气泡可见程度。

GameScene 已绑定实际蟑螂数量，场景中的初始 Progress 为 0，Demo Loop 关闭。编辑模式可以手动预览，进入游戏后由 ResourcesUI 覆盖演示值。

## 游戏中的数量进度

`ResourcesUI` 的 Cockroach Manager、Cockroach Progress 两个 Inspector 引用分别绑定场景中的管理器与 LiquidFill。进度为当前存活总数除以管理器的 Victory Population（默认 2000），超出目标显示满格。数字显示当前数量；死亡会让数字减少、液体平滑回退。

`CockroachManager` 统计所有已登记巢穴中的存活数量，白天停止活动不会把巢内蟑螂排除。出生和死亡事件直接刷新 HUD；管理器每帧汇总各巢穴的列表长度，在总数变化时补发更新，覆盖初始化或巢穴移除。没有逐帧遍历每只蟑螂。

首次总数达到 2000 时，管理器设置 HasWon 并发布已有的 `EventGameOver(true, reason)`，每局只发布一次。此前已有失败事件时不再发布胜利。这一改动提供胜利判定与事件，没有新增胜利结算画面或全局暂停逻辑。

HUD 启用和 Start 时都读取当前数量，防止错过初始化事件或把已有数量重置为零。重新启用 HUD 会同步最新值。修改目标只需调整场景中 CockroachManager 的 Victory Population，UI 使用同一个目标。

## 复用于其他数值

在负责实际数值的脚本上暴露 `ArcLiquidProgress` 引用，拖入 LiquidFill 组件，在数值变化时调用：

```csharp
liquidProgress.SetProgress(currentValue / (float)maxValue);
```

输入会限制到 0～1。Play 模式会平滑追随目标进度，编辑模式直接显示目标进度。关闭 Demo Loop 后再绑定实际游戏值。GameScene 中已有 ResourcesUI 驱动，不要再用另一个脚本同时写同一条进度。

## 实现与限制

使用原图透明度裁切液体；通过极坐标让前沿、流纹和气泡沿弧线移动。0 时液体完全隐藏，1 时填满原图且前沿波动停止，附带轻微满格脉动。支持 UI stencil 和 RectMask2D 裁切。暂停时动画仍使用 unscaled time。

Arc Mapping 是按当前 `产卵值进度条.png` 的圆心和半径校准的，更换不同形状的图片后需要重新校准。美术图片应保持普通、非旋转的 Sprite；没有做真实流体物理模拟或管外喷溅。

每个组件拥有独立临时材质，禁用时销毁并还原材质。场景中引用的材质模板是 `Assets/Resources/UI/ArcLiquidProgress.mat`。

`Tools/LiquidProgressChecks/Install.cs` 通过现有 Unity 命令桥安装到 GameScene，安装前保存场景副本至 `RecoverySnapshots/LiquidProgress/`。`LiquidVerify.cs` 可在隔离项目中检查编译、渲染、输入范围、布局不变和材质清理。

`BindPopulation.cs` 配置真实数量引用及 2000 目标；`VerifyPopulation.cs` 在可销毁的预览场景中模拟多巢穴数量，验证初始化、数量增减、1999/2000 阈值、超量、单次胜利、失败后不胜利、HUD 重新启用、巢穴移除及布局/配色不变。
