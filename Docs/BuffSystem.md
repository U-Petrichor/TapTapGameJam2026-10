# Buff 系统

统一管理玩家当前持有的全部增益 / 减益：**科技树天赋（Talent）、装扮（Dress）、临时效果（Consumable）** 都翻译成同一种「修饰符」，由一个聚合器压成「每个 key 一个最终系数」，游戏逻辑只和聚合结果打交道。

## 核心文件

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/Buffs/BuffKey.cs` | `BuffKey` 枚举（所有可被 Buff 的数值）+ `BuffKind` 方向 |
| `Assets/Scripts/Buffs/BuffKeyTable.cs` | key 的静态定义表、`snake_case` 稳定名字、`TryParse`、`Validate` |
| `Assets/Scripts/Buffs/BuffModifier.cs` | 一条修饰符：`Key` + `Op` + `Value` + `Source` |
| `Assets/Scripts/Buffs/BuffRules.cs` | 全局夹取常量 + 放大（元）规则表 |
| `Assets/Scripts/Buffs/BuffAggregator.cs` | 聚合核心（纯 C#，不依赖 UnityEngine） |
| `Assets/Scripts/Buffs/CockroachBuffSnapshot.cs` | 虫群 Buff 的只读快照（给高频读取方） |
| `Assets/Scripts/Buffs/TalentBuffSource.cs` | 科技树天赋 → `BuffModifier` 的翻译（纯 C#） |
| `Assets/Scripts/Buffs/PlayerBuffs.cs` | 挂在 Player 上的宿主：收集来源、重建、广播事件 |
| `Assets/Scripts/Buffs/Editor/BuffKeyPicker.cs` | 编辑器里选 key 的统一下拉（天赋窗口 + 装扮配表共用） |
| `Assets/Scripts/Items/DressData.cs` | 装扮的 Buff 配表（ScriptableObject） |
| `Assets/Scripts/Items/Editor/DressDataEditor.cs` | 装扮配表界面 |
| `Assets/Scripts/TalentTree/Effects/ITalentLevelSource.cs` | 让 resolver 不再依赖 MonoBehaviour 的接口 |
| `Tools/BuffChecks/` | 公式与聚合规则的回归检查（不需要开 Unity） |

验证（都不需要打开 Unity）：

```powershell
# 1. 公式与聚合规则的回归（纯 C#，305 条）
dotnet run --project Tools/BuffChecks

# 2. 用 Unity 自己的程序集把 Assets/Scripts 全量类型检查一遍（含 Editor 脚本）
dotnet build Tools/CompileCheck/CompileCheckEditor.csproj
```

第 2 条会把 `Assets/Scripts/**/*.cs` 编到临时 dll 里验证，Unity 装在别的路径时用
`-p:UnityEngineManaged="<Unity>\Editor\Data\Managed\UnityEngine"` 覆盖。
改完游戏代码不放心能不能编过时，跑这个比开编辑器快。

## 合成公式

顺序写死，**游戏逻辑不许自己另拼一套**：

```
Grow   : final = (base + Σflat) * Π(1 + pct)
Shrink : final = (base + Σflat) * Π(1 - pct)
Flat   : final = (base + Σflat)            // 档位 / 容量 / 幸运
Flag   : 只回答 Has(key)，不参与数值
```

读取入口只有 `BuffAggregator` 这几个方法：`Apply` / `ApplyInt` / `GetFactor` / `GetFlat` / `Has`。

### 已定的决策（改之前先看这里）

1. **百分比是乘法叠加**，不是加法：两个 `+10%` 得到 `×1.21`，不是 `×1.20`。
   减免同理：两个 `50%` 减免后还剩 `×0.25`，不会出现「两个 50% 就免疫」。
2. **`Shrink` 类 key 存正数表示减免多少**（`0.1` = 少受 / 少花 10%）。
   配负值就是减益（「更容易受伤」），系数允许超过 1。
3. **夹取在系数上，不在绝对值上**：`Grow` 夹到 `[0.05, 10]`，`Shrink` 夹到 `[0.05, 5]`。
   下限保证「100% 免伤 / 零体力消耗」不会把玩法搞崩。
4. **key 用枚举，稳定名字用 snake_case**，两者一一对应（`BuffKeys.Get(key).Id`）。
   历史 `TalentEffectKey` 的字符串值与之一致，**既有天赋资产不需要迁移**；改名等于改存档。
5. **放大器 key 自己永远不被放大**。`DressBonusPercent` 是系数不是数值，被放大只会让数值层层复利。
6. **放大只作用于百分比**，固定值（背包档位、巢穴容量）不被放大 —— 否则会出现「半格」这种没有意义的数。
7. **算放大系数时排除被放大的那个来源自身**，从结构上消灭自乘，不需要运行时防环。
   所以「装扮给的装扮加成」不会把它自己抬起来。
8. **来源是整份替换，不是增删单条**（`SetSource`）。天赋 / 装备天然就是全量快照，
   增量维护要处理「哪条对应哪条」，容易留残影。
9. **`Revision` 只在内容真的变了时才 +1**，消费方（UI、快照）可以放心比较。

### 元属性（放大别的 Buff）

`BuffRules.AmplifyRules` 是一张显式的小表，目前只有一条：

```
DressBonusPercent 放大 BuffSourceKind.Dress 的百分比
```

不做通用递归（A 放大 B、B 放大 C）—— 会出现循环依赖和无法解释的数值爆炸，策划也没法配平。
以后要加「某天赋放大某类装备效果」，往表里加一行即可。

## Key 一览

标 **[预留]** 的目前没有任何消费方，先占好位置，等对应玩法做出来再接线。

### 玩家个体 · 移动
| Key | Kind | Id | 消费点 |
| --- | --- | --- | --- |
| `PlayerMoveSpeedPercent` | Grow | `movement_speed` | `PlayerMovement.CurrentMoveSpeed` |
| `SprintSpeedPercent` | Grow | `sprint_speed` | **[预留]** |

### 玩家个体 · PlayerHealth
| Key | Kind | Id | 消费点 |
| --- | --- | --- | --- |
| `StaminaMaxPercent` | Grow | `stamina_max` | `PlayerHealth.BuffedMaxHealth` |
| `StaminaMaxFlat` | Flat | `stamina_max_flat` | `PlayerHealth.BuffedMaxHealth`（加在百分比之后） |
| `PlayerDamageReduction` | Shrink | `individual_damage_reduction` | `PlayerHealth.AddDamage` 入口 |
| `PoisonImpactReduction` | Shrink | `poison_impact_reduction` | `PlayerHealth.AddPoison` 入口 |
| `StaminaCostReduction` | Shrink | `stamina_cost_reduction` | 冲刺每秒消耗 |
| `StaminaRecoveryPercent` | Grow | `stamina_recovery_multiplier` | 体力每秒恢复 |
| `DetoxSpeedPercent` | Grow | `detox_speed_multiplier` | **[预留]** 中毒随时间自愈（**玩法未做**） |
| `BagWeightReduction` | Shrink | `bag_weight_reduction` | `PlayerHealth.RefreshHeavyFromBag` 的换算结果 |
| `BagCapacityTier` | Flat | `bag_capacity_bonus` | 背包档位 0/1/2，由 `PlayerBag` 决定封锁哪些格子 |

> 命名历史包袱：代码里叫 **Power**（`PowerTotal` / `PowerAvailable`），key 里叫 **stamina**。两者是同一个东西。

### 虫群（所有 Cockroach）
| Key | Kind | Id | 消费点 |
| --- | --- | --- | --- |
| `CockroachSpeedPercent` | Grow | `group_movement_speed_multiplier` | `Cockroach.UpdateLogic` 的 `_settings.Speed` |
| `CockroachCarryPercent` | Grow | `group_food_intake_multiplier` | `Cockroach.TryEat` → `FoodItem.Consume` |
| `CockroachHatchPercent` | Grow | `group_egg_production_speed_multiplier` | `CockNest.UpdateHatch` 的每秒消耗 |
| `PlayerEggProductionPercent` | Grow | `individual_egg_production_speed_multiplier` | **玩法未做** |

### 巢穴
| Key | Kind | Id | 消费点 |
| --- | --- | --- | --- |
| `NestRangePercent` | Grow | `nest_exploration_range_multiplier` | `CockNest.range`（改完要重生成范围圈） |
| `NestCapacityFlat` | Flat | `nest_capacity_bonus` | **[预留]** 巢穴容量目前没有上限 |

### 资源获取（PlayerResources）
| Key | Kind | Id | 消费点 |
| --- | --- | --- | --- |
| `CoinGainPercent` | Grow | `coin_gain_multiplier` | `PlayerResources.AddCoins`（正数吃倍率，扣钱不吃） |
| `NutritionGainPercent` | Grow | `nutrition_gain_multiplier` | `PlayerResources.AddNutrition`（正数吃倍率；目前还没有调用方） |
| `TalentPointGainPercent` | Grow | `talent_point_gain_multiplier` | `PlayerResources.AddTalentPointGain`；退款走 `AddTalentPoint` → **不加倍率** |
| `CoffeeGainPercent` | Grow | `coffee_gain_multiplier` | **[预留]** |

### 元属性 / 特殊
| Key | Kind | Id | 说明 |
| --- | --- | --- | --- |
| `DressBonusPercent` | Grow | `dress_bonus_multiplier` | 放大 `BuffSourceKind.Dress` 的百分比（装扮数值来自 `DressData`） |
| `LuckFlat` | Flat | `luck` | **[预留]** 黑盒稀有物品权重（随机池未做） |

### 敌人
| Key | Kind | Id | 说明 |
| --- | --- | --- | --- |
| `GuardPlayerDetectReduction` | Shrink | `guard_player_detect_reduction` | **[预留]** 目前决定先不做 |

### 能力型 Tag（`Has(key)`）
`FlagCockroachDeathFood`(`cockroach_death_food`)、`FlagGroupWallGnawing`(`group_wall_gnawing`)、
`FlagInstantEggLaying`(`instant_egg_laying`)、`FlagLethalDamageProtection`(`lethal_damage_protection`)、
`FlagIndividualSprint`(`individual_sprint`)、`FlagIndividualInstantHatching`(`individual_instant_hatching`)

> Tag 只回答「有没有这个能力」。**是否已经消耗**（例如绝境蜕生用掉了那一次）由玩法自己记录。

## 接入路线

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| **P0** | 聚合核心 + 单测。不碰任何现有逻辑 | ✅ 完成 |
| **P1** | resolver 解耦 + 缓存、`TalentBuffSource`、`PlayerBuffs` 宿主、`BuffsRebuiltEvent`、`CockroachBuffSnapshot` | ✅ 完成 |
| P2 | 高频消费点接线：玩家移速、虫群三件套、巢穴范围 | ✅ 完成 |
| P3 | `PlayerHealth` 四件套（受伤 / 中毒 / 体力 / 负重）+ 恢复 `RefreshHeavyFromBag` | ✅ 完成 |
| P4 | 装扮数据（`DressData`）+ `DressBonusPercent` 元规则生效 | ✅ 完成 |
| **P5a** | 黑盒随机出货（幸运值影响稀有概率） | ✅ 完成 |
| **P5b** | 巢穴容量上限、背包 0/1/2 档封锁 | 待做 |

### P1 落地的细节

**数据流**

```
PlayerTalentTree（实现 ITalentLevelSource）
  └ TalentEffectResolver（带缓存，等级变化后 Invalidate）
       └ TalentBuffSource.Build()      ← 天赋字符串 key 翻成 BuffKey
            └ BuffAggregator.SetSource(Talent, mods)
                 └ PlayerBuffs（Revision 变了就发 BuffsRebuiltEvent）
                      ├ CockroachSnapshot（struct，蟑螂/巢穴直接读）
                      └ Apply / GetFactor / Has（逐帧读的地方用）
```

**翻译规则**：配表只填一个数，**是百分比还是固定值由 key 自己决定**，策划不用选 op：

| key 的 Kind | 翻成 |
| --- | --- |
| `Grow` / `Shrink` | `BuffOp.Percent` |
| `Flat` | `BuffOp.Flat` |
| `Flag` | `BuffOp.Flag`（数值丢弃） |

解析不了的 key 不会被静默丢掉，而是进 `TalentBuffSource.UnknownKeys`，由 `PlayerBuffs` 打到 Console（每条只打一次）。

**顺序稳定性**：`TalentBuffSource.Build()` 会对结果排序（Key → Op → Value）。
不然同样的天赋进度每次重算都会产出不同顺序的列表，聚合器会误判成「内容变了」，
每次刷新都涨 `Revision` → UI 白刷、快照白算。`Tools/BuffChecks` 里有这条回归。

**宿主挂在谁身上**：`PlayerBuffs` 挂在 Player 上，全局入口 `PlayerBuffs.Current`。
`PlayerBasic.Awake` 里缺了就自动 `AddComponent` —— 老预制体 / 老场景不会自带这个组件，
少了它天赋就整个失效，自动补比「忘了拖引用 → 数值静默不生效」好查得多。

**已迁移的消费点**：`PlayerResources.ApplyCoinGainMultiplier` 不再直接问 `PlayerTalentTree`，
改走 `PlayerBuffs.ApplyInt(BuffKey.CoinGainPercent, …)`。这是第一条端到端打通的链路。

**编辑器**：天赋树窗口的「效果 Key」从手打文本框改成下拉（带「百分比 / 减免 / 固定值 / Tag」标注），
从源头消灭拼写错误；资产里已有的未知 key 会显示成「（未知 key：xxx）」而不是被清空。

### P2 已接线的消费点

| 消费点 | 改动 |
| --- | --- |
| 玩家移速 | `PlayerMovement.CurrentMoveSpeed` = `Apply(PlayerMoveSpeedPercent, _moveSpeed)`，冲刺时再乘 `GetFactor(SprintSpeedPercent)` |
| 蟑螂移速 | `Cockroach.UpdateLogic`：`_settings.Speed * _speedJitter * BuffMods.SpeedFactor` |
| 蟑螂取食量 | `Cockroach.TryEat`：`Consume(_settings.CarryCapacity * BuffMods.CarryFactor)` |
| 孵化速度 | `CockNest.UpdateHatch`：`HatchNutritionPerSecond * InsideCount * dt * BuffMods.HatchFactor` |
| 巢穴活动范围 | `CockNest.EffectiveRange = range * NestRangeFactor`；蟑螂的越界判定、范围虚线圈都用它 |

**共同原则：基准值（`_moveSpeed` / `_settings.*` / `range`）一个字都不改，倍率只乘在读取点上。**
`CockroachSettings` 是全场共享的一份配置，写回去就是不可回滚的全局改写，天赋一撤销就再也回不来。

**性能**：蟑螂和巢穴每帧都读，所以读的是 `CockroachManager.BuffMods`（按 `Revision` 缓存过的 struct），
不是每次去查聚合器。几十只蟑螂 × 每帧 3 个 key，差的就是几百次数组查询。

**巢穴范围圈会跟着变**：`CockNest` 订阅 `BuffsRebuiltEvent`，Buff 一变就 `_ring.Regenerate(EffectiveRange)`。
`CockNestRing.Regenerate` 半径没变会自己跳过，所以可以放心每次调。否则玩家看到的圈和真实活动范围会对不上。

### P3 已接线的消费点（PlayerHealth）

| 消费点 | 改动 |
| --- | --- |
| 体力上限 | `PowerTotal` 用 `BuffedMaxHealth = Apply(StaminaMaxPercent, _maxHealth) + GetFlat(StaminaMaxFlat)` |
| 受伤 | `AddDamage` 入口乘 `Apply(PlayerDamageReduction, amount)` |
| 中毒 | `AddPoison` 入口乘 `Apply(PoisonImpactReduction, amount)` |
| 冲刺消耗 | `_sprintCostPerSecond * GetFactor(StaminaCostReduction)` |
| 体力恢复 | `_powerRecoverPerSecond * GetFactor(StaminaRecoveryPercent)` |
| 负重 | `RefreshHeavyFromBag` 的换算结果乘 `Apply(BagWeightReduction, heavy)` |

**受伤 / 中毒的减免乘在入口，不乘在存储上。** 否则已经受的伤会被后来的天赋追溯减免，
buff 中途变化还会让已有数值自己跳。入口乘 = 「这一下受到多少」，之后它就固定了。

**⚠️ 行为变化**：`RefreshHeavyFromBag()` 之前在 `Update` 里是注释掉的（`TODO: 启用`），
所以负重恒为 0。P3 把它启用了——否则「负重减免」这个 Buff 永远没有作用对象。
如果暂时不想要负重惩罚，把那行注释回去即可，Buff 会自动退化成 1 倍。

**Buff 变化时会重新夹一次当前体力**：`PlayerHealth` 订阅 `BuffsRebuiltEvent` → `ClampAvailableToTotal()`。
体力上限被 Buff 压低时，当前体力不能超过新上限；如果被顶到 0，等价于「体力见底」，同样进冲刺锁定。

### 资源获取的「获得」与「退还」必须分开

`PlayerResources` 里现在有两套写法，**别混用**：

| 方法 | 是否吃倍率 | 用途 |
| --- | --- | --- |
| `AddCoins` / `AddNutrition` | 正数吃 | 奖励、捡钱、卖货 |
| `AddTalentPointGain` | 吃 | 小游戏奖励、击杀回怒：玩家**获得**天赋点 |
| `AddTalentPoint` | **不吃** | 撤销天赋的**退款**、任何原额搬运 |

退款和获取原本共用 `AddTalentPoint`，倍率如果加在函数内部，退回来的点数会比花出去的还多 ——
等于可以反复点天赋刷点。所以拆成两个入口，加分倍率只挂在 `AddTalentPointGain` 上。

### P5a 黑盒随机出货

`BlackBox` 没配 `hidedItem` 时的出货规则：

1. 先按概率决定这一盒抽「普通」（`_specialLevel` = 0）还是「稀有」（≥ 1）；
2. 再从 `MouseStore` 商品池里对应档位**等概率**抽一个模板；
3. 抽到稀有物品时，扫描时间 × `rareScanTimeMultiplier`（默认 2，即翻倍）。

概率公式在 `LootRoll.RareChance`：

```
实际稀有概率 = 基准概率 × (1 + 幸运)
```

**是「按比例提升」，不是「加百分点」**：基准 10%、幸运 +60% 得到 16%，不是 70%。
幸运来自 `BuffKey.LuckFlat`（`PlayerBuffs.GetFlat`），多个来源**加法累加**（两个 +30% 等于 +60%），
结果夹在 `[0, 1]`。`Tools/BuffChecks` 里钉了 10% → 16% 这条口径。

三个实现上的坑（都已在代码里处理）：

1. **掷骰发生在「第一次真正开始扫描」的那一帧，不是 `OnEnable`。**
   Unity 不保证一个物体的 `OnEnable` 晚于另一个物体的 `Awake`，黑盒 `OnEnable` 有可能跑在
   `GameManager.Awake` 之前，那时 `PlayerBuffs.Current` 还是 null，幸运值会被静默漏掉。
   一个扫描周期只掷一次（`_rolled`），中途取消不重掷——否则「扫一下就取消」能反复刷档位。
2. **目标档位为空会退到另一档。** 现在池子里还没有配稀有物品，抽中稀有会退回普通，
   所以黑盒不会空开。等配好稀有物品，这条兜底自然不触发。
3. **稀有度按「实际抽到的物品」判定**，不是按「想抽的档位」——因为上面有退档逻辑，两者可能不一致。

配置方式：想做稀有的物品，把预制体上的 `_specialLevel` 设成 `1` 或 `2`，并放进
`MouseStore` 的 `_pool`（黑盒只从池子里的模板抽，不扫全场景）。
`hidedItem` 配了的话，这一盒固定出那个物品，不随机、扫描时间也不翻倍。

### P4 装扮 Buff

**数据**：`Assets/Scripts/Items/DressData.cs`

一件装扮的数值写在 `DressData`（ScriptableObject）里，`DressItem` 通过一个 `_data` 字段引用它。
抽成独立资产的理由：同一套效果可给多件外观复用、改数值不产生预制体 diff、配表和做美术能并行。

```csharp
[CreateAssetMenu(menuName = "Items/Dress Data", fileName = "DressData")]
public sealed class DressData : ScriptableObject
{
    [SerializeField] private List<DressBuffEntry> _buffs;   // Key + Value
    [SerializeField, TextArea] private string _description;
}
```

`DressBuffEntry` 只有 `Key` + `Value`，**是百分比还是固定值仍然由 key 的 Kind 决定**
（`BuffModifier.FromValue` 这个入口天赋和装扮共用），所以两个地方配表语义完全一致：
百分比 0.1 = +10%，减免 0.1 = 减免 10%，固定值填绝对值，Tag 忽略数值。

**配表界面**：选中 `DressData` 资产，Inspector 由 `DressDataEditor` 接管，
和天赋树窗口共用 `BuffKeyPicker`（同一套 key 下拉 + 单位提示）。

**接线**：

```
PlayerBag.EquippedChanged  →  PlayerBuffs.Refresh()
                                 ├ RefreshDress()：三个槽 → DressData → List<BuffModifier>
                                 │                 SetSource(BuffSourceKind.Dress, …)
                                 └ 广播 BuffsRebuiltEvent
```

`PlayerBag` 里对 `_equipped[]` 的所有写入都收敛到私有方法 `SetEquipped(slot, item)`，
由它统一发 `EquippedChanged`。**新增任何改装备槽的代码都必须走它**，
直接写数组会导致「换装不生效」——这种 bug 极难查，因为表现只是数值不动。

**「装扮加成」自动生效，没有额外代码**：`BuffRules.AmplifyRules` 里已有
`DressBonusPercent → BuffSourceKind.Dress`，只要 modifier 的来源是 `Dress`，
天赋「巧饰虫装 / 精工战衣」就会放大它的百分比部分（固定值不放大）。

### 天赋树编辑器用到的 Unity 技术

`Tools > TalentTree` 是一个普通 `EditorWindow`，没有用 GraphView / UI Toolkit，全部是
`IMGUI`（`OnGUI` + `EditorGUILayout`）手绘，所以看得懂、改得动：

| 用到的 | 在哪 | 干什么 |
| --- | --- | --- |
| `EditorWindow` + `[MenuItem]` | `TalentTreeEditorWindow` | 开窗口 |
| `OnGUI` / `GUILayout` / `EditorGUILayout` | 全窗口 | 即时模式 UI，每帧重绘，无状态 |
| `GUI.BeginClip` + `GUILayout.BeginArea` + `Matrix4x4` | 画布区 | 平移 / 缩放 |
| `Handles.DrawAAPolyLine` / `Handles.DrawSolidDisc` | `TalentConnectionView` | 画连线和节点插口 |
| `SerializedObject` / `SerializedProperty` | 属性区 | 安全地读写序列化字段 |
| `Undo.RecordObject` + `EditorUtility.SetDirty` | 每次改数据 | 撤销栈 + 强制存盘 |
| `[SerializeReference]` | `TalentLevelData._effects` | 存多态的效果 / 前置条件列表 |
| `AssetDatabase` / `Selection.activeObject` | 打开资产 | 关联当前选中的天赋树 |
| `EditorGUILayout.Popup` | 效果 Key | 由 `BuffKeyPicker` 从 `BuffKey` 表生成 |

节点、连线、图标这些**都是数据驱动**的：`TalentTreeAsset` 里存 `Vector2 position` + 边表，
窗口只负责把数据画出来。所以运行时不需要这套 UI —— 游戏里的天赋面板是另一个东西（`TalentTreeUI`）。

### 排查：天赋点了但数值不动

`PlayerBuffs` 有一个 `_logRebuild` 开关（默认开），每次重建打一条：

```
[PlayerBuffs] rev=3 天赋等级=2 天赋条目=2 装扮条目=0 | 移速×1.61 巢穴范围×51 虫群速×1 …
  Talent: Talent:PlayerMoveSpeedPercentPercent(0.61), Talent:NestRangePercentPercent(50)
```

对照着看就能定位：

| 现象 | 原因 |
| --- | --- |
| `天赋等级=0` | 天赋没解锁（效果只在解锁后生效） |
| `天赋等级>0` 但 `天赋条目=0` | key 没解析出来，去看 `ReportUnknownKeys` 的 warning |
| `天赋条目>0` 但系数还是 ×1 | 配的是固定值 key，或者数值填在了 Tag 上 |
| 系数对了但手感没变 | 消费点没接线 / 组件不在同一个 GameObject 上 |

**踩过的坑（已修）**：`resolver` 实例曾经被缓存。`PlayerTalentTree.Awake` 里无条件
`_effectResolver = new TalentEffectResolver(this)`，如果 `PlayerBuffs` 的 Awake 先跑、先拿走了旧引用，
那个旧 resolver 就再也没人 `Invalidate`，会永久返回「第一次算出来的效果」（通常是空的）。
现在两边都做了防御：`PlayerTalentTree` 用 `??=`，`PlayerBuffs` 每次重建都从
`_talentTree.Effects` 现拿 resolver，不缓存实例。

## 接线时必须注意的坑

1. **`CockroachSettings` / `GuardSettings` 是全局共享的一份基准配置**，不是「每个单位的当前值」。
   倍率必须在**读取点**乘上去，**绝对不要写回 settings** —— 那是一次不可回滚的全局改写，天赋一撤销就回不来。
2. **守卫视野是烘焙的**：`VisionRange` 在 `Guard.Awake` 里被烘成 `Light.range` + `MeshCollider` 网格。
   只改判定值 → 玩家看到的光锥和真实判定不一致；改值必须重建网格，否则视觉在说谎。
3. **巢穴范围**改完要 `_ring.Regenerate(EffectiveRange)`，否则虚线圈和真实活动范围对不上。
   只放大 `range`，不要放大 `radius`（否则巢口交互距离、归巢判定都会变）。P2 已按这个做，靠
   `BuffsRebuiltEvent` 触发重建。
4. **孵化速率乘在「每秒消耗」上，不要乘 `HatchNutritionCost`**：进度条的分母是 cost，动它 UI 就得改。
5. **天赋点退款不加倍率**：退款和获取共用 `AddTalentPoint`，倍率放在函数内部会把退款也放大，等于可以刷点。
6. **`TalentEffectResolver` 有缓存，改等级必须 `Invalidate`**。缓存本身不订阅事件（它是纯 C#），
   宿主负责失效：`PlayerTalentTree` 在每次 `SetLevel` 之后、发事件之前失效一次，
   `PlayerBuffs.Refresh` 里再主动失效一次兜底。**新增任何改等级的路径都要照做**，
   否则读到的是旧效果。
7. **「每级效果值是累计总量还是增量」**：现有 resolver 的语义要求「该级数值 = 到该级为止的总量」，
   但编辑器里每级就是个自由 float，配成增量会静默算错。这条要写进策划文档。
