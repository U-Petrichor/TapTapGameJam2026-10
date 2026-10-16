using System;

namespace TapTapGameJam.UI
{
    // 生成数量包含尚在下落的颗粒，避免到达目标前多生成。
    public sealed class DogFoodPourProgress
    {
        private double _elapsed;
        public int Target { get; }
        public int Spawned { get; private set; }
        public int Landed { get; private set; }
        public bool IsComplete => Landed == Target;
        public float Progress => (float)Landed / Target;

        public DogFoodPourProgress(int target)
        {
            if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
            Target = target;
        }

        public int Tick(float deltaTime, bool canPour, float foodsPerSecond)
        {
            if (!canPour || Spawned >= Target)
            {
                _elapsed = 0;
                return 0;
            }
            if (deltaTime <= 0 || foodsPerSecond <= 0) return 0;
            _elapsed += (double)deltaTime * foodsPerSecond;
            int count = (int)Math.Min(Math.Floor(_elapsed), Target - Spawned);
            _elapsed -= count;
            Spawned += count;
            return count;
        }

        public bool Land()
        {
            if (Landed >= Spawned) return false;
            Landed++;
            return true;
        }

        public void DiscardInFlight()
        {
            // 洒到碗外的颗粒不算成功，并释放名额，允许继续倒满目标数量。
            if (Spawned > Landed) Spawned--;
        }
    }
}
