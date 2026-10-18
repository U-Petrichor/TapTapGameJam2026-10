# MainMenu UI

MainMenu 场景中的五个按钮依次为：开始游戏、继续游戏、设置、图鉴、退出游戏。

- 继续游戏暂时禁用，等存档系统接入。
- Settings Canvas：左侧切换快捷键说明与声音设置；音效、音乐接入 SoundManager，使用 PlayerPrefs 保存设置。
- 动画 ON/OFF 先保存偏好，后续动画代码可读取 SettingsMenu.AnimationsEnabled；目前不控制角色或过场动画。
- GalleryCanvas：空白图鉴页面，带标题和返回按钮。
- ExitConfirmationCanvas：遮罩拦截后方点击，取消返回，确定退出；编辑器中确定会停止 Play。
- 返回按钮或 Esc 关闭当前页面，焦点回到对应主菜单按钮。

占位色块都是标准 Image，文字为 TMP Text，可在场景 Hierarchy 中替换 Sprite、颜色与排版。Settings/Backdrop/BookPlaceholder 下包含 ShortcutsPage、SoundPage，两页切换不会重新生成物体。

快捷键目前是说明页，不提供重绑定。存档系统未实现。
