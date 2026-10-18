using TapTapGameJam.Audio;
using System;
using System.Collections.Generic;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Items.tools;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 背包里的装备槽位（头 / 身 / 足）
    /// </summary>
    public enum EquipSlot
    {
        /// <summary>头</summary>
        Head = 0,
        /// <summary>身</summary>
        Body = 1,
        /// <summary>足</summary>
        Legs = 2
    }

    /// <summary>
    /// 玩家背包：只当"后端"用，负责物品真正存进/移出背包、以及物品状态（OnMap / InBag / Equipped）的变换。
    ///
    /// 【不管什么】
    /// - 不管"界面上正被拖着哪一件"。那是纯界面状态：块自己记着它从哪个槽位来
    ///   （<c>BagItemBlock.CurrentSlot</c>），放不下时靠它回退，后端没必要记。
    /// - 不引用 UI。界面每帧直接读这里的状态。
    /// - 只有 <see cref="PendingPickupItem"/> 是个例外：它是"从地图捡起来、等着被放进背包"的那一件，
    ///   属于游戏状态而不是界面状态，所以留在后端，由界面在打开背包时消费。
    ///
    /// 【唯一的事件：<see cref="EquippedChanged"/>】
    /// 装备槽变化必须能被外部感知，否则「装扮提供的 Buff」永远不会重算。
    /// 所以槽位的所有写入都收敛到 <see cref="SetEquipped"/>，由它统一发事件——
    /// <b>不要直接写 <c>_equipped[]</c></b>，漏一处就是「换装不生效」这种极难查的 bug。
    ///
    /// 【物品状态】
    /// - OnMap：躺在地图上（也在世界物件里可见）。
    /// - InBag：属于玩家，本体被挪到暂存区隐藏。注意"被界面拿在手上拖动"的物品也算 InBag，
    ///   因为它在后端看来仍然是玩家的东西，只是暂时没有占格记录。
    /// - Equipped：穿在装备槽里，本体同样在暂存区隐藏。
    /// 进背包 / 装备时本体不销毁，所以食物剩余营养之类的运行时状态天然保留。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerBag : MonoBehaviour
    {
        #region 配置

        [Header("背包尺寸")]
        [Tooltip("列数")]
        [SerializeField, Min(1)] private int _width = 5;
        [Tooltip("行数")]
        [SerializeField, Min(1)] private int _height = 5;

        [Header("暂存区")]
        [Tooltip("进背包/装备后，物品本体被挪到这个相对位置隐藏起来。放得足够远，避免被相机或物理碰到")]
        [SerializeField] private Vector3 _stagingOffset = new Vector3(0f, 10000f, 0f);

        [Header("丢弃")]
        [Tooltip("丢弃时物品落在玩家前方多远")]
        [SerializeField, Min(0f)] private float _dropForwardDistance = 1.2f;
        [Tooltip("丢弃落点的随机散开半径。免得连着丢几件精确叠在同一个点上，之后点不到下面那些")]
        [SerializeField, Min(0f)] private float _dropScatterRadius = 0.6f;
        [Tooltip("丢弃时从玩家头顶往下打射线贴地，最长距离")]
        [SerializeField, Min(0f)] private float _dropRayLength = 5f;

        public int Width => _width;
        public int Height => _height;
        public int CellCount => _width * _height;

        #endregion

        #region 运行时数据

        /// <summary>背包里每个已放置物品的落点记录</summary>
        public class Placement
        {
            public PickableItem item;

            /// <summary>抓取格（形状里 (0,0) 那一格）落在背包的哪个格子上</summary>
            public Vector2Int origin;
        }

        // 物品数量是个位数，用"列表 + 每次遍历"比维护二维指针更不容易出错。
        private readonly List<Placement> _placements = new List<Placement>();
        private readonly PickableItem[] _equipped = new PickableItem[3];

        private Transform _stagingRoot;

        /// <summary>
        /// 从地图捡起来、等着玩家在背包界面里安顿的那一件。
        /// 由 <see cref="PickUp"/> 或 <see cref="SetPendingPickupItem"/> 设置；
        /// 界面在收到"背包打开"事件时读它来决定要不要生成手持块，
        /// 收到"背包关闭"事件时调 <see cref="ClearPendingPickupItem"/> 清掉。
        /// </summary>
        public PickableItem PendingPickupItem { get; private set; }

        public List<Placement> Placements => _placements;
        
        #endregion

        #region 只读查询

        public bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height;
        }

        public PickableItem ItemAt(Vector2Int cell)
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                if (Contains(_placements[i], cell))
                {
                    return _placements[i].item;
                }
            }

            return null;
        }

        /// <summary>某个物品是以哪个格子为抓取格放着的（不在背包格子里返回 false）</summary>
        public bool TryGetPlacement(PickableItem item, out Vector2Int origin)
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                if (_placements[i].item == item)
                {
                    origin = _placements[i].origin;
                    return true;
                }
            }

            origin = default;
            return false;
        }

        /// <summary>该物品当前占用的所有格子（按已放置的位置。不在背包里则返回空）</summary>
        public IEnumerable<Vector2Int> CellsOf(PickableItem item)
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                if (_placements[i].item != item)
                {
                    continue;
                }

                foreach (Vector2Int cell in CellsOf(item, _placements[i].origin))
                {
                    yield return cell;
                }

                yield break;
            }
        }

        /// <summary>
        /// 把物品的抓取格放在 <paramref name="origin"/> 时会覆盖到的格子。
        /// 不做越界过滤（形状允许负偏移），调用方自己判。
        /// </summary>
        public IEnumerable<Vector2Int> CellsOf(PickableItem item, Vector2Int origin)
        {
            if (item == null || item.shape == null)
            {
                yield break;
            }

            foreach (Vector2Int cell in item.shape.CellsOf(origin))
            {
                yield return cell;
            }
        }

        /// <summary>
        /// 把物品的抓取格放在 <paramref name="origin"/> 时能不能放下。
        /// 这个重载就是界面预览算出来的 ok —— 保证"预览是绿的，点下去一定放得下"。
        /// 旋转态已经存在物品自己的 shape 里，所以不需要再传旋转参数。
        /// </summary>
        /// <param name="ignore">需要排除自身占格的物品（把已经在背包里的东西挪位置时要传自己，否则会被自己挡住）</param>
        public bool CanPlace(PickableItem item, Vector2Int origin, PickableItem ignore = null)
        {
            if (item == null || item.shape == null)
            {
                return false;
            }

            foreach (Vector2Int cell in CellsOf(item, origin))
            {
                if (!InBounds(cell))
                {
                    return false;
                }

                PickableItem occupant = ItemAt(cell);
                if (occupant != null && occupant != ignore)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>找第一个能放下的抓取格位置（自动整理/兜底用）</summary>
        public bool TryFindFirstFit(PickableItem item, out Vector2Int origin)
        {
            origin = default;
            if (item == null)
            {
                return false;
            }

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);
                    if (CanPlace(item, candidate))
                    {
                        origin = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        public PickableItem GetEquipped(EquipSlot slot)
        {
            int index = (int)slot;
            return index >= 0 && index < _equipped.Length ? _equipped[index] : null;
        }

        /// <summary>三个装备槽的当前内容（下标 = <see cref="EquipSlot"/>）。槽可能为 null。</summary>
        public IReadOnlyList<PickableItem> EquippedItems => _equipped;

        /// <summary>
        /// 装备槽发生了变化（穿上 / 换下 / 摘下）。**这是装扮 Buff 唯一的刷新信号**。
        /// 一次换装可能连续触发两次（先清旧槽、再写新槽），订阅方必须做到幂等。
        /// </summary>
        public event Action EquippedChanged;

        /// <summary>
        /// 写装备槽的唯一入口。所有对 <c>_equipped[]</c> 的赋值都必须走这里，
        /// 否则订阅方（PlayerBuffs）收不到通知，装扮 Buff 会停在换装前的状态。
        /// </summary>
        private void SetEquipped(EquipSlot slot, PickableItem item)
        {
            int index = (int)slot;
            if (index < 0 || index >= _equipped.Length || _equipped[index] == item)
            {
                return;
            }

            _equipped[index] = item;
            EquippedChanged?.Invoke();
        }

        /// <summary>某个物品在哪个装备槽里（不在任何槽里返回 false）</summary>
        public bool TryGetEquipSlot(PickableItem item, out EquipSlot slot)
        {
            for (int i = 0; i < _equipped.Length; i++)
            {
                if (_equipped[i] == item && item != null)
                {
                    slot = (EquipSlot)i;
                    return true;
                }
            }

            slot = default;
            return false;
        }

        /// <summary>
        /// 背包里被物品占了多少格（只算放进格子的，界面拿在手上的那件不算）。
        /// 负重换算用这个值。
        /// </summary>
        public int OccupiedCellCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _placements.Count; i++)
                {
                    if (_placements[i].item != null && _placements[i].item.shape != null)
                    {
                        count += _placements[i].item.shape.Count;
                    }
                }

                return count;
            }
        }

        #endregion

        #region 拾取

        /// <summary>
        /// 把地图上的物品捡起来。
        /// - 饰品且对应槽位为空：直接穿上，不需要玩家再操作（返回 true，PendingPickupItem 仍为 null）。
        /// - 其余情况：记成 <see cref="PendingPickupItem"/>，等玩家在背包界面里安顿。
        ///
        /// 这里不负责开界面，也不返回"该不该开界面"——那是输入层/界面的事。
        /// </summary>
        public bool PickUp(PickableItem item)
        {
            if (item == null || !item.IsOnMap)
            {
                return false;
            }

            // 手上/待拾取位只有一件：已经有一件等着安顿就先不接受新的，
            // 否则前一件会卡在"既不在背包也不在世界"的状态。
            if (PendingPickupItem != null && PendingPickupItem != item)
            {
                return false;
            }

            if (item is DressItem dress)
            {
                EquipSlot slot = dress.ToSlot();
                if (GetEquipped(slot) == null)
                {
                    MoveToBagStorage(item);
                    SetEquipped(slot, item);
                    item.SetLocation(PickableItem.ItemLocation.Equipped);
                    Sfx.Play(SoundId.Equip, this);
                    PendingPickupItem = null;
                    return true;
                }
            }

            PendingPickupItem = item;
            GameManager.PlayerInputManager.OpenBackpack();
            EventPipeline.Publish(new BackpackEvent(true));
            return true;
        }

        /// <summary>放弃"等着安顿的那一件"（例如它已经被放进背包、或者界面关掉了）</summary>
        public void ClearPendingPickupItem()
        {
            PendingPickupItem = null;
        }

        /// <summary>
        /// 把一件已经不在世界上的物品登记成"待拾取"。
        /// 用在"从装备槽脱下来"这类场合：物品本体还在暂存区里，但已经不属于玩家了，
        /// 界面下次打开时会把它当成手持物、并提示玩家去安顿。
        /// </summary>
        public void SetPendingPickupItem(PickableItem item)
        {
            if (item == null)
            {
                return;
            }

            if (PendingPickupItem != null && PendingPickupItem != item)
            {
                // 待拾取位只有一个：旧的先回世界，免得它卡在没人记录的状态
                ReturnToWorld(PendingPickupItem, EvaluateDropPosition());
            }

            TakeFromGrid(item);
            if (TryGetEquipSlot(item, out EquipSlot slot))
            {
                SetEquipped(slot, null);
            }

            // 状态上离开背包，但本体不做"回世界"的那套摆放——它其实是被界面拿在手上
            item.SetLocation(PickableItem.ItemLocation.OnMap);
            PendingPickupItem = item;
        }

        #endregion

        #region 放置 / 取出

        /// <summary>把物品放进背包格子（抓取格落在 origin）。旋转态已经在物品自己的 shape 里</summary>
        public bool TryPlace(PickableItem item, Vector2Int origin)
        {
            return TryPlace(item, origin, item);
        }

        /// <summary>
        /// 把物品放进背包格子。
        /// </summary>
        /// <param name="ignore">
        /// 算占格时要忽略的物品。默认忽略自己 —— 把已经在背包里的东西挪位置时，
        /// 不排除自身就会被自己原来的占格挡住。
        /// </param>
        public bool TryPlace(PickableItem item, Vector2Int origin, PickableItem ignore)
        {
            if (item == null || !CanPlace(item, origin, ignore))
            {
                return false;
            }

            TakeFromGrid(item); // 挪位置的情况：先把旧的占位摘掉
            _placements.Add(new Placement
            {
                item = item,
                origin = origin
            });

            MoveToBagStorage(item);
            item.SetLocation(PickableItem.ItemLocation.InBag);

            if (PendingPickupItem == item)
            {
                // 已经安顿好了，不再"等着被拾取"
                PendingPickupItem = null;
            }

            return true;
        }

        /// <summary>把物品放进第一个能放下的位置</summary>
        public bool TryPlace(PickableItem item, out Vector2Int origin)
        {
            origin = default;
            if (item == null || !TryFindFirstFit(item, out origin))
            {
                return false;
            }

            return TryPlace(item, origin, item);
        }

        /// <summary>把物品从背包格子里摘掉（仅维护Placement）</summary>
        public bool TakeFromGrid(PickableItem item)
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                if (_placements[i].item == item)
                {
                    _placements.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
        
        
        public bool IsInGrid(PickableItem item)
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                if (_placements[i].item == item)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 装备槽

        /// <summary>
        /// 把饰品装到对应槽位。
        /// 槽里原本有的那件不在这里处理：通过 <paramref name="replaced"/> 交出去，
        /// 由界面决定怎么安顿（当前策略是送回世界并提示玩家）。
        /// </summary>
        public bool TryEquip(PickableItem item, out PickableItem replaced)
        {
            replaced = null;
            if (item is not DressItem dress)
            {
                return false;
            }

            EquipSlot slot = dress.ToSlot();
            PickableItem current = _equipped[(int)slot];
            if (current == item)
            {
                return false;
            }

            // 先把它从原来的位置摘掉（可能在格子里，也可能已经在别的槽里）
            TakeFromGrid(item);
            if (TryGetEquipSlot(item, out EquipSlot oldSlot))
            {
                SetEquipped(oldSlot, null);
            }

            if (current != null)
            {
                SetEquipped(slot, null);
                replaced = current;
            }

            MoveToBagStorage(item);
            SetEquipped(slot, item);
            item.SetLocation(PickableItem.ItemLocation.Equipped);
            Sfx.Play(SoundId.Equip, this);

            if (PendingPickupItem == item)
            {
                PendingPickupItem = null;
            }

            return true;
        }

        /// <summary>把槽位里的饰品摘下来（仅维护slot）</summary>
        public bool TakeFromSlot(EquipSlot slot, out PickableItem item)
        {
            int index = (int)slot;
            item = _equipped[index];
            if (item == null)
            {
                return false;
            }

            SetEquipped(slot, null);
            return true;
        }

        #endregion

        #region 丢弃 / 回世界

        /// <summary>
        /// 计算"丢在玩家脚边"的落点：玩家前方一点、再加一个随机散开量，然后往下打射线贴地。
        /// 射线打不到（比如脚下没碰撞体）就用原高度，至少不会掉到地底。
        ///
        /// 散开是在打射线**之前**加的，所以东西依然是贴在地面上的，只是落点不再重合。
        /// </summary>
        public Vector3 EvaluateDropPosition()
        {
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.forward;
            }

            flatForward.Normalize();

            // 随机偏移：不散开的话连着丢几件会精确叠在一个点上，堆在下面的那件再也点不到
            Vector2 scatter = UnityEngine.Random.insideUnitCircle * _dropScatterRadius;
            Vector3 target = transform.position
                             + flatForward * _dropForwardDistance
                             + new Vector3(scatter.x, 0f, scatter.y);

            if (_dropRayLength > 0f
                && Physics.Raycast(target + Vector3.up, Vector3.down, out RaycastHit hit, _dropRayLength))
            {
                return hit.point + Vector3.up * 0.1f;
            }

            return target;
        }

        /// <summary>就地丢在玩家面前</summary>
        public bool DropToWorld(PickableItem item)
        {
            return ReturnToWorld(item, EvaluateDropPosition());
        }

        /// <summary>
        /// 把物品放回地图。回地图后剩余营养等状态原样保留（本体从没被销毁过）。
        /// 本来就在地图上的物品不会重复处理，避免把玩家脚下刚丢下的东西又挪一次。
        /// </summary>
        public bool ReturnToWorld(PickableItem item, Vector3 worldPos)
        {
            if (item == null)
            {
                return false;
            }

            if (item.IsOnMap)
            {
                return false;
            }

            TakeFromGrid(item);
            if (TryGetEquipSlot(item, out EquipSlot slot))
            {
                SetEquipped(slot, null);
            }

            if (PendingPickupItem == item)
            {
                PendingPickupItem = null;
            }

            item.transform.SetParent(null, false);
            item.transform.position = worldPos;
            item.transform.rotation = Quaternion.identity;
            item.SetLocation(PickableItem.ItemLocation.OnMap);
            item.gameObject.SetActive(true);
            return true;
        }

        #endregion

        #region 使用

        /// <summary>
        /// 在背包里使用某个物品（右键），先丢回世界、再使用。
        /// - 子类通过 <see cref="PickableItem.UseItem"/> 施加效果；
        /// - 返回是否使用成功，成功则UI需要清理
        /// </summary>
        public bool UseItem(PickableItem item, PlayerBasic player)
        {
            if (item == null || player == null)
            {
                return false;
            }

            if (item is ToolItem toolItem)
            {
                if (IsInGrid(item) && toolItem.UseItem(player))
                {
                    TakeFromGrid(item);
                    Destroy(item);
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 内部：状态搬运

        /// <summary>把物品本体挂到暂存区并隐藏。不销毁，所以运行时状态全部保留</summary>
        private void MoveToBagStorage(PickableItem item)
        {
            Transform staging = EnsureStagingRoot();
            item.transform.SetParent(staging, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;

            // 藏起来之前必须主动从交互系统注销。
            // Unity 不会为"被禁用的碰撞体"触发 OnTriggerExit，所以光靠 SetActive(false)
            // 这件物品会一直留在 PlayerInteractionManager 的候选列表里；而暂存区是玩家的子物体、
            // 偏移只有 Y=+10000，PlayerInteractionManager 又刻意只比水平距离 ——
            // 它的距离恒为 0，于是永远被选成"最近的交互目标"，把焦点霸住。
            // 它的 CanInteract 这时已经是 false（Location 不再是 OnMap），
            // 结果就是按交互键只会报"无法交互"，而地上别的东西永远轮不到。
            item.ClearAllRegister();

            item.gameObject.SetActive(false);
        }

        /// <summary>
        /// 懒创建暂存区。做成 PlayerBag 的子物体：
        /// 这样玩家被销毁时暂存区跟着销毁，物品不会变成泄漏的孤儿对象。
        /// </summary>
        private Transform EnsureStagingRoot()
        {
            if (_stagingRoot != null)
            {
                return _stagingRoot;
            }

            GameObject root = new GameObject("~BagItemStaging");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = _stagingOffset;
            _stagingRoot = root.transform;
            return _stagingRoot;
        }

        private void OnDestroy()
        {
            // 玩家没了，暂存区里的物品也跟着销毁；这里只清掉悬挂的引用，避免空引用报错
            _placements.Clear();
            for (int i = 0; i < _equipped.Length; i++)
            {
                _equipped[i] = null;
            }

            PendingPickupItem = null;
        }

        private static bool Contains(Placement placement, Vector2Int cell)
        {
            if (placement == null || placement.item == null || placement.item.shape == null)
            {
                return false;
            }

            foreach (Vector2Int c in placement.item.shape.CellsOf(placement.origin))
            {
                if (c == cell)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
