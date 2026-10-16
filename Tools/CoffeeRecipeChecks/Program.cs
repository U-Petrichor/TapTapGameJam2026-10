using System;
using TapTapGameJam.Coffee;

internal static class Program
{
    private static int _passed;
    private static void Main()
    {
        Run("放豆前不能研磨、放粉或注水，放豆只接受一次", () =>
        {
            var recipe = NewRecipe();
            Check(recipe.Step == CoffeeStep.LoadBeans && recipe.Progress == 0);
            Check(!recipe.StartGrinding() && !recipe.PlaceGrounds() && !recipe.StartPouringWater());
            recipe.Grind(10); recipe.PourWater(10);
            Check(recipe.Step == CoffeeStep.LoadBeans);
            Check(recipe.LoadBeans() && !recipe.LoadBeans() && recipe.Step == CoffeeStep.Grind);
        });
        Run("研磨必须长按，松手或离开区域不会自动完成，恢复后保留进度", () =>
        {
            var recipe = NewRecipe(); recipe.LoadBeans();
            Check(recipe.StartGrinding());
            recipe.Grind(1); Near(recipe.Progress, 1f / 1.5f);
            recipe.StopGrinding(); recipe.Grind(10);
            Near(recipe.Progress, 1f / 1.5f);
            Check(!recipe.IsProcessing && recipe.StartGrinding());
            recipe.Grind(0.5f);
            Check(recipe.Step == CoffeeStep.PlaceGrounds && recipe.Progress == 0);
        });
        Run("粉杯只能在研磨完成后放置，注水步骤不能重复放粉", () =>
        {
            var recipe = NewRecipe(); Check(!recipe.PlaceGrounds()); recipe.LoadBeans();
            Check(!recipe.PlaceGrounds() && !recipe.StartPouringWater());
            recipe.StartGrinding(); recipe.Grind(1.5f);
            Check(recipe.Step == CoffeeStep.PlaceGrounds && !recipe.StartPouringWater());
            Check(recipe.PlaceGrounds() && recipe.Step == CoffeeStep.PourWater && !recipe.PlaceGrounds());
        });
        Run("注水必须持续2秒，停止后保留进度，不自动完成", () =>
        {
            var recipe = ReadyToPour();
            Check(recipe.StartPouringWater() && !recipe.StartPouringWater());
            recipe.PourWater(1); Near(recipe.Progress, 0.5f);
            recipe.StopPouringWater(); recipe.PourWater(10); Near(recipe.Progress, 0.5f);
            recipe.StartPouringWater(); recipe.PourWater(1);
            Check(recipe.Step == CoffeeStep.Complete && !recipe.IsProcessing);
        });
        Run("完整流程按顺序通知五阶段，完成只触发一次", () =>
        {
            var recipe = NewRecipe(); int complete = 0;
            var steps = new System.Collections.Generic.List<CoffeeStep> { recipe.Step };
            recipe.StepChanged += step => { steps.Add(step); if (step == CoffeeStep.Complete) complete++; };
            recipe.LoadBeans(); recipe.StartGrinding(); recipe.Grind(2); recipe.PlaceGrounds();
            recipe.StartPouringWater(); recipe.PourWater(3); recipe.PourWater(3);
            Check(complete == 1 && steps.Count == 5);
            for (int i = 0; i < 5; i++) Check((int)steps[i] == i);
            Check(!recipe.LoadBeans() && !recipe.StartGrinding() && !recipe.PlaceGrounds() && !recipe.StartPouringWater());
            Check(NewRecipe().Step == CoffeeStep.LoadBeans);
        });
        Run("取消后旧配方不再前进，重新开始从放豆计数", () =>
        {
            var recipe = ReadyToPour(); int completed = 0;
            recipe.StepChanged += _ => completed++;
            recipe.StartPouringWater(); recipe.PourWater(1); recipe.Stop(); recipe.PourWater(5);
            Check(!recipe.IsProcessing && !recipe.StartPouringWater() && completed == 0);
            Near(recipe.Progress, 0.5f);
            var next = NewRecipe(); Check(next.Step == CoffeeStep.LoadBeans && next.Progress == 0);
        });
        Run("非法时间步不增加研磨或注水进度", () =>
        {
            var recipe = NewRecipe(); recipe.LoadBeans(); recipe.StartGrinding();
            foreach (float dt in new[] { -1f, 0f, float.NaN, float.PositiveInfinity }) recipe.Grind(dt);
            Near(recipe.Progress, 0);
            recipe.Grind(1.5f); recipe.PlaceGrounds(); recipe.StartPouringWater();
            foreach (float dt in new[] { -1f, 0f, float.NaN, float.PositiveInfinity }) recipe.PourWater(dt);
            Near(recipe.Progress, 0);
        });
        Console.WriteLine($"PASS: {_passed} coffee recipe behavior checks.");
    }
    private static CoffeeRecipe ReadyToPour()
    {
        var recipe = NewRecipe(); recipe.LoadBeans(); recipe.StartGrinding(); recipe.Grind(1.5f); recipe.PlaceGrounds(); return recipe;
    }
    private static CoffeeRecipe NewRecipe() => new CoffeeRecipe(1.5f, 2f);
    private static void Run(string name, Action check) { check(); _passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool value) { if (!value) throw new Exception("Coffee recipe assertion failed."); }
    private static void Near(float actual, float expected) => Check(Math.Abs(actual - expected) < 0.0001f);
}
