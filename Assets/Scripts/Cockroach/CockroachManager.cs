using System;
using System.Collections.Generic;
using TapTapGameJam.Events;
using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 蟑螂系统的总管理者。
    /// 层级是 CockroachManager → CockNest → Cockroach，每一层只由下一层向它报到。
    /// 决策节奏由这里统一控制：固定每 <see cref="DecisionFrameInterval"/> 个 FixedUpdate
    /// </summary>
    public class CockroachManager : MonoBehaviour
    {
        private static CockroachManager _instance;

        /// <summary>
        /// 每隔几个 FixedUpdate 做一次全体决策
        /// </summary>
        public const int DecisionFrameInterval = 5;

        public static CockroachManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CockroachManager>();
                }

                return _instance;
            }
        }

        [Header("蟑螂寻路设置")]
        public CockroachSettings cockroachSetting = new CockroachSettings();

        // TODO： 和PlayerResources合并取其一
        [Header("共享总营养")] public float sharedNutrition;

        public List<CockNest> CockNestList { get; private set; } = new List<CockNest>();

        private int _decisionFrameCounter;

        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            // 1. 决策前先让每个巢穴把距离场刷新好，蟑螂才知道"哪边离家更近"
            for (int i = 0; i < CockNestList.Count; i++)
            {
                // TODO: 刷新距离场 + TraceWall 的逻辑放到场景变化时（进入夜晚 or 某个墙壁被破坏）
                CockNestList[i].RefreshDistanceField();
            }
        }

        private void FixedUpdate()
        {
            _decisionFrameCounter++;
            if (_decisionFrameCounter < DecisionFrameInterval)
            {
                return;
            }

            _decisionFrameCounter = 0;

            // 全体决策
            for (int i = 0; i < CockNestList.Count; i++)
            {
                List<Cockroach> roaches = CockNestList[i].CockroachList;
                for (int j = 0; j < roaches.Count; j++)
                {
                    roaches[j].Decide();
                }
            }
        }

        public int CockroachTotalCount()
        {
            int total = 0;
            foreach (CockNest nest in CockNestList)
            {
                if (nest.isActive) total += nest.CockroachList.Count;
            }

            return total;
        }

        /// <summary>
        /// 从共享营养池里扣掉 amount。够才扣，返回是否扣成功。
        /// 孵化、以及以后任何共用这个池子的玩法都从这里走，保证事件只在这一处发。
        /// </summary>
        public bool TrySpendSharedNutrition(float amount)
        {
            if (amount <= 0f || sharedNutrition < amount)
            {
                return false;
            }

            sharedNutrition -= amount;
            EventPipeline.Publish(new EventNutritionChange(sharedNutrition, -amount));
            return true;
        }
    }
}
