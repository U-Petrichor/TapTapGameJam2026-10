using System;

namespace TapTapGameJam.Coffee
{
    public enum CoffeeStep
    {
        LoadBeans,
        Grind,
        PlaceGrounds,
        PourWater,
        Complete
    }

    // 放豆、长按研磨、放粉、注水、完成。输入及 UI 不参与配方规则。
    // 不引用 Unity、输入或 Canvas，便于独立测试。
    public sealed class CoffeeRecipe
    {
        private readonly float _grindSeconds;
        private readonly float _waterSeconds;
        private float _elapsed;

        public CoffeeStep Step { get; private set; } = CoffeeStep.LoadBeans;
        private bool _stopped;
        public bool IsProcessing { get; private set; }
        public event Action<CoffeeStep> StepChanged;

        public float Progress
        {
            get
            {
                float duration = DurationFor(Step);
                return duration > 0f ? Math.Min(1f, _elapsed / duration) : 0f;
            }
        }

        public CoffeeRecipe(float grindSeconds, float waterSeconds)
        {
            _grindSeconds = CheckDuration(grindSeconds);
            _waterSeconds = CheckDuration(waterSeconds);
        }

        public bool StartGrinding()
        {
            if (_stopped || Step != CoffeeStep.Grind || IsProcessing) return false;
            IsProcessing = true;
            return true;
        }

        public bool LoadBeans()
        {
            if (_stopped || Step != CoffeeStep.LoadBeans) return false;
            SetStep(CoffeeStep.Grind);
            return true;
        }

        public void Stop() { _stopped = true; IsProcessing = false; }

        public void StopGrinding()
        {
            if (Step == CoffeeStep.Grind)
                IsProcessing = false;
        }

        public void Grind(float deltaTime)
        {
            if (Step == CoffeeStep.Grind && IsProcessing)
                AddProgress(deltaTime, _grindSeconds, CoffeeStep.PlaceGrounds);
        }

        public bool PlaceGrounds()
        {
            if (_stopped || Step != CoffeeStep.PlaceGrounds) return false;
            SetStep(CoffeeStep.PourWater);
            return true;
        }

        public bool StartPouringWater()
        {
            if (_stopped || Step != CoffeeStep.PourWater || IsProcessing) return false;
            IsProcessing = true;
            return true;
        }

        // 只有粉杯已经放置后，按住热水壶并停在杯子区域内才会推进。
        public void PourWater(float deltaTime)
        {
            if (Step == CoffeeStep.PourWater && IsProcessing)
                AddProgress(deltaTime, _waterSeconds, CoffeeStep.Complete);
        }

        public void StopPouringWater()
        {
            if (Step == CoffeeStep.PourWater)
                IsProcessing = false;
        }

        private void AddProgress(float deltaTime, float duration, CoffeeStep next)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            _elapsed = Math.Min(duration, _elapsed + deltaTime);
            if (_elapsed >= duration) SetStep(next);
        }

        private void SetStep(CoffeeStep next)
        {
            Step = next;
            _elapsed = 0f;
            IsProcessing = false;
            StepChanged?.Invoke(next);
        }

        private float DurationFor(CoffeeStep step) => step switch
        {
            CoffeeStep.Grind => _grindSeconds,
            CoffeeStep.PourWater => _waterSeconds,
            _ => 0f
        };

        private static float CheckDuration(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), "步骤时长必须是正数。");
            return seconds;
        }
    }
}
