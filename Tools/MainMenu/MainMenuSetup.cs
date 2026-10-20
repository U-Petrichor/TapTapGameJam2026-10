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

// Explicit rebuild tool; the request file is only used during initial installation.
[InitializeOnLoad]
public static class MainMenuSetup
{
    const string Request = "Temp/MainMenuSetup.request";
    static TMP_FontAsset font;
    static readonly Color Paper = new Color(.96f, .94f, .85f);
    static MainMenuSetup() { EditorApplication.delayCall += RunRequested; }
    static void RunRequested()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Build(); File.WriteAllText("Temp/MainMenuSetup.result", "SUCCESS"); }
        catch (Exception ex) { File.WriteAllText("Temp/MainMenuSetup.result", ex.ToString()); Debug.LogException(ex); }
    }
    [MenuItem("Tools/UI/Rebuild Main Menu Placeholders")]
    public static void Build()
    {
        const string path = "Assets/Scenes/MainMenu.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool loadedHere = !scene.isLoaded;
        if (loadedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        if (scene.isDirty) throw new InvalidOperationException("MainMenu has unsaved edits. Save the scene before rebuilding.");
        var oldActive = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/MiniGameUI SDF.asset");
        var menu = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuUIInteract>(true)).Single();
        var so = new SerializedObject(menu);
        var start = (Button)so.FindProperty("startGameBtn").objectReferenceValue;
        var settings = (Button)so.FindProperty("settingsBtn").objectReferenceValue;
        var exit = (Button)so.FindProperty("exitBtn").objectReferenceValue;
        var container = (RectTransform)start.transform.parent;
        var canvas = menu.GetComponentInParent<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        var menuRect = (RectTransform)menu.transform;
        menuRect.anchorMin = menuRect.anchorMax = new Vector2(0, .5f);
        menuRect.anchoredPosition = new Vector2(260, -100);
        menuRect.sizeDelta = new Vector2(300, 560);
        container.sizeDelta = new Vector2(300, 560);
        var layout = container.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 16; layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        var cont = UnityEngine.Object.Instantiate(start, container); cont.name = "ContinueGame";
        var buttons = new[] { start, cont, settings, exit };
        var labels = new[] { "开始游戏", "继续游戏", "设置", "退出游戏" };
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].transform.SetSiblingIndex(i);
            var label = buttons[i].GetComponentInChildren<TMP_Text>(true);
            label.text = labels[i]; label.font = font; label.fontSize = 38;
            var element = buttons[i].GetComponent<LayoutElement>() ?? buttons[i].gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 90; element.preferredWidth = 300;
        }
        cont.interactable = false;
        // Build the final artwork prefab explicitly before rebuilding the menu.
        var settingsAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsArtworkBuilder.PrefabPath);
        if (settingsAsset == null) { SettingsArtworkBuilder.Build(); settingsAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsArtworkBuilder.PrefabPath); }
        var oldSettings = scene.GetRootGameObjects().First(g => g.name == "Settings");
        UnityEngine.Object.DestroyImmediate(oldSettings);
        var settingsRoot = (GameObject)PrefabUtility.InstantiatePrefab(settingsAsset, scene);
        settingsRoot.SetActive(false);
        var back = settingsRoot.GetComponent<SettingsMenu>().BackButton;
        var exitRoot = CanvasRoot("ExitConfirmationCanvas",30);
        var dimmer = Image("InputBlocker",exitRoot.transform,Vector2.zero,new Vector2(1920,1080),new Color(0,0,0,.3f)); Stretch(dimmer.rectTransform);
        var note = Image("NotePlaceholder",dimmer.transform,Vector2.zero,new Vector2(570,430),new Color(.98f,.93f,.48f));
        Label("Question",note.transform,"是否确认退出游戏？",new Vector2(0,60),new Vector2(500,100),34);
        var cancel = Button("Cancel",note.transform,"取消",new Vector2(-140,-125),new Vector2(180,70));
        var confirm = Button("Confirm",note.transform,"确定",new Vector2(140,-125),new Vector2(180,70));
        Set(so,"continueGameBtn",cont);
        Set(so,"settingsCanvas",settingsRoot); Set(so,"exitConfirmation",exitRoot);
        Set(so,"settingsBackBtn",back); Set(so,"cancelExitBtn",cancel); Set(so,"confirmExitBtn",confirm);
        so.ApplyModifiedPropertiesWithoutUndo();
        exitRoot.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SceneManager.SetActiveScene(oldActive);
        if (loadedHere) EditorSceneManager.CloseScene(scene,true);
        Debug.Log("[MainMenuSetup] Main menu saved successfully.");
    }
    static void Set(SerializedObject so,string key,UnityEngine.Object value) => so.FindProperty(key).objectReferenceValue = value;
    static GameObject CanvasRoot(string name,int order)
    {
        var root = new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.layer = 5;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        return root;
    }
    static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var go = new GameObject(name,typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent,false); rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero; }
    static Image Image(string name,Transform parent,Vector2 position,Vector2 size,Color color)
    { var image = Rect(name,parent,position,size).gameObject.AddComponent<Image>(); image.color = color; return image; }
    static TMP_Text Label(string name,Transform parent,string text,Vector2 position,Vector2 size,int fontSize)
    { var label = Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = fontSize; label.color = new Color(.12f,.12f,.14f); label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; return label; }
    static Button Button(string name,Transform parent,string text,Vector2 position,Vector2 size)
    { var image = Image(name,parent,position,size,new Color(.87f,.85f,.75f)); var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; var label = Label("Text",image.transform,text,Vector2.zero,size,32); Stretch(label.rectTransform); return button; }
    static Slider Slider(string name,Transform parent,Vector2 position)
    {
        var bg = Image(name,parent,position,new Vector2(650,54),new Color(.84f,.8f,.65f));
        var slider = bg.gameObject.AddComponent<Slider>();
        var area = Rect("HandleArea",bg.transform,Vector2.zero,new Vector2(610,54));
        var handle = Image("Handle",area,Vector2.zero,new Vector2(24,70),new Color(.9f,.45f,.15f));
        slider.handleRect = handle.rectTransform; slider.targetGraphic = handle; slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
        return slider;
    }
}

