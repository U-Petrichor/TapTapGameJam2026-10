# 倒狗粮小游戏配置

拖住粮袋，使倾倒姿态下的袋口进入 PourArea 后持续朝左下方出粮，不需要左右晃动。出袋只赋予初速度，之后由重力和碰撞决定轨迹，不自动瞄准碗内随机落点。移出接粮区或松手后停止生成，已经生成的颗粒继续落下。三种图案共同计数，累计 100 颗落入碗中后只回调一次成功。洒在碗外的颗粒不计数，清理后可继续倒满。重新打开小游戏清空颗粒及进度。

## 场景与绑定

`Assets/Scenes/GameScene.unity` 的 PourDogFoodGame 已绑定 FoodContent 及 `Assets/Model/MiniGame/PourDogFood` 下的 Food1、Food2、Food3 Sprite，无需创建颗粒预制体。

- Food Bag：粮袋的 RectTransform，直接放在小游戏根区域下。
- Pour Origin：粮袋子物体，放在倾倒时的袋口。
- Pour Area：盆口上方的逻辑接粮区域，不需要 Image 或 Collider。
- Food Content：位于碗后层与前沿之间的 RectTransform。所有颗粒均在此物体下创建；矩形下边作为碗底、左右边作为碗壁，上边作为碗口。调整它的位置和宽高，使矩形覆盖碗内部可堆粮区域。不要挂 LayoutGroup 或 Mask，否则会干扰颗粒运动或裁掉下落过程。进入碗内的颗粒会保持在左右边界之间，粮堆可以高于碗口。
- Food Sprites：狗粮图案列表，当前已绑定三张 Sprite，每颗随机选择一种。
- Progress Bar、Food Fill、Pour Effect：可选显示。场景的进度条及其引用已移除；完成判定只依赖实际落入碗中的数量，不依赖进度条。Pour Effect 在有效出粮时持续显示。

所有装饰图片和动态狗粮 Image 均不接收射线，只有粮袋 Image 开启 Raycast Target。Canvas 需要 GraphicRaycaster，场景需要 EventSystem 及项目使用的 InputSystemUIInputModule。

## 可调参数

| 参数 | 默认值 | 用途 |
| --- | --- | --- |
| Target Food Count | 100 | 完成需要的落地数量 |
| Foods Per Second | 15 | 每秒生成颗粒数，默认约七秒完成 |
| Pour Angle | -25 | 倾倒时粮袋附加旋转角度 |
| Food Size | 16 | 颗粒最长边，使用 FoodContent 局部 UI 单位 |
| Size Variation | 0.2 | 尺寸随机浮动比例 |
| Fall Duration | 0.45 | 用于换算重力的参考下落时长（秒），接触粮堆后实际时长会变化 |
| Mouth Spread | 5 | 袋口横向生成范围的半宽 |
| Pour Velocity | (-120, -60) | 出袋初速度（UI 单位/秒），负 X 朝左、负 Y 朝下 |
| Pour Speed Variation | 0.15 | 初速度大小随机浮动比例，保持左下方向 |
| Collision Radius Scale | 0.45 | 圆形碰撞半径与图片最长边的比例，越大堆积越蓬松 |

狗粮使用 UI Image 和 UI 局部坐标中的圆形碰撞模拟，不需要手动添加 Rigidbody2D 或 Collider2D。颗粒受重力下落，彼此碰撞、挤开并在碗底逐层堆叠；接触后仍会继续参与模拟，不会固定在随机落点。使用固定小步长、碰撞分离和接触摩擦保持稳定，不受 Canvas 世界缩放和全局 Physics2D 设置影响。碰撞形状为近似圆形，不逐像素贴合三种图案。FoodContent 中非本组件创建的物体不会被清理。

停止输入只停止新颗粒，空中颗粒继续下落、粮堆继续沉降。接触碗底或由碗底支撑的粮堆时首次计数，每颗只计一次；碗内数量与仍在下落的数量合计封顶 100，洒到碗外并清理后释放名额，避免任务卡住。只有实际进入碗内才启用碗底和内壁约束，碗外颗粒不会瞬移进碗。粮堆高于碗口也能正常完成。End 冻结当前画面，供公共面板收回；ResetView 或禁用内容时清理颗粒。

## 任务接入

继续沿用 PourDogFoodPoint、PourDogFoodQuest、PourDogFoodInstance 和公共 MiniGamePanelUI 的现有接入，无需改变任务奖励逻辑。交互点使用 MiniGameKind.PourDogFood，任务组件放入 MiniGameTaskManager 的 Candidate Pool。

关闭窗口保留未完成任务；重新打开从零开始倒粮。成功后由现有任务回调关闭检测、移除任务并发奖。阶段切换或禁用组件会取消旧任务，不发奖。

## 验证

- Play 模式打开后为空盆，进度为零。
- 盆外拖动或静止按住不出粮；袋口对准盆后静止按住持续出粮，三种图案混合出现。
- 松手后粮袋复位且不再生成，空中颗粒继续落下；再次拖回可接着倒。
- 颗粒在 FoodContent 下且被碗前沿正确遮挡，不挡粮袋拖动。
- 狗粮在碗底及其他狗粮上堆叠，彼此不直接穿过；调整 FoodContent 的下边和左右边使碰撞边界贴合碗内壁。
- 从袋口朝左下方出粮，再自然下落；碗外狗粮不会横向吸附或瞬移进碗。
- 碗内第 100 颗落下后成功一次，任务奖励一次；洒落不计数，仍可继续倒满。
- 中途退出及重新打开无残留；不同分辨率下袋口及下落起点正确。

逻辑检查（包含碗底碰撞、相互堆叠、高速下落、100 颗粮堆、持续生成、停止恢复、帧率、数量上限、落地完成及旧晃动模型回归检查）：

`dotnet run --project Tools/DogFoodShakeChecks/DogFoodShakeChecks.csproj`

任务生命周期检查：

`dotnet run --project Tools/PourDogFoodQuestChecks/PourDogFoodQuestChecks.csproj`

这些检查不替代 Unity Play 模式的视觉和输入验证。
