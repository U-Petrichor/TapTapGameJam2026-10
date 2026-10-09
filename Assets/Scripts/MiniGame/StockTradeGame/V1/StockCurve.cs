using System;
using System.Collections.Generic;

namespace TapTapGameJam.UI
{
    // 等距采样点的保形三次 Hermite 插值。局部极值的斜率为零，避免额外涨跌及负价。
    public static class StockCurve
    {
        public static double Tangent(IReadOnlyList<float> prices, int index)
        {
            if (prices.Count < 2) return 0;
            if (index == 0) return (double)prices[1] - prices[0];
            if (index == prices.Count - 1) return (double)prices[index] - prices[index - 1];
            double left = (double)prices[index] - prices[index - 1];
            double right = (double)prices[index + 1] - prices[index];
            if (left == 0 || right == 0 || Math.Sign(left) != Math.Sign(right)) return 0;
            return 2 * left * right / (left + right);
        }
        public static double Evaluate(IReadOnlyList<float> prices, int segment, double progress)
        {
            if (progress <= 0) return prices[segment];
            if (progress >= 1) return prices[segment + 1];
            double a = prices[segment], b = prices[segment + 1];
            double t = progress, square = t * t, cube = square * t;
            double value = (2 * cube - 3 * square + 1) * a
                + (cube - 2 * square + t) * Tangent(prices, segment)
                + (-2 * cube + 3 * square) * b
                + (cube - square) * Tangent(prices, segment + 1);
            return Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), value));
        }
    }
}