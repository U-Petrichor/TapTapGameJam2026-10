# 食物空间索引（FoodManager）

蟑螂找食物不再"遍历全场食物列表"，而是查一张和 `GridManager` 同尺寸的格子表。
这是 800 只蟑螂同时觅食还跑得动的前提。

## 为什么改

原来的 `ItemManager` 实际上只剩一件事在被用：`GetFoodOnMap()` —— 把**所有** `FoodItem` 收集成一个列表。
`Cockroach` 每个 `FixedUpdate` 都要拿两次（决策一次、取食一次），所以：

```
800 只 × 2 次 × 500+ 件食物 ≈ 80 万次 距离判定 + 80 万个 List.Add / 帧
```

「群颚破壁」之后每面墙、每张桌子都是一个 `FoodItem`，这个数字直接爆掉。
症状很有辨识度：**只有觅食卡，归巢不卡** —— 因为归巢路径根本不碰食物列表
（`Decide` 的食物分支只在非归巢时执行，`TryEat` 只在 `Forage` 状态调用）。

## 结构

| 字段 | 作用 |
| --- | --- |
| `List<FoodItem>[] _cells` | `[y * width + x]` → 这一格里有哪几件食物。**惰性创建**，只有真正有食物的格子才 new List |
| `Dictionary<FoodItem, List<Vector2Int>> _occupied` | 食物 → 它占了哪些格子，注销时用 |
| `List<FoodItem> _foods` | 场上所有已登记的食物，给清场 / 统计这类"要全量"的地方用 |

- 尺寸 = `GridManager.width × height`（当前 500 × 300 = 15 万格）。数组本身 1.2MB；
  真正有食物的格子约几万个（「群颚破壁」注入后场上约有 7,600 个可食用物体），所以 List 也只建这个量级。
- **多格指向同一个 `FoodItem` 是允许的**：普通食物只占脚下那一格，`WallFoodItem` 占它包围盒盖住的一整片。
- 不用 `Dictionary<格子, 列表>`：查询是"一格一次索引"，字典要为每格算 key、查哈希，比数组慢且占内存。
- **单件食物最多占 `MaxCellsPerFood`（4096）格**，超过就拒收并打 warning。
  这是一道硬保险：场景里有整块的背景大模型，一旦被当成食物注册，一次就是十几万格
  （1MB+ 的 `List<Vector2Int>` + 十几万次入桶），当场卡死。

## 登记 / 注销

由 `FoodItem` 自己负责，**不是** `PickableItem`：

```
OnEnable   → 如果营养已初始化 → 登记（从背包丢回地上时走这条）
Start      → 初始化营养 → 登记（普通食物首次上场的路径）
OnDisable  → 注销
```

**为什么不在 `OnEnable` 里无条件登记**：`OnEnable` 早于 `Start`，那时 `remainingNutrition` 还是 0。
提前登记会让蟑螂查到一件"没营养"的食物，而 `Consume` 里有僵尸自愈逻辑，会当场把它删掉。
所以只有"营养初始化完成"之后才登记。

**为什么 `OnDisable` 能当"离开地图"用**：进背包 / 商店 / 仓库时物品都会被 `SetActive(false)`
（`PlayerBag.MoveToBagStorage`、`MouseStore`、`PlayerRepo`），丢回地上则是 `SetActive(true)`。
注意丢回地上的顺序是**先设坐标再 SetActive**（`PlayerBag` 里已经是这个顺序），
否则会按仓库坐标登记到错误的格子上。

`WallFoodItem` 是运行期 `AddComponent` 的，`OnEnable` 时还拿不到包围盒，
所以它的登记放在 `Configure()` 里（`NutritionInitialized = true` 之后）。

## 查询

```csharp
bool TryFindNearestFood(Vector2 worldPos, float radius, out FoodItem food, out Vector2 eatPoint)
```

- 以 `worldPos` 所在格子为中心，**由内圈向外圈**逐环搜索，**命中第一个还有营养的食物就返回**。
- `eatPoint` 是命中那一格的**中心**：蟑螂对着它转向、走到够得着为止。
- **不分配任何对象**：不 new List、不 new HashSet、不用 LINQ。

为什么不去重：一件食物占多格，但搜索是"由内向外、命中即返回"，
所以同一个食物最多只会被检查一次（命中那一格就结束了），根本不会重复命中 —— 也就省掉了 HashSet。

为什么命中即返回很重要：破壁之后墙遍地都是，通常**内圈一两格就命中了**，单次查询只有几十次数组访问。
只有在完全开阔、附近真的没有食物时才会扫满整个半径（最坏 `Decide` 是 21×21 = 441 格）。

半径换算：`ceil(radius / min(cellWidth, cellHeight))`，上限 48 格（防止有人把取食距离配大了把整张图扫一遍）。

## 取食距离

食物不再有"自己的交互半径"（`FoodItem.interactRaius` 已删除）。
统一用 `CockroachSettings.EatReach`（默认 0.8 世界单位 ≈ 4 格）。

旧口径是 `身体半径 0.34 + 食物半径 0.5 = 0.84`，新值基本等价。
`Decide` 里"看见食物就转向"用的是 `PermanentRange`（2.0），比取食距离大，所以会出现
"朝着食物走但还够不着"的过渡，和改之前一致。

## 行为变化

`TryEat` 从"列表里第一个够得着的"变成"**最近的**"，蟑螂会明显更粘、更集中。
破壁天赋下它们以前会随机挑一面墙啃，现在基本都是脚下那面 → **墙被拆得更快**，留意数值。

## 排查

- 蟑螂完全找不到食物 → 先看 Console 有没有
  `[FoodItem:xxx]：场景里没有 FoodManager`。场景里那个挂着 `FoodManager` 的物体被删了的话会有这条。
- `WallGnawingDirector` 的 `[ContextMenu] 统计已注入的墙体` 会打出场上的进度分布和"营养 0 却还活着"的僵尸数。
- 想确认某格有没有登记，临时在 `FoodManager.Register` 里打一条日志即可（正常不要常开，五百多条）。
