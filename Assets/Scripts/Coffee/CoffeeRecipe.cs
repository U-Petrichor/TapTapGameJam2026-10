using System;

namespace TapTapGameJam.Coffee
{
    public enum CoffeeStep
    {
        HeatWater,
        LoadBeans,
        GrindBeans,
        PlaceDripper,
        PlaceFilter,
        PourGrounds,
        PourWater,
        Dripping,
        Complete
    }

    // 只负责配方顺序和进度，不引用 Unity、镜头、鼠标或 Canvas。
    // 点击/拖拽合法后，由 CoffeeMinigameManager 调用这里的方法。
    public sealed class CoffeeRecipe
    {
        private readonly float _heatSeconds;
        private readonly float _grindSeconds;
        private readonly float _groundsSeconds;
        private readonly float _waterSeconds;
        private readonly float _dripSeconds;
        private float _elapsed;

        public CoffeeStep Step { get; private set; } = CoffeeStep.HeatWater;
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

        public CoffeeRecipe(float heatSeconds, float grindSeconds, float groundsSeconds,
            float waterSeconds, float dripSeconds)
        {
            _heatSeconds = CheckDuration(heatSeconds);
            _grindSeconds = CheckDuration(grindSeconds);
            _groundsSeconds = CheckDuration(groundsSeconds);
            _waterSeconds = CheckDuration(waterSeconds);
            _dripSeconds = CheckDuration(dripSeconds);
        }

        private static float CheckDuration(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), "步骤时长必须是正数。");
            return seconds;
        }

        public bool StartHeating()
        {
            if (Step != CoffeeStep.HeatWater || IsProcessing) return false;
            IsProcessing = true;
            return true;
        }

        public bool LoadBeans() => AdvanceFrom(CoffeeStep.LoadBeans, CoffeeStep.GrindBeans);
        public bool PlaceDripper() => AdvanceFrom(CoffeeStep.PlaceDripper, CoffeeStep.PlaceFilter);
        public bool PlaceFilter() => AdvanceFrom(CoffeeStep.PlaceFilter, CoffeeStep.PourGrounds);

        public bool StartPouringGrounds()
        {
            if (Step != CoffeeStep.PourGrounds || IsProcessing) return false;
            IsProcessing = true;
            return true;
        }

        // 按住研磨机时才调用；松开后保留进度，不会自动磨完。
        public void Grind(float deltaTime)
        {
            if (Step == CoffeeStep.GrindBeans)
                AddProgress(deltaTime, _grindSeconds, CoffeeStep.PlaceDripper);
        }

        // 水壶在有效区域内、按住且正在出水时才调用。
        public void PourWater(float deltaTime)
        {
            if (Step == CoffeeStep.PourWater)
                AddProgress(deltaTime, _waterSeconds, CoffeeStep.Dripping);
        }

        // 自动过程：烧水、倒粉动画、滴滤。这里没有修改全局倒计时。
        public void Tick(float deltaTime)
        {
            if (Step == CoffeeStep.HeatWater && IsProcessing)
                AddProgress(deltaTime, _heatSeconds, CoffeeStep.LoadBeans);
            else if (Step == CoffeeStep.PourGrounds && IsProcessing)
                AddProgress(deltaTime, _groundsSeconds, CoffeeStep.PourWater);
            else if (Step == CoffeeStep.Dripping)
                AddProgress(deltaTime, _dripSeconds, CoffeeStep.Complete);
        }

        private void AddProgress(float deltaTime, float duration, CoffeeStep next)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            _elapsed = Math.Min(duration, _elapsed + deltaTime);
            if (_elapsed >= duration) SetStep(next);
        }

        private bool AdvanceFrom(CoffeeStep required, CoffeeStep next)
        {
            if (Step != required) return false;
            SetStep(next);
            return true;
        }

        private void SetStep(CoffeeStep next)
        {
            Step = next;
            _elapsed = 0f;
            IsProcessing = false;
            StepChanged?.Invoke(next);
        }

        private float DurationFor(CoffeeStep step)
        {
            switch (step)
            {
                case CoffeeStep.HeatWater: return _heatSeconds;
                case CoffeeStep.GrindBeans: return _grindSeconds;
                case CoffeeStep.PourGrounds: return _groundsSeconds;
                case CoffeeStep.PourWater: return _waterSeconds;
                case CoffeeStep.Dripping: return _dripSeconds;
                default: return 0f;
            }
        }
    }
}
