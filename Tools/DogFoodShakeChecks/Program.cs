using System;
using TapTapGameJam.UI;

internal static class Program
{
    private static int _passed;

    private static void Main()
    {
        Run("出袋初速度朝左下，空中横向保持惯性且重力继续加速向下", () =>
        {
            var pile = new DogFoodPileSimulation(-500, 500, -500, -400, 200);
            var food = pile.Add(100, 200, 10, -120, -60);
            pile.Advance(0.05f);
            Check(food.X < 100 && food.Y < 200);
            Check(Math.Abs(food.VelocityX + 120) < 0.1f);
            Check(food.VelocityY < -60 && !food.Landed);
            float previousYVelocity = food.VelocityY;
            pile.Advance(0.05f);
            Check(food.VelocityY < previousYVelocity);
        });
        Run("碗外狗粮不会被瞬移进碗，洒落后可以继续倒满100颗", () =>
        {
            var pile = new DogFoodPileSimulation(-50, 50, -50, 50, 1000);
            var food = pile.Add(200, 100, 7, -10, -60);
            for (int i = 0; i < 120; i++) pile.Advance(1f / 60f);
            Check(food.Lost && !food.Landed && food.X > 150);
            var progress = new DogFoodPourProgress(100);
            Check(progress.Tick(10, true, 10) == 100);
            for (int i = 0; i < 99; i++) Check(progress.Land());
            progress.DiscardInFlight();
            Check(!progress.IsComplete && progress.Spawned == 99);
            Check(progress.Tick(0.1f, true, 10) == 1);
            Check(progress.Land() && progress.IsComplete);
        });
        Run("空中不计数，碗底碰撞后停止下落", () =>
        {
            var pile = new DogFoodPileSimulation(-50, 50, -50, 50, 1000);
            var food = pile.Add(0, 150, 7, 0);
            pile.Advance(0.1f);
            Check(!food.Landed && food.Y < 150);
            for (int i = 0; i < 240; i++) pile.Advance(1f / 60f);
            Check(food.Landed);
            Near(food.Y, -43);
        });
        Run("颗粒相互碰撞并向上堆叠", () =>
        {
            var pile = new DogFoodPileSimulation(-50, 50, -50, 50, 1000);
            var lower = pile.Add(0, -43, 7, 0);
            var upper = pile.Add(0, 100, 7, 0);
            for (int i = 0; i < 300; i++) pile.Advance(1f / 60f);
            Check(lower.Landed && upper.Landed);
            Check(upper.Y > lower.Y + 13.9f);
        });
        Run("高速下落不会穿过碗底或其他狗粮，碗壁限制横向位置", () =>
        {
            var pile = new DogFoodPileSimulation(-30, 30, 0, 60, 100000);
            var lower = pile.Add(0, 6, 6, 0);
            var upper = pile.Add(0, 250, 6, 0);
            var side = pile.Add(24, 40, 6, 1000);
            for (int i = 0; i < 300; i++) pile.Advance(1f / 60f);
            Check(lower.Landed && upper.Landed && side.Landed);
            Check(upper.Y >= lower.Y + 11f);
            Check(side.X <= 24.01f && side.X >= -24.01f);
        });
        Run("自然下落可堆满100颗，洒落补充及粮堆超过碗口都不会卡任务", () =>
        {
            var pile = new DogFoodPileSimulation(-50, 50, -50, 50, 2500);
            var foods = new System.Collections.Generic.List<DogFoodPileSimulation.Body>();
            var random = new Random(123);
            var progress = new DogFoodPourProgress(100);
            var counted = new System.Collections.Generic.HashSet<DogFoodPileSimulation.Body>();
            for (int frame = 0; frame < 3600 && !progress.IsComplete; frame++)
            {
                int count = progress.Tick(1f / 60f, true, 15);
                for (int i = 0; i < count; i++)
                    foods.Add(pile.Add(40 + (float)random.NextDouble() * 10, 200, 7, -120, -60));
                pile.Advance(1f / 60f);
                for (int i = foods.Count - 1; i >= 0; i--)
                {
                    var food = foods[i];
                    if (food.Lost)
                    {
                        progress.DiscardInFlight();
                        foods.RemoveAt(i);
                    }
                    else if (food.Landed && counted.Add(food)) Check(progress.Land());
                }
            }
            Check(progress.IsComplete && foods.Count == 100);
            float highest = -50;
            foreach (var food in foods)
            {
                Check(food.Landed && food.Y >= -43.01f);
                highest = Math.Max(highest, food.Y);
            }
            Check(highest > 50);
        });
        Run("按住静止持续出粮，按时间累计且不超过100颗", () =>
        {
            var pour = new DogFoodPourProgress(100);
            for (int i = 0; i < 20; i++) pour.Tick(0.5f, true, 15f);
            Check(pour.Spawned == 100 && pour.Landed == 0 && !pour.IsComplete);
            Check(pour.Tick(10f, true, 15f) == 0);
        });
        Run("盆外和松手停止生成，恢复时不补发等待期间的狗粮", () =>
        {
            var pour = new DogFoodPourProgress(100);
            Check(pour.Tick(0.05f, true, 10f) == 0);
            Check(pour.Tick(20f, false, 10f) == 0);
            Check(pour.Tick(0.05f, true, 10f) == 0);
            Check(pour.Tick(0.05f, true, 10f) == 1);
            Check(pour.Land() && pour.Landed == 1);
        });
        Run("最后一颗落地才完成，落地计数不会超出生成数量", () =>
        {
            var pour = new DogFoodPourProgress(100);
            Check(!pour.Land());
            Check(pour.Tick(100f, true, 15f) == 100);
            for (int i = 0; i < 99; i++) Check(pour.Land());
            Check(!pour.IsComplete);
            Near(pour.Progress, 0.99f);
            Check(pour.Land() && pour.IsComplete);
            Check(!pour.Land() && pour.Landed == 100);
            Near(pour.Progress, 1f);
            var restarted = new DogFoodPourProgress(100);
            Check(restarted.Spawned == 0 && restarted.Landed == 0 && !restarted.IsComplete);
        });
        Run("不同更新频率的出粮数量一致", () =>
        {
            var fast = new DogFoodPourProgress(100);
            var slow = new DogFoodPourProgress(100);
            for (int i = 0; i < 256; i++) fast.Tick(1f / 64f, true, 15f);
            for (int i = 0; i < 32; i++) slow.Tick(1f / 8f, true, 15f);
            Check(fast.Spawned == 60 && slow.Spawned == 60);
        });
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
