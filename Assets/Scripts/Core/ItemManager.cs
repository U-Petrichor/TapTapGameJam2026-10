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

        #region 拾取

        /// <summary>
        /// 【已废弃】把地图上的物品粗略收进背包。
        /// 摆放算法现在归 PlayerBag 管，正常路径请调用 <see cref="TapTapGameJam.Items.PlayerBag.PickUp"/>。
        /// 这里只作为"场景里没有 PlayerBag"时的兜底，不做任何格子检查。
        /// </summary>
        [System.Obsolete("拾取请走 PlayerBag.PickUp，本方法只作无 PlayerBag 场景的兜底")]
        public bool TryAddToBag(PickableItem item)
        {
            if (item == null || !item.IsOnMap)
            {
                return false;
            }

            item.SetLocation(PickableItem.ItemLocation.InBag);
            item.gameObject.SetActive(false); // 进背包后不再出现在世界里
            return true;
        }

        /// <summary>
        /// 把物品从背包放回地图（丢弃）。真正的实现在 PlayerBag.ReturnToWorld，
        /// 这里保留一个入口是为了兼容还没改完的调用方。
        /// </summary>
        [System.Obsolete("请使用 PlayerBag.ReturnToWorld")]
        public bool TryDropToMap(PickableItem item, Vector3 worldPos)
        {
            if (item == null || item.IsOnMap)
            {
                return false;
            }

            item.transform.position = worldPos;
            item.SetLocation(PickableItem.ItemLocation.OnMap);
            item.gameObject.SetActive(true);
            return true;
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
            foreach (PickableItem item in PickableItemList)
            {
                if (item != null && item is FoodItem food && item.IsOnMap)
                {
                    result.Add(food);
                }
            }

            return result;
        }

        #endregion
    }
}
