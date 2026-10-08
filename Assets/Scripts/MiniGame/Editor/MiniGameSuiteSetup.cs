using System.Collections.Generic;
using TapTapGameJam.Interactable;
using TapTapGameJam.MiniGame;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

public static class MiniGameSuiteSetup
{
    [MenuItem("Tools/MiniGames/配置清理、炒股与2D咖啡")]
    public static void Setup()
    {
        if (Application.isPlaying) { Debug.LogError("请退出 Play 模式后配置小游戏。"); return; }
        var panels = Object.FindObjectsByType<MiniGamePanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (panels.Length != 1) { Debug.LogError("请打开仅有一个 MiniGamePanelUI 的游戏场景。"); return; }
        var panel = panels[0];
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("配置三个小游戏与任务候选点");
        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "配置小游戏面板");
        var before = new HashSet<int>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            before.Add(go.GetInstanceID());
        foreach (var point in Object.FindObjectsByType<PanelTaskPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (point.gameObject.scene == panel.gameObject.scene)
                Undo.RegisterFullObjectHierarchyUndo(point.gameObject, "配置任务交互点");
        foreach (var manager in Object.FindObjectsByType<MiniGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.RecordObject(manager, "注册任务候选");
        MiniGameDefaultSetup.Ensure(panel);
        ConfigureFonts(panel);
        foreach (var content in panel.GetComponentsInChildren<MiniGameContent>(true)) content.gameObject.SetActive(false);
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go.scene == panel.gameObject.scene && !before.Contains(go.GetInstanceID()))
                Undo.RegisterCreatedObjectUndo(go, "创建小游戏对象");
        // 写入编辑器配置时补齐点位引用，Inspector 中可直接查看和调整。
        foreach (var quest in Object.FindObjectsByType<MiniGameCandidate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (quest.gameObject.scene != panel.gameObject.scene) continue;
            var data = new SerializedObject(quest);
            if (quest is CleanQuest) data.FindProperty("_cleanPoint").objectReferenceValue = quest.GetComponent<CleanMopWater>();
            if (quest is CoffeeQuest) data.FindProperty("_coffeePoint").objectReferenceValue = quest.GetComponent<TaskMakeCoffee>();
            if (quest is StockQuest) data.FindProperty("_stockPoint").objectReferenceValue = quest.GetComponent<StockTradePoint>();
            if (quest is PrinterRepairQuest) data.FindProperty("_printerPoint").objectReferenceValue = quest.GetComponent<PrinterRepairPoint>();
            data.ApplyModifiedProperties();
        }
        foreach (var root in panel.gameObject.scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null) EditorUtility.SetDirty(component);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("清理 Index 0、炒股 Index 3、咖啡 Index 4 已配置。炒股占位终端在咖啡点右侧 3 米，可自行移动；请保存场景。", panel);
    }

    private static void ConfigureFonts(MiniGamePanelUI panel)
    {
        const string assetPath = "Assets/TextMesh Pro/Fonts/MiniGameUI SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/AlibabaPuHuiTi-3-55-Regular.ttf");
            if (source == null) return;
            font = TMP_FontAsset.CreateFontAsset(source, 64, 7, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "MiniGameUI SDF";
            AssetDatabase.CreateAsset(font, assetPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            // 字库可在运行时继续增加字符；预填常用文本，让编辑器预览也完整。
            font.TryAddCharacters("清理垃圾按住鼠标左键滑动擦掉污渍即可完成已目标模拟炒股价格金币持仓浮动盈亏第个点独立随机每次等待产生新自动强制清空提前退出当前买入一只卖全部结果本轮制作咖啡开关烧水拖豆到研磨机滤杯纸粉壶在上方注中顺序点击或拽器具保持区域就位获得天赋0123456789%[]><:+-~ /，。·…", out _);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
        }
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            if (text.GetComponentInParent<global::CleanGame>(true) != null || text.GetComponentInParent<StockGame>(true) != null || text.GetComponentInParent<CoffeeGame>(true) != null || text.GetComponentInParent<PrinterRepairGame>(true) != null)
            { Undo.RecordObject(text, "配置小游戏中文字库"); text.font = font; }
    }
}
