using System.Collections.Generic;
using TapTapGameJam.Items;
using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 物品登记处：谁存在、放在哪、还剩多少。
    /// 只提供只读查询，把"怎么用这些信息做决策"留给调用方（例如蟑螂的 decide）。
    /// </summary>
    public class ItemManager : MonoBehaviour
    {
        private static ItemManager _instance;

        /// <summary>
        /// 全局唯一入口。
        /// 物品在 OnEnable 里就要注册自己，而 OnEnable 的执行顺序不保证晚于 ItemManager.Awake，
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

        /// <summary>
        /// 背包的格子容量，先留出来，具体摆放算法未实现
        /// </summary>
        [Header("背包配置")]
        public int bagWidth = 6;
        public int bagHeight = 4;

        // 场上所有还活着的物品（含已进背包的）
        public List<PickableItem> PickableItems { get; private set; }  = new List<PickableItem>();


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
            if (item == null || PickableItems.Contains(item))
            {
                return;
            }

            PickableItems.Add(item);
            Debug.Log("Registered PickableItem: " + item.name);
            // TODO: 后续背包 UI / 蟑螂 AI 需要知道物品列表变了，可以在这里发事件广播
        }

        public void Unregister(PickableItem item)
        {
            if (item == null)
            {
                return;
            }

            PickableItems.Remove(item);
        }

        #endregion

        #region 拾取
        // TODO: 拾取整块都交给背包而不是ItemManager

        /// <summary>
        /// 尝试把地图上的物品收进背包。
        /// 摆放算法（俄罗斯方块式装箱）按你的要求没有实现，这里只负责状态切换。
        /// </summary>
        /// <returns>是否成功放进背包</returns>
        public bool TryAddToBag(PickableItem item)
        {
            if (item == null || !item.IsOnMap)
            {
                return false;
            }

            // TODO: 检查 _bagWidth x _bagHeight 里能不能摆下 item.itemShape，摆不下返回 false

            item.SetLocation(PickableItem.ItemLocation.InBag);
            item.gameObject.SetActive(false); // 进背包后不再出现在世界里
            return true;
        }

        /// <summary>
        /// 把物品从背包放回地图（使用时消耗、或玩家丢弃）
        /// </summary>
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

            foreach (PickableItem item in PickableItems)
            {
                if (item != null && item.IsOnMap)
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
            foreach (PickableItem item in PickableItems)
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
