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
    /// 【分页 / 货源】和仓库一样按 <see cref="ItemCategory"/> 分三页，但货源不是手摆的：
    /// 由 <see cref="RefreshStore"/> 从 <see cref="PoolEntry"/> 商品池里随机生成
    /// （每页 3~6 件、价格在各自范围内随机、不去重）。一件货属于哪一页由它自己的 Category 决定。
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

        /// <summary>商品池里的一条：模板 + 售价范围（含两端）</summary>
        [Serializable]
        public class PoolEntry
        {
            [Tooltip("商品模板（预制体 / 场景里的模板物体都行；刷新时会 Instantiate 一份）")]
            public PickableItem item;

            [Tooltip("售价范围（含两端），刷新时在这个区间里随机")]
            public Vector2Int priceRange = new Vector2Int(1, 3);
        }

        [Header("商品随机列表（随机刷新的候选，不去重）")]
        [Tooltip("候选商品 + 各自的售价范围。刷新时会 Instantiate 一份，所以填预制体或场景里的模板物体都行")]
        [SerializeField] private List<PoolEntry> _pool = new List<PoolEntry>();

        [Tooltip("随机刷新时每页最少摆几件")]
        [SerializeField, Min(0)] private int _minPerPage = 3;

        [Tooltip("随机刷新时每页最多摆几件（不能超过一页的格子数）")]
        [SerializeField, Min(0)] private int _maxPerPage = 6;

        [Header("货架尺寸（每一页）")]
        [Tooltip("每页的列数。商店界面的格子按它排，改这里要同步面板上的三个 GridRoot 摆位")]
        [SerializeField, Min(1)] private int _width = 3;
        [Tooltip("每页的行数")]
        [SerializeField, Min(1)] private int _height = 2;

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
            foreach (ItemCategory page in ItemCategories.All)
            {
                _shelves[page] = new List<Goods>();
            }

            if (_resources == null)
            {
                _resources = GetComponent<PlayerResources>();
            }

            Assert.IsTrue(_resources != null, "MouseStore：没连 PlayerResources（扣钱要用）");

            // 配置自检
            Assert.IsTrue(_minPerPage <= _maxPerPage,
                $"MouseStore：每页最少 {_minPerPage} 件 > 最多 {_maxPerPage} 件");
            Assert.IsTrue(_maxPerPage <= CellCount,
                $"MouseStore：每页最多 {_maxPerPage} 件，但一页只有 {CellCount} 个格子 —— 多出来的在界面上看不见");

            for (int i = 0; i < _pool.Count; i++)
            {
                Assert.IsTrue(_pool[i].item != null, $"MouseStore：商品池第 {i} 条没连商品模板");
                Assert.IsTrue(_pool[i].priceRange.x <= _pool[i].priceRange.y,
                    $"MouseStore：商品池第 {i} 条（{_pool[i].item.name}）的售价范围写反了");
            }

            // 开张：先随机刷一版货（想每天换一批就再调 RefreshStore）
            RefreshStore();
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

        #region 随机刷新

        /// <summary>
        /// 随机刷新整店：三页各自清空，再从商品池里按类别随机抽 3~6 件
        /// （数量由 _minPerPage / _maxPerPage 决定），每件 Instantiate 一份副本、
        /// 价格在各条自己的范围内随机，**不去重**（同一件商品可以同时出现好几次）。
        /// 某一页在池子里没有候选就空着。
        ///
        /// 注意：只在店关着的时候调 —— 开着调的话界面上的块会指向被销毁的旧货。
        /// </summary>
        public void RefreshStore()
        {
            ClearShelves();

            foreach (ItemCategory page in ItemCategories.All)
            {
                List<PoolEntry> candidates = CollectCandidates(page);
                if (candidates.Count == 0)
                {
                    continue; // 这一页没配候选：空着
                }

                List<Goods> shelf = _shelves[page];
                int count = UnityEngine.Random.Range(_minPerPage, _maxPerPage + 1);

                for (int i = 0; i < count; i++)
                {
                    PoolEntry entry = candidates[UnityEngine.Random.Range(0, candidates.Count)];

                    // 每次都 Instantiate：这样"不去重"也不会让同一件被摆两次（一件物品只能有一个块）
                    PickableItem clone = Instantiate(entry.item);
                    MoveToStoreStorage(clone);

                    Vector2Int range = entry.priceRange;
                    shelf.Add(new Goods
                    {
                        item = clone,
                        price = Mathf.Max(0, UnityEngine.Random.Range(range.x, range.y + 1)),
                        purchased = false,
                    });
                }
            }
        }

        /// <summary>清空三页货架，并把上一轮的副本销毁掉</summary>
        private void ClearShelves()
        {
            foreach (ItemCategory page in ItemCategories.All)
            {
                List<Goods> shelf = _shelves[page];
                for (int i = 0; i < shelf.Count; i++)
                {
                    if (shelf[i].item != null)
                    {
                        Destroy(shelf[i].item.gameObject);
                    }
                }

                shelf.Clear();
            }
        }

        /// <summary>池子里属于这一页的候选（按商品自己的 Category 归类）</summary>
        private List<PoolEntry> CollectCandidates(ItemCategory page)
        {
            List<PoolEntry> result = new List<PoolEntry>();
            for (int i = 0; i < _pool.Count; i++)
            {
                PoolEntry entry = _pool[i];
                if (entry.item != null && entry.item.Category == page)
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        #endregion

        #region 随机出货（黑盒等）

        /// <summary>
        /// 从商品池里<b>等概率</b>抽一个模板，按稀有度分档：
        /// <paramref name="rare"/> = true 抽 <see cref="PickableItem.SpecialLevel"/> ≥ 1 的那一档，
        /// false 抽 0 那一档。这一档为空时返回 false，<b>不会</b>自动跨档（由调用方决定要不要退一步）。
        ///
        /// 抽的是<b>模板</b>，不是货架上的货：调用方自己 Instantiate 一份，
        /// 所以黑盒开出来的东西和商店里卖的是互相独立的实例。
        /// 也不去重：池子里有几条同样的模板，抽中它的概率就按条数翻倍。
        /// </summary>
        public bool TryPickRandomLoot(bool rare, out PickableItem item)
        {
            item = null;

            // 两遍扫描：先数出这一档有多少条，再按下标取。
            // 池子只有个位数，比每次都 new 一个候选列表便宜。
            int count = 0;
            for (int i = 0; i < _pool.Count; i++)
            {
                PoolEntry entry = _pool[i];
                if (entry != null && entry.item != null && LootRoll.IsRare(entry.item.SpecialLevel) == rare)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return false;
            }

            int pick = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < _pool.Count; i++)
            {
                PoolEntry entry = _pool[i];
                if (entry == null || entry.item == null || LootRoll.IsRare(entry.item.SpecialLevel) != rare)
                {
                    continue;
                }

                if (pick-- == 0)
                {
                    item = entry.item;
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// 把一件东西放回货架的第 <paramref name="index"/> 个货位（玩家把东西还回商店）。
        /// 那个货位必须空着；放进去就算**已购买**（自己还回去的东西当然能再拿走）。
        /// 本体挪进暂存区隐藏，和商店本来进的货一样处理。
        /// </summary>
        public bool TryPutItem(ItemCategory page, int index, PickableItem item)
        {
            if (item == null)
            {
                return false;
            }

            Assert.IsTrue(item.Category == page,
                $"MouseStore.TryPutItem：{item.name} 是 {item.Category} 类，却往 {page} 页放");

            if (index < 0 || index >= CellCount)
            {
                return false;
            }

            List<Goods> shelf = _shelves[page];

            // 已经被占着就不收（不做交换）
            if (index < shelf.Count && shelf[index].item != null)
            {
                return false;
            }

            // 货位可能在表尾之外：先补空条目，让下标保持稳定
            while (shelf.Count <= index)
            {
                shelf.Add(new Goods { item = null, price = 0, purchased = false });
            }

            Goods goods = shelf[index];
            goods.item = item;
            goods.price = 0;
            goods.purchased = true;

            MoveToStoreStorage(item);
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
