using System;
using System.IO;
using System.Linq;
using TMPro;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit authoring only. Runtime code never rebuilds or repositions this layout.
public static class MainMenuArtworkBuilder
{
    const string Art = "Assets/Model/UI/开始界面/";
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string FontPath = "Assets/TextMesh Pro/Examples & Extras/Fonts/fusion-pixel-12px-monospaced-zh_hans 2 SDF.asset";
    static TMP_FontAsset font;

    [MenuItem("Tools/UI/Apply Main Menu Artwork")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before applying main menu artwork.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (scene.isDirty) throw new InvalidOperationException("Save MainMenu edits before applying artwork.");
        var previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        Directory.CreateDirectory("RecoverySnapshots/MainMenuArtwork");
        EditorSceneManager.SaveScene(scene, "RecoverySnapshots/MainMenuArtwork/MainMenu_before_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
        try
        {
            foreach (string name in new[] { "背景", "标题", "气泡一", "气泡二", "气泡三", "气泡四", "退出游戏" })
            {
                Import(name);
                if (name != "背景" && name != "标题") Import(name + "-未点击");
            }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("Missing Chinese UI font.");
            var menu = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuUIInteract>(true)).Single();
            var canvas = menu.GetComponentInParent<Canvas>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            // The full design fits inside every aspect ratio; extra space uses a dark matte.
            var oldMatte = canvas.transform.Find("AspectMatte");
            if (oldMatte != null) UnityEngine.Object.DestroyImmediate(oldMatte.gameObject);
            var matte = Rect("AspectMatte", canvas.transform, 0, 0, 0, 0).gameObject.AddComponent<Image>();
            matte.color = new Color(.035f, .055f, .085f, 1); matte.raycastTarget = false;
            matte.rectTransform.anchorMin = Vector2.zero; matte.rectTransform.anchorMax = Vector2.one;
            matte.rectTransform.offsetMin = matte.rectTransform.offsetMax = Vector2.zero;
            matte.transform.SetAsFirstSibling();

            var design = (RectTransform)menu.transform;
            design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f, .5f);
            design.anchoredPosition = Vector2.zero; design.sizeDelta = new Vector2(1920, 1080); design.localScale = Vector3.one;
            // This command is an explicit rebuild; subsequent designer edits remain untouched at runtime.
            for (int i = design.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(design.GetChild(i).gameObject);
            Pic("Background", design, "背景", 960, 540);
            Pic("Title", design, "标题", 936, 510);

            var start = ArtButton("StartGame", design, "气泡一", "开始游戏", 274, 247, 320, 100, 62);
            var resume = ArtButton("ContinueGame", design, "气泡二", "继续游戏", 263, 444, 320, 100, 62);
            var settings = ArtButton("Settings", design, "气泡三", "系统设置", 245, 662, 320, 100, 62);
            var gallery = ArtButton("Gallery", design, "气泡四", "图鉴", 215, 853, 315, 100, 68);
            var exit = ArtButton("Exit", design, "退出游戏", "原地下班", 1664, 978, 280, 92, 58);
            var buttons = new[] { start, resume, settings, gallery, exit };
            for (int i = 0; i < buttons.Length; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                    selectOnDown = buttons[(i + 1) % buttons.Length],
                    selectOnLeft = i == 4 ? gallery : null, selectOnRight = i != 4 ? exit : null };
                buttons[i].navigation = nav;
            }
            var availability = resume.gameObject.AddComponent<CanvasGroup>();
            var hint = Label("UnavailableHint", design, "暂无存档", 263, 516, 260, 40, 25);
            hint.color = new Color(1f, .94f, .65f); hint.gameObject.SetActive(false);
            var data = new SerializedObject(menu);
            Set(data, "startGameBtn", start); Set(data, "continueGameBtn", resume);
            Set(data, "settingsBtn", settings); Set(data, "galleryBtn", gallery); Set(data, "exitBtn", exit);
            Set(data, "continueAvailabilityVisual", availability); Set(data, "continueUnavailableHint", hint.gameObject);
            data.ApplyModifiedPropertiesWithoutUndo();
            Validate(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save MainMenu.");
            Debug.Log("MainMenu artwork applied. Editable design: Canvas/MainMenuPanel; existing panels and intro preserved.");
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    [MenuItem("Tools/UI/Apply Main Menu Pixel Font")]
    public static void ApplyFontOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before editing the main menu.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Save MainMenu edits before applying the font.");
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (asset == null) throw new FileNotFoundException(FontPath);
            var menu = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuUIInteract>(true)).Single();
            var labels = menu.GetComponentsInChildren<TMP_Text>(true);
            foreach (var label in labels)
            {
                Undo.RecordObject(label, "Apply main menu pixel font");
                label.font = asset;
                label.fontSharedMaterial = asset.material;
                EditorUtility.SetDirty(label);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save MainMenu.");
            Debug.Log("MainMenu: applied " + asset.name + " to " + labels.Length + " labels; layout preserved.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static void Import(string name)
    {
        string path = Art + name + ".PNG";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { AssetDatabase.ImportAsset(path); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (importer == null) throw new FileNotFoundException(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.filterMode = FilterMode.Bilinear;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static Sprite Sprite(string name) => AssetDatabase.LoadAllAssetsAtPath(Art + name + ".PNG").OfType<Sprite>().Single();
    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
    }
    static Image Pic(string name, Transform parent, string sprite, float x, float y)
    {
        var image = Rect(name, parent, x, y, 1920, 1080).gameObject.AddComponent<Image>();
        image.sprite = Sprite(sprite); image.raycastTarget = false; return image;
    }
    static TMP_Text Label(string name, Transform parent, string text, float x, float y, float w, float h, float size)
    {
        var label = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size; label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; return label;
    }
    static Button ArtButton(string name, Transform parent, string sprite, string text, float x, float y, float w, float h, float size)
    {
        var rect = Rect(name, parent, x, y, w, h);
        var hit = rect.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
        var artwork = Pic("Artwork", rect, sprite + "-未点击", 960 - x + w / 2, 540 - y + h / 2);
        var label = Label("Label", rect, text, w / 2, h / 2, w, h, size);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = artwork;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState { highlightedSprite = Sprite(sprite), selectedSprite = Sprite(sprite),
            pressedSprite = Sprite(sprite), disabledSprite = Sprite(sprite + "-未点击") };
        return button;
    }
    static void Set(SerializedObject data, string field, UnityEngine.Object value) => data.FindProperty(field).objectReferenceValue = value;

    public static void Validate(MainMenuUIInteract menu)
    {
        var data = new SerializedObject(menu);
        foreach (string field in new[] { "startGameBtn", "continueGameBtn", "settingsBtn", "galleryBtn", "exitBtn", "settingsCanvas", "galleryCanvas", "exitConfirmation", "settingsBackBtn", "galleryBackBtn", "cancelExitBtn", "confirmExitBtn", "introTransition", "continueAvailabilityVisual", "continueUnavailableHint" })
            if (data.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing main menu reference: " + field);
        foreach (var button in menu.GetComponentsInChildren<Button>(true))
        {
            if (button.onClick.GetPersistentEventCount() != 0) throw new InvalidOperationException("Unexpected persistent listener: " + button.name);
            if (button.GetComponentsInChildren<Image>().Count(i => i.raycastTarget) != 1)
                throw new InvalidOperationException("Expected exactly one local hit area: " + button.name);
        }
    }
}
