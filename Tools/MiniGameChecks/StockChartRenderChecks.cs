using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TapTapGameJam.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class StockChartRenderChecks
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("These checks need Play mode.");
        var results = new List<string>();
        Action<bool, string> check = (ok, message) =>
        { if (!ok) throw new InvalidOperationException(message); results.Add("PASS: " + message); };
        var root = new GameObject("StockChartOffscreenChecks", typeof(RectTransform), typeof(Canvas));
        try
        {
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            root.transform.position = new Vector3(100000, 100000, 100000);
            var go = new GameObject("Chart", typeof(RectTransform)); go.transform.SetParent(root.transform, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(900, 310);
            var chart = go.AddComponent<StockChart>(); chart.raycastTarget = false;
            chart.SetPriceFont(UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/MiniGameUI SDF.asset"));
            int wallet = 1000;
            var round = new StockRound(() => wallet, n => { wallet -= n; return true; }, n => wallet += n, new Forecasts());
            Action refresh = () =>
            {
                chart.SetPrices(round.Prices, round.Elapsed, round.CurvePrices, round.IsFinished);
                Canvas.ForceUpdateCanvases();
            };
            refresh();
            var plot = chart.GetComponentInChildren<StockPlotGraphic>();
            check(round.Prices.Count == 12 && round.CurvePrices.Count == 14 && chart.VisiblePriceLabelCount == 12,
                "twelve completed dots initialize with two unpublished precomputed prices");
            check(chart.canvasRenderer.GetMesh().vertexCount == 72 && plot.canvasRenderer.GetMesh().vertexCount > 600,
                "thirteen-position grid and tessellated cubic history render in the actual Canvas");
            var labels = chart.GetComponentsInChildren<TMP_Text>(true)
                .Where(label => label.name.StartsWith("PriceLabel_")).OrderBy(label => int.Parse(label.name.Substring(11))).ToArray();
            check(labels.Length == 12 && labels.All(label => label.text == "100.00" && !label.raycastTarget),
                "labels show only completed samples and do not reveal target one hundred twenty");
            float initialX = plot.canvasRenderer.GetMesh().vertices[0].x;
            round.Tick(0.125); refresh();
            float movedX = plot.canvasRenderer.GetMesh().vertices[0].x;
            check(movedX < initialX && chart.DrawingProgress == 0.25 && round.Prices.Count == 12,
                "known points move left while a partial curve grows without a new quotation dot");
            double firstTip = chart.DisplayedPrice;
            round.Tick(0.125); refresh();
            check(chart.DisplayedPrice > firstTip && chart.DisplayedPrice < 120 && chart.DrawingProgress == 0.5,
                "visible curve tip progresses on every frame within the half-second interval");
            round.Buy(); round.Sell();
            check(round.Balance == 1000 && round.LastTradePrice == 120 && wallet == 1000 && round.Profit == 0,
                "buy and sell while drawing twelve-to-thirteen execute at target quote one hundred twenty");
            round.Tick(0.2501); refresh();
            check(round.Prices.Count == 13 && labels.Last().text == "120.00" && round.Price == 140
                && round.CurvePrices.Count == 15, "completed quote is labeled while next target and following price advance");
            var samples = new List<float>(Enumerable.Repeat(100f, 12)); samples.Add(100); samples.Add(100000);
            var completed = new List<float>(samples.Take(12));
            chart.SetPrices(completed, 0, samples); Canvas.ForceUpdateCanvases();
            double stableMin = chart.AxisMinimum, stableMax = chart.AxisMaximum;
            chart.SetPrices(completed, 0.25, samples); Canvas.ForceUpdateCanvases();
            check(chart.AxisMinimum == stableMin && chart.AxisMaximum == stableMax,
                "reserved future shock neither draws a dot nor moves the buffered axis");
            check(chart.GetComponentsInChildren<TMP_Text>(true).Count(label => label.name.StartsWith("PriceLabel_")) == 12
                && plot.GetComponentInParent<RectMask2D>() != null,
                "the reusable twelve-label pool and clipped curve layer remain bounded");
            refresh(); round.Tick(18.5 - round.Elapsed); refresh();
            var finalVertices = plot.canvasRenderer.GetMesh().vertices;
            double finalMin = chart.AxisMinimum, finalMax = chart.AxisMaximum;
            round.Tick(1.5); refresh();
            check(finalVertices.SequenceEqual(plot.canvasRenderer.GetMesh().vertices)
                && finalMin == chart.AxisMinimum && finalMax == chart.AxisMaximum && chart.DrawingProgress == 0,
                "final quote freezes curve and both animated axis bounds through the result phase");
            check(round.Prices.Count == 49 && round.CurvePrices.Count == 50 && round.Price == round.ConfirmedPrice,
                "normal settlement ignores the unused fiftieth price");
            chart.SetPrices(null); Canvas.ForceUpdateCanvases();
            check(chart.VisiblePriceLabelCount == 0 && plot.canvasRenderer.GetMesh().vertexCount == 0,
                "closing clears the completed dots, partial stroke and labels");
            Directory.CreateDirectory("Temp/StockChartRenderChecks");
            File.WriteAllText("Temp/StockChartRenderChecks/report.txt", string.Join(Environment.NewLine, results));
            File.WriteAllText("Temp/StockChartRenderChecks/status.txt", "PASSED");
            return results.Count + " continuous curve Play rendering checks passed.";
        }
        finally { Object.DestroyImmediate(root); }
    }
    private sealed class Forecasts : IStockPriceGenerator
    {
        private int _draw;
        public float InitialPrice => 100;
        public float NextPrice(float current, float? ceiling)
        {
            int index = _draw++ - 11;
            return index < 0 ? 100 : 120 + index * 20;
        }
    }
}