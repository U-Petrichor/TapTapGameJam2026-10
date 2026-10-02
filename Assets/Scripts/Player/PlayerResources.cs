using System;
using TapTapGameJam.Events;
using UnityEngine;

namespace TapTapGameJam.Player
{
    public class PlayerResources : MonoBehaviour
    {
        [Tooltip("金币数")]
        [field: SerializeField] public int Coins { get; private set; } = 0;
        [Tooltip("营养值")]
        [field: SerializeField] public int Nutrition { get; private set; } = 0;

        public void AddCoins(int amount)
        {
            Coins += amount;
            EventPipeline.Publish<EventCoinsChange>(new EventCoinsChange(Coins,amount));
        }
        public bool TrySpendCoins(int amount)
        {
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
        
    }
}
