# 装扮穿戴外观（蟑螂头部的 3D 挂饰）

把背包**头部**装备槽里的装扮显示到夜晚蟑螂模型头上。

> 身体（背部）和脚部的装备**照常能穿、照常给 Buff**，只是不做 3D 外观 —— 见下方「为什么只有头」。
> 数值仍然走 `DressData` + `PlayerBuffs`，外观和数值两条线互不影响。

## 数据流

```
背包里穿 / 换 / 脱 / 丢
        │
        ▼
PlayerBag.SetEquipped          ← 装备槽的唯一写入口
        │
        ▼
    EquippedChanged 事件
        ├──► PlayerBuffs.RefreshDress     数值侧（已有，三槽都算）
        └──► PlayerDressView.Refresh      外观侧（本系统，只处理 Head）
                  │
                  ├─ 拆掉上次生成的全部穿戴物
                  └─ 头部槽有东西就生成一份，挂 mixamorig:Head
```

**没有改任何装备逻辑**：`PlayerBag` / `BagPanelUI` / `EquipSlot` 一行都没动，外观纯靠订阅事件。

## 文件

| 文件 | 作用 |
|---|---|
| `Assets/Scripts/Items/DressWornVisual.cs` | 一件装扮的**穿戴配置**（穿哪份模型、挂哪根骨骼、偏移/缩放），挂在 Dress 预制体根上 |
| `Assets/Scripts/Player/PlayerDressView.cs` | 执行者：监听事件、解析骨骼、生成/销毁穿戴物，挂在 Player 根上 |
| `Assets/Scripts/Items/DressWornFitting.cs` | 调参辅助：运行时生成物自带，右键可把当前摆放复制成配置文本 |

## 为什么只有头

外观只给头部做，是当前的美术范围决定。代码里只有一个开关：

```csharp
// PlayerDressView
private const EquipSlot VisibleSlot = EquipSlot.Head;
```

身体 / 脚部的预制体上**没有** `DressWornVisual` 组件（挂了也不会被读到，因为槽位被筛掉）。
要恢复：

1. 把 `VisibleSlot` 换成一个槽位集合，并把 `Refresh` 里的 `slot != VisibleSlot` 改成集合判断；
2. 给对应 Dress 预制体挂回 `DressWornVisual`（模型可以留空，会自动取预制体里第一个带 `Renderer` 的子物体）。

骨骼映射、左右脚镜像、双脚各生成一份的逻辑**都还在**，没有删。

## 骨骼

蟑螂模型 `zhanglang(5).fbx` 是 **Humanoid** 导入的标准 Mixamo 骨架。两套命名对不上，注意：

| 槽位 | `WornBone`（Mixamo 名） | Unity `HumanBodyBones` |
|---|---|---|
| **Head（在用）** | `mixamorig:Head` | `Head` |
| Body（备用） | `mixamorig:Spine2` | **`UpperChest`** |
| Body（备用） | `mixamorig:Spine1` | `Chest` |
| Legs（备用） | `mixamorig:LeftFoot` / `RightFoot` | `LeftFoot` / `RightFoot` |

`PlayerDressView` 找骨骼的顺序：**按名字递归找**（`mixamorig:xxx`，白天模型 inactive 也能找到）→ 去前缀再找
→ 最后才用 `GetBoneTransform` 兜底（模型激活后最稳）。三路都失败会打一条带节点名建议的 `LogError`。

## 配置

### 头部的 2 件 Dress 预制体

`Head_HanBao` / `Head_ZhiDai` 根物体上挂了 `DressWornVisual`：

| 字段 | 说明 |
|---|---|
| `_wornModelPrefab` | 穿戴用的模型。留空则自动取预制体里第一个带 `Renderer` 的子物体（气泡 Canvas 不会被误选） |
| `_attach` | 挂点：`bone`（Auto = 按槽默认 = Head）、`localPosition`、`localEuler`、`scale` |
| `_mirrorToRightFoot` / `_rightFootAttach` | 足部专用，目前没用；以后恢复脚部外观时用它 |

- `localPosition` / `localEuler` 是**骨骼的局部坐标系**，不是世界坐标。
- `scale` 在 `compensateBoneScale` 勾上时填的是**世界尺寸**（代码按 `bone.lossyScale` 反算局部缩放）。
  蟑螂模型根是 2.3 倍，勾上就不用管这个倍数；不勾就得自己乘 1/2.3。
- **不要用负缩放做镜像**：URP 下会翻面（法线反向 + 背面剔除）。

### Player 预制体

根上挂 `PlayerDressView`，两个引用：

- `_bag` → PlayerBag（留空会自动 `GetComponent`）
- `_nightModelRoot` → `zhanglang(5)`

## 调参流程

模型是 tripo 出的静态网格，原点不在包围盒中心，两件头套都得对一遍。步骤：

1. 进夜晚场景，戴上要调的那件；
2. 在 Hierarchy 里展开蟑螂骨骼，找到 `xxx_Worn`（挂在 `mixamorig:Head` 下）；
3. 拖位置、转角度、改缩放，直到看着对；
4. 选中它 → 右键组件 `DressWornFitting` → **复制挂点配置到剪贴板**；
5. 粘进对应 Dress 预制体的 `DressWornVisual._attach`。

## 坑

| 坑 | 说明 |
|---|---|
| 装备后物品本体在哪 | 被 `PlayerBag.MoveToBagStorage` 挪到 `~BagItemStaging`（Y+10000）并隐藏。**不能**把它直接搬到骨骼上，否则丢弃/换下时位置和显示都会乱 —— 所以穿戴件是独立的一份拷贝 |
| 事件会连发两次 | `TryEquip` 先清旧槽、再写新槽。`Refresh` 全拆全建，所以幂等 |
| 身体 / 脚部没外观 | 是有意为之（`VisibleSlot` 筛掉了），不是漏了 `DressWornVisual` |
| 白天不显示 | 靠"骨骼子物随父级隐藏"天然实现，不需要监听昼夜事件 |
| 碰撞体 / Animator | 穿戴物会摘掉全部 Collider / Rigidbody / Animator（tripo FBX 是 Generic 导入，自带空 Animator），并改到 Player 层，避免抢交互焦点 |
| 材质风格 | 蟑螂用 `Cockroach_Outline.mat`（描边 Shader），服装是各自 tripo 材质。觉得"像贴纸"再统一换 Shader |

## 验收清单

- [ ] 夜晚拾取头套 → 自动穿上 → 头上出现模型
- [ ] 同槽换一件 → 旧的消失、新的出现，不留两个
- [ ] 从槽里拖出来 → 地上出现物品本体、头上模型消失
- [ ] 穿身体 / 脚部 → **不出现任何模型**，但 Buff 照常生效
- [ ] 白天切换 → 自动隐藏；回夜晚 → 还在、位置正确
- [ ] 携带（Carry）动画时头饰跟着动
- [ ] 反复穿脱 20 次，Hierarchy 里 `_Worn` 数量始终 ≤ 1
