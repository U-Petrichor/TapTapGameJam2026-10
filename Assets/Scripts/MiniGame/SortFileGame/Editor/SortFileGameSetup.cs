using System;
using System.Collections.Generic;
using TMPro;
using TapTapGameJam.MiniGame;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SortFileInteraction = TapTapGameJam.MiniGame.SortFilePoint;
using SortFileQuestCandidate = TapTapGameJam.MiniGame.SortFileQuest;

// 在 Unity 当前场景中配置，支持 Undo，不会自动保存或覆盖磁盘上的场景。
public static class SortFileGameSetup
{
    private static readonly string[] DepartmentObjects =
        { "HumanResources", "Administration", "Talent", "Development" };
    private static readonly Color[] DepartmentColors =
    {
        new Color(0.65f, 0.87f, 0.74f), new Color(1f, 0.83f, 0.61f),
        new Color(0.65f, 0.80f, 1f), new Color(0.84f, 0.73f, 0.97f)
    };

    [MenuItem("Tools/MiniGames/配置分拣文件小游戏")]
    public static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("请退出 Play 模式后配置分拣文件小游戏。");
            return;
        }
        RectTransform root = null;
        foreach (var rect in UnityEngine.Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rect.name == "SortFileGame" && rect.GetComponentInParent<MiniGamePanelUI>(true) != null)
            {
                if (root != null) { Debug.LogError("存在多个 SortFileGame，请保留一个再配置。"); return; }
                root = rect;
            }
        }
        if (root == null)
        {
            Debug.LogError("请打开 GameScene，确保 Panel 的 ContentRoot 下有 SortFileGame。");
            return;
        }
        var panel = root.GetComponentInParent<MiniGamePanelUI>(true);
        var panelData = new SerializedObject(panel);
        var entries = panelData.FindProperty("_miniGames");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var mapped = entry.FindPropertyRelative("Content").objectReferenceValue as MiniGameContent;
            if (entry.FindPropertyRelative("Index").intValue == 2 && mapped != null && mapped.gameObject != root.gameObject)
            {
                Debug.LogError("Index 2 已被其他小游戏使用，请先调整编号。");
                return;
            }
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("配置分拣文件小游戏");
        var game = root.GetComponent<SortFileGame>() ?? Undo.AddComponent<SortFileGame>(root.gameObject);
        Undo.RecordObject(root, "设置小游戏区域");
        if (root.sizeDelta == new Vector2(100f, 100f)) root.sizeDelta = new Vector2(1100f, 600f);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Fonts/AlibabaPuHuiTi-3-55-Regular SDF.asset") ?? TMP_Settings.defaultFontAsset;

        bool defaultLayout = true;
        foreach (string name in DepartmentObjects)
        {
            var area = root.Find(name) as RectTransform;
            if (area != null && area.anchoredPosition != Vector2.zero) defaultLayout = false;
        }
        var gameData = new SerializedObject(game);
        var departments = gameData.FindProperty("_departments");
        departments.arraySize = 4;
        Vector2[] positions = { new Vector2(-370f, 145f), new Vector2(370f, 145f),
            new Vector2(-370f, -145f), new Vector2(370f, -145f) };
        for (int i = 0; i < 4; i++)
        {
            var area = EnsureRect(root, DepartmentObjects[i], new Vector2(240f, 220f), positions[i]);
            if (defaultLayout) SetRect(area, new Vector2(240f, 220f), positions[i]);
            var background = area.GetComponent<Image>();
            if (background == null)
            {
                background = Undo.AddComponent<Image>(area.gameObject);
                background.color = new Color(DepartmentColors[i].r, DepartmentColors[i].g, DepartmentColors[i].b, 0.25f);
            }
            Undo.RecordObject(background, "设置接收区域");
            background.raycastTarget = false;
            var count = EnsureText(area, "CountLabel", new Vector2(220f, 40f), new Vector2(0f, -85f), font);
            count.text = SortFileRound.DepartmentName((SortFileDepartment)i) + "  0";
            var entry = departments.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("Department").enumValueIndex = i;
            entry.FindPropertyRelative("Area").objectReferenceValue = area;
            entry.FindPropertyRelative("FileColor").colorValue = DepartmentColors[i];
            entry.FindPropertyRelative("CountText").objectReferenceValue = count;
        }

        var stack = EnsureRect(root, "FileStackRoot", new Vector2(260f, 220f), Vector2.zero);
        var drag = EnsureRect(root, "DragRoot", Vector2.zero, Vector2.zero);
        Undo.RecordObject(drag, "设置拖动层");
        drag.anchorMin = Vector2.zero;
        drag.anchorMax = Vector2.one;
        drag.anchoredPosition = Vector2.zero;
        drag.sizeDelta = Vector2.zero;
        drag.SetAsLastSibling();
        var templateRect = EnsureRect(root, "FileTemplate", new Vector2(160f, 110f), Vector2.zero);
        var template = templateRect.GetComponent<SortFileCard>() ?? Undo.AddComponent<SortFileCard>(templateRect.gameObject);
        var label = EnsureText(templateRect, "DepartmentLabel", new Vector2(150f, 100f), Vector2.zero, font);
        label.text = "部门文件";
        var templateData = new SerializedObject(template);
        templateData.FindProperty("_departmentText").objectReferenceValue = label;
        templateData.ApplyModifiedProperties();
        Undo.RecordObject(templateRect.gameObject, "禁用文件模板");
        templateRect.gameObject.SetActive(false);
        var status = EnsureText(root, "StatusText", new Vector2(600f, 40f), new Vector2(0f, -265f), font);
        status.text = "已整理 0 / 12";
        var instruction = EnsureText(root, "InstructionText", new Vector2(800f, 40f), new Vector2(0f, 265f), font);
        instruction.text = "拖动最上面的文件，放到对应部门";
        gameData.FindProperty("_fileStackRoot").objectReferenceValue = stack;
        gameData.FindProperty("_dragRoot").objectReferenceValue = drag;
        gameData.FindProperty("_fileTemplate").objectReferenceValue = template;
        gameData.FindProperty("_statusText").objectReferenceValue = status;
        gameData.ApplyModifiedProperties();

        int mapIndex = -1;
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Index").intValue == 2) mapIndex = i;
        if (mapIndex < 0) { mapIndex = entries.arraySize; entries.arraySize++; }
        var mapping = entries.GetArrayElementAtIndex(mapIndex);
        mapping.FindPropertyRelative("Index").intValue = 2;
        mapping.FindPropertyRelative("Content").objectReferenceValue = game;
        panelData.ApplyModifiedProperties();

        var points = new List<SortFileInteraction>();
        foreach (var point in UnityEngine.Object.FindObjectsByType<SortFileInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (point.gameObject.scene == root.gameObject.scene) points.Add(point);

        SortFileQuestCandidate quest = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<SortFileQuestCandidate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (candidate.gameObject.scene == root.gameObject.scene) { quest = candidate; break; }
        if (quest == null && points.Count > 0)
            quest = Undo.AddComponent<SortFileQuestCandidate>(points[0].gameObject);
        if (quest != null)
        {
            quest.ConfigurePoints(points);
            foreach (var manager in UnityEngine.Object.FindObjectsByType<MiniGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (manager.gameObject.scene != root.gameObject.scene) continue;
                var managerData = new SerializedObject(manager);
                var pool = managerData.FindProperty("_candidatePool");
                bool found = false;
                for (int i = 0; i < pool.arraySize; i++)
                    if (pool.GetArrayElementAtIndex(i).objectReferenceValue == quest) found = true;
                if (!found) { pool.arraySize++; pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = quest; }
                managerData.ApplyModifiedProperties();
            }
        }
        int pointCount = points.Count;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = root.gameObject;
        Debug.Log($"分拣 UI 和 Index 2 已配置，已接入 {pointCount} 个分拣交互点。请检查 DetectCollider 绑定并保存场景。", root);
    }

    private static RectTransform EnsureRect(RectTransform parent, string name, Vector2 size, Vector2 position)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect != null) return rect;
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "创建分拣 UI");
        rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        SetRect(rect, size, position);
        return rect;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        Undo.RecordObject(rect, "设置 UI 布局");
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static TMP_Text EnsureText(RectTransform parent, string name, Vector2 size, Vector2 position, TMP_FontAsset font)
    {
        var rect = EnsureRect(parent, name, size, position);
        var text = rect.GetComponent<TextMeshProUGUI>() ?? Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
        Undo.RecordObject(text, "设置 UI 文字");
        text.font = font;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.12f, 0.15f, 0.2f);
        text.raycastTarget = false;
        return text;
    }
}
