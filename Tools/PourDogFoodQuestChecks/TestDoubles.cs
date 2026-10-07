using System;

// 只替代 Unity 对象、资源和物理注册；测试运行真正的交互、任务和事件总线代码。
// 这些检查不覆盖 Unity Trigger、UI 射线或原生对象销毁行为。
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public bool isActiveAndEnabled = true;
        public Transform transform = new Transform();
        public T GetComponent<T>() where T : class => null;
    }
    public class Transform { }
    public class Collider { public bool enabled = true; }
    public class CullingGroup { }
    public static class Time { public static float deltaTime = 1f; }
    public static class Random
    {
        public static int Range(int min, int max) => min;
        public static float Range(float min, float max) => min;
    }
    public static class Debug
    {
        public static void Log(object value) { }
        public static void LogError(object value) { }
    }
    public sealed class SerializeField : Attribute { }
    public sealed class MinAttribute : Attribute { public MinAttribute(float min) { } }
}

namespace TapTapGameJam.Player
{
    public class PlayerBasic { public PlayerResources PlayerResources = new PlayerResources(); }
    public class PlayerResources
    {
        public int Coins;
        public int TalentPoint;
        public void AddCoins(int amount) => Coins += amount;
        public void AddTalentPoint(int amount) => TalentPoint += amount;
    }
}

namespace TapTapGameJam.Interactable
{
    public abstract class AbstractInteractable : UnityEngine.MonoBehaviour
    {
        public int ClearCount;
        public abstract string InteractionPrompt { get; }
        public virtual bool CanInteract(TapTapGameJam.Player.PlayerBasic player) => true;
        public abstract void Interact(TapTapGameJam.Player.PlayerBasic player);
        protected void ClearAllRegister() => ClearCount++;
    }
}
