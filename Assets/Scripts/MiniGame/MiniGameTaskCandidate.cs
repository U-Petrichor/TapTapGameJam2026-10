using TapTapGameJam.MiniGame;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public abstract class MiniGameTaskCandidate : MonoBehaviour
    {
        // 由候选任务决定现在能不能刷。
        // 例如：咖啡桌是否已经被另一个任务占用。
        public abstract bool CanSpawn { get; }

        // 每次调用都应创建一个新的任务实例。
        public abstract MiniGameTask CreateTask();
    }
}