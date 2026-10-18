# 门开放运镜

## SecurityDoor (10) 闪退与通行修复（2026-10-19）

- 三次编辑器崩溃均落在 DX12 图形线程。用本机 6000.3.25f1 的 `Unity_x64.pdb` 解析最近一次崩溃地址，得到 `GfxVersionList::Impl::ReleaseFromGfxVersionList → BufferD3D12 → GfxTaskExecutorD3D12`。这与 Unity 已知问题 [UUM-150682](https://issuetracker.unity.com/issues/24291/crash-on-gfxversionlistimplreleasefromgfxversionlist-when-performing-various-unity-operations) 的调用栈一致，官方标注 6000.3.27f1 修复。曾临时使用 DX11 验证，但用户反馈贴图错乱，已撤回项目级 DX11 设置，恢复原来的自动 API 配置；不能将 DX11 作为交付方案。DX12 下的闪退尚未解决，后续应在团队统一验证修复版引擎后处理。
- 进屋的直接阻挡来自 `NPCManager/PatrolPoints/WorkSpace1`：这个巡逻范围的 BoxCollider 原先是实体，边界位于 z≈8.50，玩家开门后仍停在 z≈8.24。将它改为 Trigger，保留 NPC 取样所需的 bounds。
- 门新增 `OnTriggerStay` 补注册：玩家先进入检测范围、随后到开放日期时，无需退出范围重进。解锁中和已解锁的门不再重复注册，禁用时清理候选。
- 开门动画事件保留兼容入口，完成逻辑统一在逐帧采样返回后执行，避免动画回调重入 `Animator.Update`。

`Tools/DoorRevealChecks/VerifyDoor10Passage.cs` 使用真实 GameScene 玩家、交互管理器和物理移动，覆盖开放前站在检测范围、到期获得交互、开门关闭阻挡和走进房间。结果保存到 `Temp/Door10PassageVerification.txt`。在新 Play Mode 会话执行，结束后退出 Play Mode，不保存测试状态。

本次在 Direct3D11 下通行测试 7 项通过，玩家从门外 z≈6.81 走到屋内 z≈10.55；原有 `VerifyDoorReveal.cs` 的第 3/5/7 天全部 10 扇门、重复事件、输入与时间恢复、暂停共存和中断清理共 28 项也全部通过，未发生闪退。

```powershell
unity command run_script --file Tools/DoorRevealChecks/VerifyDoor10Passage.cs --timeout 20 --result-only
```

GameScene 已配置 `SecurityDoorRevealDirector`，其子物体 `SecurityDoorRevealCamera` 是独立的 Cinemachine 虚拟相机。平时禁用，播放期间以高优先级接管画面。门仍由玩家交互打开，特写只提示新的开放区域。

## 播放规则

- 天数从 1 开始。GamePhaseManager 进入白天时先发送阶段事件，再发送 `EventNewDay(DayIndex)`。
- SecurityDoor 仅在首次达到 `_whichDayOpen` 时报告开放。第 3 天展示 4 扇，第 5 天展示 3 扇，第 7 天展示 3 扇；`-1` 的两扇不参与。
- 同帧通知合并成一批，从玩家位置出发，按最近邻顺序逐门展示，距离相同时按名称排序。
- 镜头先对齐当前实际画面，保留观察方位，俯角限制为 20–65 度以看清门面。所有门展示完后由 Brain 平滑切回游戏相机。
- 播放期间暂停游戏时间、物理和玩家输入，运镜使用真实时间。玩家原有旋转、缩放和 ActionMap 保留。
- 已有背包、菜单、小游戏和镜头过渡会延后播放。退出白天、游戏结束或禁用运镜管理器会清理播放；已有菜单暂停原因不会被误解除。

## 调整效果

选中场景根物体 `SecurityDoorRevealDirector`：

| Inspector 参数 | 当前值 | 用途 |
| --- | --- | --- |
| Move Duration | 1 秒 | 移动到每扇门的时间 |
| Hold Duration | 1 秒 | 每扇门的停留时间 |
| Return Duration | 1 秒 | 返回游戏镜头的混合时间 |
| Close Up Distance | 7 | 特写相机到取景中心的距离 |
| Priority | 100 | 播放优先级，运行时还会确保高于接手的相机 |

单独选择一扇 SecurityDoor，可以配置：

- Reveal Focus：取景中心的 Transform；留空使用阻挡碰撞体中心。
- Reveal Camera Pose：可选的专用机位，使用其世界位置和旋转，适合需要绕开墙体或强调正面的门。
- Reveal Distance：单门距离；0 表示沿用管理器距离。

## 验证

Unity 6000.3.25f1、Cinemachine 3.1.7 下已完成真实 GameScene Play Mode 检查：三批逐门展示、重复事件不重播、时间与玩家位置冻结、正常返回、禁用中断和暂停原因共存。也验证了旋转 90 度、拉到 89 度高空俯视后一次开放全部 10 扇门，返回后旋转、缩放及实际输出机位恢复。

自动验证脚本保留在 `Tools/DoorRevealChecks`，由项目已有 Unity Pipeline 执行。每个脚本应在全新 GameScene Play Mode 会话中运行，会加速修改测试会话的日期；测试后退出 Play Mode，不保存运行时状态。

```powershell
unity command run_script --file Tools/DoorRevealChecks/VerifyDoorReveal.cs --timeout_ms 55000 --timeout 60 --result-only
unity command run_script --file Tools/DoorRevealChecks/VerifyDoorRevealExtremeView.cs --timeout_ms 30000 --timeout 35 --result-only
```

截图和首轮检查记录输出至 `Temp/DoorReveal*`。场景修改前的本地备份为 `Temp/GameScene_before_door_reveal.unity`。
