using System;

namespace TapTapGameJam.UI
{
    public enum PrinterHitResult { Ignored, Miss, Hit, Complete }

    // 指针和判定使用同一归一化坐标，独立于 UI 尺寸及帧率。
    public sealed class PrinterRepairRound
    {
        private readonly Func<double> _random;
        private readonly double _speed;
        private double _phase;
        public double Position => _phase <= 1 ? _phase : 2 - _phase;
        public double ZoneStart { get; private set; }
        public double ZoneWidth { get; }
        public int Successes { get; private set; }
        public int RequiredHits { get; }
        public bool IsComplete => Successes == RequiredHits;
        public bool IsRunning { get; private set; } = true;

        public PrinterRepairRound(int requiredHits, double speed, double zoneWidth, Func<double> random)
        {
            if (requiredHits < 1) throw new ArgumentOutOfRangeException(nameof(requiredHits));
            if (!Finite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (!Finite(zoneWidth) || zoneWidth <= 0 || zoneWidth > 1) throw new ArgumentOutOfRangeException(nameof(zoneWidth));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            RequiredHits = requiredHits; _speed = speed; ZoneWidth = zoneWidth;
            RandomizeZone();
        }
        public void Tick(double seconds)
        {
            if (!IsRunning || !Finite(seconds) || seconds <= 0) return;
            double distance = seconds * _speed;
            if (!Finite(distance)) return;
            _phase = (_phase + distance % 2) % 2;
        }
        public PrinterHitResult Hit()
        {
            if (!IsRunning) return PrinterHitResult.Ignored;
            // 黄色区域两侧边界均算命中。
            if (Position < ZoneStart || Position > ZoneStart + ZoneWidth) return PrinterHitResult.Miss;
            Successes++;
            if (IsComplete) { IsRunning = false; return PrinterHitResult.Complete; }
            RandomizeZone();
            return PrinterHitResult.Hit;
        }
        private void RandomizeZone()
        {
            double sample = _random();
            if (!Finite(sample) || sample < 0 || sample > 1)
                throw new InvalidOperationException("QTE random sample must be between zero and one.");
            ZoneStart = sample * (1 - ZoneWidth);
        }
        public void Stop() => IsRunning = false;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
