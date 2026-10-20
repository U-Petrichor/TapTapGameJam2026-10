using TapTapGameJam.Core;
using TapTapGameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DailySettlementSetup
{
    [MenuItem("Tools/Shopping/Build Daily Settlement")]
    public static void Build()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
        var manager = Object.FindFirstObjectByType<ShoppingManager>(FindObjectsInactive.Include);
        if (manager == null || !manager.gameObject.scene.path.EndsWith("/GameScene.unity"))
            throw new System.InvalidOperationException("Open GameScene first.");
        if (manager.transform.Find("DailySettlementCanvas") != null)
            throw new System.InvalidOperationException("Settlement already exists; edit it in Hierarchy.");
        var data = new SerializedObject(manager);
        var font = ((TMP_Text)data.FindProperty("_fadeText").objectReferenceValue).font;
        var root = Rect("DailySettlementCanvas", manager.transform);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build daily settlement");
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 75;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var shade = Rect("DimBackground", root);
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
        shade.offsetMin = shade.offsetMax = Vector2.zero;
        shade.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .65f);
        var paper = Rect("SettlementPaper", root);
        paper.sizeDelta = new Vector2(850, 935);
        var paperImage = paper.gameObject.AddComponent<Image>();
        paperImage.sprite = Sprite("IMG_4892 2"); paperImage.raycastTarget = false;
        var illustration = Rect("IllustrationPlaceholder", paper);
        illustration.anchoredPosition = new Vector2(-15, 105);
        illustration.sizeDelta = new Vector2(420, 310);
        var placeholder = illustration.gameObject.AddComponent<Image>();
        placeholder.color = new Color(.85f, .85f, .85f); placeholder.raycastTarget = false;
        var talent = Label("TalentEarned", paper, font, new Vector2(-15, -145), "获得天赋点：0");
        var coins = Label("CoinsEarned", paper, font, new Vector2(-15, -205), "赚取金币：0");
        var back = Rect("ContinueButton", paper);
        back.anchoredPosition = new Vector2(240, 290); back.sizeDelta = new Vector2(140, 140);
        var icon = back.gameObject.AddComponent<Image>(); icon.sprite = Sprite("IMG_5042 3");
        var button = back.gameObject.AddComponent<Button>(); button.targetGraphic = icon;
        var view = root.gameObject.AddComponent<DailySettlementView>();
        var viewData = new SerializedObject(view);
        viewData.FindProperty("_coinsText").objectReferenceValue = coins;
        viewData.FindProperty("_talentText").objectReferenceValue = talent;
        viewData.FindProperty("_continueButton").objectReferenceValue = button;
        viewData.ApplyModifiedPropertiesWithoutUndo();
        Undo.RecordObject(manager, "Link daily settlement");
        data.FindProperty("_settlementView").objectReferenceValue = view;
        data.FindProperty("_phaseManager").objectReferenceValue = Object.FindFirstObjectByType<GamePhaseManager>();
        data.ApplyModifiedProperties();
        root.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("Daily settlement created, linked and saved.");
    }

    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, Vector2 position, string value)
    {
        var rt = Rect(name, parent); rt.anchoredPosition = position; rt.sizeDelta = new Vector2(450, 60);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = 40; text.color = Color.black;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
        text.enableAutoSizing = true; text.fontSizeMin = 22; text.fontSizeMax = 40;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        return rt;
    }

    private static Sprite Sprite(string name)
    {
        string path = "Assets/Model/UI/菜单组件/每日结算/" + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
