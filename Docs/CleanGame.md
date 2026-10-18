# CleanGame 美术与污渍组合

GameScene 的 CleanGame 显式绑定 `Assets/Model/MiniGame/Clean/污渍.png`，擦拭工具由 `抹布.png` 和 `手 擦.png` 组成。原图不修改，也不要求开启 Read/Write。

## 操作

鼠标进入清理区域后，手臂与抹布跟随指针；按住左键拖动擦除。抹布中心即刷子接触点，默认擦拭半径 48 UI 单位。快速移动使用线段擦除，避免漏擦两个采样点之间的路径。移出区域只暂停接触，左键按住状态会继续保留；移回来后自动继续擦除，无需再次按下。重入时从当前接触点开始，避免从离开前的位置跨区域连线。松手（包括在区域外松手）、失焦、暂停或关闭会取消本次按住状态。区域的 RectMask2D 会裁切超出边界的手臂和抹布。

进度只统计实际生成的非透明污渍像素，重复擦同一区域不重复计数；清理 90% 后只通知一次完成。关闭再打开会生成新布局。

## 单张素材的变化

`StainPattern.Compose` 在分区中抖动污痕位置，组合四条主体污痕和四处裁片。每处独立改变旋转、镜像、长宽比例、局部弯曲、棕色明暗及透明度；裁片使用不规则圆润轮廓淡出，保留原图的小液滴。布局使用独立 System.Random，不影响任务或股价的随机序列。

这是同一种污渍风格的多种组合，不会新增另一种物质的纹理。通过改变轮廓与布局降低重复感，后续补充不同美术污渍时可再扩展来源。

## 绑定与检查

- Stain Image：可擦除的 RawImage，保持 Raycast Target 开启。
- Stain Texture：美术污渍 Texture2D；未绑定时提示配置错误，不生成替代图。
- Wipe Tool：接触中心节点，Cloth 在下、WipingArm 在上；装饰 Image 不接收射线。
- Progress Text / Progress Bar：可选进度显示。
- `Tools/MiniGameChecks/CleanArtworkChecks.cs`：通过 Unity 命令桥运行隔离检查和渲染，验证素材绑定、布局变化、悬停不擦除、快速划动、完成回调、关闭与重开，并检查 24 个种子的布局差异。

`Tools/MiniGameChecks/ContinuousDragChecks.cs` 验证跨边界返回、区域外松手、断开轨迹、焦点丢失，以及与拆快递共用的持续按住体验。
