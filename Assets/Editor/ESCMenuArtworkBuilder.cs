using System;
using System.IO;
using System.Linq;
using TMPro;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit authoring tool. Runtime scripts never change designer-authored layout.
public static class ESCMenuArtworkBuilder
{
    const string PrefabPath = "Assets/Prefabs/UI/ESCMenu.prefab";
    const string Art = "Assets/Model/UI/菜单组件/";
    static TMP_FontAsset font;

    [MenuItem("Tools/UI/Build ESC Menu Artwork")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits first.");
        var backup = "RecoverySnapshots/ESCMenu/" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        GameObject root = null;
        try
        {
            var main = SceneManager.GetSceneByPath("Assets/Scenes/MainMenu.unity");
            if (!main.isLoaded) main = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Additive);
            var mainMenu = main.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuUIInteract>(true)).Single();
            var source = new SerializedObject(mainMenu);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Fonts/fusion-pixel-12px-monospaced-zh_hans 2 SDF.asset");
            if (font == null) throw new InvalidOperationException("Missing Chinese font.");
            root = new GameObject("ESCMenu", typeof(RectTransform));
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.AddComponent<GraphicRaycaster>();
            var controller = root.AddComponent<ESCMenu>();
            var menu = Rect("MenuRoot", root.transform, 0, 0, 0, 0); Stretch(menu);
            var shade = Picture("Dimmer", menu, null, 0, 0, 0, 0, true);
            Stretch(shade.rectTransform); shade.color = new Color(0, 0, 0, .65f);
            var design = Rect("Design", menu, 0, 0, 1920, 1080);
            Picture("PaperBackground", design, Sprite(Art + "每日结算/IMG_4892 2.png"), 0, 0, 1010, 1128, false);
            var group = design.gameObject.AddComponent<CanvasGroup>();
            var resume = Button("ContinueGame", design, Sprite(Art + "每日结算/IMG_5042 3.png"), "", 310, 365, 160, 155);
            var strip = Sprite(Art + "系统设置/信息栏.png");
            var settings = Button("Settings", design, strip, "系统设置", -20, 170, 420, 94);
            var gallery = Button("Gallery", design, strip, "游戏图鉴", -20, 35, 420, 94);
            var save = Button("SaveAndExit", design, strip, "保存并退出", -20, -100, 420, 94);
            var exit = Button("ExitToDesktop", design, strip, "退出至桌面", -20, -235, 420, 94);
            var hint = Label("SaveStatus", design, "进度按阶段检查点保存，继续游戏将从本阶段开始。", -20, -327, 490, 64, 21);
            hint.color = new Color(.25f, .22f, .16f);
            var buttons = new[] {settings, gallery, save, exit, resume};
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].navigation = new Navigation {mode=Navigation.Mode.Explicit,
                    selectOnUp=buttons[(i+buttons.Length-1)%buttons.Length], selectOnDown=buttons[(i+1)%buttons.Length],
                    selectOnRight=resume, selectOnLeft=settings};

            var gallerySource = (GameObject)source.FindProperty("galleryCanvas").objectReferenceValue;
            var exitSource = (GameObject)source.FindProperty("exitConfirmation").objectReferenceValue;
            var galleryPanel = ClonePanel(gallerySource, root.transform, 160);
            var exitPanel = ClonePanel(exitSource, root.transform, 170);
            var galleryBack = FindCloneButton(source, "galleryBackBtn", gallerySource, galleryPanel);
            var cancel = FindCloneButton(source, "cancelExitBtn", exitSource, exitPanel);
            var confirm = FindCloneButton(source, "confirmExitBtn", exitSource, exitPanel);
            cancel.navigation = new Navigation {mode=Navigation.Mode.Explicit, selectOnRight=confirm, selectOnDown=confirm};
            confirm.navigation = new Navigation {mode=Navigation.Mode.Explicit, selectOnLeft=cancel, selectOnUp=cancel};
            galleryBack.navigation = new Navigation {mode=Navigation.Mode.None};
            var data = new SerializedObject(controller);
            Set(data,"ContinueGameBtn",resume); Set(data,"SettingsBtn",settings); Set(data,"GalleryBtn",gallery);
            Set(data,"SaveAndExitBtn",save); Set(data,"ExitGameBtn",exit); Set(data,"menuRoot",menu.gameObject);
            Set(data,"menuGroup",group); Set(data,"saveStatus",hint); Set(data,"galleryPanel",galleryPanel);
            Set(data,"galleryBackBtn",galleryBack); Set(data,"exitConfirmation",exitPanel);
            Set(data,"cancelExitBtn",cancel); Set(data,"confirmExitBtn",confirm);
            data.ApplyModifiedPropertiesWithoutUndo();
            menu.gameObject.SetActive(false); root.SetActive(true);
            if (File.Exists(PrefabPath)) File.Copy(PrefabPath, backup + "/ESCMenu.prefab", true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root); root = null;
            foreach (var path in new[] {"Assets/Scenes/GameScene.unity", "Assets/Scenes/GameScene_lx.unity", "Assets/Scenes/TestNight.unity"})
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var existing = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ESCMenu>(true)).ToArray();
                if (existing.Length != 1) throw new InvalidOperationException("Expected one ESC menu in " + path);
                if (!EditorSceneManager.SaveScene(scene, backup + "/" + Path.GetFileName(path), true)) throw new IOException("Backup failed.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "ESCMenu";
                UnityEngine.Object.DestroyImmediate(existing[0].gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed: " + path);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("ESC menu artwork applied to GameScene, GameScene_lx and TestNight. Backup: " + backup);
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    static GameObject ClonePanel(GameObject source, Transform parent, int order)
    {
        var clone = UnityEngine.Object.Instantiate(source, parent, false); clone.name = source.name;
        Stretch((RectTransform)clone.transform);
        var canvas = clone.GetComponent<Canvas>();
        canvas.overrideSorting = true; canvas.sortingOrder = order;
        foreach(var button in clone.GetComponentsInChildren<Button>(true))
        {
            for(int i=button.onClick.GetPersistentEventCount()-1; i>=0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
        clone.SetActive(false); return clone;
    }
    static Button FindCloneButton(SerializedObject data, string property, GameObject source, GameObject clone)
    {
        var original = (Button)data.FindProperty(property).objectReferenceValue;
        var path = AnimationUtility.CalculateTransformPath(original.transform, source.transform);
        return clone.transform.Find(path).GetComponent<Button>();
    }
    static Sprite Sprite(string path)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if(sprite == null) throw new InvalidOperationException("Missing sprite: " + path);
        return sprite;
    }
    static void Set(SerializedObject data, string name, UnityEngine.Object value) => data.FindProperty(name).objectReferenceValue=value;
    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.gameObject.layer=5;
        r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        r.anchoredPosition=new Vector2(x,y); r.sizeDelta=new Vector2(w,h); return r;
    }
    static void Stretch(RectTransform r)
    {
        r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero;
        r.localScale=Vector3.one; r.localRotation=Quaternion.identity;
    }
    static Image Picture(string name, Transform parent, Sprite sprite, float x,float y,float w,float h,bool raycast)
    {
        var image=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>(); image.sprite=sprite;
        image.raycastTarget=raycast; image.preserveAspect=sprite!=null; return image;
    }
    static Button Button(string name, Transform parent, Sprite sprite,string text,float x,float y,float w,float h)
    {
        var image=Picture(name,parent,sprite,x,y,w,h,true);
        var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1,.93f,.65f);
        colors.selectedColor=colors.highlightedColor; colors.pressedColor=new Color(.85f,.76f,.48f); button.colors=colors;
        if(!string.IsNullOrEmpty(text)) Label("Label",button.transform,text,0,0,w-28,h-12,38);
        return button;
    }
    static TextMeshProUGUI Label(string name,Transform parent,string text,float x,float y,float w,float h,float size)
    {
        var label=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=font; label.text=text; label.fontSize=size; label.alignment=TextAlignmentOptions.Center;
        label.color=Color.black; label.raycastTarget=false; return label;
    }
}
