using System.Collections.Generic;
using TapTapGameJam.Core;
using TapTapGameJam.Items;
using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 蟑螂个体。
    /// 这里只做两件事：拿到"地图信息"和"食物信息"，以及执行移动/转向。
    /// 具体怎么用这些信息做决策（decide）留空，由你来实现。
    /// </summary>
    public class Cockroach
    {
        private float _forwardAngle;
        private float _targetAngle;

        /// <summary>
        /// 蟑螂当前所在的世界坐标。只读，写位置请改 transform（移动逻辑里做）
        /// </summary>
        public Vector3 WorldPosition { get; set; }

        private GridManager Grid => GridManager.Instance;
        private ItemManager Items => ItemManager.Instance;


        #region 食物信息

        /// <summary>
        /// 地图上所有还有营养的食物。每次调用返回新的 List，可以安全地边遍历边吃
        /// </summary>
        public IReadOnlyList<FoodItem> GetFoodSources()
        {
            if (Items == null)
            {
                return System.Array.Empty<FoodItem>();
            }

            return Items.GetFoodOnMap();
        }


        #endregion

        /// <summary>
        /// 决策核心。地图信息走 IsBlocked / RayCast，食物信息走 GetFoodSources / GetNearestFood。
        /// </summary>
        private void Decide()
        {
            // TODO: 由你实现
        }

        private void Update()
        {
            // 执行移动
            // 转向

            // TODO: 由你实现。大致形状：
            //   decide(); 算出 targetAngle
            //   forwardAngle = Mathf.MoveTowardsAngle(forwardAngle, targetAngle, turnSpeed * dt);
            //   然后按 forwardAngle 把 WorldPosition 往前推，并同步 CurrentCell = WorldToCell(WorldPosition)
        }
    }
}
