using System;
using System.Collections.Generic;
using TapTapGameJam.UI;

internal static class StockChartChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("Twelve completed dots and two private forecasts initialize without revealing future values", () =>
        {
            var prices = Preview(100); var samples = Ahead(prices, 120, 140);
            var view = new StockChartView(); view.SetFrame(prices, 0, samples);
            Check(view.PriceCount == 12 && StockChartView.TimeSlots == 13 && view.HasTarget);
            Near(view.HorizontalPosition(11), 12.0 / 13);
            Near(view.HorizontalPosition(12), 1);
            Check(!view.IsPointVisible(12) && !view.IsPointVisible(13));
            Near(view.TipPrice, 100);
            Check(view.AxisMinimum > 0 && view.AxisMaximum < 120);
        });
        run("Curve grows inside one quote interval while every confirmed dot stays unchanged", () =>
        {
            var prices = Preview(100); var samples = Ahead(prices, 120, 140);
            var view = new StockChartView(); view.SetFrame(prices, 0, samples);
            view.SetFrame(prices, 0.125, samples);
            double first = view.TipPrice, pointX = view.HorizontalPosition(11);
            view.SetFrame(prices, 0.25, samples);
            Check(view.TipPrice > first && view.TipPrice < 120 && view.PriceCount == 12);
            Near(view.SegmentProgress, 0.5);
            Near(pointX - view.HorizontalPosition(11), 0.25 / 13);
            Near(view.HorizontalPosition(view.TipIndex), 12.0 / 13);
            Check(prices.Count == 12 && prices[11] == 100);
        });
        run("Quote transition preserves curve position and a shared tangent at the completed point", () =>
        {
            var prices = Preview(100); var samples = Ahead(prices, 120, 140);
            var view = new StockChartView(); view.SetFrame(prices, 0, samples);
            view.SetFrame(prices, 0.4999, samples);
            double beforeX = view.HorizontalPosition(11), beforePrice = view.TipPrice;
            double previousShape = StockCurve.Evaluate(samples, 11, 0.4);
            prices.Add(samples[12]); samples.Add(150);
            view.SetFrame(prices, 0.5001, samples);
            Check(view.PriceCount == 13 && view.HasTarget && view.TipPrice > beforePrice);
            Near(beforeX - view.HorizontalPosition(11), 0.0004 / 13);
            Near(previousShape, StockCurve.Evaluate(samples, 11, 0.4));
            double left = (StockCurve.Evaluate(samples, 11, 1) - StockCurve.Evaluate(samples, 11, 0.99999)) / 0.00001;
            double right = (StockCurve.Evaluate(samples, 12, 0.00001) - StockCurve.Evaluate(samples, 12, 0)) / 0.00001;
            Check(Math.Abs(left - right) < 0.002);
        });
        run("Preserving cubic interpolation never overshoots endpoints or produces negative prices", () =>
        {
            var random = new Random(72);
            for (int trial = 0; trial < 500; trial++)
            {
                var samples = new List<float>();
                for (int point = 0; point < 15; point++) samples.Add(random.Next(0, 10000) / 100f);
                for (int point = 0; point < samples.Count - 1; point++)
                {
                    double previous = samples[point];
                    for (int step = 0; step <= 32; step++)
                    {
                        double price = StockCurve.Evaluate(samples, point, step / 32.0);
                        Check(price >= Math.Min(samples[point], samples[point + 1])
                            && price <= Math.Max(samples[point], samples[point + 1]));
                        Check(samples[point] <= samples[point + 1] ? price >= previous - 0.00001 : price <= previous + 0.00001);
                        previous = price;
                    }
                }
            }
            var reversal = new List<float> { 90, 100, 80 };
            Check(StockCurve.Tangent(reversal, 1) == 0);
        });
        run("Scrolling and continuous drawing are consistent at thirty and one hundred twenty FPS", () =>
        {
            foreach (int fps in new[] { 30, 120 })
            {
                int wallet = 1000; int draw = 0;
                var generator = new SequenceGenerator(() => 100 + (++draw));
                var round = new StockRound(() => wallet, n => true, n => wallet += n, generator);
                var view = new StockChartView();
                view.SetFrame(round.Prices, 0, round.CurvePrices);
                for (int frame = 1; frame <= fps; frame++)
                {
                    round.Tick(1.0 / fps);
                    view.SetFrame(round.Prices, round.Elapsed, round.CurvePrices, round.IsFinished);
                    Near(view.DisplayStart, round.Elapsed * 2);
                    Near(view.HorizontalPosition(view.TipIndex), 12.0 / 13);
                    Check(view.PriceCount == round.Prices.Count);
                }
            }
        });
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
        run("Reserved future prices never move the axis before their curve becomes visible", () =>
        {
            var prices = Preview(100); var samples = Ahead(prices, 100, 100000);
            var view = new StockChartView(); view.SetFrame(prices, 0, samples);
            double min = view.AxisMinimum, max = view.AxisMaximum;
            view.SetFrame(prices, 0.25, samples);
            Near(view.TipPrice, 100); Near(view.AxisMinimum, min); Near(view.AxisMaximum, max);
        });
        run("Result freezes the curve and an unfinished axis animation, while reopening clears history", () =>
        {
            var prices = Preview(100); var samples = Ahead(prices, 110, 120);
            var view = new StockChartView(); view.SetFrame(prices, 0, samples);
            while (prices.Count < StockRound.MaxPoints - 1) prices.Add(100);
            samples.Clear(); samples.AddRange(prices); samples.Add(200); samples.Add(900);
            view.SetFrame(prices, 18.4, samples);
            prices.Add(200);
            view.SetFrame(prices, 18.5, samples, true);
            double min = view.AxisMinimum, max = view.AxisMaximum, tip = view.TipPrice;
            foreach (double time in new[] { 18.6, 19.5, 20 })
            {
                view.SetFrame(prices, time, samples, true);
                Near(view.DisplayStart, 37); Near(view.AxisMinimum, min); Near(view.AxisMaximum, max); Near(view.TipPrice, tip);
                Check(!view.HasTarget && view.SegmentProgress == 0 && tip == 200);
            }
            view.SetFrame(null, 0);
            Check(view.PriceCount == 0);
            view.SetFrame(Preview(20), 0, Ahead(Preview(20), 21, 22));
            Check(view.AxisMaximum < 30 && view.DisplayStart == 0);
        });
        run("Zero and maximum floating prices produce finite axis coordinates and curve values", () =>
        {
            var prices = Preview(0); var view = new StockChartView();
            view.SetFrame(prices, 0, Ahead(prices, 0, 0));
            Check(view.AxisMinimum == 0 && view.AxisMaximum > 0 && double.IsFinite(view.VerticalPosition(0)));
            prices = Preview(float.MaxValue);
            view.SetFrame(prices, 0, Ahead(prices, float.MaxValue, float.MaxValue));
            Check(double.IsFinite(view.AxisMaximum) && double.IsFinite(view.VerticalPosition(float.MaxValue)));
            Near(view.TipPrice, float.MaxValue);
        });
    }
    private static List<float> Preview(float price)
    {
        var prices = new List<float>();
        for (int i = 0; i < StockRound.VisiblePoints; i++) prices.Add(price);
        return prices;
    }
    private static List<float> Ahead(List<float> prices, float target, float following)
    {
        var samples = new List<float>(prices); samples.Add(target); samples.Add(following); return samples;
    }
    private static void Near(double actual, double expected) => Check(Math.Abs(actual - expected) < 0.00001);
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("Stock curve/axis check failed"); }
    private sealed class SequenceGenerator : IStockPriceGenerator
    {
        private readonly Func<float> _next;
        public SequenceGenerator(Func<float> next) { _next = next; }
        public float InitialPrice => 100;
        public float NextPrice(float price, float? ceiling) => _next();
    }
}