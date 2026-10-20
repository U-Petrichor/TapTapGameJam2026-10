# 任务方向指示器

预制体：`Assets/Prefabs/UI/HUD/Arrow.prefab`。

- `Content/TaskIcon`：任务图案；`Content/TaskReward`：奖励或每日活动开放信息。内容不会随箭头旋转。
- `ArrowImage`：单独移动并旋转，始终沿任务方向环绕内容。
- `Content` 同时作为环绕中心；调整 `OrbitRadiusPoint` 与中心的距离即可改变圆周半径。默认半径 156，给奖励文字保留间距。
- `Arrow` 组件的 `Direction Smooth Time` 控制方向变化的过渡时间，设为 0 则直接跟随方向；`Angle Offset` 对齐箭头图片原始方向。
- 图案、尺寸、颜色、中心和半径均可在预制体 Inspector 中编辑。运行时按实际图形边界保留屏幕边距。

送文件与分拣文件共用文件图案，其余任务分别使用白天任务目录中的对应素材。炒股沿用每日剩余次数信息，分成两行显示。

`Tools/TaskIndicatorChecks/UnityArtworkChecks.cs` 可通过 Unity 命令桥在编辑模式执行。它在独立预览场景中检查图案、奖励、圆周位置、屏幕边界、角度跨越和显隐，并生成 `Temp/TaskIndicatorPreview.png`；不会保存或修改游戏场景。
