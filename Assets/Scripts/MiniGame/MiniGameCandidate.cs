using TapTapGameJam.MiniGame;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public abstract class MiniGameCandidate : MonoBehaviour
    {
        // 由候选任务决定现在能不能刷。
        public abstract bool CanSpawn { get; }
        // 白天固定入口单独创建，不参与随机刷新。
        public virtual bool IsDailyActivity => false;
        public virtual void BeginNewDay() { }

        // 每次调用都应创建一个新的任务实例。
        public abstract MiniGameInstanceBasic CreateTask();
    }
}
