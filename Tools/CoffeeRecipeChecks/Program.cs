using System;
using System.Collections.Generic;
using TapTapGameJam.Coffee;

// 不需要启动 Unity，直接测试真正的配方类。失败时进程返回非零状态。
internal static class Program
{
    private static int _passed;

    private static void Main()
    {
        Run("不能跳过加热或提前放滤纸", () =>
        {
            var recipe = NewRecipe();
            Check(!recipe.LoadBeans() && !recipe.PlaceFilter() && !recipe.PlaceDripper());
            Check(!recipe.StartPouringGrounds());
            recipe.Grind(100f);
            recipe.PourWater(100f);
            recipe.Tick(100f);
            Check(recipe.Step == CoffeeStep.HeatWater && recipe.Progress == 0f);
        });
        Run("加热必须先点击，不能重复启动或用剩余时间跳步骤", () =>
        {
            var recipe = NewRecipe();
            Check(recipe.StartHeating());
            Check(!recipe.StartHeating());
            recipe.Tick(1f);
            Near(recipe.Progress, 0.5f);
            recipe.Tick(100f);
            Check(recipe.Step == CoffeeStep.LoadBeans);
            Check(!recipe.StartHeating());
        });
        Run("咖啡豆只能放一次；研磨松手不会自动继续", () =>
        {
            var recipe = AtGrind();
            Check(!recipe.LoadBeans());
            recipe.Grind(0.5f);
            Near(recipe.Progress, 0.25f);
            recipe.Tick(100f); // 模拟世界时间继续流逝，但玩家没有按住。
            Near(recipe.Progress, 0.25f);
            recipe.Grind(1.5f);
            Check(recipe.Step == CoffeeStep.PlaceDripper);
        });
        Run("必须先放滤杯，再放滤纸，再倒粉", () =>
        {
            var recipe = AtGrind();
            recipe.Grind(2f);
            Check(!recipe.PlaceFilter() && !recipe.StartPouringGrounds());
            Check(recipe.PlaceDripper());
            Check(!recipe.PlaceDripper() && !recipe.StartPouringGrounds());
            Check(recipe.PlaceFilter());
            recipe.Tick(100f);
            Check(recipe.Step == CoffeeStep.PourGrounds && !recipe.IsProcessing);
        });
        Run("倒粉需启动并等待，不能直接开始注水", () =>
        {
            var recipe = AtGrounds();
            Check(recipe.StartPouringGrounds());
            Check(!recipe.StartPouringGrounds());
            recipe.PourWater(100f);
            recipe.Tick(0.5f);
            Check(recipe.Step == CoffeeStep.PourGrounds);
            Near(recipe.Progress, 0.5f);
            recipe.Tick(0.5f);
            Check(recipe.Step == CoffeeStep.PourWater);
        });
        Run("注水可以分次完成；移开水壶不增加进度", () =>
        {
            var recipe = AtWater();
            recipe.PourWater(1f);
            Near(recipe.Progress, 0.25f);
            recipe.Tick(100f); // 没有有效注水输入，世界倒计时仍可继续。
            Near(recipe.Progress, 0.25f);
            recipe.PourWater(3f);
            Check(recipe.Step == CoffeeStep.Dripping);
        });
        Run("滴滤结束自动完成，并且完成通知仅一次", () =>
        {
            var recipe = AtWater();
            int completions = 0;
            recipe.StepChanged += step => { if (step == CoffeeStep.Complete) completions++; };
            recipe.PourWater(4f);
            recipe.Tick(0.5f);
            Check(completions == 0);
            recipe.Tick(0.5f);
            Check(recipe.Step == CoffeeStep.Complete && completions == 1);
            recipe.Tick(100f);
            recipe.PourWater(100f);
            recipe.Grind(100f);
            Check(!recipe.StartHeating() && !recipe.LoadBeans());
            Check(completions == 1);
        });
        Run("完整状态顺序一致，新一轮没有旧进度", () =>
        {
            var recipe = NewRecipe();
            var steps = new List<CoffeeStep>();
            recipe.StepChanged += steps.Add;
            recipe.StartHeating(); recipe.Tick(2f); recipe.LoadBeans(); recipe.Grind(2f);
            recipe.PlaceDripper(); recipe.PlaceFilter(); recipe.StartPouringGrounds();
            recipe.Tick(1f); recipe.PourWater(4f); recipe.Tick(1f);
            var expected = new[] { CoffeeStep.LoadBeans, CoffeeStep.GrindBeans,
                CoffeeStep.PlaceDripper, CoffeeStep.PlaceFilter, CoffeeStep.PourGrounds,
                CoffeeStep.PourWater, CoffeeStep.Dripping, CoffeeStep.Complete };
            Check(steps.Count == expected.Length);
            for (int i = 0; i < expected.Length; i++) Check(steps[i] == expected[i]);
            var next = NewRecipe();
            Check(next.Step == CoffeeStep.HeatWater && next.Progress == 0f && !next.IsProcessing);
        });
        Run("零、负数和无效时间不能推进或污染进度", () =>
        {
            var recipe = AtWater();
            foreach (float delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                recipe.PourWater(delta);
            Check(recipe.Step == CoffeeStep.PourWater && recipe.Progress == 0f);
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                bool threw = false;
                try { new CoffeeRecipe(invalid, 1f, 1f, 1f, 1f); }
                catch (ArgumentOutOfRangeException) { threw = true; }
                Check(threw);
            }
        });
        Console.WriteLine($"PASS: {_passed} coffee recipe behavior checks.");
    }

    private static CoffeeRecipe NewRecipe() => new CoffeeRecipe(2f, 2f, 1f, 4f, 1f);
    private static CoffeeRecipe AtGrind()
    {
        var recipe = NewRecipe();
        Check(recipe.StartHeating()); recipe.Tick(2f); Check(recipe.LoadBeans());
        return recipe;
    }
    private static CoffeeRecipe AtGrounds()
    {
        var recipe = AtGrind(); recipe.Grind(2f);
        Check(recipe.PlaceDripper()); Check(recipe.PlaceFilter());
        return recipe;
    }
    private static CoffeeRecipe AtWater()
    {
        var recipe = AtGrounds(); Check(recipe.StartPouringGrounds()); recipe.Tick(1f);
        return recipe;
    }
    private static void Run(string name, Action check)
    {
        check(); _passed++; Console.WriteLine("PASS " + name);
    }
    private static void Check(bool condition)
    {
        if (!condition) throw new Exception("Coffee recipe behavior assertion failed.");
    }
    private static void Near(float actual, float expected) => Check(Math.Abs(actual - expected) < 0.0001f);
}
