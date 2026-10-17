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

        [Header("巡逻路线")]
        [Tooltip("巡逻路线离行动区域边界的内缩量。小于身体半径会贴着边界走，建议 >= BodyRadius")]
        public float PatrolEdgeMargin = 0.5f;

        [Tooltip("四个角的圆角半径。圆角让切线连续变化，转向才平滑；0 = 直角")]
        public float PatrolCornerRadius = 1f;

        [Tooltip("路线方向：true = 俯视顺时针，false = 俯视逆时针")]
        public bool PatrolClockwise = false;

        [Tooltip("沿路线往前看多远来瞄准。越大越早开始拐弯、也越容易切角")]
        public float PatrolLookAhead = 1f;

        [Header("冲刺")]
        [Tooltip("一次冲刺最长持续多久（秒）")]
        public float DashDuration = 3.2f;

        [Tooltip("冲刺结束后的冷却（秒）")]
        public float DashCooldown = 6f;

        [Header("脱离区域与归巢")]
        [Tooltip("焦点消失后，还允许在区域外逗留多久再往回走（秒）")]
        public float EngageLinger = 1.5f;

        [Tooltip("想走却几乎没挪动，持续这么久就判定为卡住（秒）")]
        public float StallTimeout = 0.6f;
    }
}
