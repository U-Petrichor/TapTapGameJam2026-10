using System;
using System.Collections.Generic;
using TapTapGameJam.Buffs;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
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

        [Header("胜利目标")]
        [Min(1), SerializeField] private int victoryPopulation = 2000;
        public int VictoryPopulation => Mathf.Max(1, victoryPopulation);
        public bool HasWon { get; private set; }

        [Header("死亡产物")]
        [Tooltip("「同类相食」tag 生效时，蟑螂死在巢外留下的食物（Assets/Prefabs/Items/Food_ZhangLang.prefab）。留空则不掉东西")]
        public FoodItem deathFoodPrefab;

        public List<CockNest> CockNestList { get; private set; } = new List<CockNest>();

        private int _decisionFrameCounter;
        private int _lastPopulation = -1;
        private bool _gameEnded;

        private CockroachBuffSnapshot _buffMods = CockroachBuffSnapshot.Identity;
        private int _buffRevision = int.MinValue;

        /// <summary>
        /// 虫群 Buff 快照。蟑螂 / 巢穴每帧都读，所以在这里再缓存一层：
        /// 一次 PlayerBuffs 查找 + 一次 Revision 比较，之后就只是读结构体字段。
        ///
        /// 注意读的是<b>快照</b>而不是每次去查聚合器：几十只蟑螂 × 每帧 3 个 key 的话，
        /// 就是每帧几百次字典/数组查询。
        /// </summary>
        public CockroachBuffSnapshot BuffMods
        {
            get
            {
                PlayerBuffs buffs = PlayerBuffs.Current;
                if (buffs == null)
                {
                    return CockroachBuffSnapshot.Identity;
                }

                if (buffs.Revision != _buffRevision)
                {
                    _buffMods = buffs.CockroachSnapshot;
                    _buffRevision = buffs.Revision;
                }

                return _buffMods;
            }
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnEnable() => EventPipeline.Subscribe<EventGameOver>(OnGameOver);
        private void OnDisable() => EventPipeline.UnSubscribe<EventGameOver>(OnGameOver);
        private void OnGameOver(EventGameOver evt) { _gameEnded = true; }

        // Reconcile once per frame, including nest removal and initial scene population.
        // Only sum nest list lengths; never scan all individual cockroaches here.
        private void LateUpdate()
        {
            int population = CockroachTotalCount();
            if (population != _lastPopulation)
            {
                int change = _lastPopulation < 0 ? 0 : population - _lastPopulation;
                _lastPopulation = population;
                EventPipeline.Publish(new EventCockroachCountChange(population, change));
            }

            if (!_gameEnded && population >= VictoryPopulation)
            {
                HasWon = true;
                _gameEnded = true;
                Debug.Log($"[CockroachManager] 胜利：蟑螂数量达到 {population}/{VictoryPopulation}。");
                EventPipeline.Publish(new EventGameOver(true, $"蟑螂数量达到 {VictoryPopulation}"));
            }
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
                if (!CockNestList[i].isCockroachActive) continue;
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
                // Daytime pauses activity; surviving cockroaches still count toward victory.
                if (nest != null) total += nest.CockroachList.Count;
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

        /// <summary>
        /// 在 <paramref name="worldPosition"/> 留下一具蟑螂尸体（普通 <see cref="FoodItem"/>，
        /// 会被蟑螂当食物、也会被玩家捡）。
        ///
        /// 预制体引用放在这里而不是每只蟑螂上：和 <see cref="cockroachSetting"/> 一样是"全场一份"的配置，
        /// 换尸体外观只改一处，不用去动蟑螂预制体。
        /// 要不要掉由 <see cref="Cockroach"/> 按 tag 决定，这里只管生成。
        /// </summary>
        public FoodItem SpawnDeathFood(Vector3 worldPosition)
        {
            if (deathFoodPrefab == null)
            {
                return null;
            }

            FoodItem corpse = Instantiate(deathFoodPrefab, worldPosition, Quaternion.identity);
            corpse.name = deathFoodPrefab.name;
            return corpse;
        }
    }
}
