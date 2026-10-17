using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫的可调数值。
    /// 字段名就是 prefab 里的序列化键，改名等于改存档，动之前先想好旧值怎么迁移。
    /// 这里只放数字，不放引用、不放逻辑。
    /// </summary>
    [System.Serializable]
    public class GuardSettings
    {
        [Header("视野")]
        [Tooltip("视野距离。灯光锥和 trigger 锥都按它生成，改这里两边一起变")]
        public float VisionRange = 6f;

        [Tooltip("视野半角（弧度）")]
        public float VisionCone = 0.4f;

        [Tooltip("原地扫视的角速度（rad/s）")]
        public float ScanSpeed = 0.5f;

        [Tooltip("原地扫视时，隔多久换一次扫的方向（秒）")]
        public float ScanIntervalMin = 4.0f;
        public float ScanIntervalMax = 8.5f;

        [Header("移动")]
        public float PatrolSpeed = 1.5f;
        public float DashSpeed = 8.8f;

        [Tooltip("转向角速度（rad/s）")]
        public float TurnSpeed = 4.5f;

        [Tooltip("避障预判距离：提前这么远发现墙就开始拐")]
        public float WallLook = 1.5f;

        [Tooltip("身体半径。视野锥、身体 trigger、击杀判定都按它")]
        public float BodyRadius = 0.4f;

        [Tooltip("避障和挡墙用的半径，要比身体略小，否则贴着墙会卡住")]
        public float WallProbeRadius = 0.28f;

        [Header("巡逻")]
        [Tooltip("走到一个巡逻点后，原地扫视多久再去下一个点（秒）")]
        public float PatrolPauseMin = 1.5f;
        public float PatrolPauseMax = 4f;

        [Tooltip("离巡逻点多近算走到了")]
        public float PatrolArriveRadius = 0.5f;

        [Tooltip("巡逻点和行动区域边界的留白，避免贴着边来回蹭")]
        public float PatrolEdgeMargin = 0.5f;

        [Tooltip("挑一个能站人的巡逻点最多试几次；一直挑不到就原地扫视")]
        public int PatrolSampleAttempts = 12;

        [Header("冲刺")]
        [Tooltip("一次冲刺最长持续多久（秒）")]
        public float DashDuration = 3.2f;

        [Tooltip("冲刺结束后的冷却（秒）")]
        public float DashCooldown = 6f;

        [Header("脱离区域与归巢")]
        [Tooltip("焦点消失后，还允许在区域外逗留多久再往回走（秒）")]
        public float EngageLinger = 1.5f;

        [Tooltip("离出生点多近算回到家")]
        public float HomeArriveRadius = 0.6f;

        [Tooltip("想走却几乎没挪动，持续这么久就判定为卡住（秒）")]
        public float StallTimeout = 0.6f;
    }
}
