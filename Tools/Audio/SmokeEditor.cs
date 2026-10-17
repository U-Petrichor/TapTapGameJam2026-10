using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class AudioSmokeEditor
{
    private static double lastState;
    static AudioSmokeEditor()
    {
        EditorApplication.update += () =>
        {
            // A fresh headless Unity editor can pause on its own Search indexer startup exception.
            // Tests use Time.timeScale, never the editor's Pause button.
            if (EditorApplication.isPlaying && EditorApplication.isPaused) EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - lastState > 1)
            {
                lastState = EditorApplication.timeSinceStartup;
                System.IO.File.WriteAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../smoke-state.txt"),
                    $"playing={EditorApplication.isPlaying}, paused={EditorApplication.isPaused}, frame={UnityEngine.Time.frameCount}");
            }
        };
    }
    public static void Run()
    {
        SessionState.SetBool("AudioSmoke", true);
        PlayerSettings.runInBackground = true;
        EditorApplication.isPaused = false;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/GameScene.unity");
        EditorApplication.EnterPlaymode();
    }
}
