using TapTapGameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StockPointVersionSetup
{
    [MenuItem("Tools/MiniGames/配置炒股小游戏")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("请先退出 Play 模式。");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var panel in Object.FindObjectsByType<MiniGamePanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (panel.gameObject.scene != scene) continue;
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Configure stock game");
            var label = panel.GetComponentInChildren<TMP_Text>(true);
            MiniGameDefaultSetup.EnsureStock(panel, label != null ? label.font : TMP_Settings.defaultFontAsset);
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("已配置炒股小游戏：开局一个点，逐点连线，超过十三点后横向压缩。", panel);
            return;
        }
        throw new System.InvalidOperationException("当前场景中没有 MiniGamePanelUI。");
    }
}
