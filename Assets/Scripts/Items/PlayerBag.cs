using System.Collections.Generic;
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
    /// 捡起物品的结果
    /// </summary>
    public enum PickUpResult
    {
        /// <summary>手上没有空位 / 物品不该被捡起，什么都没发生</summary>
        Failed = 0,
        /// <summary>成功拿在手上，等待玩家放进背包</summary>
        InHand = 1,
        /// <summary>饰品且对应槽位为空，已直接穿上（不需要打开背包界面）</summary>
        EquippedDirectly = 2
    }

    /// <summary>
    /// 玩家背包：管"哪些物品、分别占了哪些格子"，外加一个手位和三个装备槽。
    ///
    /// 设计要点：
    /// 1. 纯逻辑，不引用任何 UI 类型，也不发事件。
    ///    界面（BagPanelUI）每帧直接读这里的状态 —— 数据只有这一份真相，谁改了界面都能看到，
    ///    不会出现"忘了触发事件导致显示陈旧"的问题。
    /// 2. 物品进背包 / 装备时本体不销毁，而是被挪到"暂存区"隐藏起来。
    ///    这样食物剩余营养之类的运行时状态天然保留，不用额外写一套存档结构。
    /// 3. 形状与旋转态都在物品自己的 <see cref="BagShape"/> 里（(0,0) 是抓取格），
    ///    这里只记"抓取格落在背包的哪个格子"，不维护第二份旋转数据。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerBag : MonoBehaviour
    {
        #region 配置

        [Header("背包尺寸")]
        [Tooltip("列数")]
        [SerializeField, Min(1)] private int _width = 6;
        [Tooltip("行数")]
        [SerializeField, Min(1)] private int _height = 4;

        [Header("暂存区")]
        [Tooltip("进背包/装备后，物品本体被挪到这个相对位置隐藏起来。放得足够远，避免被相机或物理碰到")]
        [SerializeField] private Vector3 _stagingOffset = new Vector3(0f, 10000f, 0f);

        [Header("丢弃")]
        [Tooltip("丢弃时物品落在玩家前方多远")]
        [SerializeField, Min(0f)] private float _dropForwardDistance = 1.2f;
        [Tooltip("丢弃时从玩家头顶往下打射线贴地，最长距离")]
        [SerializeField, Min(0f)] private float _dropRayLength = 5f;

        public int Width => _width;
        public int Height => _height;
        public int CellCount => _width * _height;

        #endregion

        #region 运行时数据

        /// <summary>背包里每个已放置物品的落点记录</summary>
        private class Placement
        {
            public PickableItem item;

            /// <summary>抓取格（形状里 (0,0) 那一格）落在背包的哪个格子上</summary>
            public Vector2Int origin;
        }

        // 物品数量是个位数，用"列表 + 每次遍历"比维护二维指针更不容易出错。
        private readonly List<Placement> _placements = new List<Placement>();
        private readonly PickableItem[] _equipped = new PickableItem[3];

        private Transform _stagingRoot;

        #endregion

        #region 手

        /// <summary>当前拿在手上、还没放进格子的物品。同一时间只允许一件</summary>
        public PickableItem HandItem { get; private set; }

        public bool HasHandItem => HandItem != null;

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

        /// <summary>某个物品是以哪个格子为左上角放着的（不在背包里返回 false）</summary>
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
        /// <param name="ignore">需要排除自身占格的物品（把已经在背包里的东西挪个位置时必须传自己，否则会被自己挡住）</param>
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

        #endregion

        #region 捡起 / 手

        /// <summary>
        /// 把地图上的物品捡起来。
        /// 饰品且对应槽位为空时直接穿上；否则拿在手上，由界面进入摆放流程。
        /// </summary>
        public PickUpResult PickUp(PickableItem item)
        {
            if (item == null || !item.IsOnMap)
            {
                return PickUpResult.Failed;
            }

            if (item is DressItem dress)
            {
                EquipSlot slot = dress.ToSlot();
                if (GetEquipped(slot) == null)
                {
                    BeginMoveToBag(item);
                    _equipped[(int)slot] = item;
                    item.SetLocation(PickableItem.ItemLocation.Equipped);
                    return PickUpResult.EquippedDirectly;
                }
            }

            if (HasHandItem)
            {
                // 手上只允许拿一个 piece（需求约定）。拿不下就不动地图上的东西
                return PickUpResult.Failed;
            }

            BeginMoveToBag(item);
            HandItem = item;
            return PickUpResult.InHand;
        }

        /// <summary>
        /// 把手上的物品放进背包格子（走"试放"）。放不下（越界/被占）则什么都不做。
        /// </summary>
        /// <param name="origin">形状里 (0,0) 那一格（也就是鼠标咬住的那格）落在背包的位置</param>
        public bool PlaceHandAt(PickableItem item, Vector2Int origin)
        {
            if (item == null || item != HandItem)
            {
                return false;
            }

            if (!TryPlace(item, origin, item))
            {
                // 放不下：什么都不做，东西继续留在手上（别把它的旧位置弄丢）
                return false;
            }

            HandItem = null;
            return true;
        }

        /// <summary>
        /// 把背包里的某件物品（已放置的）拿到手上，用于在格子间挪位置。
        /// 会顺手处理"手上原本还拿着东西"的情况。
        /// </summary>
        public bool TakeToHand(PickableItem item)
        {
            if (item == null || HandItem == item)
            {
                return false;
            }

            if (!TakeFromGrid(item))
            {
                return false;
            }

            ReturnToHand(item);
            return true;
        }

        /// <summary>顺时针旋转手上的物品 90 度（绕抓取格转，所以鼠标咬点不会跑）</summary>
        public void RotateHand()
        {
            if (HandItem == null || HandItem.shape == null)
            {
                return;
            }

            HandItem.shape.RotateInPlace();
        }

        /// <summary>让某件物品拿在手上（内部用，不做 CanPlace 判断）</summary>
        private void ReturnToHand(PickableItem item)
        {
            if (item == null)
            {
                return;
            }

            if (HandItem != null && HandItem != item)
            {
                // 手上已经有一件了：先找地方放，放不下就丢到玩家脚边回地图，避免卡住
                if (!TryPlace(HandItem, out _))
                {
                    ReturnToWorld(HandItem, EvaluateDropPosition());
                }
            }

            BeginMoveToBag(item);
            HandItem = item;
        }

        #endregion

        #region 放置 / 取下

        /// <summary>把物品放到指定位置（抓取格落在 origin）。旋转态已经在物品自己的 shape 里</summary>
        public bool TryPlace(PickableItem item, Vector2Int origin)
        {
            return TryPlace(item, origin, item);
        }

        /// <summary>
        /// 把物品放到指定位置。
        /// </summary>
        /// <param name="ignore">
        /// 算占格时要忽略的物品。默认忽略自己 —— 把已经在背包里的东西挪一格时，
        /// 不排除自身就会被自己原来的占格挡住。第一次放置时忽略一个不在背包里的东西也没有副作用。
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

            MoveToBagRoot(item);
            item.SetLocation(PickableItem.ItemLocation.InBag);
            return true;
        }

        /// <summary>把物品放到第一个能放下的位置。手上的东西放不下时用它兜底</summary>
        public bool TryPlace(PickableItem item, out Vector2Int origin)
        {
            origin = default;
            if (item == null || !TryFindFirstFit(item, out origin))
            {
                return false;
            }

            return TryPlace(item, origin, item);
        }

        /// <summary>把物品从网格里摘掉（不改变 Location，也不挪 transform）。返回原本是否在网格里</summary>
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

        /// <summary>把物品从背包（网格或手）里摘掉</summary>
        public bool RemoveFromBag(PickableItem item)
        {
            bool removed = TakeFromGrid(item);
            if (HandItem == item)
            {
                HandItem = null;
                removed = true;
            }

            return removed;
        }

        #endregion

        #region 装备槽

        /// <summary>
        /// 尝试把饰品装到对应槽位。槽里原本有东西时会换下来并交回手上（需求：原饰品回到手上）。
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

            // 先把它从网格里摘掉（右键使用的情况它还在格子里）
            RemoveFromBag(item);

            if (current != null && current != item)
            {
                replaced = current;
                _equipped[(int)slot] = null;
                ReturnToHand(current);
            }

            BeginMoveToBag(item);
            _equipped[(int)slot] = item;
            item.SetLocation(PickableItem.ItemLocation.Equipped);
            return true;
        }

        /// <summary>把槽位里的饰品脱下来拿在手上</summary>
        public bool TryUnequip(EquipSlot slot)
        {
            int index = (int)slot;
            PickableItem item = _equipped[index];
            if (item == null)
            {
                return false;
            }

            _equipped[index] = null;
            ReturnToHand(item);
            return true;
        }

        #endregion

        #region 丢弃

        /// <summary>把手上的物品丢回地图（点背包外面）</summary>
        public bool DropHandToWorld(Vector3 worldPos)
        {
            if (HandItem == null)
            {
                return false;
            }

            PickableItem item = HandItem;
            HandItem = null;
            ReturnToWorld(item, worldPos);
            return true;
        }

        /// <summary>
        /// 计算"丢在玩家脚边"的落点：玩家前方一点，再往下打射线贴地。
        /// 射线打不到（比如脚下没碰撞体）就用原高度，至少不会掉到地底。
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
            Vector3 target = transform.position + flatForward * _dropForwardDistance;

            if (_dropRayLength > 0f
                && Physics.Raycast(target + Vector3.up, Vector3.down, out RaycastHit hit, _dropRayLength))
            {
                return hit.point + Vector3.up * 0.1f;
            }

            return target;
        }

        /// <summary>把物品放回地图。回地图后剩余营养等状态原样保留（本体从没被销毁过）</summary>
        public bool ReturnToWorld(PickableItem item, Vector3 worldPos)
        {
            if (item == null)
            {
                return false;
            }

            RemoveFromBag(item);

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
        /// 在背包里右键使用某个物品。
        /// - 子类通过 <see cref="PickableItem.UseInBag"/> 施加效果；
        /// - 返回 true 表示"这件事到此为止"，由这里把它从背包摘掉并销毁（食物被吃掉之类）；
        /// - 返回 false 表示"效果做了，但东西还在"，这里只把它从网格摘掉、保留在手上（换装下来的旧饰品走这条路）。
        /// </summary>
        public bool UseItem(PickableItem item, PlayerBasic player)
        {
            if (item == null || !item.IsUseAble)
            {
                return false;
            }

            if (player == null)
            {
                player = GetComponent<PlayerBasic>();
            }

            bool consumed = item.UseInBag(player);

            if (!consumed)
            {
                // 还在的东西统一交回手上，让玩家自己决定放进格子还是丢掉
                TakeFromGrid(item);
                if (HandItem != item)
                {
                    ReturnToHand(item);
                }
            }
            else
            {
                // 用完了：先摘数据再销毁，否则会留下指向已销毁对象的占格记录
                if (!RemoveFromBag(item))
                {
                    TakeFromGrid(item);
                }

                ItemManagerSafeUnregister(item);
                Destroy(item.gameObject);
            }

            return true;
        }

        #endregion

        #region 内部：状态搬运

        /// <summary>把地图上的物品收进背包：改状态 + 挪到暂存区隐藏</summary>
        private void BeginMoveToBag(PickableItem item)
        {
            item.SetLocation(PickableItem.ItemLocation.InBag);
            MoveToBagRoot(item);
        }

        /// <summary>把物品本体挂到暂存区并隐藏。不销毁，所以运行时状态全部保留</summary>
        private void MoveToBagRoot(PickableItem item)
        {
            Transform staging = EnsureStagingRoot();
            item.transform.SetParent(staging, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
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
            // 玩家没了，暂存区里的物品也跟着销毁；这里只是清掉悬挂的引用，避免空引用报错
            _placements.Clear();
            for (int i = 0; i < _equipped.Length; i++)
            {
                _equipped[i] = null;
            }

            HandItem = null;
        }

        /// <summary>ItemManager 可能不存在（例如 TestNight），这里统一做空判断</summary>
        private static void ItemManagerSafeUnregister(PickableItem item)
        {
            if (Core.ItemManager.Instance != null)
            {
                Core.ItemManager.Instance.Unregister(item);
            }
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
