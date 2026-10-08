using System;
using System.Collections.Generic;

// 只替代 Unity 对象和钱包；测试直接编译项目的任务、配方、擦除和交易代码。
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public bool isActiveAndEnabled = true;
        public Transform transform = new();
        public readonly Dictionary<Type, object> Components = new();
        public T GetComponent<T>() where T : class => Components.TryGetValue(typeof(T), out var item) ? item as T : null;
    }
    public class Transform { }
    public class Collider { public bool enabled = true; }
    public class SerializeField : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string value) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public static class Mathf { public static int Max(int a, int b) => Math.Max(a, b); }
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
namespace TapTapGameJam.Interactable
{
    public abstract class MiniGameInteractable : UnityEngine.MonoBehaviour
    {
        public int ClearCount;
        public abstract string InteractionPrompt { get; }
        public virtual bool CanInteract(TapTapGameJam.Player.PlayerBasic player) => true;
        public abstract void Interact(TapTapGameJam.Player.PlayerBasic player);
        protected void ClearAllRegister() => ClearCount++;
    }
}
namespace TapTapGameJam.MiniGame
{
    public class FileSource
    {
        public UnityEngine.Transform transform = new();
        public bool IsFree = true, Enabled;
        public event Action EventFileSourceFinish;
        public void StartTask() => Enabled = true;
        public void ResetTask() { Enabled = false; IsFree = true; }
        public void PickUp() { ResetTask(); EventFileSourceFinish?.Invoke(); }
    }
    public class FileDestination
    {
        public UnityEngine.Transform transform = new();
        public bool IsFree = true, Enabled;
        public event Action<TapTapGameJam.Player.PlayerBasic> EventFileDestinationFinish;
        public void StartTask() => Enabled = true;
        public void ResetTask() { Enabled = false; IsFree = true; }
        public void Deliver(TapTapGameJam.Player.PlayerBasic player) { ResetTask(); EventFileDestinationFinish?.Invoke(player); }
    }
}
