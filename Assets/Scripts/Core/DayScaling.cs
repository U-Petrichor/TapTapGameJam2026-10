using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 按天数滚雪球的数值。目前只有"食物营养"在用：
    /// 越往后天数，场上/商店里的食物越肥。
    ///
    /// 权重 = <see cref="GrowthPerDay"/> ^ 当前天数（<see cref="GamePhaseManager.DayIndex"/>，首日为 1）：
    /// Day 1 = 1.13、Day 2 = 1.28、Day 3 = 1.44、Day 7 ≈ 2.35。
    /// 这个 1.13 是照策划案「地图残余基础营养 230 → 460（7 晚）」反推的：
    /// 230 × 1.13^6 ≈ 470，7 晚总量也对得上；1.2 会在第 4 晚之后明显偏肥（D7 +50%）。
    ///
    /// 权重是**整体乘在食物的 totalNutrition 上**的（不是只乘剩余量），
    /// 所以食物看起来就是"这块更大"，蟑螂一次能搬的量没变，只是这块更经啃。
    ///
    /// 想让首日等于 1.0（Day 0 起步）就把 <see cref="CurrentDay"/> 的返回值减一，
    /// 或者在调用方自己偏移 —— 现在按需求字面实现：Day X 用 1.2^X。
    /// </summary>
    public static class DayScaling
    {
        /// <summary>每过一天，食物营养乘多少</summary>
        public const float GrowthPerDay = 1.13f;

        /// <summary>
        /// 当前天数。取不到阶段管理器时返回 0（权重 = 1），
        /// 免得纯测试场景里食物凭空变肥。
        /// </summary>
        public static int CurrentDay
        {
            get
            {
                GamePhaseManager phase = GameManager.PhaseManager;
                return phase != null ? Mathf.Max(0, phase.DayIndex) : 0;
            }
        }

        /// <summary>食物营养的日期权重 = 1.2 ^ 当前天数</summary>
        public static float NutritionWeight => Mathf.Pow(GrowthPerDay, CurrentDay);
    }
}
