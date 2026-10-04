using System;
using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 蟑螂决策/移动的可调数值。
    /// 数值直接对应参考实现 ant_test/js/config.js 里的 DEFAULT_SETTINGS，
    /// 单位换成了 Unity 的世界单位（因为网格不是像素）。
    ///
    /// 注意这里是 class 不是 struct：C# 规定"带字段初始化器的 struct 必须显式声明构造函数"，
    /// 而把默认值写在字段定义旁边（不用构造函数）可读性最好，所以用引用类型。
    /// 它只作为 CockroachManager 的一个配置字段存在，不存在被拷贝/装箱的性能顾虑。
    /// </summary>
    [Serializable]
    public class CockroachSettings
    {
        // ---- 感知 ----

        /// <summary>锥形随机采样点数</summary>
        [Tooltip("锥形随机采样点数")] public int SampleCount = 24;

        /// <summary>探测距离（世界单位）</summary>
        [Tooltip("探测距离")] public float DetectRange = 6f;

        /// <summary>探测半角（弧度），π/2 即正前方 ±90°</summary>
        [Tooltip("探测半角(rad)，π/2 即正前方 ±90°")] public float DetectCone = 1.57f;

        /// <summary>探索率：提前中断采样的概率</summary>
        [Tooltip("探索率：提前中断采样的概率")] public float LibertyCoef = 0.01f;

        /// <summary>贴墙惩罚强度</summary>
        [Tooltip("贴墙惩罚强度")] public float WallAvoid = 0.7f;

        /// <summary>撞墙预判距离：在身体之外提前"看到"墙</summary>
        [Tooltip("撞墙预判距离")] public float WallLook = 1.5f;

        /// <summary>距食物/巢穴足够近时强制转向的距离</summary>
        [Tooltip("距食物/巢穴足够近时强制转向的距离")] public float PermanentRange = 2f;

        // ---- 移动 ----

        /// <summary>移动速度（世界单位/秒）</summary>
        [Tooltip("移动速度")] public float Speed = 3.5f;

        /// <summary>转向速率基准（rad/s）</summary>
        [Tooltip("转向速率基准(rad/s)")] public float TurnSpeed = 4f;

        /// <summary>决策方向噪声（弧度）</summary>
        [Tooltip("决策方向噪声(rad)")] public float DirectionNoise = 0.1f;

        /// <summary>速度抖动下限（乘数），让每只蟑螂速度略有差别</summary>
        [Tooltip("速度抖动下限")] public float SpeedJitterMin = 0.86f;

        /// <summary>速度抖动上限（乘数）</summary>
        [Tooltip("速度抖动上限")] public float SpeedJitterMax = 1.14f;

        /// <summary>雷达/交互半径（世界单位）</summary>
        [Tooltip("身体半径")] public float InteractRadius = 0.34f;

        /// <summary>在巢穴里待命时的游荡速度</summary>
        [Tooltip("巢内游荡速度")] public float InsideSpeed = 4.9f;

        /// <summary>超出活动范围时，每秒被拉回巢穴多少距离</summary>
        [Tooltip("超范围回拉速度")] public float RangePushSpeed = 1.6f;

        // ---- 信息素 ----

        /// <summary>释放强度基准（刚离巢 / 刚取到食物时）</summary>
        [Tooltip("释放强度基准")] public float MarkerMax = 1000f;

        /// <summary>释放强度随时间指数衰减的系数：M·exp(-k·t)</summary>
        [Tooltip("释放强度时间衰减系数")] public float MarkerTimeCoef = 0.25f;


        /// <summary>
        /// 信息素刷子半径（世界单位）。释放时以蟑螂所在位置为圆心，按这个半径刷到网格上：
        /// 圆心强度高，向外抛物线衰减到边缘的 0。<b>0 = 退化成只写一个格子</b>。
        /// </summary>
        [Tooltip("信息素刷子半径（世界单位），0 = 只写一格")] public float MarkerRadius = 0.3f;

        /// <summary>危险信息素负权重，越大越躲</summary>
        [Tooltip("危险信息素负权重")] public float DangerAvoid = 2.5f;

        /// <summary>觅食信息素在巢口 fade 距离内线性淡出，避免在巢口堆出"穹顶"</summary>
        [Tooltip("巢口信息素淡出距离")] public float FoodTrailFade = 3f;

        // ---- 归巢 ----

        /// <summary>归巢偏置：探测不到信息素时仍能大致朝巢穴走</summary>
        [Tooltip("归巢偏置")] public float HomeBias = 8f;

        /// <summary>离巢散开区：刚出巢的蟑螂忽略信息素，先向外散开</summary>
        [Tooltip("离巢散开区")] public float LeaveZone = 2.2f;

        /// <summary>单次携带营养上限</summary>
        [Tooltip("单次携带营养上限")] public float CarryCapacity = 5f;

        /// <summary>食物被吃空时，清空这一半径内的觅食信息素</summary>
        [Tooltip("吃空清除半径")] public float EmptyClearRadius = 3f;

        // ---- 信息素衰减（由 GridManager.Update 每帧使用） ----

        /// <summary>信息素线性衰减速率 /s。觅食通道 ×1.3、危险通道 ×2.2</summary>
        [Tooltip("信息素线性衰减 /s")] public float PheromoneDecay = 3f;

        /// <summary>玩家通道的独立衰减速率 /s</summary>
        [Tooltip("玩家通道衰减 /s")] public float PlayerDecay = 200f;
    }
}
