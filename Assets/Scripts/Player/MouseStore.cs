using System;
using System.Collections.Generic;
using NUnit.Framework;
using TapTapGameJam.Items;
using UnityEngine;

namespace TapTapGameJam.Player
{
    /// <summary>
    /// 商店后端：只存"货架 + 价格 + 买没买下"，以及"扣钱买下"和"把货取出去"两件事。
    ///
    /// 【和 PlayerBag / PlayerRepo 一致】
    /// - 货的本体在开张前就挪到暂存区隐藏，不销毁重建，运行时状态全保留；
    /// - 不管"界面上正被拖着哪一件"，也不引用 UI。
    ///
    /// 【分页】和仓库一样按 <see cref="ItemCategory"/> 分三页，但这里三页是**三张独立的表**
    /// （<see cref="Goods"/> 里带了价格，不能像仓库那样只看物品自己）。
    /// 每张表里的货，类别必须和表一致，Awake 里会断言。
    ///
    /// 【买卖流程】没买下的货不许从货架上拿走（<see cref="TryTakeOut"/> 会拦）：
    /// 先在界面上买下（<see cref="TryBuy"/> 扣金币 + 标记），再拖出去。
    /// 拖出去即售罄（那一格从此空着，每件只能买一次，跨天不重置）。
    /// </summary>
    [DisallowMultipleComponent]
    public class MouseStore : MonoBehaviour
    {
        #region 商品 / 配置

        /// <summary>一件商品：货 + 价格 + 有没有被买下</summary>
        [Serializable]
        public class Goods
        {
            [Tooltip("商品本体（场景里的 PickableItem 实例）")]
            public PickableItem item;

            [Tooltip("价格（金币）。0 = 白送")]
            [Min(0)] public int price = 1;

            /// <summary>买下之后才允许从货架上拿走。运行期状态，不序列化</summary>
            [NonSerialized] public bool purchased;
        }

        [Header("货架（item 的 Category 必须和所在表一致）")]
        [SerializeField] private List<Goods> _foodGoods = new List<Goods>();
        [SerializeField] private List<Goods> _toolGoods = new List<Goods>();
        [SerializeField] private List<Goods> _dressGoods = new List<Goods>();

        [Header("货架尺寸（每一页）")]
        [Tooltip("每页的列数。商店界面的格子按它排，改这里要同步面板上的三个 GridRoot 摆位")]
        [SerializeField, Min(1)] private int _width = 4;
        [Tooltip("每页的行数")]
        [SerializeField, Min(1)] private int _height = 5;

        public int Width => _width;
        public int Height => _height;

        /// <summary>**每一页**能摆多少件货</summary>
        public int CellCount => _width * _height;

        [Header("暂存区")]
        [Tooltip("开张前，货的本体被挪到这个相对位置隐藏起来。放得足够远，避免被相机或物理碰到")]
        [SerializeField] private Vector3 _stagingOffset = new Vector3(0f, 30000f, 0f);

        [Header("组件")]
        [Tooltip("扣金币用。留空则从自己身上找 PlayerResources")]
        [SerializeField] private PlayerResources _resources;

        #endregion

        #region 运行时数据

        /// <summary>页 → 货架</summary>
        private readonly Dictionary<ItemCategory, List<Goods>> _shelves =
            new Dictionary<ItemCategory, List<Goods>>();

        private Transform _stagingRoot;

        /// <summary>还摆在货架上的货（售罄的不算）</summary>
        public IEnumerable<PickableItem> Items
        {
            get
            {
                foreach (ItemCategory page in ItemCategories.All)
                {
                    List<Goods> shelf = _shelves[page];
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        if (shelf[i].item != null)
                        {
                            yield return shelf[i].item;
                        }
                    }
                }
            }
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            _shelves.Clear();
            _shelves[ItemCategory.Food] = _foodGoods;
            _shelves[ItemCategory.Tool] = _toolGoods;
            _shelves[ItemCategory.Dress] = _dressGoods;

            if (_resources == null)
            {
                _resources = GetComponent<PlayerResources>();
            }

            Assert.IsTrue(_resources != null, "MouseStore：没连 PlayerResources（扣钱要用）");

            // 货架自检 + 把货藏进暂存区
            foreach (ItemCategory page in ItemCategories.All)
            {
                List<Goods> shelf = _shelves[page];
                Assert.IsTrue(shelf.Count <= CellCount,
                    $"MouseStore：{page} 页摆了 {shelf.Count} 件货，但一页只有 {CellCount} 个格子 —— 多出来的在界面上看不见");

                for (int i = 0; i < shelf.Count; i++)
                {
                    Goods goods = shelf[i];
                    Assert.IsTrue(goods.item != null, $"MouseStore：{page} 页第 {i} 个货位没连商品本体");
                    Assert.IsTrue(goods.item.Category == page,
                        $"MouseStore：{goods.item.name} 是 {goods.item.Category} 类，却摆在 {page} 页的货架上");

                    MoveToStoreStorage(goods.item);
                }
            }
        }

        #endregion

        #region 只读查询

        /// <summary>某一页的货架（顺序 = 界面上的格子顺序）</summary>
        public List<Goods> GoodsOf(ItemCategory page)
        {
            return _shelves[page];
        }

        /// <summary>某一页第 <paramref name="index"/> 个货位（越界 / 没这件返回 null）</summary>
        public Goods GoodsAt(ItemCategory page, int index)
        {
            List<Goods> shelf = _shelves[page];
            if (index < 0 || index >= shelf.Count)
            {
                return null;
            }

            return shelf[index];
        }

        #endregion

        #region 买 / 取

        /// <summary>
        /// 买下：扣金币 + 标记已购买。
        /// 钱不够 / 已经买过 / 已经售罄 → 返回 false，**什么都不改**（前端表现以后再做）。
        /// </summary>
        public bool TryBuy(Goods goods)
        {
            if (goods == null || goods.item == null || goods.purchased)
            {
                return false;
            }

            // 0 块的白送品不用走扣钱（TrySpendCoins 对 amount <= 0 也会拒绝）
            if (goods.price > 0 && !_resources.TrySpendCoins(goods.price))
            {
                return false;
            }

            goods.purchased = true;
            return true;
        }

        /// <summary>把货从货架上取走（售罄）。本体返回世界由 UI 侧按统一规则处理</summary>
        public bool TryTakeOut(Goods goods, out PickableItem item)
        {
            item = null;

            if (goods == null || goods.item == null || !goods.purchased)
            {
                return false;
            }

            item = goods.item;
            goods.item = null; // 售罄
            return true;
        }

        #endregion

        #region 内部：状态搬运

        /// <summary>把货的本体挂到暂存区并隐藏。不销毁，所以运行时状态全部保留</summary>
        private void MoveToStoreStorage(PickableItem item)
        {
            Transform staging = EnsureStagingRoot();
            item.transform.SetParent(staging, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;

            // 藏起来之前必须主动从交互系统注销（和 PlayerBag / PlayerRepo 同理）：
            // 光靠 SetActive(false) 拦不住 InteractionManager 的候选列表，
            // 而暂存区是玩家的子物体、距离恒为 0，会一直霸住"最近交互目标"
            item.ClearAllRegister();

            item.SetLocation(PickableItem.ItemLocation.InStore);
            item.gameObject.SetActive(false);
        }

        private Transform EnsureStagingRoot()
        {
            if (_stagingRoot != null)
            {
                return _stagingRoot;
            }

            GameObject root = new GameObject("~StoreItemStaging");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = _stagingOffset;
            _stagingRoot = root.transform;
            return _stagingRoot;
        }

        #endregion
    }
}
