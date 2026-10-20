# ESC 暂停菜单

`Assets/Prefabs/UI/ESCMenu.prefab` 用于 GameScene、GameScene_lx 和 TestNight。布局、图片、字体、颜色与按钮引用均在 Prefab / Inspector 内编辑，运行时不会重建或重设布局。

- 完整纸张背景：`Assets/Model/UI/菜单组件/每日结算/IMG_4892 2.png`。
- 右上角继续游戏按钮：`Assets/Model/UI/菜单组件/每日结算/IMG_5042 3.png`，关闭菜单并恢复玩家输入。
- 三个按钮各自的 Image 使用 `Assets/Model/UI/菜单组件/系统设置/信息栏.png`，依次为系统设置、保存并退出、退出至桌面。
- 系统设置复用 `Resources/UI/SettingsMenu.prefab`。退出确认是 MainMenu 现有页面的编辑时副本。
- 保存并退出将最近捕获的阶段开始检查点重新写盘，成功后返回 MainMenu；失败显示提示并留在暂停菜单。沿用现有存档范围，不记录阶段中途的世界、背包或位置；菜单下方说明继续游戏将回到本阶段开始。
- 退出至桌面先显示与 MainMenu 一致的 ExitConfirmationCanvas，默认选择取消。取消返回暂停菜单；确定在构建中退出应用，在编辑器中停止 Play。
- 设置及退出确认内按 Esc 先关闭子页面，并将焦点还给入口按钮；暂停菜单内再次按 Esc 恢复游戏。
- 菜单遮罩拦截后方点击，子页面打开时禁用菜单按钮；装饰图与文字不接收射线。
- Canvas 使用 1920×1080 参考尺寸和 Expand，保持纸张及箭头比例。

显式重建入口：`Tools/UI/Build ESC Menu Artwork`。退出 Play、保存打开场景后执行；它会先备份原 Prefab 和三个场景到 `RecoverySnapshots/ESCMenu/时间戳/`，随后替换 ESC 菜单。此操作会覆盖 ESC 菜单手工布局；日常调整请直接编辑 Prefab。

## 验证

验证脚本位于 `Tools/ESCMenu/`，通过 Unity CLI 的 `run_script` 调用。`ESCQASetup.Prepare` 备份玩家存档后打开 GameScene；进入 Play 后依次执行 `ESCMenuChecks.Verify`、`VerifyEscape`、`VerifySaveFailure`、`SaveAndReturn`、`InspectReturn`。`ESCExitCheck` 验证确认退出；结束 Play 后执行 `ESCQASetup.Restore` 恢复原存档与 MainMenu 编辑场景。

已在 GameScene Play 模式验证：

- 1920×1080、1280×720、1280×960、2560×1080 下按钮完整可见，射线点击命中正确。
- 精确素材引用、三个按钮文字、箭头继续游戏、玩家输入及暂停状态同步。
- 设置返回、退出确认按钮点击、默认取消、焦点恢复、阻止后方按钮交互。
- 真实 Input System Esc 输入关闭子页面、恢复游戏、重新打开菜单。
- 无检查点时保存失败不退出；检查点写入及读回一致；保存成功返回 MainMenu 并恢复时间流速。
- 点击退出确认的确定按钮能停止编辑器 Play。

截图和报告在 `Temp/ESCMenuQA/`。测试前的玩家存档已恢复。

TestNight 加载时已有多个缺失 Prefab 引用；此次仅迁移它的 ESC 菜单。测试过程中还捕获到一次 Unity 编辑器 Hierarchy TreeView 索引异常，调用栈无游戏脚本；主流程交互检查通过。
