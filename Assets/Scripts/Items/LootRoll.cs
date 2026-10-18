namespace TapTapGameJam.Items
{
    /// <summary>
    /// 黑盒等「随机出货」的纯计算部分。放在这里而不是塞进 MonoBehaviour，
    /// 是因为概率公式需要能被 Tools/BuffChecks 直接验证（那边不开 Unity）。
    /// </summary>
    public static class LootRoll
    {
        /// <summary>
        /// 稀有物品的 <see cref="PickableItem.SpecialLevel"/> 门槛：1 和 2 都算稀有，0 是普通。
        /// 用 `>=` 而不是枚举，将来加 3 级稀有不用改这里。
        /// </summary>
        public const int RareSpecialLevel = 1;

        /// <summary>这个稀有度算不算「稀有」。</summary>
        public static bool IsRare(int specialLevel) => specialLevel >= RareSpecialLevel;

        /// <summary>
        /// 稀有物品的实际概率 = 基准概率 × (1 + 幸运)。
        ///
        /// <b>是「按比例提升」，不是「加百分点」</b>：基准 10%、幸运 +60%（luck = 0.6）
        /// 得到 10% × 1.6 = 16%，不是 10% + 60%。
        /// 幸运是几个来源的<b>加法</b>之和（两个 +30% 等价于一个 +60%）。
        /// 结果夹在 [0, 1]：负幸运能压到 0，但也压不成负数。
        /// </summary>
        public static float RareChance(float baseChance, float luckSum)
        {
            float chance = baseChance * (1f + luckSum);
            if (chance < 0f)
            {
                return 0f;
            }

            return chance > 1f ? 1f : chance;
        }
    }
}
