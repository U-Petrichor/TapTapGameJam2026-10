using System;
using System.Collections.Generic;
using TapTapGameJam.UI;

internal static class StockChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("Stock opens with one quotation and two immutable affordable internal forecasts", () =>
        {
            int wallet = 100;
            var generator = new SequencePrices(63.75f, 200, 200, 200);
            var round = Round(() => wallet, n => wallet -= n, n => wallet += n, generator);
            Check(round.Prices.Count == 1 && round.CurvePrices.Count == 3);
            Check(round.ConfirmedPrice == 63.75f && round.Price == 100);
            Check(round.CurvePrices[1] == 100 && round.CurvePrices[2] == 100);
            int draws = generator.Draws;
            Check(round.Buy() && !round.Buy());
            Check(round.Price == 100 && generator.Draws == draws);
            round.Tick(0.5);
            Check(round.ConfirmedPrice == 100 && round.Price == 100 && round.CurvePrices[3] == 200);
            round.Tick(0.5);
            Check(round.Price == 200 && !generator.LastCeiling.HasValue && wallet == 100);
        });
        run("Buying and selling before the second quotation use the same target price with no instant spread", () =>
        {
            int wallet = 100;
            var round = Round(() => wallet, n => wallet -= n, n => wallet += n,
                new SequencePrices(100, 80, 40, 90));
            Check(round.ConfirmedPrice == 100 && round.Price == 80);
            round.Tick(0.25); Check(round.Prices.Count == 1 && round.Price == 80);
            for (int i = 0; i < 10; i++)
            {
                Check(round.Buy() && round.Balance == 20 && !round.Buy());
                Check(round.Sell() && round.Balance == 100 && !round.Sell() && round.Profit == 0);
            }
            round.Buy(); round.Tick(0.25);
            Check(round.ConfirmedPrice == 80 && round.Price == 40 && round.Sell());
            Near((float)round.Profit, -40);
            round.Close(); Check(wallet == 60);
        });
        run("Repeated one-share trades use advancing target quotes and round net profit only once", () =>
        {
            int wallet = 100, credits = 0;
            var round = Round(() => wallet, n => wallet -= n, n => { wallet += n; credits++; },
                new SequencePrices(10, 10.26f, 10.52f, 10.78f));
            Check(round.Buy() && !round.Buy());
            round.Tick(0.5); Check(round.Sell() && !round.Sell());
            Check(round.Buy()); round.Tick(0.5); Check(round.Sell());
            Check(wallet == 100 && credits == 0 && Math.Abs(round.Profit - 0.52) < 0.0001);
            round.Close(); round.Close();
            Check(wallet == 101 && credits == 1 && round.SettledProfit == 1);
            Check(!round.Buy() && !round.Sell());
        });
        run("At 18.5 seconds stock settles final completed quote rather than the unused forecast", () =>
        {
            int wallet = 100, credits = 0;
            var prices = new float[38]; Array.Fill(prices, 75);
            prices[0] = 50; prices[36] = 80; prices[37] = 900;
            var generator = new SequencePrices(50, prices);
            var round = Round(() => wallet, n => wallet -= n, n => { wallet += n; credits++; }, generator);
            Check(round.Buy());
            round.Tick(0.499); Check(round.Prices.Count == 1 && round.Price == 50);
            round.Tick(0.001); Check(round.Prices.Count == 2 && round.Price == 75);
            round.Tick(17.999);
            Check(round.Prices.Count == 37 && !round.IsFinished && round.Price == 80 && wallet == 100);
            round.Tick(0.001);
            Check(round.Prices.Count == 38 && round.CurvePrices.Count == 39 && round.IsFinished && !round.IsComplete);
            Check(round.Price == 80 && round.CurvePrices[38] == 900 && wallet == 130 && credits == 1);
            int draws = generator.Draws;
            round.Tick(1.499); Check(!round.IsComplete);
            round.Tick(0.001); Check(round.IsComplete && round.Elapsed == 20);
            round.Tick(100); round.Close();
            Check(generator.Draws == draws && wallet == 130 && credits == 1);
        });
        run("Early close while drawing liquidates at the same target price, not the last visible quote", () =>
        {
            int wallet = 100;
            var round = Round(() => wallet, n => wallet -= n, n => wallet += n, new SequencePrices(100, 80, 40));
            round.Tick(0.25); round.Buy(); round.Close(); round.Close();
            Check(round.Price == 80 && round.ConfirmedPrice == 100 && round.Profit == 0 && wallet == 100);
            round = Round(() => wallet, n => wallet -= n, n => wallet += n, new SequencePrices(100, 80, 40));
            round.Buy(); round.Tick(0.6); round.Close(); round.Close();
            Check(round.Price == 40 && round.ConfirmedPrice == 80 && round.Shares == 0 && wallet == 60);
        });
        run("Stock handles lag and invalid frame times without extra result quotes", () =>
        {
            int wallet = 100;
            var round = Round(() => wallet, n => wallet -= n, n => wallet += n, new SequencePrices(60, 80));
            round.Tick(double.NaN); round.Tick(double.PositiveInfinity); round.Tick(-1);
            Check(round.Elapsed == 0 && round.Prices.Count == 1);
            round.Tick(100);
            Check(round.Prices.Count == 38 && round.CurvePrices.Count == 39 && round.IsComplete && wallet == 100);
        });
        run("Settlement keeps external earnings and respects zero balance and integer wallet limits", () =>
        {
            int wallet = 100;
            var round = Round(() => wallet, n => wallet -= n, n => wallet += n, new SequencePrices(50, 50, 75));
            round.Buy(); round.Tick(0.5); wallet += 17; round.Close(); Check(wallet == 142);
            wallet = int.MaxValue;
            round = Round(() => wallet, n => wallet -= n, n => wallet += n, new SequencePrices(50, 50, 100));
            round.Buy(); round.Tick(0.5); round.Close(); Check(wallet == int.MaxValue && round.SettledProfit == 0);
            round = Round(() => wallet, n => wallet -= n, n => wallet += n, new SequencePrices(float.MaxValue));
            Check((double)round.Price <= wallet && round.CanBuy && round.Buy()); round.Close();
            Check(wallet == int.MaxValue);
            wallet = 0;
            round = Round(() => wallet, n => wallet -= n, n => wallet += n, new SequencePrices(0, 0));
            Check(!round.CanBuy && !round.Buy()); round.Close(); Check(wallet == 0);
        });
        run("Stock declines stay positive and halve near zero: 10 to 5 to 2.5", () =>
        {
            var settings = new StockPriceSettings { ShockProbability = 0, TrendVariation = 0,
                PullbackProbability = 0, GentleMinRatio = 0.1f, GentleMaxRatio = 0.1f };
            var random = new Queue<double>(new[] { 0.0, 0.5, 0.0 });
            var generator = new StockPriceGenerator(200, () => random.Count > 0 ? random.Dequeue() : 0.5, settings);
            float price = generator.InitialPrice;
            Check(price == 100);
            for (int i = 0; i < 9; i++) price = generator.NextPrice(price, null);
            Near(price, 10); price = generator.NextPrice(price, null); Near(price, 5);
            price = generator.NextPrice(price, null); Near(price, 2.5f);
            price = generator.NextPrice(price, null); Near(price, 1.25f);
        });
        run("Ordinary stock variation uses a stable baseline instead of compounding into flatness", () =>
        {
            var random = new Queue<double>(new[] { 0.0, 0.5, 0.9, 0.5, 0.0, 0.5, 1.0 });
            var generator = new StockPriceGenerator(200, () => random.Count > 0 ? random.Dequeue() : 0.5,
                new StockPriceSettings { ShockProbability = 0, TrendVariation = 0.2f,
                    PullbackProbability = 0, GentleMinRatio = 0.1f, GentleMaxRatio = 0.1f });
            float first = generator.NextPrice(100, null);
            Near(first, 108);
            float second = generator.NextPrice(first, null);
            Near(second, 120); // 仍围绕趋势步幅 10 波动，不将上一步的随机变化继续累乘。
        });
        run("Ordinary stock paths have pullbacks while retaining a readable overall trend", () =>
        {
            int readable = 0, withPullbacks = 0;
            for (int seed = 0; seed < 500; seed++)
            {
                var random = new Random(seed);
                var generator = new StockPriceGenerator(1000, random.NextDouble,
                    new StockPriceSettings { ShockProbability = 0 });
                float start = generator.InitialPrice;
                float price = generator.NextPrice(start, null);
                bool upward = price > start, pullback = false;
                double travel = Math.Abs(price - start);
                for (int step = 1; step < StockRound.RefreshedPoints; step++)
                {
                    float next = generator.NextPrice(price, null);
                    Check(float.IsFinite(next) && next > 0);
                    if ((next > price) != upward && next != price) pullback = true;
                    travel += Math.Abs(next - price);
                    price = next;
                }
                if ((price > start) == upward && Math.Abs(price - start) > travel * 0.2) readable++;
                if (pullback) withPullbacks++;
            }
            Check(readable >= 475 && withPullbacks >= 475);
        });
        run("Stock shocks change thirty to seventy percent of current price then force a gentle step", () =>
        {
            foreach (double magnitude in new[] { 0.0, 0.5, 1.0 })
            foreach (double direction in new[] { 0.0, 1.0 })
            {
                var random = new Queue<double>(new[] { 0.5, 0.5, 0.9, 0.05, magnitude, direction, 0.5, 0.9, 0.5 });
                var generator = new StockPriceGenerator(100, () => random.Count > 0 ? random.Dequeue() : 0.05);
                float shock = generator.NextPrice(100, null);
                float ratio = 0.3f + 0.4f * (float)magnitude;
                Near(shock, 100 * (direction < 0.5 ? 1 - ratio : 1 + ratio));
                float gentle = generator.NextPrice(shock, null);
                Check(gentle > shock && gentle - shock < shock * 0.12f);
            }
        });
        run("Stock ceiling resets an upward trend downward instead of locking at capital", () =>
        {
            var random = new Queue<double>(new[] { 0.0, 0.5, 0.9 });
            var generator = new StockPriceGenerator(100, () => random.Count > 0 ? random.Dequeue() : 0.5,
                new StockPriceSettings { ShockProbability = 0 });
            float capped = generator.NextPrice(100, 100);
            Check(capped == 100 && generator.NextPrice(capped, 100) < 100);
        });
        run("Five hundred seeded stock rounds preserve affordability, funds and single settlement", () =>
        {
            var random = new Random(41);
            for (int game = 0; game < 500; game++)
            {
                int wallet = random.Next(1, 10000), start = wallet, mutations = 0;
                var generator = new StockPriceGenerator(wallet, random.NextDouble);
                var round = Round(() => wallet, n => { wallet -= n; mutations++; },
                    n => { wallet += n; mutations++; }, generator);
                Check(round.Prices[0] >= start * 0.5f && round.Prices[0] <= start * 0.9f);
                bool bought = false;
                for (int point = 0; point < 37; point++)
                {
                    Check(float.IsFinite(round.Price) && round.Price >= 0 && round.Balance >= 0);
                    if (!bought) Check(round.Price <= start && round.CanBuy);
                    double equity = round.Profit;
                    if (random.Next(2) == 0) bought |= round.Buy(); else round.Sell();
                    Check(Math.Abs(round.Profit - equity) < 0.00001 && round.Shares >= 0 && round.Shares <= 1);
                    round.Tick(0.5);
                }
                Check(round.Prices.Count == 38 && round.IsFinished && round.Shares == 0 && mutations <= 1);
                Check(wallet == start + round.SettledProfit && wallet >= 0);
                int ending = wallet;
                round.Tick(1.5); round.Close(); round.Close();
                Check(round.IsComplete && wallet == ending);
            }
        });
    }
    private static StockRound Round(Func<int> balance, Action<int> debit, Action<int> credit, IStockPriceGenerator generator) =>
        new StockRound(balance, n => { if (n <= 0 || balance() < n) return false; debit(n); return true; }, credit, generator);
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("Stock check failed"); }
    private static void Near(float value, float expected) => Check(Math.Abs(value - expected) < 0.0001f);
    private sealed class SequencePrices : IStockPriceGenerator
    {
        public float InitialPrice { get; }
        public int Draws { get; private set; }
        public float? LastCeiling { get; private set; }
        private readonly float[] _next;
        public SequencePrices(float initial, params float[] next) { InitialPrice = initial; _next = next; }
        public float NextPrice(float current, float? ceiling)
        {
            LastCeiling = ceiling;
            int index = Draws++;
            return index < 0 || _next.Length == 0 ? InitialPrice : _next[Math.Min(index, _next.Length - 1)];
        }
    }
}
