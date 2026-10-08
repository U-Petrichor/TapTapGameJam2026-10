using System;
using System.Collections.Generic;
using System.Reflection;
using TapTapGameJam.Events;
using TapTapGameJam.MiniGame;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using UnityEngine;

internal static class Program
{
    private static void Main()
    {
        Run("任务生成前不可交互；单点不重复生成", () =>
        {
            var f = new Fixture();
            Check(!f.Point.CanInteract(f.Player) && !f.Detector.enabled);
            Check(f.Spawn());
            Check(f.Manager.Tasks.Count == 1 && f.Detector.enabled && !f.Quest.CanSpawn);
            Check(f.Point.CanInteract(f.Player) && f.Quest.CreateTask() == null);
        });
        Run("关闭保留任务；成功只结算一次；之后可再生成", () =>
        {
            var f = new Fixture();
            Check(f.Spawn());
            f.Point.Interact(f.Player);
            f.Point.Interact(f.Player);
            Check(f.OpenCount == 1);
            f.Callback(false);
            Check(f.Manager.Tasks.Count == 1 && f.Point.CanInteract(f.Player));
            Check(f.Player.PlayerResources.Coins == 0);
            f.Point.Interact(f.Player);
            Action<bool> completed = f.Callback;
            completed(true);
            completed(true);
            Check(f.Manager.Tasks.Count == 0 && f.Quest.CanSpawn && !f.Detector.enabled);
            Check(f.Player.PlayerResources.Coins == 30 && f.Player.PlayerResources.TalentPoint == 1);
            Check(f.Spawn() && f.Point.CanInteract(f.Player));
        });
        Run("阶段切换取消不发奖；旧UI回调不能完成新任务", () =>
        {
            var f = new Fixture();
            Check(f.Spawn());
            f.Point.Interact(f.Player);
            Action<bool> old = f.Callback;
            EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Night));
            Check(f.CloseCount == 1 && f.Manager.Tasks.Count == 0 && !f.Detector.enabled);
            Check(f.Player.PlayerResources.Coins == 0);
            EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
            Check(f.Spawn());
            f.Point.Interact(f.Player);
            old(true);
            Check(f.Manager.Tasks.Count == 1 && f.Player.PlayerResources.Coins == 0);
            f.Callback(true);
            Check(f.Manager.Tasks.Count == 0 && f.Player.PlayerResources.Coins == 30);
        });
        Run("交互点或任务组件禁用都会释放任务", () =>
        {
            foreach (bool disablePoint in new[] { true, false })
            {
                var f = new Fixture();
                Check(f.Spawn());
                f.Point.Interact(f.Player);
                Call(disablePoint ? (object)f.Point : f.Quest, "OnDisable");
                Check(f.Manager.Tasks.Count == 0 && !f.Detector.enabled && f.CloseCount == 1);
                Check(f.Player.PlayerResources.Coins == 0);
            }
        });
        Run("Panel忙碌拒绝请求后可以重试", () =>
        {
            var f = new Fixture();
            Check(f.Spawn());
            f.RejectOpen = true;
            f.Point.Interact(f.Player);
            Check(f.Point.CanInteract(f.Player) && f.Manager.Tasks.Count == 1);
            f.RejectOpen = false;
            f.Point.Interact(f.Player);
            Check(!f.Point.CanInteract(f.Player));
            f.Callback(true);
            Check(f.Manager.Tasks.Count == 0);
        });
        Run("新一天清理多个任务不修改遍历中的列表", () =>
        {
            foreach (bool managerFirst in new[] { true, false })
            {
                var f = new Fixture(managerFirst);
                Check(f.Spawn());
                Call(f.Manager, "AddTask", new ProbeTask(new Transform()));
                EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
                Check(f.Manager.Tasks.Count == 0 && f.Quest.CanSpawn && !f.Detector.enabled);
            }
        });
        Run("生成失败返回null时不加入任务列表", () =>
        {
            var f = new Fixture();
            Set(f.Manager, "_candidatePool", new List<MiniGameCandidate> { new EmptyCandidate() });
            Check(!f.Spawn() && f.Manager.Tasks.Count == 0);
        });
        Console.WriteLine("Passed 7 quest lifecycle checks (managed callbacks; no Unity physics/UI).");
    }

    private sealed class EmptyCandidate : MiniGameCandidate
    {
        public override bool CanSpawn => true;
        public override MiniGameInstanceBasic CreateTask() => null;
    }

    private sealed class Fixture
    {
        public readonly PourDogFood Point = new PourDogFood();
        public readonly PourDogFoodQuest Quest = new PourDogFoodQuest();
        public readonly MiniGameManager Manager = new MiniGameManager();
        public readonly Collider Detector = new Collider();
        public readonly PlayerBasic Player = new PlayerBasic();
        public Action<bool> Callback;
        public int OpenCount;
        public int CloseCount;
        public bool RejectOpen;

        public Fixture(bool managerFirst = true)
        {
            EventPipeline.ClearAll();
            Call(Point, "Reset");
            Set(Point, "_detectCollider", Detector);
            Call(Point, "Awake");
            Set(Quest, "_pourDogFood", Point);
            Call(Quest, "Awake");
            Set(Manager, "_candidatePool", new List<MiniGameCandidate> { Quest });
            if (managerFirst) Call(Manager, "OnEnable");
            Call(Quest, "OnEnable");
            if (!managerFirst) Call(Manager, "OnEnable");
            EventPipeline.Subscribe<EventOpenMiniGamePanel>(evt =>
            {
                OpenCount++;
                Callback = evt.OnFinished;
                if (RejectOpen) Callback(false);
            });
            EventPipeline.Subscribe<EventRequestCloseMiniGamePanel>(evt =>
            {
                CloseCount++;
                Callback?.Invoke(false);
            });
            EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
        }

        public bool Spawn() => (bool)Call(Manager, "TrySpawnTask");
    }

    private sealed class ProbeTask : MiniGameInstanceBasic
    {
        public ProbeTask(Transform target) : base(target) { }
        protected override void Release() { }
    }
    private static object Call(object target, string name, params object[] args)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method != null) return method.Invoke(target, args);
        }
        throw new MissingMethodException(name);
    }
    private static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(target, value); return; }
        }
        throw new MissingFieldException(name);
    }
    private static void Check(bool condition)
    {
        if (!condition) throw new Exception("Check failed.");
    }
    private static void Run(string name, Action test)
    {
        test();
        Console.WriteLine($"PASS: {name}");
    }
}
