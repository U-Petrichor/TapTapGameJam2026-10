using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ParcelCutGameSetup
{
    [MenuItem("Tools/MiniGames/配置拆快递小游戏")]
    public static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("请退出 Play 模式后配置拆快递小游戏。");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("请先打开 GameScene。");
            return;
        }

        var panels = Object.FindObjectsByType<MiniGamePanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (panels.Length != 1)
        {
            Debug.LogError("当前场景需要恰好一个 MiniGamePanelUI。");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("配置拆快递小游戏");
        Undo.RegisterFullObjectHierarchyUndo(panels[0].gameObject, "配置拆快递面板");
        MiniGameDefaultSetup.Ensure(panels[0]);
        foreach (var game in Object.FindObjectsByType<ParcelCutGame>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (game.gameObject.scene == scene) game.RebuildTapeGeometry();

        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null) EditorUtility.SetDirty(component);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = panels[0].gameObject;
        Debug.Log("拆快递小游戏已配置并保存到当前场景。", panels[0]);
    }

    [MenuItem("Tools/MiniGames/校准拆快递胶带判定路径")]
    public static void CalibratePaths()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("请退出 Play 后校准并保存场景。");
        var games = Object.FindObjectsByType<ParcelCutGame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (games.Length != 1) throw new System.InvalidOperationException("当前场景需要恰好一个拆快递小游戏。");
        var game = games[0];
        Undo.RecordObject(game, "从胶带图片校准判定路径");
        game.RebuildTapeGeometry();
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        Debug.Log("已从六张胶带图片校准中心线与轮廓，保留现有 UI 布局。", game);
    }
}
