# 夜晚物品生成 / 天亮清场

夜里在地图上随机撒黑盒和食物，天亮把没带回巢的蟑螂和地上的食物收拾掉。
全部逻辑在 `Assets/Scripts/Core/NightManager.cs` 里，挂在场景的 NightManager 上。

## 时间点

| 时机 | 发生什么 |
|---|---|
| 进入夜晚、黑屏**全黑的时候** | `SpawnNightItems()`：随机选点刷黑盒 / 食物 |
| 进入夜晚、淡入完成 | 照旧发 `NightTransitionCompletedEvent`，倒计时开始 |
| 进入白天 | `CleanupNightLeftovers()`：击杀没归巢的蟑螂 + 清空地上的食物 |

刷物品故意放在黑屏期间，玩家看不到东西凭空出现。

## 选点规则

1. 在 `GridManager` 的 `width × height` 里随机取一个格子，重复取 `_spawnPointCount` 次；
2. 该格子以及周围 `_wallClearance` 圈（默认 1 = 3x3）里只要有一个 `TileWall`，**直接放弃这个点**，不找替代点；
3. 网格外一律算墙（`GridManager.IsBlocked` 的约定），所以贴边的点会被自动排掉；
4. 通过检查的点掷一次骰：`_blackBoxChance` 出黑盒 → `_foodChance` 出食物 → 剩下的概率这个点空着。

两个概率相加超过 1 时，食物概率会被自动压到 `1 - 黑盒概率`。

> 默认值是 30% 黑盒 / 50% 食物 / 20% 空点。
> 实际生成的件数一定 ≤ `_spawnPointCount`（点被墙挡掉的就没了）。

## 参数

| 字段 | 说明 |
|---|---|
| `_spawnItems` | 总开关，关掉就完全不刷 |
| `_spawnPointCount` | 随机选点的尝试次数（默认 8） |
| `_wallClearance` | 墙体检测半径，单位是**格**。0 = 只查自己那一格，1 = 3x3 |
| `_blackBoxPrefab` | 黑盒预制体 |
| `_blackBoxChance` / `_foodChance` | 两种物品的概率 |
| `_itemPool` | 商品池资产。这里只取它里面 `Category == Food` 的条目当食物来源，工具/装扮不参与 |
| `_spawnYOffset` | 生成高度偏移，相对网格平面（默认 0 = 贴地平面） |
| `_cleanupAtDay` | 天亮是否清场 |
| `_keepHandPlacedOnFirstDay` | 第一个白天不清场，保留场景里手工摆的东西 |

两个带 `[ContextMenu]` 的方法可以直接右键组件测试，不用真的等阶段切换：
**立即刷一批夜晚物品（测试用）** / **立即清场（天亮逻辑）**。

## 商品池（`ItemPool.asset`）

`Assets/Prefabs/Items/ItemPool.asset` 是一份**纯数据**商品列表（模板 + 售价范围），
商店进货、黑盒出货、夜晚刷食物**挂的是同一个资产**，加一件商品只改它。

| 谁用 | 怎么用 |
|---|---|
| `MouseStore._itemPool` | 按 `Category` 分页进货，价格从各条的 `priceRange` 抽 |
| `BlackBox.itemPool` | 按稀有度分档等概率抽一件 |
| `NightManager._itemPool` | 只抽 `Category == Food` 的，当夜晚刷点的食物来源 |

分类不用单独配：由物品自己的 `PickableItem.Category` 决定（`FoodItem` → Food，`ToolItem` → Tool）。

## 注意

- **黑盒的出货源是资产，不是场景物体**：`BlackBox.itemPool` 在预制体里就已经连上商品池了，
  所以刷出来的黑盒和场景里手摆的那一个都能正常出货。
  万一以后新建的黑盒预制体忘了连，`NightManager.WireBlackBox` 会用夜晚上这一份补上。
- **黑盒不会在天亮被清掉**（只有食物会）。没被扫掉的黑盒会一直留在场上，一夜一夜累积。
  如果不想累积，要么在天亮时也清一遍，要么把黑盒概率调低。
- **只清地图上的食物**：玩家背包里的、装备槽里的、商店/仓库里的都不动。
- 清食物时会把那一带的**觅食信息素**一起抹掉（半径用 `CockroachSettings.EmptyClearRadius`），
  否则下一夜蟑螂还会往空地上跑。
- 蟑螂的清理和 `CockNest` 自己那套（阶段切换时杀没回巢的）是重复的，但两边都幂等：
  `Cockroach.Die` 有 `_dead` 兜底，所以不会重复结算。
  `CockNest` 那份留着是为了没挂 NightManager 的测试场景。
- 墙体检测只看 `GridManager.wallLayer`（目前是 Wall 层），
  桌子柜子这类不挡路的家具不算，物品可能和它们视觉上重叠。
