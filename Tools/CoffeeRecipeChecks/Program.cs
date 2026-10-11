using System;
using TapTapGameJam.Coffee;

internal static class Program
{
    private static int _passed;

    private static void Main()
    {
        Run("研磨必须长按 1.5 秒，松手不会自动完成", () =>
        {
            var recipe = NewRecipe();
            Check(recipe.Step == CoffeeStep.Grind && recipe.StartGrinding());
            recipe.Grind(1f); Near(recipe.Progress, 1f / 1.5f);
            recipe.StopGrinding();
            Check(!recipe.IsProcessing && recipe.StartGrinding());
            recipe.Grind(0.5f);
            Check(recipe.Step == CoffeeStep.PlaceGrounds);
        });
        Run("粉杯只能在研磨完成后放到杯子上", () =>
        {
            var recipe = NewRecipe();
            Check(!recipe.PlaceGrounds());
            recipe.StartGrinding(); recipe.Grind(1.5f);
            Check(recipe.Step == CoffeeStep.PlaceGrounds && recipe.PlaceGrounds());
            Check(recipe.Step == CoffeeStep.PourWater && !recipe.PlaceGrounds());
        });
        Run("热水必须在正确步骤开始，并持续 2 秒完成", () =>
        {
            var recipe = NewRecipe(); recipe.StartGrinding(); recipe.Grind(1.5f); recipe.PlaceGrounds();
            Check(recipe.StartPouringWater() && !recipe.StartPouringWater());
            recipe.PourWater(1f); Near(recipe.Progress, 0.5f);
            recipe.StopPouringWater(); recipe.PourWater(1f); Near(recipe.Progress, 0.5f);
            recipe.StartPouringWater(); recipe.PourWater(1f);
            Check(recipe.Step == CoffeeStep.Complete && !recipe.IsProcessing);
        });
        Run("完成事件只触发一次，新配方从研磨开始", () =>
        {
            var recipe = NewRecipe(); int complete = 0;
            recipe.StepChanged += step => { if (step == CoffeeStep.Complete) complete++; };
            recipe.StartGrinding(); recipe.Grind(1.5f); recipe.PlaceGrounds();
            recipe.StartPouringWater(); recipe.PourWater(2f); recipe.PourWater(2f);
            Check(complete == 1 && recipe.Step == CoffeeStep.Complete);
            var next = NewRecipe(); Check(next.Step == CoffeeStep.Grind && next.Progress == 0f);
        });
        Console.WriteLine($"PASS: {_passed} coffee recipe behavior checks.");
    }

    private static CoffeeRecipe NewRecipe() => new CoffeeRecipe(1.5f, 2f);
    private static void Run(string name, Action check) { check(); _passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool value) { if (!value) throw new Exception("Coffee recipe assertion failed."); }
    private static void Near(float actual, float expected) => Check(Math.Abs(actual - expected) < 0.0001f);
}
