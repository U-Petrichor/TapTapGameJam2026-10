using System;
using System.Linq;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CoffeeGrinderSetup
{
    private const string PrefabPath = "Assets/Resources/MiniGame/CoffeeGame.prefab";
    private const string ArtworkPath = "Assets/Model/MiniGame/png(1)/png/";

    [MenuItem("Tools/MiniGames/更新咖啡研磨机与旋转手柄")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后更新研磨机。");
        var body = AssetDatabase.LoadAllAssetsAtPath(ArtworkPath + "研磨机.png").OfType<Sprite>()
            .FirstOrDefault(s => s.rect.size == new Vector2(589, 668));
        var handle = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtworkPath + "研磨机手柄.png");
        if (body == null || handle == null || handle.width != 589 || handle.height != 668)
            throw new InvalidOperationException("需要相同 589×668 画布的研磨机和研磨机手柄素材。");

        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Apply(prefab.GetComponent<CoffeeGame>(), body, handle);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        foreach (var game in UnityEngine.Object.FindObjectsByType<CoffeeGame>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(game) != PrefabPath) continue;
            Undo.RegisterFullObjectHierarchyUndo(game.gameObject, "更新咖啡研磨机手柄");
            Apply(game, body, handle);
            PrefabUtility.RecordPrefabInstancePropertyModifications(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
        }
        Debug.Log("研磨机与手柄已叠合，转轴位于圆形连接处；预制体和场景已保存。");
    }

    private static void Apply(CoffeeGame game, Sprite body, Texture2D handle)
    {
        var data = new SerializedObject(game);
        var grinder = (RectTransform)data.FindProperty("_grindButton").objectReferenceValue;
        var image = grinder.GetComponent<Image>();
        image.sprite = body;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = true;
        var placeholder = grinder.Find("GrinderPlaceholderLabel");
        if (placeholder != null) placeholder.gameObject.SetActive(false);

        // The PNGs share a canvas. Match Image.preserveAspect before overlaying them.
        var frame = grinder.Find("GrinderHandleFrame") as RectTransform;
        if (frame == null)
        {
            frame = (RectTransform)new GameObject("GrinderHandleFrame", typeof(RectTransform), typeof(AspectRatioFitter)).transform;
            frame.SetParent(grinder, false);
        }
        frame.gameObject.layer = grinder.gameObject.layer;
        var fitter = frame.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 589f / 668f;
        var visual = frame.Find("GrinderHandle") as RectTransform;
        if (visual == null)
        {
            visual = (RectTransform)new GameObject("GrinderHandle", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).transform;
            visual.SetParent(frame, false);
        }
        visual.gameObject.layer = grinder.gameObject.layer;
        visual.anchorMin = Vector2.zero;
        visual.anchorMax = Vector2.one;
        // Source pixel (333,285), measured from the top left, is the axle centre.
        visual.pivot = new Vector2(333f / 589f, 1f - 285f / 668f);
        visual.offsetMin = visual.offsetMax = Vector2.zero;
        var graphic = visual.GetComponent<RawImage>();
        graphic.texture = handle;
        graphic.color = Color.white;
        // The rotating picture must not change the stationary grinding hit region.
        graphic.raycastTarget = false;
        data.FindProperty("_grinderHandle").objectReferenceValue = visual;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(game);
        EditorUtility.SetDirty(image);
    }
}
