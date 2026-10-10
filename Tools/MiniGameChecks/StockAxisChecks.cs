using System;
using TapTapGameJam.UI;

internal static class StockAxisChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("Axis holds within its safety buffer and expands smoothly only on approaching the edge", () =>
        {
            var axis = new StockAxisRange(); axis.Reset(93, 107, 100);
            Near(axis.Minimum, 90); Near(axis.Maximum, 110);
            double initialMin = axis.Minimum, initialMax = axis.Maximum;
            for (int step = 0; step < 20; step++) axis.Step(93, 107, 100, 0.01);
            Near(axis.Minimum, initialMin); Near(axis.Maximum, initialMax);
            axis.Step(93, 107.1, 107.1, 0.01);
            double target = axis.TargetMaximum, first = axis.Maximum;
            Check(target > initialMax && first > initialMax && first < target);
            axis.Step(93, 107.1, 107.1, 0.01);
            Check(axis.TargetMaximum == target && axis.Maximum > first);
            axis.Step(93, 107.1, 107.1, 0.18);
            Near(axis.Maximum, target);
        });
        run("Dynamic axis waits two stable seconds and then shrinks slowly without immediate oscillation", () =>
        {
            var axis = new StockAxisRange(); axis.Reset(50, 150, 100);
            double initialMin = axis.Minimum, initialMax = axis.Maximum;
            axis.Step(99, 101, 100, 1.9);
            Near(axis.Minimum, initialMin); Near(axis.Maximum, initialMax);
            axis.Step(99, 101, 100, 0.1);
            Check(axis.TargetMaximum < initialMax && axis.Maximum > axis.TargetMaximum && axis.Minimum > initialMin);
            axis.Step(99, 101, 100, 0.9);
            Near(axis.Maximum, axis.TargetMaximum);
            Near(axis.Minimum, axis.TargetMinimum);
            Check(axis.Maximum - axis.Minimum >= 10);
        });
        run("Fast shocks keep the tip visible while the axis catches up and a zero floor remains valid", () =>
        {
            var axis = new StockAxisRange(); axis.Reset(99, 101, 100);
            axis.Step(99, 1000, 1000, 0.001);
            Check(axis.Maximum > 1000 && axis.Minimum >= 0);
            axis.Step(0, 1000, 0, 0.001);
            Check(axis.Minimum == 0 && axis.Maximum > 0 && double.IsFinite(axis.Maximum));
            double targetMin = axis.TargetMinimum, targetMax = axis.TargetMaximum;
            axis.Step(0, 1000, 0, 0.01);
            Check(axis.TargetMinimum == targetMin && axis.TargetMaximum == targetMax);
        });
    }
    private static void Near(double actual, double expected) => Check(Math.Abs(actual - expected) < 0.00001);
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("Stock axis check failed"); }
}
