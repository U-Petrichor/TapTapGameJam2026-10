namespace TapTapGameJam.Buffs
{
    /// <summary>
    /// 虫群相关 Buff 的只读快照。
    ///
    /// 为什么要有它：蟑螂是几十上百只 × 每只每个 FixedUpdate 都要读速度 / 携带量。
    /// 让每只蟑螂去查 <see cref="BuffAggregator"/> 就是每帧几百次字典/数组查找 + switch。
    /// 改成「Buff 变化时才重算一次快照，蟑螂只读一个字段」：读取成本降到一次字段访问。
    ///
    /// 由 <see cref="PlayerBuffs.CockroachSnapshot"/> 产出并缓存，按
    /// <see cref="BuffAggregator.Revision"/> 判断要不要重算。**不要自己 new**。
    /// </summary>
    public readonly struct CockroachBuffSnapshot
    {
        /// <summary>乘在 <c>CockroachSettings.Speed</c> 上</summary>
        public readonly float SpeedFactor;

        /// <summary>乘在 <c>CockroachSettings.CarryCapacity</c> 上</summary>
        public readonly float CarryFactor;

        /// <summary>乘在 <c>CockroachSettings.HatchNutritionPerSecond</c> 上（不要乘 Cost）</summary>
        public readonly float HatchFactor;

        /// <summary>乘在 <c>CockNest.range</c> 上（改完要重生成范围圈）</summary>
        public readonly float NestRangeFactor;

        /// <summary>群颚破壁：蟑螂能啃墙</summary>
        public readonly bool WallGnawing;

        /// <summary>同类相食：蟑螂死亡变食物</summary>
        public readonly bool DeathFood;

        public CockroachBuffSnapshot(float speedFactor, float carryFactor, float hatchFactor, float nestRangeFactor,
            bool wallGnawing, bool deathFood)
        {
            SpeedFactor = speedFactor;
            CarryFactor = carryFactor;
            HatchFactor = hatchFactor;
            NestRangeFactor = nestRangeFactor;
            WallGnawing = wallGnawing;
            DeathFood = deathFood;
        }

        /// <summary>没有任何 Buff 时的快照（全 1 倍、没有能力）。给「场上还没有玩家」的兜底用。</summary>
        public static CockroachBuffSnapshot Identity =>
            new CockroachBuffSnapshot(1f, 1f, 1f, 1f, false, false);

        public override string ToString()
            => $"speed×{SpeedFactor:0.###} carry×{CarryFactor:0.###} hatch×{HatchFactor:0.###} " +
               $"nestRange×{NestRangeFactor:0.###} gnaw={WallGnawing} deathFood={DeathFood}";
    }
}
