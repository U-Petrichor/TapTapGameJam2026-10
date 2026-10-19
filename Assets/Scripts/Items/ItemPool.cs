using System;
using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 商品池：**纯数据**的商品 / 掉落列表。商店进货、黑盒出货、夜晚刷食物都从这里取，三处挂同一个资产。
    ///
    /// 【为什么抽成资产】
    /// 这份列表原来挂在 MouseStore 组件上（场景物体），于是：
    /// - 黑盒想"从商店的商品池里抽"，只能硬引用场景里的 MouseStore —— 而预制体里连不了场景物体，
    ///   于是黑盒预制体的这个字段长期是空的，开出来什么都没有；
    /// - 夜晚刷食物又把同一份列表在 NightManager 上抄一遍，加一件食物要改两个地方，漏一处就是"商店有、地上没有"。
    /// 抽成资产之后：加一件商品只改这一个文件，谁想用就挂它。
    ///
    /// 【一条记录】模板 + 售价范围。售价只有商店刷新会读，黑盒和夜晚生成不看价格。
    ///
    /// 【分类不看这里】一件商品属于食物 / 工具 / 装扮，由它自己的
    /// <see cref="PickableItem.Category"/> 决定（FoodItem → Food …），
    /// 这样加一件新食物不用回来改枚举，也不会出现"配置里写着 Food、实际是个 ToolItem"的错配。
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Item Pool", fileName = "ItemPool")]
    public sealed class ItemPool : ScriptableObject
    {
        /// <summary>商品池里的一条：模板 + 售价范围（含两端）</summary>
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("商品模板（预制体）。取用方会自己 Instantiate 一份")]
            public PickableItem item;

            [Tooltip("售价范围（含两端）。只有商店刷新会读它")]
            public Vector2Int priceRange = new Vector2Int(1, 3);
        }

        [Tooltip("商品池。顺序无关，抽取时等概率；同一条写两次等于概率翻倍")]
        [SerializeField] private List<Entry> _entries = new List<Entry>();

        /// <summary>池子里的全部条目（可能有空条目，读的人自己判）</summary>
        public IReadOnlyList<Entry> Entries => _entries;

        public int Count => _entries != null ? _entries.Count : 0;

        #region 按分类取

        /// <summary>
        /// 某一分类的所有候选条目（商店分页用）。
        /// 每条都带售价范围，所以商店拿它进货。空条目 / 模板为空的自跳过。
        /// </summary>
        public List<Entry> CollectByCategory(ItemCategory category)
        {
            List<Entry> result = new List<Entry>();
            if (_entries == null)
            {
                return result;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (IsUsable(entry) && entry.item.Category == category)
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>
        /// 某一分类的所有模板（只要物品本身，不要价格）。夜晚刷食物用。
        /// 和 <see cref="CollectByCategory"/> 的区别只是不把 Entry 包出去。
        /// </summary>
        public List<PickableItem> ItemsOf(ItemCategory category)
        {
            List<PickableItem> result = new List<PickableItem>();
            if (_entries == null)
            {
                return result;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (IsUsable(entry) && entry.item.Category == category)
                {
                    result.Add(entry.item);
                }
            }

            return result;
        }

        #endregion

        #region 抽取

        /// <summary>
        /// 按稀有度分档<b>等概率</b>抽一个模板：
        /// <paramref name="rare"/> = true 抽 <see cref="PickableItem.SpecialLevel"/> ≥ 1 的那一档，false 抽 0 那一档。
        /// 这一档为空时返回 false，<b>不会</b>自动跨档（要不要退一步由调用方决定）。
        ///
        /// 抽的是模板不是实例，调用方自己 Instantiate 一份。
        /// 也不去重：池子里同一条写两次，抽中它的概率就翻倍。
        /// </summary>
        public bool TryPickRandom(bool rare, out PickableItem item)
        {
            item = null;
            if (_entries == null)
            {
                return false;
            }

            // 两遍扫描：先数出这一档有多少条，再按下标取。池子只有个位数，比每次 new 一个候选表便宜
            int count = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (IsUsable(entry) && LootRoll.IsRare(entry.item.SpecialLevel) == rare)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return false;
            }

            int pick = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (!IsUsable(entry) || LootRoll.IsRare(entry.item.SpecialLevel) != rare)
                {
                    continue;
                }

                if (pick == 0)
                {
                    item = entry.item;
                    return true;
                }

                pick--;
            }

            return false;
        }

        /// <summary>不分稀有度、等概率抽一个模板</summary>
        public bool TryPickAny(out PickableItem item)
        {
            item = null;
            if (_entries == null)
            {
                return false;
            }

            int count = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (IsUsable(_entries[i]))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return false;
            }

            int pick = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!IsUsable(_entries[i]))
                {
                    continue;
                }

                if (pick == 0)
                {
                    item = _entries[i].item;
                    return true;
                }

                pick--;
            }

            return false;
        }

        #endregion

        private static bool IsUsable(Entry entry)
        {
            return entry != null && entry.item != null;
        }

        private void OnValidate()
        {
            _entries ??= new List<Entry>();

            // 售价范围写反了会让 Random.Range 抽出一个空区间，直接在编辑器里纠正
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.priceRange.x < 0)
                {
                    entry.priceRange.x = 0;
                }

                if (entry.priceRange.y < entry.priceRange.x)
                {
                    entry.priceRange.y = entry.priceRange.x;
                }
            }
        }
    }
}
