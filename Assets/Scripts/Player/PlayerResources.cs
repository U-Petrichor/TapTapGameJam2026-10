using System;
using TapTapGameJam.Buffs;
using TapTapGameJam.Cockroach;
using TapTapGameJam.Events;
using UnityEngine;

namespace TapTapGameJam.Player
{
    public class PlayerResources : MonoBehaviour
    {
        [Tooltip("金币数")]
        [field: SerializeField] public int Coins { get; private set; } = 0;
        // 白天和夜晚读取同一个营养池，不再保存玩家独立副本。
        public float Nutrition => CockroachManager.Instance != null ? CockroachManager.Instance.sharedNutrition : 0f;
        [Tooltip("天赋点/天赋点")]
        [field: SerializeField] public int TalentPoint { get; private set; } = 0;
        

        private PlayerBuffs _buffs;


        private void Start()
        {
            PublishCurrentResources();
        }

        // 直接恢复快照，不走获得资源的 Buff 倍率，也不产生奖励增量。
        public void RestoreResources(int coins, float nutrition, int talentPoint)
        {
            Coins = Mathf.Max(0, coins);
            CockroachManager.Instance?.RestoreSharedNutrition(nutrition);
            TalentPoint = Mathf.Max(0, talentPoint);
            PublishCurrentResources();
        }

        private void PublishCurrentResources()
        {
            EventPipeline.Publish<EventCoinsChange>(new EventCoinsChange(Coins,0));
            EventPipeline.Publish<EventNutritionChange>(new EventNutritionChange(Nutrition,0));
            EventPipeline.Publish<EventTalentPointChange>(new EventTalentPointChange(TalentPoint, 0));
        }
        

        public void AddCoins(int amount)
        {
            int adjustedAmount = ApplyGainPercent(BuffKey.CoinGainPercent, amount);
            ApplyCoins(adjustedAmount);
        }

        /// <summary>发放固定金币奖励，不应用金币获取加成。</summary>
        public void AddCoinsWithoutBuff(int amount)
        {
            ApplyCoins(amount);
        }

        private void ApplyCoins(int amount)
        {
            Coins += amount;
            EventPipeline.Publish<EventCoinsChange>(new EventCoinsChange(Coins, amount));
        }

        /// <summary>
        /// 「获得」类资源统一走这里：正数吃 Buff 倍率，非正数（扣/退）原样返回。
        /// 找不到 PlayerBuffs 时按原额通过——早期测试场景里可能没挂玩家组件。
        /// </summary>
        private int ApplyGainPercent(BuffKey key, int amount)
        {
            if (amount <= 0)
            {
                return amount;
            }

            _buffs ??= GetComponent<PlayerBuffs>();
            return _buffs == null ? amount : _buffs.ApplyInt(key, amount);
        }
        public bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return false;
            if (Coins >= amount)
            {
                Coins -= amount;
                EventPipeline.Publish<EventCoinsChange>(new EventCoinsChange(Coins,amount));
                return true;
            }
            return false;
        }
        public void AddNutrition(int amount)
        {
            int adjustedAmount = ApplyGainPercent(BuffKey.NutritionGainPercent, amount);
            CockroachManager.Instance?.AddSharedNutrition(adjustedAmount);
        }
        public bool TrySpendNutrition(int amount)
        {
            var manager = CockroachManager.Instance;
            return manager != null && manager.TrySpendSharedNutrition(amount);
        }
        /// <summary>
        /// 天赋点原额增减。<b>撤销天赋的退款走这里，不吃「怒气获取」加成</b>——
        /// 否则退回来的点数比花出去的还多，等于可以无限刷点。
        /// </summary>
        public void AddTalentPoint(int amount)
        {
            TalentPoint += amount;
            EventPipeline.Publish<EventTalentPointChange>(new EventTalentPointChange(TalentPoint, amount));
        }

        /// <summary>
        /// 玩家「获得」天赋点（吃「怒气获取 / 天赋点获取」加成）。
        /// 小游戏奖励、击杀回怒这类正面来源走这里；退款请用 <see cref="AddTalentPoint"/>。
        /// </summary>
        public void AddTalentPointGain(int amount)
        {
            AddTalentPoint(ApplyGainPercent(BuffKey.TalentPointGainPercent, amount));
        }
        public bool TrySpendTalentPoint(int amount)
        {
            if (TalentPoint >= amount)
            {
                TalentPoint -= amount;
                EventPipeline.Publish<EventTalentPointChange>(new EventTalentPointChange(TalentPoint, amount));
                return true;
            }
            return false;
        }

    }
}
