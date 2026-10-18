using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TapTapGameJam.Core;
using TapTapGameJam.UI;

public static class ShoppingRoomSetup
{
    [MenuItem("Tools/Shopping/Build 2D Room")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("/GameScene.unity") || Application.isPlaying)
            throw new System.InvalidOperationException("Open GameScene in edit mode first.");
        var manager = Object.FindFirstObjectByType<ShoppingManager>(FindObjectsInactive.Include);
        if (manager == null) throw new System.InvalidOperationException("ShoppingManager missing.");
        if (manager.transform.Find("ShoppingRoomCanvas") != null)
            throw new System.InvalidOperationException("2D room already exists; edit the existing Canvas.");
        Undo.RegisterFullObjectHierarchyUndo(manager.gameObject, "Build Shopping 2D room");
        manager.gameObject.SetActive(true);

        var root = Rect("ShoppingRoomCanvas", manager.transform);
        root.gameObject.SetActive(false);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var room = root.gameObject.AddComponent<ShoppingRoomView>();

        // 黑色全屏底板覆盖游戏画面，并接收房间空白处的鼠标事件。
        var bars = Rect("BlackBars", root);
        Stretch(bars);
        bars.gameObject.AddComponent<Image>().color = Color.black;
        var frame = Rect("RoomFrame", root);
        Stretch(frame);
        var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;
        frame.gameObject.AddComponent<RectMask2D>();
        var background = Rect("Background_01", frame);
        Stretch(background);
        var bg = background.gameObject.AddComponent<Image>();
        bg.sprite = Sprite("01");
        bg.raycastTarget = false;

        // 背景坐标以图片左上角为原点。床先画，其余热点优先接收鼠标。
        AddHotspot(frame, "床", new Rect(317, 368, 1107, 712));
        AddHotspot(frame, "仓库", new Rect(550, 498, 283, 226));
        AddHotspot(frame, "电脑", new Rect(913, 347, 536, 232));
        AddHotspot(frame, "门", new Rect(1450, 482, 215, 367));

        var so = new SerializedObject(manager);
        so.FindProperty("_shoppingRoom").objectReferenceValue = room;
        so.ApplyModifiedProperties();
        ConfigureNightAndClicks();
        var overlay = manager.transform.Find("FadeOverlayCanvas");
        if (overlay != null)
        {
            overlay.localScale = Vector3.one;
            overlay.gameObject.SetActive(false);
        }
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("ShoppingRoomCanvas created, linked and saved with 4 hover hotspots.");
    }

    private static void AddHotspot(RectTransform frame, string name, Rect bounds)
    {
        Vector2 center = bounds.center;
        Vector2 pivot = new Vector2(center.x / 1920f, 1f - center.y / 1080f);
        var hit = Rect(name, frame);
        hit.anchorMin = new Vector2(bounds.xMin / 1920f, 1f - bounds.yMax / 1080f);
        hit.anchorMax = new Vector2(bounds.xMax / 1920f, 1f - bounds.yMin / 1080f);
        hit.offsetMin = hit.offsetMax = Vector2.zero;
        var target = hit.gameObject.AddComponent<Image>();
        target.color = Color.clear;
        target.raycastTarget = true;

        // 图片仍按整张 1920x1080 对齐，但 pivot 放在物件中心。
        // 用相对尺寸而非像素尺寸，以便在所有分辨率下保持一致。
        var visual = Rect("Visual", hit);
        visual.anchorMin = new Vector2(-bounds.xMin / bounds.width, (bounds.yMax - 1080f) / bounds.height);
        visual.anchorMax = new Vector2((1920f - bounds.xMin) / bounds.width, bounds.yMax / bounds.height);
        visual.pivot = pivot;
        visual.offsetMin = visual.offsetMax = Vector2.zero;
        var image = visual.gameObject.AddComponent<Image>();
        var normal = Sprite(name + "-状态1");
        var hover = Sprite(name + "-状态2");
        image.sprite = normal;
        image.raycastTarget = false;
        var outlineRect = Rect("Outline", visual);
        Stretch(outlineRect);
        var outline = outlineRect.gameObject.AddComponent<Image>();
        string outlineName = name switch
        {
            "床" => "床状态1",
            "仓库" => "箱子状态1",
            "电脑" => "电脑状态1",
            "门" => "门口状态1",
            _ => throw new System.ArgumentOutOfRangeException(nameof(name), name, "Unknown room hotspot.")
        };
        outline.sprite = Sprite("描边/" + outlineName);
        outline.raycastTarget = false;
        outline.enabled = false;
        var component = hit.gameObject.AddComponent<ShoppingRoomHotspot>();
        var so = new SerializedObject(component);
        so.FindProperty("_visual").objectReferenceValue = image;
        so.FindProperty("_outline").objectReferenceValue = outline;
        so.FindProperty("_normal").objectReferenceValue = normal;
        so.FindProperty("_hover").objectReferenceValue = hover;
        so.FindProperty("_hoverScale").floatValue = 1.05f;
        so.FindProperty("_scaleDuration").floatValue = 0.12f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite Sprite(string name)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Model/House/" + name + ".png");
        foreach (var asset in assets) if (asset is Sprite sprite) return sprite;
        throw new System.InvalidOperationException("Missing Sprite: " + name);
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    [MenuItem("Tools/Shopping/Configure Night And Clicks")]
    public static void ConfigureNightAndClicks()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
        var manager = Object.FindFirstObjectByType<ShoppingManager>(FindObjectsInactive.Include);
        if (manager == null) throw new System.InvalidOperationException("ShoppingManager missing.");
        var managerData = new SerializedObject(manager);
        var room = (ShoppingRoomView)managerData.FindProperty("_shoppingRoom").objectReferenceValue;
        var talent = (TalentTree.TalentTreeUI)managerData.FindProperty("_talentTreeUI").objectReferenceValue;
        if (talent == null)
        {
            talent = Object.FindFirstObjectByType<TalentTree.TalentTreeUI>(FindObjectsInactive.Include);
            managerData.FindProperty("_talentTreeUI").objectReferenceValue = talent;
            managerData.ApplyModifiedProperties();
        }
        if (room == null || talent == null) throw new System.InvalidOperationException("Room / talent tree missing.");
        foreach (var hotspot in room.GetComponentsInChildren<ShoppingRoomHotspot>(true))
        {
            Undo.RecordObject(hotspot, "Configure Shopping click");
            var data = new SerializedObject(hotspot);
            data.FindProperty("_modalTalentTree").objectReferenceValue = talent;
            data.ApplyModifiedProperties();
            UnityEngine.Events.UnityAction action = null;
            string method = null;
            if (hotspot.name == "床") { action = manager.OpenTalentTree; method = "OpenTalentTree"; }
            if (hotspot.name == "门") { action = manager.FinishShopping; method = "FinishShopping"; }
            bool exists = false;
            for (int i = 0; i < hotspot.OnClick.GetPersistentEventCount(); i++)
                if (hotspot.OnClick.GetPersistentTarget(i) == manager && hotspot.OnClick.GetPersistentMethodName(i) == method) exists = true;
            if (action != null && !exists) UnityEditor.Events.UnityEventTools.AddPersistentListener(hotspot.OnClick, action);
            EditorUtility.SetDirty(hotspot);
        }
        var night = Object.FindFirstObjectByType<NightManager>(FindObjectsInactive.Include);
        if (night == null)
        {
            var go = new GameObject("NightManager");
            Undo.RegisterCreatedObjectUndo(go, "Create NightManager");
            night = Undo.AddComponent<NightManager>(go);
        }
        Undo.RecordObject(night, "Configure night transition");
        var so = new SerializedObject(night);
        so.FindProperty("_shoppingManager").objectReferenceValue = manager;
        so.FindProperty("_phaseManager").objectReferenceValue = Object.FindFirstObjectByType<GamePhaseManager>(FindObjectsInactive.Include);
        foreach (string field in new[] { "_fadeOverlayCanvas", "_fadeImage", "_fadeText" })
            so.FindProperty(field).objectReferenceValue = managerData.FindProperty(field).objectReferenceValue;
        so.ApplyModifiedProperties();
        var text = (TMPro.TMP_Text)managerData.FindProperty("_fadeText").objectReferenceValue;
        Undo.RecordObject(text, "Fit transition messages");
        Undo.RecordObject(text.rectTransform, "Fit transition messages");
        text.enableAutoSizing = true;
        text.fontSizeMin = 60;
        text.fontSizeMax = 222;
        text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(1400, 320);
        var overlay = (GameObject)managerData.FindProperty("_fadeOverlayCanvas").objectReferenceValue;
        var scaler = overlay != null ? overlay.GetComponent<CanvasScaler>() : null;
        if (scaler != null)
        {
            Undo.RecordObject(scaler, "Scale transition text to screen");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            EditorUtility.SetDirty(scaler);
        }
        EditorUtility.SetDirty(text);
        var scene = manager.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Configured bed / door click events and NightManager.");
    }
}
