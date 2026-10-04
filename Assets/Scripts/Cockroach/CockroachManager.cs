using System;
using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 蟑螂系统的总管理者。
    /// 层级是 CockroachManager → CockNest → Cockroach，每一层只由下一层向它报到。
    /// 决策节奏由这里统一控制：固定每 <see cref="DecisionFrameInterval"/> 个 FixedUpdate
    /// 走一次「刷新各巢穴距离场 → 全体蟑螂决策 → 全体释放信息素」。
    /// 蟑螂自己的 Update 只负责移动、转向、撞墙，不含任何计时器。
    /// </summary>
    public class CockroachManager : MonoBehaviour
    {
        private static CockroachManager _instance;

        /// <summary>
        /// 每隔几个 FixedUpdate 做一次全体决策
        /// </summary>
        public const int DecisionFrameInterval = 10;

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

        [Header("共享总营养")] public float sharedNutrition;

        public List<CockNest> CockNestList { get; private set; } = new List<CockNest>();

        private int _decisionFrameCounter;

        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            // TODO: 临时游戏测试逻辑，后续由正式游戏流程接管
            
            // TODO: 在地图中生成一个Nest
            
        }

        private void FixedUpdate()
        {
            _decisionFrameCounter++;
            if (_decisionFrameCounter < DecisionFrameInterval)
            {
                return;
            }

            _decisionFrameCounter = 0;

            // 1. 决策前先让每个巢穴把距离场刷新好，蟑螂才知道"哪边离家更近"
            for (int i = 0; i < CockNestList.Count; i++)
            {
                CockNestList[i].RefreshDistanceField();
            }

            // 2. 全体决策 + 释放信息素
            for (int i = 0; i < CockNestList.Count; i++)
            {
                List<Cockroach> roaches = CockNestList[i].CockroachList;
                for (int j = 0; j < roaches.Count; j++)
                {
                    roaches[j].Decide();
                    roaches[j].Deposit();
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
    }
}
