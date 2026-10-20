using System.Collections.Generic;
using TapTapGameJam.Items;
using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 物品登记处：谁存在、放在哪、还剩多少。
    /// 只提供只读查询，把"怎么用这些信息做决策"留给调用方（例如蟑螂的 decide）。
    ///
    /// 注意：背包的格子容量、摆放规则、装备槽都在 <see cref="TapTapGameJam.Items.PlayerBag"/> 上，
    /// 这里只管"场景里存在哪些物品"。物品进背包时不会被注销（只是 GameObject 被隐藏），
    /// 真正被销毁时才会走 Unregister。
    /// </summary>
    public class ItemManager : MonoBehaviour
    {
        private static ItemManager _instance;

        /// <summary>
        /// 全局唯一入口。
        /// 物品在 Start 里就要注册自己，而 Start 的执行顺序不保证晚于 ItemManager.Awake，
        /// 所以这里做一个惰性查找兜底，避免出现"第一个物品注册不进去"的偶发问题。
        /// </summary>
        public static ItemManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ItemManager>();
                }

                return _instance;
            }
        }

        // 场上所有还活着的物品（含已进背包、已装备的）
        public List<PickableItem> PickableItemList { get; private set; } = new List<PickableItem>();

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #region 登记

        public void Register(PickableItem item)
        {
            if (item == null || PickableItemList.Contains(item))
            {
                return;
            }

            PickableItemList.Add(item);
            // TODO: 后续背包 UI / 蟑螂 AI 需要知道物品列表变了，可以在这里发事件广播
        }

        public void Unregister(PickableItem item)
        {
            if (item == null)
            {
                return;
            }

            PickableItemList.Remove(item);
        }

        #endregion

        #region 只读查询

        public IReadOnlyList<PickableItem> GetItemsOnMap()
        {
            List<PickableItem> result = new List<PickableItem>();

            foreach (PickableItem item in PickableItemList)
            {
                if (item != null && item.IsOnMap)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// 取所有已经属于玩家背包的物品（包含"手上正拿着"的那件）。
        /// 具体的格子归属由 PlayerBag 决定。
        /// </summary>
        public IReadOnlyList<PickableItem> GetItemsInBag()
        {
            List<PickableItem> result = new List<PickableItem>();

            foreach (PickableItem item in PickableItemList)
            {
                if (item != null && item.Location == PickableItem.ItemLocation.InBag)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// 取地图上所有食物。
        /// </summary>
        public IReadOnlyList<FoodItem> GetFoodOnMap()
        {
            List<FoodItem> result = new List<FoodItem>();
            GetFoodOnMap(result);
            return result;
        }

        /// <summary>
        /// 取地图上所有食物，<b>写进调用方给的 list</b>（会先 Clear），不自己 new。
        ///
        /// 给蟑螂用：它每个 FixedUpdate 都要查两次（决策 + 取食），
        /// 而「群颚破壁」之后场上每面墙 / 每张桌子都是一个 FoodItem，
        /// 列表会从个位数涨到几百 —— 每帧 new 一个几百长度的 List 纯属浪费。
        /// </summary>
        public void GetFoodOnMap(List<FoodItem> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (int i = 0; i < PickableItemList.Count; i++)
            {
                PickableItem item = PickableItemList[i];
                if (item != null && item is FoodItem food && item.IsOnMap)
                {
                    results.Add(food);
                }
            }
        }

        #endregion
    }
}
