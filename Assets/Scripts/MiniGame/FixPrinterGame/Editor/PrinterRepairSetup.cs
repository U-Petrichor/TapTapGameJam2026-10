using System.Linq;
using TMPro;
using TapTapGameJam.MiniGame;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PrinterRepairSetup
{
    [MenuItem("Tools/MiniGames/配置打印机修复QTE")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("请退出 Play 后配置打印机小游戏。");
        var panels = Object.FindObjectsByType<MiniGamePanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (panels.Length != 1) throw new System.InvalidOperationException("请打开只有一个 MiniGamePanelUI 的场景。");
        var panel = panels[0];
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("配置打印机修复QTE");
        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "配置打印机面板");
        var before = new System.Collections.Generic.HashSet<int>(Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(go => go.GetInstanceID()));
        var points = Object.FindObjectsByType<PrinterRepairPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(point => point.gameObject.scene == panel.gameObject.scene).ToArray();
        if (points.Length == 0)
        {
            var pointObject = new GameObject("PrinterRepairPoint");
            var coffee = Object.FindFirstObjectByType<CoffeeQuest>(FindObjectsInactive.Include);
            if (coffee != null) pointObject.transform.SetParent(coffee.transform.parent, false);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/MiniGame/打印文件.fbx");
            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, pointObject.transform);
                visual.name = "PrinterModel";
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new System.InvalidOperationException("打印机模型没有 Renderer。");
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                // 原模型带有导出时的世界坐标偏移。入口以真实模型中心为原点。
                Vector3 modelPosition = visual.transform.position;
                pointObject.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                visual.transform.position = modelPosition;
            }
            else
            {
                pointObject.transform.position = coffee != null ? coffee.transform.position + Vector3.left * 3 : Vector3.zero;
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "PrinterPlaceholder"; visual.transform.SetParent(pointObject.transform, false);
                visual.transform.localPosition = Vector3.up * 0.7f; visual.transform.localScale = new Vector3(1.8f, 1.2f, 0.8f);
            }
            var detector = pointObject.AddComponent<CapsuleCollider>();
            detector.isTrigger = true; detector.center = Vector3.up * 0.7f; detector.radius = 1.6f; detector.height = 3f;
            var point = pointObject.AddComponent<PrinterRepairPoint>(); point.ConfigureGameIndex(5);
            pointObject.AddComponent<PrinterRepairQuest>();
            points = new[] { point };
        }
        foreach (var point in points) Undo.RegisterFullObjectHierarchyUndo(point.gameObject, "配置打印机任务");
        foreach (var manager in Object.FindObjectsByType<MiniGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (manager.gameObject.scene == panel.gameObject.scene) Undo.RecordObject(manager, "注册打印机候选任务");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/MiniGameUI SDF.asset");
        if (font == null) font = TMP_Settings.defaultFontAsset;
        MiniGameDefaultSetup.EnsurePrinter(panel, font);
        var quest = points[0].GetComponent<PrinterRepairQuest>() ??
            Undo.AddComponent<PrinterRepairQuest>(points[0].gameObject);
        quest.ConfigurePoints(points);
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go.scene == panel.gameObject.scene && !before.Contains(go.GetInstanceID())) Undo.RegisterCreatedObjectUndo(go, "创建打印机小游戏对象");
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        Debug.Log("打印机修复 QTE 已配置为 Index 5；移动 PrinterRepairPoint 父物体即可同步模型、交互与箭头。", points[0]);
    }
}
