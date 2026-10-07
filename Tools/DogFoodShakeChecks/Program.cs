using System;
using TapTapGameJam.UI;

internal static class Program
{
    private static int _passed;

    private static void Main()
    {
        Run("静止、单向拖动和大幅单向跳动不倒粮", () =>
        {
            var shake = new DogFoodShakeProgress(20f, 0.1f);
            foreach (float x in new[] { 0f, 0f, 10f, 30f, 200f, 500f })
                Check(!shake.Sample(x));
            Near(shake.Progress, 0f);
        });
        Run("小幅抖动不能刷进度，反向足够距离才倒粮", () =>
        {
            var shake = new DogFoodShakeProgress(20f, 0.1f);
            shake.Sample(0f);
            shake.Sample(40f);
            foreach (float x in new[] { 39f, 35f, 38f, 30f, 25f }) Check(!shake.Sample(x));
            Near(shake.Progress, 0f);
            Check(shake.Sample(20f));
            Near(shake.Progress, 0.1f);
        });
        Run("盆外或松手后重新接入，不把外部位移记为一次晃动", () =>
        {
            var shake = new DogFoodShakeProgress(20f, 0.1f);
            shake.Sample(0f);
            shake.Sample(40f);
            Check(shake.Sample(0f));
            shake.StopTracking();
            Check(!shake.Sample(300f));
            Check(!shake.Sample(250f));
            Near(shake.Progress, 0.1f);
            Check(shake.Sample(300f));
            Near(shake.Progress, 0.2f);
        });
        Run("完成后不再增加，重新开始清空进度", () =>
        {
            var shake = new DogFoodShakeProgress(20f, 0.1f);
            shake.Sample(0f);
            shake.Sample(40f);
            for (int i = 0; i < 10; i++) Check(shake.Sample(i % 2 == 0 ? 0f : 40f));
            Near(shake.Progress, 1f);
            Check(shake.IsComplete && !shake.Sample(0f));
            shake.Reset();
            Check(!shake.IsComplete);
            Near(shake.Progress, 0f);
            Check(!shake.Sample(0f));
        });
        Run("进度在最后一次晃动封顶", () =>
        {
            var shake = new DogFoodShakeProgress(20f, 0.3f);
            shake.Sample(0f);
            shake.Sample(40f);
            for (int i = 0; i < 4; i++) Check(shake.Sample(i % 2 == 0 ? 0f : 40f));
            Near(shake.Progress, 1f);
        });
        Console.WriteLine($"Passed {_passed} dog food behavior checks.");
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine($"PASS: {name}");
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new Exception("Check failed.");
    }

    private static void Near(float actual, float expected)
    {
        Check(Math.Abs(actual - expected) < 0.00001f);
    }
}
