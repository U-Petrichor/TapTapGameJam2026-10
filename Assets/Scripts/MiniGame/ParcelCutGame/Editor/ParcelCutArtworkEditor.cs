using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ParcelCutArtworkEditor
{
    public const string CutterArtworkDirectory = "Assets/Model/MiniGame/ParcelCut/";

    [MenuItem("Tools/MiniGames/更新拆快递手臂与美工刀素材")]
    public static void UpdateArtwork()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("请退出 Play 后更新并保存场景。");
        var games = Object.FindObjectsByType<ParcelCutGame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (games.Length != 1) throw new System.InvalidOperationException("当前场景需要恰好一个拆快递小游戏。");
        var game = games[0];
        Undo.RegisterFullObjectHierarchyUndo(game.gameObject, "更新拆快递持刀素材");
        ApplyCutterArtwork(game);
        game.RebuildTapeGeometry();
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        Debug.Log("拆快递手臂、美工刀已更新，胶带判定按当前图片重新校准。", game);
    }

    public static void ApplyCutterArtwork(ParcelCutGame game)
    {
        var bladeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CutterArtworkDirectory + "美工刀.png");
        var armTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CutterArtworkDirectory + "手刀.png");
        if (bladeTexture == null || armTexture == null)
            throw new System.InvalidOperationException("缺少手刀或美工刀素材。");
        var root = game.transform.Find("CutterRig") as RectTransform;
        if (root == null)
        {
            var go = new GameObject("CutterRig", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "创建持刀组合");
            root = (RectTransform)go.transform;
            root.SetParent(game.transform, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            root.anchoredPosition = new Vector2(240, 35);
        }
        root.gameObject.layer = game.gameObject.layer;
        // Keep these existing names/references for scene compatibility.
        var blade = FindOrCreate(game, root, "CutterBlade");
        var arm = FindOrCreate(game, root, "CutterHandle");
        SetVisual(blade, bladeTexture, new Vector2(187.52f, 49.28f), new Vector2(0, 0), Vector2.zero);
        SetVisual(arm, armTexture, new Vector2(259.2f, 162.56f), new Vector2(0.5f, 0.5f), new Vector2(180, -42));
        blade.transform.SetAsFirstSibling();
        arm.transform.SetAsLastSibling();
        root.SetAsLastSibling();
        game.ConfigureCutterVisual(root, blade, arm);
        foreach (var label in game.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (!label.text.Contains("剪刀")) continue;
            label.text = label.text.Replace("剪刀", "美工刀");
            EditorUtility.SetDirty(label);
        }
        EditorUtility.SetDirty(blade); EditorUtility.SetDirty(arm);
    }

    private static RawImage FindOrCreate(ParcelCutGame game, RectTransform root, string name)
    {
        var found = root.Find(name) ?? game.transform.Find(name);
        if (found == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            Undo.RegisterCreatedObjectUndo(go, "创建持刀图片");
            found = go.transform;
        }
        found.SetParent(root, false);
        found.gameObject.layer = game.gameObject.layer;
        return found.GetComponent<RawImage>() ?? Undo.AddComponent<RawImage>(found.gameObject);
    }

    private static void SetVisual(RawImage image, Texture texture, Vector2 size, Vector2 pivot, Vector2 position)
    {
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition3D = position;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        image.texture = texture; image.uvRect = new Rect(0, 0, 1, 1);
        image.color = Color.white; image.raycastTarget = false;
        image.gameObject.SetActive(true);
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
