using UnityEngine;
using UnityEngine.SceneManagement;

namespace TapTapGameJam.Tutorial
{
    /// <summary>
    /// GameScene 里如果还没有教程组件，运行时自动挂上，避免依赖大量场景 YAML 改动。
    /// </summary>
    internal static class TutorialBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            // Build 通常先加载 MainMenu，再由开场过渡加载 GameScene。
            // AfterSceneLoad 只覆盖启动时的第一张场景，因此这里提前订阅所有场景加载。
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "GameScene") return;

            // 每次进入 GameScene 重新允许“首次移动”信号；是否已看过由 PlayerProfile 判断。
            TapTapGameJam.Player.MoveState.ResetFirstMoveTracking();

            GameObject tutorial = GameObject.Find("Tutorial");
            if (tutorial == null)
            {
                Debug.LogWarning("[Tutorial] GameScene 中找不到 Tutorial 对象，教程系统未启动。");
                return;
            }

            if (tutorial.GetComponent<TutorialDialogueView>() == null)
                tutorial.AddComponent<TutorialDialogueView>();

            if (Object.FindFirstObjectByType<TutorialDirector>() == null)
            {
                var director = new GameObject("TutorialDirector");
                director.AddComponent<TutorialDirector>();
            }

            if (Object.FindFirstObjectByType<TutorialTaskBootstrap>() == null)
            {
                var taskBootstrap = new GameObject("TutorialTaskBootstrap");
                taskBootstrap.AddComponent<TutorialTaskBootstrap>();
            }
        }
    }
}
