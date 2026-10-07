using System;
using System.Reflection;
using TapTapGameJam.Events;
using TapTapGameJam.MiniGame;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using TapTapGameJam.UI;

internal static class Program
{
    private static int _checks;

    private static void Main()
    {
        Run("Each task switches its own target and text; displayed reward matches payout", () =>
        {
            var a = new Fixture();
            var b = new Fixture();
            int changes = 0;
            a.Task.EventTargetChanged += task =>
            {
                changes++;
                Check(task.GuideText == "送达文件" && task.TargetPosition == a.Destination.transform);
            };
            a.Source.PickUp();
            Check(changes == 1 && a.Destination.Enabled && !b.Destination.Enabled);
            Check(b.Task.TargetPosition == b.Source.transform && b.Task.GuideText == "领取文件");
            a.Destination.Deliver(a.Player);
            Check(a.Player.PlayerResources.Coins == a.Task.RewardCoins);
            Check(a.Player.PlayerResources.TalentPoint == a.Task.RewardTalentPoints);
        });

        Run("Completion notifies removal once; reused destination cannot reward old task", () =>
        {
            var f = new Fixture();
            int ended = 0;
            f.Task.EventFinished += _ => ended++;
            f.Source.PickUp();
            f.Destination.Deliver(f.Player);
            f.Destination.Deliver(f.Player);
            f.Task.Cancel();
            Check(ended == 1 && f.Task.IsFinished && f.Task.TargetPosition == null);
            Check(f.Player.PlayerResources.Coins == f.Task.RewardCoins);
        });

        Run("Cancel before pickup closes both points and stale interactions do not advance", () =>
        {
            var f = new Fixture();
            f.Task.Cancel();
            f.Source.PickUp();
            f.Destination.Deliver(f.Player);
            Check(f.Source.IsFree && f.Destination.IsFree && !f.Destination.Enabled);
            Check(f.Task.IsFinished && f.Player.PlayerResources.Coins == 0);
        });

        Run("Ending old delivery does not release a source now owned by another task", () =>
        {
            foreach (bool success in new[] { false, true })
            {
                var f = new Fixture();
                f.Source.PickUp();
                var nextDestination = new FileDestination();
                var next = new DeliveryFileInsance(f.Source, nextDestination);
                next.Begin();
                if (success) f.Destination.Deliver(f.Player); else f.Task.Cancel();
                Check(!f.Source.IsFree && f.Source.Enabled && !next.IsFinished);
                f.Source.PickUp();
                Check(next.TargetPosition == nextDestination.transform);
            }
        });

        Run("Leaving day removes all indicators via notifications; disabled manager cancels too", () =>
        {
            foreach (bool disable in new[] { false, true })
            {
                EventPipeline.ClearAll();
                var manager = new MiniGameManager();
                int added = 0, removed = 0;
                manager.TaskAdded += _ => added++;
                manager.TaskRemoved += _ => removed++;
                Call(manager, "OnEnable");
                EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
                var a = new Fixture();
                var b = new Fixture();
                Call(manager, "AddTask", a.Task);
                Call(manager, "AddTask", b.Task);
                if (disable) Call(manager, "OnDisable");
                else EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Shopping));
                Check(added == 2 && removed == 2 && manager.Tasks.Count == 0);
                Check(a.Task.IsFinished && b.Task.IsFinished);
                Check(a.Source.IsFree && b.Destination.IsFree);
                a.Task.NotifyFinished();
                Check(removed == 2);
                Call(manager, "OnDisable");
            }
        });

        Run("Screen-edge placement keeps the ray direction instead of clamping axes", () =>
        {
            Edge(2, 1, 90, 40, 80, 40);
            Edge(-2, -1, 90, 40, -80, -40);
            Edge(1, 0, 90, 40, 90, 0);
            Edge(0, 1, 90, 40, 0, 40);
            Edge(0, 0, 90, 40, 0, -40);
            Check(!TaskIndicatorGeometry.TryGetEdgePosition(1, 1, -10, 40, out _, out _));
            Check(!TaskIndicatorGeometry.TryGetEdgePosition(float.NaN, 1, 90, 40, out _, out _));
            var random = new System.Random(42);
            for (int i = 0; i < 1000; i++)
            {
                float dx = (float)(random.NextDouble() * 200 - 100);
                float dy = (float)(random.NextDouble() * 200 - 100);
                Check(TaskIndicatorGeometry.TryGetEdgePosition(dx, dy, 90, 40, out float x, out float y));
                Check(Math.Abs(x) <= 90.001f && Math.Abs(y) <= 40.001f);
                Check(Math.Abs(Math.Abs(x) - 90) < 0.001f || Math.Abs(Math.Abs(y) - 40) < 0.001f);
                Check(Math.Abs(x * dy - y * dx) < 0.01f);
            }
        });
        Console.WriteLine($"Passed {_checks} checks. Unity rendering/physics are not exercised here.");
    }

    private sealed class Fixture
    {
        public readonly FileSource Source = new();
        public readonly FileDestination Destination = new();
        public readonly PlayerBasic Player = new();
        public readonly DeliveryFileInsance Task;
        public Fixture() { Task = new DeliveryFileInsance(Source, Destination); Task.Begin(); }
    }

    private static void Edge(float dx, float dy, float hw, float hh, float expectedX, float expectedY)
    {
        Check(TaskIndicatorGeometry.TryGetEdgePosition(dx, dy, hw, hh, out float x, out float y));
        Check(Math.Abs(x - expectedX) < 0.001f && Math.Abs(y - expectedY) < 0.001f);
    }
    private static void Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Check(bool condition)
    {
        if (!condition) throw new Exception("Check failed.");
    }
    private static void Run(string name, Action check)
    {
        check();
        _checks++;
        Console.WriteLine($"PASS: {name}");
    }
}
