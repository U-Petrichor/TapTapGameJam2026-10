# MainMenu UI

MainMenu 场景中的五个按钮依次为：开始游戏、继续游戏、设置、图鉴、退出游戏。

- 继续游戏根据 SaveManager 的存档读取结果启用；无可用存档时变淡并显示“暂无存档”，键盘上下导航会跳过它。
- Settings Canvas：左侧切换快捷键说明与声音设置；音效、音乐接入 SoundManager，使用 PlayerPrefs 保存设置。
- 动画 ON/OFF 先保存偏好，后续动画代码可读取 SettingsMenu.AnimationsEnabled；目前不控制角色或过场动画。
- GalleryCanvas：空白图鉴页面，带标题和返回按钮。
- ExitConfirmationCanvas：遮罩拦截后方点击，取消返回，确定退出；编辑器中确定会停止 Play。
- 返回按钮或 Esc 关闭当前页面，焦点回到对应主菜单按钮。

设置页使用 `Assets/Resources/UI/SettingsMenu.prefab`，主菜单引用同一个 Prefab，游戏内 ESC 菜单按需加载。美术来自 `Assets/Model/UI/菜单组件/系统设置/`，以 1920×1080 为基准，CanvasScaler Expand 保持内容比例。Design 下包含 ShortcutsPage 与 SoundPage，两页切换不会重新生成物体。背景拦截后方点击，装饰图不接收射线。

- 页签切换同步蓝/灰胶带与图钉位置。
- 游戏内设置打开时保留 ESC 菜单的暂停状态，返回按钮或第一次 Esc 只关闭设置，焦点回到设置按钮。
- 快捷键说明按当前输入配置显示 WASD、E/F、Q；存档显示暂未开放。箭头仅为说明装饰，不提供改键。
- 鼠标左键图示由右键素材镜像；滚轮图示使用滚轮高亮素材搭配原生 UI 箭头。
- 显式重建入口：`Tools/UI/Build Settings Artwork Prefab`，仅生成 Prefab，不自动保存场景。主菜单重建工具也会引用该 Prefab。

快捷键目前是说明页，不提供重绑定。

主菜单美术使用 `Assets/Model/UI/开始界面/` 中的独立 PNG，以 `示意图1.PNG` 的构图为基准。`Canvas/MainMenuPanel` 为 1920×1080 的设计区域，包含 Background、Title、StartGame、ContinueGame、Settings、Gallery、Exit 和 UnavailableHint；所有位置、文字、图片和按钮引用可在 Hierarchy / Inspector 中编辑。人物、办公室及左下蟑螂已包含在背景图中。

- CanvasScaler 使用 Expand 保持完整构图，非 16:9 比例显示深色留边。
- 每个按钮有局部透明 Image 作为点击范围、独立 Artwork 和 TMP Label。Artwork 使用原始 1920×1080 透明图层来对齐美术，但不接收射线。白色为普通状态，黄色为悬停、按下及键盘选中状态。
- 右下角 Exit 的文字为“原地下班”，继续使用已有退出确认弹窗。主菜单按钮及无存档提示使用 `fusion-pixel-12px-monospaced-zh_hans 2 SDF`；可在 Label 上继续调整。`Tools/UI/Apply Main Menu Pixel Font` 仅替换主菜单字体，不重建布局。
- 显式美术重建入口为 `Tools/UI/Apply Main Menu Artwork`。执行前需退出 Play 并保存 MainMenu；工具先在 `RecoverySnapshots/MainMenuArtwork/` 备份场景，再重建主菜单设计区，保留设置、图鉴、退出确认和开场视频引用。此命令会替换设计区内的手工布局，仅在明确需要重建时使用；正常运行不会重建或覆盖布局。旧的 `Tools/MainMenu/MainMenuSetup.cs` 是占位界面构建器，不用于最终美术界面。
- Play 模式验证：`unity command run_script --file Tools/MainMenu/MainMenuArtworkChecks.cs --entry MainMenuArtworkChecks.Verify`。检查 1920×1080、1280×720、1280×960、2560×1080 的布局、按钮射线范围、面板返回、焦点、存档可用状态及素材切换，报告与截图位于 `Temp/MainMenuArtwork/`。验证不写玩家存档，不执行完整游戏场景过渡。
