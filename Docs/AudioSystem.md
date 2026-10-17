# 音效系统

## 使用与配置

正式入口为 `MainMenu`、`GameScene`。`SoundManager` 在启动时自动创建、跨场景保留，场景无需手动挂载；其他场景不响应播放请求。

配置：`Assets/Resources/Audio/SoundLibrary.asset`。在 Inspector 中展开 `sounds`，`id` 对应音效表的 S01～S43。S11（CoffeeMachine）、S15（PrinterQte）禁用。

| 字段 | 含义／初值 |
|---|---|
| masterVolume / musicVolume / effectsVolume | 总音量 1／音乐 0.65／音效 0.8 |
| defeatDuckVolume | 失败声播放期间，音乐及其他音效乘以 0.25 |
| voiceLimit | 最多 32 个同时分配的声源；重要声音可以抢占低优先级声音 |
| volume / pitch | 单项音量及随机音调范围；大多数声音为 0.75／1 |
| loop | 仅对受控制的播放请求启用循环 |
| cooldown / cooldownPerOwner | 使用真实时间限频；可按发声对象分别计算 |
| maxInstances / priority | 同类同时播放上限／越大越重要 |
| nearDistance / farDistance | 默认水平距离 2 以内完整音量，18 以外静音；守卫脚步为 24 |
| maxDuration | 0 表示自然结束；正数表示播放到指定秒数后结束 |
| fadeOut | 主动停止默认淡出 0.04 秒；对象移除立即停止 |
| volumePerLevel / pitchPerLevel | S23 每高一级，音量 +0.06、音调 +0.04 |

初值供手动调整，没有进行主观听感定案。错误、受击默认间隔 0.2 秒，整理、啃食为 0.12 秒，其余短声多为 0.06 秒；操作循环不限制重启间隔。脚步按对象限频；虫群受击共享限频，玩家受击独立并优先于虫群。

运行时可调用 `SoundManager.Instance.MasterVolume / MusicVolume / EffectsVolume`，范围 0～1。不会自动保存设置，本次未添加设置界面。现有 `BackgroundMusicManager` 已接入 `MusicGain`，但尚未挂载、配置到正式场景；以后挂载时会自动服从音乐音量和失败压低规则。

## 动画事件：需要手动设置帧

**没有按移动距离或定时器自动生成脚步声。S06、S26、S34 等待美术／开发在动画的落脚帧添加事件。**

1. 找到播放该动画的 **Animator 所在的同一个 GameObject**。
2. 添加 `SoundAnimationEvents` 组件。正式场景运行时也会自动补挂到玩家、普通 NPC、守卫的 Animator 上；为了在编辑器中直接选择事件方法，建议事先手动挂好。
3. 在 Animation 窗口的落脚帧添加事件。
4. Function 选择 **`PlayFootstep`**，填写 Int 参数。

| Int 参数 | 效果 |
|---|---|
| **0** | 自动判断：白天人形玩家／普通 NPC 用 S06；母体用 S26；Guard 及其子类用 S34 |
| **6** | 明确播放人类脚步 S06 |
| **26** | 明确播放母体爬行 S26 |
| **34** | 明确播放危险 NPC 脚步 S34 |

普通行走可以左右脚各一帧；母体爬行按希望的步频设置。方法在游戏暂停、模型禁用时不发声。请勿在待机动画加落脚事件，也不要同时给同一只脚添加重复事件。共用动画推荐传 0；如果 Animator 不是角色的子物体，需手动挂接并使用明确编号。

方法签名：`public void PlayFootstep(int soundNumber)`，不需要传素材名或 AudioClip。音效素材和变体都从 SoundLibrary 查找。脚步、爬行均使用拆出的独立短样本，随机选取并避免连续重复。原始行走录音不会整段播放。

## 代码调用

```csharp
using TapTapGameJam.Audio;

// 一次性全局反馈。
Sfx.Play(SoundId.Coin, this);

// 固定位置的一次性声音，默认随世界暂停。
Sfx.At(SoundId.Stomp, transform.position, this);

// 从 UI 中执行的放下／拾取：保持空间位置，但允许在世界暂停时反馈。
Sfx.At(SoundId.Drop, position, this, SoundBus.Interface);

// 持续动作：在状态变化时调用；重复 true 不叠加。
var sound = Sfx.Emitter(this);
sound.SetPlaying(SoundId.Grinding, grinding, SoundBus.MiniGame);
sound.SetPlaying(SoundId.Search, searching, SoundBus.World, spatial: true);
sound.StopAll();
```

也可直接发布 `PlaySoundEvent`、`StopSoundEvent`。可控制播放必须先用 `SoundPlaybackId.New()` 生成编号，并提供有效 Owner。固定位置的短声不会跟随 Owner 移动；持续空间声通过 Follow 跟随对象。事件只传递请求，配置和运行状态由管理器分开维护。

SoundEmitter 每个编号管理一个当前动作实例。停止后重新开始会获得新编号，旧停止请求无法误停新动作。若低优先级循环被抢占，持续调用 `SetPlaying(..., true)` 会在有空闲声源后尝试恢复。

## 暂停、场景与生命周期

- `World` 声音在 `Time.timeScale == 0` 时暂停，恢复时间流逝后继续；暂停期间不会释放仍处于暂停状态的实例。
- `Interface`、`MiniGame` 声音使用真实时间，跟随实际 UI／小游戏操作。小游戏 End、关闭、禁用时停止其持续声音。
- 搜索完成或离开、持有对象禁用／销毁时停止相关持续声音。
- 玩法阶段变化清理持续声音，下班铃和夜末结算声自然播完。
- 卸载场景清理旧场景声音；导航点击声允许跨场景播完，管理器和声源池保留。
- 首次进入白天不播放 S43；只有 Night → Day 才播放。S42 跟随 Day → Shopping，未修改 90 秒时长。
- 位置衰减只使用玩家与声源的水平距离；左右声道以主相机的水平向右方向计算。相机高度、缩放不影响距离音量。不包含墙体遮挡、混响或门隔音。

## 逐项接入

| 编号 | 当前触发 |
|---|---|
| S01 | Q 实际切换交互目标、仓库分类实际切换 |
| S02 | 普通 UI 按钮、3D 按钮、家园热点、没有专用反馈的 E 交互；专用反馈优先 |
| S03 | 背包／仓库、小游戏面板实际打开关闭，均使用正向素材 |
| S04 | 背包／仓库投放失败、分拣错误、打印机失误、股票操作失败、天赋操作失败 |
| S05 | 任务成功奖励一次；股票买／卖成功各一次，未从钱包变化推断 |
| S06 | 动画事件 `PlayFootstep(0/6)`，帧由用户添加 |
| S07 | 送件拿取、交付成功，各一次空间声 |
| S08 | 真实推进研磨的帧开始／维持循环；停手、移出、完成、退出即停 |
| S09 | 咖啡豆／粉拿起；巢穴拿起／放下；NPC 成功放毒饵 |
| S10 | 实际倒水期间循环，停止／完成／退出即停 |
| S11 | 明确禁用 |
| S12 | 维修面板打开起循环，成功／关闭停止 |
| S13 | 实际敲击一次；失误也敲响，并额外发 S04；无效输入忽略 |
| S14 | 按住并移动擦拭时持续；静止按住、松手、失焦、退出停止 |
| S15 | 明确禁用，维修敲击统一 S13 |
| S16 | 每条胶带有效切开完成一次 |
| S17 | 狗粮袋拿起、实际移动调整时限频播放 |
| S18 | 实际出粮时循环，停止出粮／完成／退出停止 |
| S19 | 一批有效消杀预警开始时全局一次 |
| S20 | 直接拾取穿戴／更换装备成功 |
| S21 | 背包／仓库投放成功，快速操作限频 |
| S22 | 背包物品每次旋转 90 度 |
| S23 | 天赋解锁、升级成功，按最终等级加强 |
| S24 | 单项退回成功一次；整树重置成功整体一次 |
| S25 | 保留定义：商人信息气泡流程待业务实现 |
| S26 | 动画事件 `PlayFootstep(0/26)`，帧由用户添加 |
| S27 | 母体实际从非冲刺进入冲刺状态一次 |
| S28 | 成功拾取一次，在打开背包导致世界暂停后仍允许反馈 |
| S29 | UI 确认丢回世界、食物从背包放回世界；内部拖拽暂存转换不触发 |
| S30 | 虫群实际成功取食一次短声，共享限频／并发；未增加持续取食玩法 |
| S31 | RefillPower 实际增加体力；受伤／中毒从有到无。自然恢复不触发 |
| S32 | 黑盒搜索中循环、空间跟随，离开／完成停止 |
| S33 | 搜索完成、巢穴产卵／首批孵化状态完成 |
| S34 | 动画事件 `PlayFootstep(0/34)`，帧由用户添加 |
| S35 | 保留定义：当前 CleanerGuard 放饵，没有喷洒流程 |
| S36 | KillerGuard 真正执行扑杀时，使用受害目标位置 |
| S37 | 保留定义：当前缺少针对母体的完整发现／追击流程 |
| S38 | AddDamage／AddPoison 增加状态、虫群 Die；虫群限频，玩家反馈优先 |
| S39 | PlayerHealth 首次判定失败时播放并压低其他声音，没有补写失败结算玩法 |
| S40 | 保留定义：工牌使用解锁流程待实现；不绑定日期开放事件 |
| S41 | 玩家／NPC 实际开门，使用 V2/开门.wav |
| S44 | 实际关门，使用 V2/关门.wav；重复请求不重复播放 |
| S42 | 实际白天结束进入购物阶段 |
| S43 | 夜晚结束进入白天；不包含首次进入白天 |

待业务：商品购买、主动归巢确认、商人气泡、喷雾、母体发现／追击、工牌、持续啃食与未落地的天赋效果。恢复接口已接入，但当前正式代码尚无对应治疗／药品业务调用，不应把它计为已完成治疗玩法。

## 素材与验证工具

- 原音频保留在 `Assets/Model/Music/音效`。
- `Assets/Resources/Audio/Derived/Wipe.wav` 从指定的清洁布 M4A 转换。
- `Derived/Samples/` 保存从连录中拆出的独立动作变体及连续循环片段。翻页、点击、脚步等一次请求只随机选一份样本。
- `Derived/GnawShort.wav` 为原咀嚼素材的 0.5 秒短片段，尾部淡出；需要时可替换。
- `python Tools/Audio/build_sound_library.py` 仅生成缺失配置／派生素材；已存在的 SoundLibrary 不会被覆盖。
- `python Tools/Audio/prepare_sound_samples.py --apply` 根据脚本中的秒数范围生成短样本，只替换原始录音／自身派生素材的引用；自定义素材覆盖会跳过，现有调音参数保持不变。
- `python Tools/Audio/prepare_smoke.py` 在 `Temp/AudioSmokeProject` 准备隔离的 Unity 验证工程；不会切换当前正式工程的场景或进入其 Play Mode。隔离工程用简化角色依赖验证音效层，完整业务源码编译另行执行。
- 隔离工程可用 Unity batchmode 执行 `AudioSmokeEditor.Run`，结果写入 `smoke-result.txt`。脚步帧和最终听感仍需用户在正式场景戴耳机验证。

交互验收：分别取消研磨／倒水／倒粮／擦拭／搜索；操作中关闭面板／切阶段；暂停恢复；左右移动声源、旋转／缩放镜头；大量虫群同时取食；返回主菜单再开局；检查任务奖励及整树重置是否只反馈一次。
