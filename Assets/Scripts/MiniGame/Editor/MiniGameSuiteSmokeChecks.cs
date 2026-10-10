using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TapTapGameJam.Coffee;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.MiniGame;
using TapTapGameJam.Player;
using TapTapGameJam.UI;
using TapTapGameJam.Types;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 用户显式调用菜单或 Run 后才进入 Play。测试改动随退出 Play 撤销。
[InitializeOnLoad]
public static class MiniGameSuiteSmokeChecks
{
    private const string PendingKey = "MiniGameSuiteSmokeChecks.Pending";
    private const string StockOnlyKey = "MiniGameSuiteSmokeChecks.StockOnly";
    private static IEnumerator _routine;
    private static readonly StringBuilder Report = new StringBuilder();
    private static Mouse _mouse;
    private static Mouse _previousMouse;
    private static Keyboard _keyboard;
    private static Keyboard _previousKeyboard;
    private static MiniGamePanelUI _panel;
    private static PlayerBasic _player;
    private static int _lastFrame = -1;
    private static bool _savedBackground;
    private static string Output => Path.GetFullPath(SessionState.GetBool(StockOnlyKey, false)
        ? "Temp/StockPointGameSmokeChecks" : "Temp/MiniGameSmokeChecks");
    static MiniGameSuiteSmokeChecks() { EditorApplication.playModeStateChanged += StateChanged; }

    [MenuItem("Tools/MiniGames/运行小游戏Play模式检查")]
    public static void Run()
    { BeginChecks(false); }
    [MenuItem("Tools/MiniGames/运行炒股Play模式检查")]
    public static void RunStocks()
    { BeginChecks(true); }
    private static void BeginChecks(bool stockOnly)
    {
        if (EditorApplication.isPlaying || EditorUtility.scriptCompilationFailed)
            throw new InvalidOperationException("请先退出 Play 并修复编译错误。");
        SessionState.SetBool(StockOnlyKey, stockOnly);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "status.txt"), "RUNNING");
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }
    private static void StateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
        {
            SessionState.SetBool(PendingKey, false);
            EditorApplication.delayCall += Start;
        }
        if (state == PlayModeStateChange.ExitingPlayMode) Stop();
    }
    private static void Start()
    {
        Report.Clear();
        _lastFrame = -1;
        _savedBackground = Application.runInBackground;
        Application.runInBackground = true;
        _routine = Checks();
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (_routine == null || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (Time.frameCount == _lastFrame) return;
        _lastFrame = Time.frameCount;
        try
        {
            if (_routine.MoveNext()) return;
            File.WriteAllText(Path.Combine(Output, "report.txt"), Report.ToString());
            File.WriteAllText(Path.Combine(Output, "status.txt"), "PASSED");
            Debug.Log("MiniGame suite Play smoke checks passed. " + Report);
            Stop();
            EditorApplication.isPlaying = false;
        }
        catch (Exception ex)
        {
            Report.AppendLine("FAILED: " + ex);
            File.WriteAllText(Path.Combine(Output, "report.txt"), Report.ToString());
            File.WriteAllText(Path.Combine(Output, "status.txt"), "FAILED");
            Debug.LogException(ex);
            Stop();
            EditorApplication.isPlaying = false;
        }
    }
    private static void Stop()
    {
        EditorApplication.update -= Tick;
        _routine = null;
        if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
        _mouse = null;
        if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
        if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        _keyboard = null;
        if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
        Application.runInBackground = _savedBackground;
    }
    private static IEnumerator Checks()
    {
        yield return null;
        // 自动检查单独驱动任务阶段，避免编辑器后台帧较慢时世界倒计时抢先关闭面板。
        foreach (var phase in Object.FindObjectsByType<TapTapGameJam.Core.GamePhaseManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            phase.CouldWork = false;
        _panel = Object.FindFirstObjectByType<MiniGamePanelUI>();
        _player = Object.FindFirstObjectByType<PlayerBasic>();
        Assert(_panel != null && _player != null && _player.PlayerResources != null, "panel/player/resources exist");
        foreach (var manager in Object.FindObjectsByType<MiniGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) manager.enabled = false;
        var panelData = new SerializedObject(_panel);
        panelData.FindProperty("_duration").floatValue = 0;
        panelData.ApplyModifiedPropertiesWithoutUndo();
        _previousMouse = Mouse.current;
        _mouse = InputSystem.AddDevice<Mouse>("MiniGameSmokeMouse");
        _mouse.MakeCurrent();

        var cleanQuest = Object.FindFirstObjectByType<CleanQuest>();
        var cleanPoint = cleanQuest.GetComponent<CleanMopWater>();
        int before = _player.PlayerResources.Coins;
        var cleanTask = cleanQuest.CreateTask();
        Assert(cleanTask != null, "clean task generated");
        cleanPoint.Interact(_player);
        Canvas.ForceUpdateCanvases();
        yield return null;
        var clean = _panel.GetComponentInChildren<global::CleanGame>(true);
        var stain = Field<RawImage>(clean, "_stainImage");
        Assert(stain.gameObject.activeInHierarchy && _panel.GetComponent<CanvasGroup>().interactable, "clean UI active and accepts input");
        Capture("clean.png");
        yield return null;
        Camera camera = EventCamera(stain);
        Rect rect = stain.rectTransform.rect;
        Vector2 first = Screen(stain.rectTransform, new Vector2(rect.xMin + 5, rect.yMin + 5), camera);
        var evt = Pointer(stain, first);
        Assert(RaycastHits(stain, first), "clean image receives real UI raycast");
        ExecuteEvents.ExecuteHierarchy(stain.gameObject, evt, ExecuteEvents.pointerDownHandler);
        for (float y = rect.yMin + 5; y <= rect.yMax; y += 35)
        {
            evt.position = Screen(stain.rectTransform, new Vector2(rect.xMin + 5, y), camera);
            ExecuteEvents.ExecuteHierarchy(stain.gameObject, evt, ExecuteEvents.dragHandler);
            evt.position = Screen(stain.rectTransform, new Vector2(rect.xMax - 5, y), camera);
            ExecuteEvents.ExecuteHierarchy(stain.gameObject, evt, ExecuteEvents.dragHandler);
        }
        Assert(cleanTask.IsFinished && cleanTask.WasSuccessful, "dragging erases 90 percent and finishes clean task");
        Assert(_player.PlayerResources.Coins == before + cleanTask.RewardCoins, "clean reward matches task configuration");

        var stockQuest = Object.FindFirstObjectByType<StockQuest>();
        var stockPoint = stockQuest.GetComponent<StockTradePoint>();
        var taskManager = Object.FindFirstObjectByType<MiniGameManager>();
        Action<GamePhase> changePhase = phase => typeof(MiniGameManager)
            .GetMethod("HandlePhaseChanged", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(taskManager, new object[] { new GamePhaseChangedEvent(phase) });
        changePhase(GamePhase.Day);
        _player.PlayerResources.AddCoins(500 - _player.PlayerResources.Coins);
        var stockTask = stockQuest.Activity;
        Assert(stockTask != null && stockTask.IsDailyActivity && taskManager.Tasks.Contains(stockTask),
            "stock daily availability created immediately");
        var indicator = Object.FindFirstObjectByType<TaskIndicatorUI>();
        var arrows = Field<System.Collections.Generic.Dictionary<string, Arrow>>(indicator, "_arrows");
        Assert(arrows.ContainsKey(stockTask.Id) && stockTask.TargetPosition == stockPoint.transform,
            "stock guide points to existing chair entry");
        stockPoint.Interact(_player);
        var stock = _panel.GetComponentInChildren<StockPointGame>(true);
        var round = Field<StockRound>(stock, "_round");
        Assert(round.Prices.Count == 1 && round.CurvePrices.Count == 3 && round.Prices[0] >= 250 && round.Prices[0] <= 450
            && round.CurvePrices.All(p => p >= 0 && p <= 500), "opening price and internal forecasts are affordable");
        Assert(stockQuest.RemainingPlays == 0, "opening consumes the sole daily attempt");

        float stockDeadline = Time.realtimeSinceStartup + 5;
        while (!_panel.GetComponent<CanvasGroup>().interactable)
        { Assert(Time.realtimeSinceStartup < stockDeadline, "stock popup timeout"); yield return null; }
        Canvas.ForceUpdateCanvases();
        var buy = Field<Button>(stock, "_buyOne");
        var sell = Field<Button>(stock, "_sellAll");
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert(RaycastHits(buy.targetGraphic,
            RectTransformUtility.WorldToScreenPoint(EventCamera(buy.targetGraphic), buy.transform.position)),
            "stock buy button receives real UI raycast");
        buy.onClick.Invoke(); buy.onClick.Invoke();
        Assert(round.Shares == 1 && _player.PlayerResources.Coins == 500
            && !buy.interactable && sell.interactable, "one-share purchase uses float account without wallet rounding");
        sell.onClick.Invoke(); sell.onClick.Invoke(); buy.onClick.Invoke();
        Assert(round.Shares == 1 && Math.Abs(round.Profit) < 0.00001,
            "repeated buy-sell is permitted with at most one share and no same-price gain");
        var chart = Field<StockPointChart>(stock, "_chart");

        stockDeadline = Time.realtimeSinceStartup + 5;
        while (round.Prices.Count < 3)
        { Assert(Time.realtimeSinceStartup < stockDeadline, "stock automatic refresh timeout"); yield return null; }

        stockDeadline = Time.realtimeSinceStartup + 10;
        while (chart.PriceCount < 14)
        { Assert(Time.realtimeSinceStartup < stockDeadline, "stock compression timeout"); yield return null; }
        Canvas.ForceUpdateCanvases();
        Assert(chart.PriceCount == round.Prices.Count
            && Enumerable.Range(0, chart.PriceCount).All(i => chart.HorizontalPosition(i) >= 0 && chart.HorizontalPosition(i) <= 1),
            "all confirmed quotes remain visible after X compression");
        var mesh = chart.GetComponentInChildren<StockPointPlotGraphic>().canvasRenderer.GetMesh();
        Assert(mesh != null && mesh.vertexCount > 0 && chart.canvasRenderer.GetMesh().vertexCount == 20,
            "stock chart renders confirmed dots, straight connections and only horizontal grid lines");
        Assert(!chart.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(label => label.name == "UpcomingLabel"),
            "future quotation position is not shown");
        Assert(stock.GetComponentsInChildren<Button>().Length == 2
            && buy.GetComponentInChildren<TMPro.TMP_Text>().text == "买入下一报价"
            && sell.GetComponentInChildren<TMPro.TMP_Text>().text == "卖出下一报价", "buy and sell use the target quotation");
        Capture("stocks.png");
        yield return null;
        round.Tick(StockRound.TradingSeconds - round.Elapsed);
        typeof(StockPointGame).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(stock, null);
        int settledBalance = 500 + round.SettledProfit;
        Assert(round.IsFinished && !round.IsComplete && round.Prices.Count == 38 && round.Shares == 0
            && _player.PlayerResources.Coins == settledBalance, "18.5 seconds liquidates and commits integer net profit");
        Assert(!buy.interactable && !sell.interactable
            && Field<TMPro.TMP_Text>(stock, "_resultText").text.Contains("结算"), "results freeze trades and show settled profit");
        double fixedStart = chart.HorizontalPosition(5);
        double fixedAxis = chart.AxisMaximum;
        double fixedAxisMin = chart.AxisMinimum;
        Capture("stocks-result.png");

        stockDeadline = Time.realtimeSinceStartup + 5;
        while (stock.gameObject.activeInHierarchy)
        {
            Assert(round.Prices.Count == 38 && chart.HorizontalPosition(5) == fixedStart && chart.AxisMaximum == fixedAxis && chart.AxisMinimum == fixedAxisMin,
                "stock frozen results timeout");
            Assert(Time.realtimeSinceStartup < stockDeadline, "stock results timeout");
            yield return null;
        }
        Assert(round.IsComplete && stockTask.IsFinished && !stockPoint.CanInteract(_player)
            && !arrows.ContainsKey(stockTask.Id), "20 seconds closes panel and removes exhausted daily guide");
        stockPoint.Interact(_player);
        Assert(!stock.gameObject.activeInHierarchy, "same-day reentry is blocked");
        changePhase(GamePhase.Night); changePhase(GamePhase.Day);
        Assert(stockQuest.RemainingPlays == 1 && stockPoint.CanInteract(_player),
            "new day restores exactly one attempt");
        // 提前退出也消耗一次；既有资金和本局净盈亏只结算一次。
        _player.PlayerResources.AddCoins(500 - _player.PlayerResources.Coins);
        stockPoint.Interact(_player); stock.Buy();
        var closingRound = Field<StockRound>(stock, "_round");
        closingRound.Tick(0.5);
        int expectedClose = 500 + (int)Math.Round(closingRound.Profit, MidpointRounding.AwayFromZero);
        _panel.OnEscKeyPressed(); _panel.OnEscKeyPressed(); closingRound.Close();
        Assert(_player.PlayerResources.Coins == expectedClose && closingRound.Shares == 0
            && stockQuest.RemainingPlays == 0 && !stockPoint.CanInteract(_player),
            "early exit settles float net profit once and consumes daily attempt");

        foreach (bool panelFirst in new[] { true, false })
        {
            changePhase(GamePhase.Night); changePhase(GamePhase.Day);
            _player.PlayerResources.AddCoins(500 - _player.PlayerResources.Coins);
            var activity = stockQuest.Activity;
            stockPoint.Interact(_player); stock.Buy();
            var dayRound = Field<StockRound>(stock, "_round");
            dayRound.Tick(0.5);
            float lastPrice = dayRound.Price;
            int dayBalance = 500 + (int)Math.Round(dayRound.Profit, MidpointRounding.AwayFromZero);
            if (panelFirst) EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Shopping));
            changePhase(GamePhase.Shopping);
            if (!panelFirst) EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Shopping));
            dayRound.Close(); _panel.OnEscKeyPressed();
            Assert(dayRound.IsFinished && dayRound.Shares == 0 && dayRound.Price == lastPrice
                && dayRound.Prices.Count == 2 && _player.PlayerResources.Coins == dayBalance
                && activity.IsFinished && !stock.gameObject.activeInHierarchy,
                "day-end close settles once at current float price for either subscription order");
    
        }
        changePhase(GamePhase.Day);

        if (SessionState.GetBool(StockOnlyKey, false)) yield break;
        var coffeeQuest = Object.FindFirstObjectByType<CoffeeQuest>();
        var coffeePoint = coffeeQuest.GetComponent<TaskMakeCoffee>();
        var coffee = _panel.GetComponentInChildren<CoffeeGame>(true);
        var coffeeData = new SerializedObject(coffee);
        foreach (string field in new[] { "_heatSeconds", "_grindSeconds", "_groundsSeconds", "_waterSeconds", "_dripSeconds" }) coffeeData.FindProperty(field).floatValue = 0.1f;
        coffeeData.ApplyModifiedPropertiesWithoutUndo();
        before = _player.PlayerResources.Coins;
        int coffeeBefore = _player.PlayerResources.CoffeeCount;
        var coffeeTask = coffeeQuest.CreateTask();
        Assert(coffeeTask != null, "coffee task generated");
        coffeePoint.Interact(_player);
        float deadline = Time.realtimeSinceStartup + 5;
        while (!_panel.GetComponent<CanvasGroup>().interactable)
        { Assert(Time.realtimeSinceStartup < deadline, "coffee popup timeout"); yield return null; }
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        Field<Button>(coffee, "_heatButton").onClick.Invoke();
        deadline = Time.realtimeSinceStartup + 5;
        while (Field<CoffeeRecipe>(coffee, "_recipe").Step != CoffeeStep.LoadBeans)
        { Assert(Time.realtimeSinceStartup < deadline, "coffee heating timeout"); yield return null; }
        Capture("coffee.png");
        yield return null;
        DragCoffee(coffee, "_beans", "_grinderZone");
        Assert(Field<CoffeeRecipe>(coffee, "_recipe").Step == CoffeeStep.GrindBeans, "coffee bean UI drag advances recipe");
        var grindRect = Field<RectTransform>(coffee, "_grindButton");
        var grindGraphic = grindRect.GetComponent<Graphic>();
        Vector2 grindScreen = RectTransformUtility.WorldToScreenPoint(EventCamera(grindGraphic), grindRect.position);
        InputState.Change(_mouse, new MouseState { position = grindScreen }.WithButton(MouseButton.Left));
        ExecuteEvents.ExecuteHierarchy(grindRect.gameObject, Pointer(grindGraphic, grindScreen), ExecuteEvents.pointerDownHandler);
        deadline = Time.realtimeSinceStartup + 5;
        while (Field<CoffeeRecipe>(coffee, "_recipe").Step == CoffeeStep.GrindBeans)
        {
            // 编辑器后台运行时原生鼠标或失焦事件可能覆盖模拟按住状态。
            _mouse.MakeCurrent();
            InputState.Change(_mouse, new MouseState { position = grindScreen }.WithButton(MouseButton.Left));
            ExecuteEvents.ExecuteHierarchy(grindRect.gameObject, Pointer(grindGraphic, grindScreen), ExecuteEvents.pointerDownHandler);
            Assert(Time.realtimeSinceStartup < deadline, "coffee grinding timeout");
            yield return null;
        }
        InputState.Change(_mouse, new MouseState { position = grindScreen });
        Assert(Field<CoffeeRecipe>(coffee, "_recipe").Step == CoffeeStep.PlaceDripper, "held grinder finishes grinding");
        DragCoffee(coffee, "_dripper", "_cupZone");
        yield return null;
        DragCoffee(coffee, "_filter", "_cupZone");
        yield return null;
        DragCoffee(coffee, "_grounds", "_pourZone");
        deadline = Time.realtimeSinceStartup + 5;
        while (Field<CoffeeRecipe>(coffee, "_recipe").Step != CoffeeStep.PourWater)
        { Assert(Time.realtimeSinceStartup < deadline, "coffee grounds timeout"); yield return null; }
        var kettle = Field<RectTransform>(coffee, "_kettle");
        var zone = Field<RectTransform>(coffee, "_pourZone");
        var kettleGraphic = kettle.GetComponent<Graphic>();
        camera = EventCamera(kettleGraphic);
        evt = Pointer(kettleGraphic, RectTransformUtility.WorldToScreenPoint(camera, kettle.position));
        ExecuteEvents.ExecuteHierarchy(kettle.gameObject, evt, ExecuteEvents.beginDragHandler);
        evt.position = RectTransformUtility.WorldToScreenPoint(camera, zone.position);
        ExecuteEvents.ExecuteHierarchy(kettle.gameObject, evt, ExecuteEvents.dragHandler);
        InputState.Change(_mouse, new MouseState { position = evt.position }.WithButton(MouseButton.Left));
        deadline = Time.realtimeSinceStartup + 5;
        while (!coffeeTask.IsFinished)
        { _mouse.MakeCurrent(); Assert(Time.realtimeSinceStartup < deadline, "coffee pour/drip timeout"); yield return null; }
        InputState.Change(_mouse, new MouseState());
        Assert(coffeeTask.WasSuccessful && _player.PlayerResources.CoffeeCount == coffeeBefore + 1, "2D coffee awards exactly one coffee");
        Assert(_player.PlayerResources.Coins == before + coffeeTask.RewardCoins, "coffee reward matches task configuration");
        Assert(!_panel.GetComponent<CanvasGroup>().blocksRaycasts, "completed panel releases UI raycasts");
        coffeeTask = coffeeQuest.CreateTask(); coffeePoint.Interact(_player);
        _panel.OnEscKeyPressed();
        Assert(!coffeeTask.IsFinished && coffeePoint.CanInteract(_player), "coffee early exit retains task for retry");
        coffeePoint.Interact(_player);
        Assert(Field<CoffeeRecipe>(coffee, "_recipe").Step == CoffeeStep.HeatWater, "coffee retry starts fresh recipe");
        coffeeTask.Cancel();
        Assert(coffeeTask.IsFinished && !_panel.GetComponent<CanvasGroup>().blocksRaycasts, "task cancellation closes its panel");
        Assert(_player.PlayerResources.CoffeeCount == coffeeBefore + 1, "coffee cancellation gives no extra coffee");
        var printerChecks = PrinterChecks(taskManager, arrows);
        while (printerChecks.MoveNext()) yield return printerChecks.Current;
        yield return null;
    }

    private static IEnumerator PrinterChecks(MiniGameManager manager, System.Collections.Generic.Dictionary<string, Arrow> arrows)
    {
        var quest = Object.FindFirstObjectByType<PrinterRepairQuest>();
        var point = quest.GetComponent<PrinterRepairPoint>();
        var game = _panel.GetComponentInChildren<PrinterRepairGame>(true);
        var configuration = new SerializedObject(game);
        configuration.FindProperty("_pointerSpeed").floatValue = 0.05f;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        var task = quest.CreateTask();
        typeof(MiniGameManager).GetMethod("AddTask", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { task });
        Assert(task != null && arrows.ContainsKey(task.Id) && task.TargetPosition == point.transform,
            "printer task enters manager and guide follows actual printer point");
        var model = point.GetComponentInChildren<Renderer>();
        Assert(model != null && Vector2.Distance(new Vector2(model.bounds.center.x, model.bounds.center.z),
            new Vector2(point.transform.position.x, point.transform.position.z)) < 0.01f,
            "printer guide origin matches visible model rather than FBX export offset");
        int coins = _player.PlayerResources.Coins, talent = _player.PlayerResources.TalentPoint;
        point.Interact(_player); Canvas.ForceUpdateCanvases();
        yield return null;
        Assert(game.gameObject.activeInHierarchy && _panel.GetComponent<CanvasGroup>().interactable,
            "printer QTE opens in shared 2D panel");
        Capture("printer.png");
        _previousKeyboard = Keyboard.current;
        _keyboard = InputSystem.AddDevice<Keyboard>("PrinterSmokeKeyboard"); _keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        yield return null;
        var round = Field<PrinterRepairRound>(game, "_round");
        double beforePosition = round.Position;
        for (int i = 0; i < 3; i++) yield return null;
        Assert(round.Position != beforePosition, "printer pointer moves in actual Update");
        var yellow = Field<RectTransform>(game, "_successZone");
        Assert(Mathf.Abs(yellow.anchorMin.x - (float)round.ZoneStart) < 0.0001f
            && Mathf.Abs(yellow.anchorMax.x - (float)(round.ZoneStart + round.ZoneWidth)) < 0.0001f,
            "yellow UI anchors match QTE success interval");
        MovePrinterTo(game, round, round.ZoneStart > 0.1 ? 0 : 1);
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
        yield return null;
        yield return null;
        Assert(round.Successes == 0 && !task.IsFinished, "space outside yellow does not count or finish task");
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        yield return null;
        for (int hit = 1; hit <= 5; hit++)
        {
            MovePrinterTo(game, round, round.ZoneStart + round.ZoneWidth * 0.5);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            float deadline = Time.realtimeSinceStartup + 5;
            while (round.Successes < hit)
            { _keyboard.MakeCurrent(); Assert(Time.realtimeSinceStartup < deadline, "printer input timeout"); yield return null; }
            Assert(round.Successes == hit, "space inside yellow counts one hit");
            if (hit == 1)
            {
                for (int i = 0; i < 4; i++) yield return null;
                Assert(round.Successes == 1, "holding space does not auto-repeat hits");
                _panel.OnEscKeyPressed();
                Assert(!task.IsFinished && point.CanInteract(_player) && _player.PlayerResources.Coins == coins,
                    "printer early exit preserves task without reward");
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); yield return null;
                point.Interact(_player); round = Field<PrinterRepairRound>(game, "_round");
                Assert(round.Successes == 0, "printer retry resets progress");
                MovePrinterTo(game, round, round.ZoneStart + round.ZoneWidth * 0.5);
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
                deadline = Time.realtimeSinceStartup + 5;
                while (round.Successes < 1)
                { Assert(Time.realtimeSinceStartup < deadline, "printer retry input timeout"); yield return null; }
            }
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); yield return null;
        }
        Assert(task.IsFinished && task.WasSuccessful && !arrows.ContainsKey(task.Id) && !point.CanInteract(_player),
            "fifth printer hit completes task and removes guide and interaction");
        Assert(_player.PlayerResources.Coins == coins + task.RewardCoins && _player.PlayerResources.TalentPoint == talent + task.RewardTalentPoints,
            "printer awards configured coins and talent once");
        game.Hit(); _panel.OnEscKeyPressed();
        Assert(_player.PlayerResources.Coins == coins + task.RewardCoins, "printer stale input and repeat close cannot double-reward");
        var next = quest.CreateTask(); point.Interact(_player);
        var cancelledRound = Field<PrinterRepairRound>(game, "_round");
        EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Shopping));
        Assert(next.IsFinished && !next.WasSuccessful && !game.gameObject.activeInHierarchy && !cancelledRound.IsRunning,
            "day end cancels printer task and stops QTE input");
        Assert(_player.PlayerResources.Coins == coins + task.RewardCoins, "printer phase cancellation gives no reward");
    }
    private static void MovePrinterTo(PrinterRepairGame game, PrinterRepairRound round, double position)
    {
        double phase = Field<double>(round, "_phase");
        double distance = (position - phase + 2) % 2;
        round.Tick(distance / Field<float>(game, "_pointerSpeed"));
    }

    private static void DragCoffee(CoffeeGame coffee, string itemField, string zoneField)
    {
        Canvas.ForceUpdateCanvases();
        var item = Field<RectTransform>(coffee, itemField);
        var zone = Field<RectTransform>(coffee, zoneField);
        var graphic = item.GetComponent<Graphic>();
        Camera camera = EventCamera(graphic);
        var evt = Pointer(graphic, RectTransformUtility.WorldToScreenPoint(camera, item.position));
        Assert(RaycastHits(graphic, evt.position), "coffee " + itemField + " receives UI raycast");
        ExecuteEvents.ExecuteHierarchy(item.gameObject, evt, ExecuteEvents.beginDragHandler);
        evt.position = RectTransformUtility.WorldToScreenPoint(camera, zone.position);
        ExecuteEvents.ExecuteHierarchy(item.gameObject, evt, ExecuteEvents.dragHandler);
        ExecuteEvents.ExecuteHierarchy(item.gameObject, evt, ExecuteEvents.endDragHandler);
    }
    private static Camera EventCamera(Graphic graphic) => graphic.GetComponentInParent<Canvas>().GetComponent<GraphicRaycaster>().eventCamera;
    private static Vector2 Screen(RectTransform rect, Vector2 point, Camera camera) => RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(point));
    private static PointerEventData Pointer(Graphic graphic, Vector2 point) => new PointerEventData(EventSystem.current)
    {
        position = point, button = PointerEventData.InputButton.Left, pointerId = -991,
        pointerPressRaycast = new RaycastResult { gameObject = graphic.gameObject, module = graphic.GetComponentInParent<Canvas>().GetComponent<GraphicRaycaster>() }
    };
    private static bool RaycastHits(Graphic graphic, Vector2 point)
    {
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(Pointer(graphic, point), hits);
        if (hits.Count == 0) { Report.AppendLine("Raycast had no hits at " + point); return false; }
        if (hits[0].gameObject != graphic.gameObject && !hits[0].gameObject.transform.IsChildOf(graphic.transform))
            Report.AppendLine("Raycast first hit " + hits[0].gameObject.name + " instead of " + graphic.gameObject.name);
        return hits[0].gameObject == graphic.gameObject || hits[0].gameObject.transform.IsChildOf(graphic.transform);
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Capture(string filename) => ScreenCapture.CaptureScreenshot(Path.Combine(Output, filename));
    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        // 不记录轮询成功次数，报告只留下有意义的检查。
        if (!message.EndsWith("timeout")) Report.AppendLine("PASS: " + message);
    }
}
