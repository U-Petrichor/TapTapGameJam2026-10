using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Items;
using UnityEngine;

namespace TapTapGameJam.Player
{
    /// <summary>
    /// 玩家仓库：只当"后端"用，**一格一件**（不像背包要拼形状）。
    ///
    /// 【和 PlayerBag 一致的两条约定】
    /// - 本体进仓库时挪到暂存区隐藏，不销毁重建，运行时状态全保留；
    /// - 不管"界面上正被拖着哪一件"，也不引用 UI。
    ///
    /// 【格子】固定 Width * Height 个格子，**允许空洞**：落在第几格就是第几格，
    /// 中间取走一件不会让后面的件整体前移（前移会让界面上的块和格子对不上）。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerRepo : MonoBehaviour
    {
        #region 配置

        [Header("仓库尺寸")]
        [Tooltip("列数。仓库界面的格数直接读这里，面板上不再另写一套")]
        [SerializeField, Min(1)] private int _width = 5;
        [Tooltip("行数")]
        [SerializeField, Min(1)] private int _height = 6;

        [Header("暂存区")]
        [Tooltip("进仓库后，物品本体被挪到这个相对位置隐藏起来。放得足够远，避免被相机或物理碰到")]
        [SerializeField] private Vector3 _stagingOffset = new Vector3(0f, 20000f, 0f);

        public int Width => _width;
        public int Height => _height;
        public int CellCount => _width * _height;

        #endregion

        #region 运行时数据

        private PickableItem[] _cells;
        private Transform _stagingRoot;

        /// <summary>仓库里的所有物品（跳过空格子）</summary>
        public IEnumerable<PickableItem> Items
        {
            get
            {
                PickableItem[] cells = EnsureCells();
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] != null)
                    {
                        yield return cells[i];
                    }
                }
            }
        }

        #endregion

        #region 只读查询

        public bool InBounds(int index)
        {
            return index >= 0 && index < CellCount;
        }

        public PickableItem ItemAt(int index)
        {
            Assert.IsTrue(InBounds(index), $"PlayerRepo：格子 {index} 越界（一共 {CellCount} 格）");
            return EnsureCells()[index];
        }

        public bool IsCellOccupied(int index)
        {
            return ItemAt(index) != null;
        }

        /// <summary>某个物品在哪个格子（不在仓库返回 false）</summary>
        public bool TryGetCellIndex(PickableItem item, out int index)
        {
            index = -1;
            if (item == null)
            {
                return false;
            }

            PickableItem[] cells = EnsureCells();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == item)
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        /// <summary>还空着的第一个格子（自动入库 / 兜底用）</summary>
        public bool TryFindFirstFreeCell(out int index)
        {
            PickableItem[] cells = EnsureCells();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        #endregion

        #region 放入 / 取出

        /// <summary>把物品放进指定格子（必须是空格子）</summary>
        public bool PutItem(int index, PickableItem item)
        {
            Assert.IsTrue(item != null, "PlayerRepo.PutItem：item 是 null");
            Assert.IsTrue(InBounds(index), $"PlayerRepo.PutItem：格子 {index} 越界（一共 {CellCount} 格）");

            PickableItem[] cells = EnsureCells();
            if (cells[index] != null)
            {
                return false;
            }

            cells[index] = item;
            MoveToRepoStorage(item);
            item.SetLocation(PickableItem.ItemLocation.InRepo);
            return true;
        }

        /// <summary>放进第一个空格子</summary>
        public bool PutItem(PickableItem item, out int index)
        {
            index = -1;

            if (item == null || !TryFindFirstFreeCell(out int free))
            {
                return false;
            }

            if (!PutItem(free, item))
            {
                return false;
            }

            index = free;
            return true;
        }

        /// <summary>只摘登记：物品已经离开仓库（本体由 UI 侧按统一规则送回世界）</summary>
        public bool TakeFromRepo(PickableItem item)
        {
            if (!TryGetCellIndex(item, out int index))
            {
                return false;
            }

            EnsureCells()[index] = null;
            return true;
        }

        /// <summary>把物品从仓库直接放进背包格子（后端直连用；界面拖放走的是 PlayerBag 的判定）</summary>
        public bool MoveItemToBag(PickableItem item, PlayerBag bag, Vector2Int origin)
        {
            Assert.IsTrue(bag != null, "PlayerRepo.MoveItemToBag：bag 是 null");

            if (item == null
                || !TryGetCellIndex(item, out int index)
                || !bag.CanPlace(item, origin, item))
            {
                return false;
            }

            // 先问过 bag 再摘登记：不然 TryPlace 失败会把物品滞留在"仓库已经没了、背包也没进"的状态
            if (!bag.TryPlace(item, origin))
            {
                return false;
            }

            EnsureCells()[index] = null;
            return true;
        }

        #endregion

        #region 内部：状态搬运

        /// <summary>把物体本体挂到暂存区并隐藏。不销毁，所以运行时状态全部保留</summary>
        private void MoveToRepoStorage(PickableItem item)
        {
            Transform staging = EnsureStagingRoot();
            item.transform.SetParent(staging, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;

            // 藏起来之前必须主动从交互系统注销（和 PlayerBag.MoveToBagStorage 同理）：
            // Unity 不会为"被禁用的碰撞体"触发 OnTriggerExit，所以光靠 SetActive(false)
            // 这件物品会一直留在 PlayerInteractionManager 的候选列表里；而暂存区是玩家的子物体，
            // 那个管理器又刻意只比水平距离 —— 它的距离恒为 0，于是永远被选成"最近的交互目标"、
            // 把焦点霸住，地上别的东西再也轮不到。
            item.ClearAllRegister();

            item.gameObject.SetActive(false);
        }

        /// <summary>懒创建暂存区。做成 PlayerRepo 的子物体，玩家销毁时一起销毁，不留孤儿</summary>
        private Transform EnsureStagingRoot()
        {
            if (_stagingRoot != null)
            {
                return _stagingRoot;
            }

            GameObject root = new GameObject("~RepoItemStaging");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = _stagingOffset;
            _stagingRoot = root.transform;
            return _stagingRoot;
        }

        private PickableItem[] EnsureCells()
        {
            if (_cells == null)
            {
                _cells = new PickableItem[CellCount];
            }

            Assert.AreEqual(CellCount, _cells.Length,
                "PlayerRepo：Width/Height 在运行时被改了，格子数组和 CellCount 对不上");
            return _cells;
        }

        private void OnDestroy()
        {
            // 玩家没了，暂存区里的物品跟着销毁；这里只清引用，避免空引用报错
            _cells = null;
        }

        #endregion
    }
}
