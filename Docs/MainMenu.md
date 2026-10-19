# MainMenu UI

MainMenu 场景中的五个按钮依次为：开始游戏、继续游戏、设置、图鉴、退出游戏。

- 继续游戏暂时禁用，等存档系统接入。
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

快捷键目前是说明页，不提供重绑定。存档系统未实现。
