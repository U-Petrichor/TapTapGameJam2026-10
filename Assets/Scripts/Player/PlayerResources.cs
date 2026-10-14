using System;
using TapTapGameJam.Events;
using TalentTree;
using UnityEngine;

namespace TapTapGameJam.Player
{
    public class PlayerResources : MonoBehaviour
    {
        [Tooltip("金币数")]
        [field: SerializeField] public int Coins { get; private set; } = 0;
        [Tooltip("营养值")]
        [field: SerializeField] public int Nutrition { get; private set; } = 0;
        [Tooltip("天赋点/天赋点")]
        [field: SerializeField] public int TalentPoint { get; private set; } = 0;
        
        [field: SerializeField] public int CoffeeCount { get; private set; } = 0;

        private PlayerTalentTree _talentTree;


        private void Start()
        {
            EventPipeline.Publish<EventCoinsChange>(new EventCoinsChange(Coins,0));
            EventPipeline.Publish<EventNutritionChange>(new EventNutritionChange(Nutrition,0));
            EventPipeline.Publish<EventTalentPointChange>(new EventTalentPointChange(TalentPoint, 0));
        }
        
        // 一次完整制作结束时获得一份咖啡，取消或超时不调用。
        public void AddCoffee()
        {
            CoffeeCount++;
            EventPipeline.Publish(new EventCoffeeCountChange(CoffeeCount));
        }

        public void AddCoins(int amount)
        {
            int adjustedAmount = amount > 0 ? ApplyCoinGainMultiplier(amount) : amount;
            Coins += adjustedAmount;
            EventPipeline.Publish<EventCoinsChange>(new EventCoinsChange(Coins, adjustedAmount));
        }

        private int ApplyCoinGainMultiplier(int baseAmount)
        {
            _talentTree ??= GetComponent<PlayerTalentTree>();
            float multiplier = _talentTree == null
                ? 0f
                : Mathf.Max(0f, _talentTree.Effects.GetNumericTotal(TalentEffectKey.CoinGainMultiplier));
            return Mathf.Max(0, Mathf.RoundToInt(baseAmount * (1f + multiplier)));
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
            Nutrition += amount;
            EventPipeline.Publish<EventNutritionChange>(new EventNutritionChange(Nutrition,amount));
        }
        public bool TrySpendNutrition(int amount)
        {
            if (Nutrition >= amount)
            {
                Nutrition -= amount;
                EventPipeline.Publish<EventNutritionChange>(new EventNutritionChange(Nutrition,amount));
                return true;
            }
            return false;
        }
        public void AddTalentPoint(int amount)
        {
            TalentPoint += amount;
            EventPipeline.Publish<EventTalentPointChange>(new EventTalentPointChange(TalentPoint, amount));
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
