using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using TapTapGameJam.Core;

[InitializeOnLoad]
public static class IntroSmokeEditor
{
    private static double lastState;
    static IntroSmokeEditor()
    {
        EditorApplication.update += () =>
        {
            // Fresh batch editors can pause on Unity's Search indexer startup exception.
            if (EditorApplication.isPlaying && EditorApplication.isPaused)
                EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - lastState > 2)
            {
                lastState = EditorApplication.timeSinceStartup;
                var transition = UnityEngine.Object.FindFirstObjectByType<TapTapGameJam.UI.IntroVideoTransition>();
                var player = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Video.VideoPlayer>();
                System.IO.File.WriteAllText("smoke-state.txt",
                    $"playing={EditorApplication.isPlaying} paused={EditorApplication.isPaused} frame={Time.frameCount} " +
                    $"transition={transition != null} active={TapTapGameJam.UI.IntroVideoTransition.IsTransitioning} " +
                    $"videoPrepared={player != null && player.isPrepared} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            }
        };
    }

    public static void Run()
    {
        var game = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("EventSystem", typeof(EventSystem));
        new GameObject("Pause", typeof(GamePauseManager));
        EditorSceneManager.SaveScene(game, "Assets/GameScene.unity");
        var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("MenuEventSystem", typeof(EventSystem));
        new GameObject("Tests", typeof(IntroSmokeRunner));
        EditorSceneManager.SaveScene(menu, "Assets/Menu.unity");
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Menu.unity", true),
            new EditorBuildSettingsScene("Assets/GameScene.unity", true)
        };
        PlayerSettings.runInBackground = true;
        EditorApplication.EnterPlaymode();
    }
}
