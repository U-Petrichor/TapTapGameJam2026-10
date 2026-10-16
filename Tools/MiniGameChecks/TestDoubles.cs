using System;
using System.Collections.Generic;

// 只替代 Unity 对象和钱包；测试直接编译项目的任务、配方、擦除和交易代码。
namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float value) => new(a.x * value, a.y * value);
        public float sqrMagnitude => x * x + y * y;
        public static float Distance(Vector2 a, Vector2 b) => (float)Math.Sqrt((a - b).sqrMagnitude);
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a + (b - a) * t;
    }
    public class MonoBehaviour
    {
        public bool isActiveAndEnabled = true;
        public Transform transform = new();
        public readonly Dictionary<Type, object> Components = new();
        public T GetComponent<T>() where T : class => Components.TryGetValue(typeof(T), out var item) ? item as T : null;
    }
    public class Transform { }
    public class GameObject { }
    public class Collider { public bool enabled = true; }
    public class SerializeField : Attribute { }
    namespace Serialization { public class FormerlySerializedAsAttribute : Attribute { public FormerlySerializedAsAttribute(string name) { } } }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string value) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static int CeilToInt(float value) => (int)Math.Ceiling(value);
        public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    }
    public static class Time { public static float deltaTime = 1; }
    public static class Random
    {
        public static int Range(int a, int b) => a;
        public static float Range(float a, float b) => a;
    }
    public static class Debug
    {
        public static void Log(object value) { }
        public static void LogError(object value, object context = null) { }
    }
}
namespace TapTapGameJam.Player
{
    public class PlayerBasic { public PlayerResources PlayerResources = new(); }
    public class PlayerResources
    {
        public int Coins, TalentPoint, CoffeeCount;
        public void AddCoins(int amount) => Coins += amount;
        public void AddTalentPoint(int amount) => TalentPoint += amount;
        public void AddCoffee() => CoffeeCount++;
        public bool TrySpendCoins(int amount) { if (amount <= 0 || Coins < amount) return false; Coins -= amount; return true; }
    }
}
namespace TapTapGameJam.Interactables
{
    public enum InteractState { InteractIdle, InteractPending, Interacting }
    public abstract class Interactable : UnityEngine.MonoBehaviour
    {
        public int ClearCount;
        public abstract string InteractionPrompt { get; }
        public InteractState InteractState { get; set; }
        public virtual bool CanInteract(TapTapGameJam.Player.PlayerBasic player) => true;
        public abstract void Interact(TapTapGameJam.Player.PlayerBasic player, InteractKey key);
        protected void ClearAllRegister() => ClearCount++;
    }
}
namespace TapTapGameJam.MiniGame
{
    public class FileSourcePoint
    {
        public UnityEngine.Transform transform = new();
        public bool IsFree = true, Enabled;
        public event Action EventFileSourceFinish;
        public void StartTask() => Enabled = true;
        public void ResetTask() { Enabled = false; IsFree = true; }
        public void PickUp() { ResetTask(); EventFileSourceFinish?.Invoke(); }
    }
    public class FileDestinationPoint
    {
        public UnityEngine.Transform transform = new();
        public bool IsFree = true, Enabled;
        public event Action<TapTapGameJam.Player.PlayerBasic> EventFileDestinationFinish;
        public void StartTask() => Enabled = true;
        public void ResetTask() { Enabled = false; IsFree = true; }
        public void Deliver(TapTapGameJam.Player.PlayerBasic player) { ResetTask(); EventFileDestinationFinish?.Invoke(player); }
    }
}
