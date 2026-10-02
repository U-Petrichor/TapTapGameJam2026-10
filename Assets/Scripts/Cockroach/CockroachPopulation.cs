using System;
using TapTapGameJam.Events;
using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    public class CockroachPopulation : MonoBehaviour
    {
        [SerializeField] public int CockroachCount;

        public void Add(int amount)
        {
            CockroachCount += amount;
            EventPipeline.Publish<EventCockroachCountChange>(new EventCockroachCountChange(CockroachCount, amount));
        }
        public void Remove(int amount)
        {
            CockroachCount -= amount;
            if (CockroachCount < 0)
            {
                CockroachCount = 0;
                // 这个事件意味着游戏结束，玩家失败了
                EventPipeline.Publish<EventGameOver>(new EventGameOver(false,"你的蟑螂全死了"));
                return;
            }
            EventPipeline.Publish<EventCockroachCountChange>(new EventCockroachCountChange(CockroachCount, amount));
        }
    }

}