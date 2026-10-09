using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in Play mode via unity run_script. Uses the real panel and Unity Update;
// a temporary player owns the test wallet, so gameplay funds are not changed.
public static class StockPointPlayChecks
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Report = new List<string>();
    private static readonly Dictionary<TapTapGameJam.Core.GamePhaseManager, bool> Phases =
        new Dictionary<TapTapGameJam.Core.GamePhaseManager, bool>();
    private static readonly object Owner = new object();
    private static string Output => Path.GetFullPath("Temp/StockPointPlayChecks");
    private static IEnumerator _routine;
    private static MiniGamePanelUI _panel;
    private static GameObject _playerObject;
    private static bool _background;
    private static int _lastFrame;
    private static double _deadline;

    public static string Main()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        if (_routine != null) throw new InvalidOperationException("Checks are already running.");
        _panel = Object.FindFirstObjectByType<MiniGamePanelUI>();
        if (_panel == null || Current() != null)
            throw new InvalidOperationException("The minigame panel must exist and be closed.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "status.txt"), "RUNNING");
        Report.Clear();
        _background = Application.runInBackground;
        Application.runInBackground = true;
        foreach (var phase in Object.FindObjectsByType<TapTapGameJam.Core.GamePhaseManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Phases[phase] = phase.CouldWork;
            phase.CouldWork = false;
        }
        _lastFrame = -1;
        _deadline = EditorApplication.timeSinceStartup + 60;
        _routine = Checks();
        EditorApplication.update += Tick;
        return "Started real panel / Update / rendering checks; output: " + Output;
    }

    private static void Tick()
    {
        try
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup > _deadline)
                throw new InvalidOperationException("Play checks interrupted or timed out.");
            EditorApplication.QueuePlayerLoopUpdate();
            if (_lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;
            if (_routine.MoveNext()) return;
            Finish("PASSED");
        }
        catch (Exception ex)
        {
            Report.Add("FAILED: " + ex);
            Finish("FAILED");
        }
    }

    private static IEnumerator Checks()
    {
        var point = _panel.GetComponentInChildren<StockPointGame>(true);
        var continuous = _panel.GetComponentInChildren<StockGame>(true);
        Check(point != null && continuous != null, "both stock versions exist");
        // Exercise the same default initializer used by MiniGamePanelUI.Awake.
        point.enabled = false;
        MiniGameDefaultSetup.Ensure(_panel);
        Check(Registered() == continuous && continuous.enabled, "continuous initialization registers its enabled game");
        point.enabled = true;
        MiniGameDefaultSetup.Ensure(_panel);
        Check(Registered() == point && point.enabled && !continuous.enabled,
            "point initialization keeps point game registered rather than overriding it");
        var chart = _panel.GetComponentInChildren<StockPointChart>(true);
        var oldChart = _panel.GetComponentInChildren<StockChart>(true);

        _playerObject = new GameObject("StockPointCheckPlayer");
        _playerObject.SetActive(false);
        var player = _playerObject.AddComponent<PlayerBasic>();
        var wallet = _playerObject.AddComponent<PlayerResources>();
        typeof(PlayerBasic).GetField("<PlayerResources>k__BackingField", Private).SetValue(player, wallet);
        typeof(PlayerResources).GetField("<Coins>k__BackingField", Private).SetValue(wallet, 1000);
        bool started = false, completed = false, successful = false;
        EventPipeline.Publish(new EventOpenMiniGamePanel(3,
            success => { completed = true; successful = success; }, player, Owner, () => started = true));
        Check(started && Current() == point && point.isActiveAndEnabled, "real panel opens the enabled point game");
        var round = (StockRound)typeof(StockPointGame).GetField("_round", Private).GetValue(point);
        Check(round.Prices.Count == 1 && chart.PriceCount == 1, "opening shows one confirmed quote");
        Check(!oldChart.gameObject.activeInHierarchy && chart.gameObject.activeInHierarchy,
            "only the selected chart hierarchy is visible");
        Canvas.ForceUpdateCanvases();
        var initialPlot = chart.GetComponentInChildren<StockPointPlotGraphic>();
        Check(initialPlot != null && initialPlot.canvasRenderer.GetMesh().vertexCount == 18,
            "unconfirmed target has no visible dot, guide line or label");
        while (round.Elapsed < 1.1) yield return null;
        Check(round.Prices.Count >= 3 && chart.PriceCount == round.Prices.Count,
            "actual Unity Update refreshes quotes and chart every half second");
        Canvas.ForceUpdateCanvases();
        Check(chart.canvasRenderer.GetMesh().vertexCount == 20, "actual Canvas renders five horizontal grid lines");
        var plot = chart.GetComponentInChildren<StockPointPlotGraphic>();
        Check(plot != null && plot.canvasRenderer.GetMesh().vertexCount > 0, "quote dots and connecting lines have visible geometry");
        while (round.Elapsed < 5.5) yield return null;
        double originalX = chart.HorizontalPosition(5);
        while (round.Elapsed < 7) yield return null;
        Check(chart.PriceCount > 13 && chart.HorizontalPosition(5) < originalX,
            "past thirteen points, an existing point moves left as the X axis compresses");
        Check(Enumerable.Range(0, chart.PriceCount).All(i => chart.HorizontalPosition(i) >= 0 && chart.HorizontalPosition(i) <= 1),
            "all historical quotes remain within the X viewport");
        Check(chart.HasTarget && Math.Abs(chart.HorizontalPosition(chart.PriceCount) - 1) < 0.00001,
            "one pending quote stays on the right edge");
        while (!round.IsFinished) yield return null;
        Check(round.Prices.Count == 38 && chart.PriceCount == 38 && !chart.HasTarget,
            "at 18.5 seconds all 38 confirmed points remain and the pending quote disappears");
        while (round.Elapsed < 19.3) yield return null;
        Check(chart.PriceCount == 38, "result interval does not append additional quotes");
        while (!completed) yield return null;
        Check(successful && Current() == null && wallet.Coins == 1000,
            "real panel completes and closes at twenty seconds; test wallet settles once without a trade");
    }

    private static MiniGameContent Current() => typeof(MiniGamePanelUI)
        .GetField("_currentContent", Private).GetValue(_panel) as MiniGameContent;
    private static MiniGameContent Registered() => (MiniGameContent)typeof(MiniGamePanelUI)
        .GetMethod("FindContent", Private).Invoke(_panel, new object[] { 3 });
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Report.Add("PASS: " + message);
        File.WriteAllLines(Path.Combine(Output, "report.txt"), Report);
    }
    private static void Finish(string status)
    {
        EditorApplication.update -= Tick;
        _routine = null;
        EventPipeline.Publish(new EventRequestCloseMiniGamePanel(Owner));
        if (_playerObject != null) Object.Destroy(_playerObject);
        foreach (var pair in Phases) if (pair.Key != null) pair.Key.CouldWork = pair.Value;
        Phases.Clear();
        Application.runInBackground = _background;
        File.WriteAllLines(Path.Combine(Output, "report.txt"), Report);
        File.WriteAllText(Path.Combine(Output, "status.txt"), status);
    }
}

