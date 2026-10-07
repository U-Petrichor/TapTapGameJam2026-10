using System;
using TapTapGameJam.Player;
using TapTapGameJam.Types;

namespace UnityEngine
{
    public class MonoBehaviour { public bool isActiveAndEnabled = true; }
    public class Transform { }
    public class SerializeField : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public static class Time { public static float deltaTime = 1f; }
    public static class Random
    {
        public static int Range(int min, int max) => min;
        public static float Range(float min, float max) => (min + max) / 2f;
    }
    public static class Debug { public static void Log(object value) { } }
    public class CullingGroup { }
}

namespace TapTapGameJam.Player
{
    public class PlayerBasic { public PlayerResources PlayerResources = new(); }
    public class PlayerResources
    {
        public int Coins;
        public int TalentPoint;
        public void AddCoins(int amount) => Coins += amount;
        public void AddTalentPoint(int amount) => TalentPoint += amount;
    }
}

namespace TapTapGameJam.Events
{
    public interface IEvent { }
    public readonly struct GamePhaseChangedEvent : IEvent
    {
        public GamePhase NewPhase { get; }
        public GamePhaseChangedEvent(GamePhase phase) => NewPhase = phase;
    }
}

namespace TapTapGameJam.MiniGame
{
    // Only stand in for Unity physics and point visuals. Task/manager code is real.
    public class FileSource
    {
        public readonly UnityEngine.Transform transform = new();
        public bool IsFree = true;
        public bool Enabled;
        public int ResetCount;
        public event Action EventFileSourceFinish;
        public void StartTask() => Enabled = true;
        public void ResetTask() { Enabled = false; IsFree = true; ResetCount++; }
        public void PickUp() { ResetTask(); EventFileSourceFinish?.Invoke(); }
    }
    public class FileDestination
    {
        public readonly UnityEngine.Transform transform = new();
        public bool IsFree = true;
        public bool Enabled;
        public int ResetCount;
        public event Action<PlayerBasic> EventFileDestinationFinish;
        public void StartTask() => Enabled = true;
        public void ResetTask() { Enabled = false; IsFree = true; ResetCount++; }
        public void Deliver(PlayerBasic player) { ResetTask(); EventFileDestinationFinish?.Invoke(player); }
    }
}
