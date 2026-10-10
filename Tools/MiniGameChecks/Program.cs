using System;
using System.Collections.Generic;
using System.Reflection;
using TapTapGameJam.Coffee;
using TapTapGameJam.Events;
using TapTapGameJam.Interactable;
using TapTapGameJam.MiniGame;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using TapTapGameJam.UI;
using UnityEngine;

internal static class Program
{
    private static int _checks;
    private static void Main(string[] args)
    {
        if (Array.IndexOf(args, "--stock-only") >= 0)
        {
            StockChecks.Run(Run);
            StockAxisChecks.Run(Run);
            Console.WriteLine($"Passed {_checks} stock and axis scenarios.");
            return;
        }
        Run("Different instances store independent rewards; settlement and release happen once", () =>
        {
            var player = new PlayerResources();
            var a = new ProbeInstance(7, 2);
            var b = new ProbeInstance(45, 3);
            int events = 0;
            a.EventFinished += _ => { events++; Check(a.IsFinished && a.WasSuccessful); };
            a.Win(player); a.Win(player); a.Cancel();
            Check(player.Coins == 7 && player.TalentPoint == 2 && a.Releases == 1 && events == 1);
            b.Cancel(); b.Win(player);
            Check(player.Coins == 7 && b.Releases == 1 && !b.WasSuccessful);
        });
        foreach (string kind in new[] { "sort", "dog", "clean", "coffee", "printer" })
        {
            Run(kind + ": single point occupancy, completion, configurable reward and reuse", () =>
            {
                var f = new Fixture(kind);
                Check(!f.Point.CanInteract(f.Player));
                f.Spawn();
                Check(!f.Quest.CanSpawn && f.Quest.CreateTask() == null && f.Point.CanInteract(f.Player));
                f.Point.Interact(f.Player); f.Point.Interact(f.Player);
                Check(f.OpenCount == 1 && f.OpenPlayer == f.Player);
                var old = f.Callback;
                old(true); old(true); f.Task.Cancel();
                Check(f.Manager.Tasks.Count == 0 && f.Quest.CanSpawn && f.Task.WasSuccessful);
                Check(f.Player.PlayerResources.Coins == 17 && f.Player.PlayerResources.TalentPoint == 2);
                Check(f.Player.PlayerResources.CoffeeCount == (kind == "coffee" ? 1 : 0));
                f.Spawn(); f.Point.Interact(f.Player); old(true);
                Check(f.Manager.Tasks.Count == 1 && f.Player.PlayerResources.Coins == 17);
                f.Callback(true);
                Check(f.Player.PlayerResources.Coins == 34);
            });
            Run(kind + ": close policy, phase cancellation, stale callback and no cancellation rewards", () =>
            {
                var f = new Fixture(kind);
                f.Spawn(); f.Point.Interact(f.Player);
                var old = f.Callback;
                old(false);
                Check(f.Manager.Tasks.Count == 1);
                Check(f.Player.PlayerResources.Coins == 0);
                f.Point.Interact(f.Player);
                old = f.Callback;
                EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Night));
                Check(f.Manager.Tasks.Count == 0 && !f.Detector.enabled && f.CloseCount == 1);
                EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
                f.Spawn(); f.Point.Interact(f.Player); old(true);
                Check(f.Manager.Tasks.Count == 1 && f.Player.PlayerResources.Coins == 0);
                f.Callback(true);
                Check(f.Player.PlayerResources.Coins == 17);
            });
            Run(kind + ": point disable and game over release active task", () =>
            {
                foreach (bool pointDisable in new[] { false, true })
                {
                    var f = new Fixture(kind);
                    f.Spawn(); f.Point.Interact(f.Player);
                    if (pointDisable) Call(f.Point, "OnDisable");
                    else EventPipeline.Publish(new EventGameOver(false, "test"));
                    Check(f.Task.IsFinished && !f.Task.WasSuccessful && !f.Detector.enabled && f.CloseCount == 1);
                    Check(f.Manager.Tasks.Count == 0 && f.Player.PlayerResources.Coins == 0);
                }
            });
        }
        Run("点位池跳过受限点位并保留只读原因", () =>
        {
            EventPipeline.ClearAll();
            var lockedPoint = new SortFile();
            var openPoint = new SortFile();
            var lockedDetector = new Collider();
            var openDetector = new Collider();
            Set(lockedPoint, "_detectCollider", lockedDetector);
            Set(openPoint, "_detectCollider", openDetector);
            Call(lockedPoint, "Awake");
            Call(openPoint, "Awake");
            var condition = new ConditionProbe(false, "办公室门尚未开放");
            Set(lockedPoint, "_spawnConditionObjects", new List<MonoBehaviour> { condition });
            Check(!lockedPoint.CanStartTask && lockedPoint.UnavailableReason == "办公室门尚未开放");

            var quest = new SortFileQuest();
            Set(quest, "_sortFilePoints", new List<SortFile> { lockedPoint, openPoint });
            Check(quest.CanSpawn);
            var task = quest.CreateTask();
            Check(task != null && !lockedPoint.CanInteract(new PlayerBasic()) && openPoint.CanInteract(new PlayerBasic()));
            task.Cancel();
            Check(openPoint.CanStartTask && lockedPoint.UnavailableReason == "办公室门尚未开放");
        });
        Run("同一种任务可以按并发上限占用多个不同点位", () =>
        {
            EventPipeline.ClearAll();
            var first = new SortFile(); var second = new SortFile();
            var firstDetector = new Collider(); var secondDetector = new Collider();
            Set(first, "_detectCollider", firstDetector); Set(second, "_detectCollider", secondDetector);
            Call(first, "Awake"); Call(second, "Awake");
            var quest = new SortFileQuest();
            Set(quest, "_sortFilePoints", new List<SortFile> { first, second });
            quest.MaxActiveTasks = 2;
            var a = quest.CreateTask(); var b = quest.CreateTask();
            Check(a != null && b != null && quest.ActiveTaskCount == 2 && !first.CanStartTask && !second.CanStartTask);
            Check(quest.CreateTask() == null);
            a.Cancel(); Check(first.CanStartTask && quest.ActiveTaskCount == 1);
            b.Cancel(); Check(quest.ActiveTaskCount == 0 && first.CanStartTask && second.CanStartTask);
        });
        Run("Stock opens immediately on day, is tracked for guidance and excluded from random refresh", () =>
        {
            var f = new StockFixture();
            Check(!f.Point.CanInteract(f.Player) && f.Manager.Tasks.Count == 0);
            f.Day();
            Check(f.Manager.Tasks.Count == 1 && f.Added == 1 && f.Quest.Activity.IsDailyActivity);
            Check(f.Quest.Activity.TargetPosition == f.Point.transform && f.Point.CanInteract(f.Player));
            Check(!(bool)Call(f.Manager, "TrySpawnTask"));
            Call(f.Manager, "Update");
            Check(f.Manager.Tasks.Count == 1 && f.Added == 1);
        });
        Run("Stock consumes one attempt including early exit and restores only on a new day", () =>
        {
            foreach (bool success in new[] { false, true })
            {
                var f = new StockFixture(); f.Day(); var activity = f.Quest.Activity;
                f.Point.Interact(f.Player);
                Check(f.Quest.RemainingPlays == 0 && !activity.IsFinished);
                var old = f.Callback; old(success); old(true); old(false);
                Check(activity.IsFinished && f.Manager.Tasks.Count == 0 && !f.Detector.enabled && !f.Quest.CanSpawn);
                Call(f.Manager, "Update"); f.Day();
                Check(f.Quest.RemainingPlays == 0 && f.Added == 1 && f.Removed == 1);
                EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Night)); f.Day();
                Check(f.Quest.RemainingPlays == 1 && f.Point.CanInteract(f.Player) && f.Added == 2);
            }
        });
        Run("Rejected stock open does not consume quota; duplicate start callbacks count once", () =>
        {
            var f = new StockFixture(); f.Day(); f.AcceptOpen = false;
            f.Point.Interact(f.Player); f.Callback(false);
            Check(f.Quest.RemainingPlays == 1 && f.Point.CanInteract(f.Player));
            f.AcceptOpen = true; f.Point.Interact(f.Player); f.Started(); f.Started();
            Check(f.Quest.RemainingPlays == 0);
            f.Callback(false); Call(f.Manager, "Update");
            Check(f.Quest.RemainingPlays == 0 && f.Quest.Activity == null);
        });
        Run("Old stock callbacks cannot close the following day's round", () =>
        {
            var f = new StockFixture(); f.Day(); f.Point.Interact(f.Player);
            var old = f.Callback; old(false);
            EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Night)); f.Day();
            f.Point.Interact(f.Player); old(true); old(false);
            Check(!f.Point.CanInteract(f.Player) && f.Manager.Tasks.Count == 1);
            f.Callback(true); Check(!f.Point.CanInteract(f.Player) && f.Manager.Tasks.Count == 0);
        });
        Run("Leaving day closes stock and guide; next day creates exactly one fresh activity", () =>
        {
            var f = new StockFixture(); f.Day(); f.Point.Interact(f.Player);
            var old = f.Callback; var oldActivity = f.Quest.Activity;
            EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Shopping));
            Check(oldActivity.IsFinished && f.Manager.Tasks.Count == 0 && f.Quest.Activity == null && !f.Detector.enabled && f.Closed == 1);
            EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Night));
            Check(!f.Point.CanInteract(f.Player));
            f.Day(); Check(f.Added == 2 && f.Removed == 1 && f.Manager.Tasks.Count == 1);
            f.Point.Interact(f.Player); old(true);
            Check(!f.Point.CanInteract(f.Player) && !f.Quest.Activity.IsFinished);
            f.Callback(false); Check(!f.Point.CanInteract(f.Player));
        });
        Run("Disabled stock point and game over release daily guide and interaction", () =>
        {
            foreach (bool pointDisable in new[] { false, true })
            {
                var f = new StockFixture(); f.Day(); f.Point.Interact(f.Player);
                if (pointDisable) Call(f.Point, "OnDisable"); else EventPipeline.Publish(new EventGameOver(false, "test"));
                Check(f.Manager.Tasks.Count == 0 && !f.Detector.enabled && f.Closed == 1 && f.Quest.Activity == null);
            }
        });
        Run("Failure to acquire an occupied point cannot release another task", () =>
        {
            var f = new Fixture("clean"); f.Spawn();
            var other = new CleanInstance((CleanMopWater)f.Point, 99, 9);
            Check(!other.Begin() && other.IsFinished && f.Detector.enabled && !f.Task.IsFinished);
            f.Point.Interact(f.Player); f.Callback(true);
            Check(f.Player.PlayerResources.Coins == 17);
        });
        Run("Delivery stage switch, reward and source reuse remain correct", () =>
        {
            var source = new FileSource(); var dest = new FileDestination(); var player = new PlayerBasic();
            var task = new DeliveryFileInsance(source, dest, 31, 4); task.Begin();
            source.PickUp(); Check(task.GuideText == "送达文件" && task.TargetPosition == dest.transform);
            var next = new DeliveryFileInsance(source, new FileDestination()); next.Begin();
            dest.Deliver(player); dest.Deliver(player); task.Cancel();
            Check(task.IsFinished && !source.IsFree && source.Enabled && !next.IsFinished);
            Check(player.PlayerResources.Coins == 31 && player.PlayerResources.TalentPoint == 4);
        });
        Run("Wipe mask excludes transparent pixels and cannot count repeated wiping twice", () =>
        {
            var mask = new WipeMask(4, 1, new byte[] { 255, 0, 20, 0 });
            Check(mask.Total == 2);
            Check(mask.EraseSegment(0, 0, 0, 0, 0.4f) && mask.Remaining == 1);
            Check(!mask.EraseSegment(0, 0, 0, 0, 0.4f) && mask.Progress == 0.5f);
            mask.EraseSegment(0, 0, 3, 0, 0.4f); Check(mask.Progress == 1);
        });
        Run("Fast wiping covers continuous segments; exact 90 percent threshold is reachable", () =>
        {
            byte[] alpha = new byte[500]; Array.Fill(alpha, (byte)255);
            var fast = new WipeMask(500, 1, alpha); fast.EraseSegment(0, 0, 499, 0, 1); Check(fast.Remaining == 0);
            var mask = new WipeMask(10, 1, new byte[] { 1,1,1,1,1,1,1,1,1,1 });
            mask.EraseSegment(0, 0, 8, 0, 0.4f); Check(Math.Abs(mask.Progress - 0.9f) < 0.00001f && mask.Remaining == 1);
            Check(!mask.EraseSegment(float.NaN, 0, 9, 0, 1));
            Check(new WipeMask(1, 1, new byte[] { 0 }).Progress == 1);
        });
        StockChecks.Run(Run);
        StockAxisChecks.Run(Run);
        Run("Coffee recipe enforces order and held progress; restart creates a fresh recipe", () =>
        {
            var recipe = new CoffeeRecipe(1,1,1,1,1);
            Check(!recipe.LoadBeans() && !recipe.PlaceFilter());
            recipe.StartHeating(); recipe.Tick(1); Check(recipe.Step == CoffeeStep.LoadBeans);
            recipe.LoadBeans(); recipe.Grind(0.5f); recipe.Tick(20); Check(recipe.Step == CoffeeStep.GrindBeans && recipe.Progress == 0.5f);
            recipe.Grind(0.5f); recipe.PlaceDripper(); recipe.PlaceFilter(); recipe.StartPouringGrounds(); recipe.Tick(1);
            Check(recipe.Step == CoffeeStep.PourWater); recipe.Tick(20); Check(recipe.Step == CoffeeStep.PourWater);
            recipe.PourWater(1); recipe.Tick(1); Check(recipe.Step == CoffeeStep.Complete);
            Check(new CoffeeRecipe(1,1,1,1,1).Step == CoffeeStep.HeatWater);
        });
        Run("Printer pointer bounces continuously and handles large frame steps", () =>
        {
            var round = new PrinterRepairRound(5, 1, 0.25, () => 0.5);
            round.Tick(0.75); Check(Math.Abs(round.Position - 0.75) < 1e-9);
            round.Tick(0.5); Check(Math.Abs(round.Position - 0.75) < 1e-9);
            round.Tick(1000000.75); Check(Math.Abs(round.Position) < 1e-9);
            round.Tick(double.NaN); round.Tick(double.PositiveInfinity); round.Tick(-2);
            Check(round.Position == 0);
        });
        Run("Printer hit uses inclusive yellow bounds, misses keep progress and success moves zone", () =>
        {
            int draws = 0;
            var round = new PrinterRepairRound(5, 1, 0.25, () => draws++ == 0 ? 0.5 : 1);
            Check(round.Hit() == PrinterHitResult.Miss && round.Successes == 0);
            round.Tick(0.375); Check(round.Hit() == PrinterHitResult.Hit && round.Successes == 1);
            Check(round.ZoneStart == 0.75 && round.Hit() == PrinterHitResult.Miss && round.Successes == 1);
            round.Tick(0.625); Check(round.Position == 1 && round.Hit() == PrinterHitResult.Hit && round.Successes == 2);
            var upper = new PrinterRepairRound(5, 1, 0.25, () => 0.5);
            upper.Tick(0.625); Check(upper.Hit() == PrinterHitResult.Hit);
        });
        Run("Printer completes on fifth hit, stops movement/input and new attempt resets", () =>
        {
            var round = new PrinterRepairRound(5, 1, 0.25, () => 0);
            for (int i = 0; i < 4; i++) Check(round.Hit() == PrinterHitResult.Hit && !round.IsComplete);
            Check(round.Hit() == PrinterHitResult.Complete && round.IsComplete && round.Successes == 5);
            round.Tick(1); Check(round.Position == 0 && round.Hit() == PrinterHitResult.Ignored && round.Successes == 5);
            var retry = new PrinterRepairRound(5, 1, 0.25, () => 0);
            Check(retry.Successes == 0 && retry.IsRunning); retry.Stop();
            Check(retry.Hit() == PrinterHitResult.Ignored); retry.Tick(0.5); Check(retry.Position == 0);
        });
        Console.WriteLine($"Passed {_checks} scenarios. Unity rendering/input/physics need the editor smoke checks.");
    }

    private sealed class ProbeInstance : MiniGameInstanceBasic
    {
        public int Releases;
        public ProbeInstance(int coins, int talent) : base(new Transform(), coins, talent) { }
        public void Win(PlayerResources resources) => Complete(resources);
        protected override void Release() { Releases++; Cancel(); }
    }

    private sealed class ConditionProbe : MonoBehaviour, ITaskSpawnCondition
    {
        private readonly bool _available;
        public bool IsAvailable => _available;
        public string UnavailableReason { get; }

        public ConditionProbe(bool available, string reason)
        {
            _available = available;
            UnavailableReason = reason;
        }
    }
    private sealed class Fixture
    {
        public readonly PanelTaskPoint Point;
        public readonly PanelMiniGameQuest Quest;
        public readonly MiniGameManager Manager = new();
        public readonly Collider Detector = new();
        public readonly PlayerBasic Player = new();
        public MiniGameInstanceBasic Task;
        public Action<bool> Callback;
        public PlayerBasic OpenPlayer;
        public int OpenCount, CloseCount;
        public Fixture(string kind)
        {
            EventPipeline.ClearAll();
            switch (kind)
            {
                case "sort": Point = new SortFile(); Quest = new SortFileQuest(); Set(Quest, "_sortFile", Point); break;
                case "dog": Point = new PourDogFood(); Quest = new PourDogFoodQuest(); Set(Quest, "_pourDogFood", Point); break;
                case "clean": Point = new CleanMopWater(); Quest = new CleanQuest(); Set(Quest, "_cleanPoint", Point); break;
                case "coffee": Point = new TaskMakeCoffee(); Quest = new CoffeeQuest(); Set(Quest, "_coffeePoint", Point); break;
                case "printer": Point = new PrinterRepairPoint(); Quest = new PrinterRepairQuest(); Set(Quest, "_printerPoint", Point); break;
                default: throw new ArgumentException(kind);
            }
            Set(Point, "_detectCollider", Detector); Call(Point, "Awake");
            Quest.ConfigureRewards(17, 2);
            Manager.RegisterCandidate(Quest);
            Call(Manager, "OnEnable"); Call(Quest, "OnEnable");
            EventPipeline.Subscribe<EventOpenMiniGamePanel>(evt => { OpenCount++; Callback = evt.OnFinished; OpenPlayer = evt.Player; });
            EventPipeline.Subscribe<EventRequestCloseMiniGamePanel>(evt => { CloseCount++; Callback?.Invoke(false); });
            EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
        }
        public void Spawn() { Check((bool)Call(Manager, "TrySpawnTask")); Task = Manager.Tasks[0]; }
    }
    private sealed class StockFixture
    {
        public readonly StockTradePoint Point = new();
        public readonly StockQuest Quest = new();
        public readonly MiniGameManager Manager = new();
        public readonly PlayerBasic Player = new();
        public readonly Collider Detector = new();
        public int Added, Removed, Closed;
        public bool AcceptOpen = true;
        public Action Started;
        public Action<bool> Callback;
        public StockFixture()
        {
            EventPipeline.ClearAll();
            Set(Point, "_detectCollider", Detector); Call(Point, "Awake");
            Set(Quest, "_stockPoint", Point);
            Manager.RegisterCandidate(Quest);
            Manager.EventTaskAdded += _ => Added++;
            Manager.EventTaskRemoved += _ => Removed++;
            Call(Manager, "OnEnable");
            EventPipeline.Subscribe<EventOpenMiniGamePanel>(evt => { Callback = evt.OnFinished; Started = evt.OnStarted; if (AcceptOpen) Started?.Invoke(); });
            EventPipeline.Subscribe<EventRequestCloseMiniGamePanel>(evt => { Closed++; Callback?.Invoke(false); });
        }
        public void Day() => EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
    }
    private static object Call(object target, string name, params object[] args)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (method != null) return method.Invoke(target, args);
        }
        throw new MissingMethodException(name);
    }
    private static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(target, value); return; }
        }
        throw new MissingFieldException(name);
    }
    private static void Check(bool condition) { if (!condition) throw new Exception("Check failed"); }
    private static void Run(string name, Action test) { test(); _checks++; Console.WriteLine("PASS: " + name); }
}
