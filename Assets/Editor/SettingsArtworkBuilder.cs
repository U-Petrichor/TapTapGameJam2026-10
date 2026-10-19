using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Explicit asset builder. Never rebuilds or saves an open scene automatically.
public static class SettingsArtworkBuilder
{
    const string Art = "Assets/Model/UI/菜单组件/系统设置/";
    public const string PrefabPath = "Assets/Resources/UI/SettingsMenu.prefab";
    static TMP_FontAsset font;
    static Sprite Sprite(string name) => AssetDatabase.LoadAllAssetsAtPath(Art + name + ".png").OfType<Sprite>().First();
    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var r = (RectTransform)go.transform; r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(.5f,.5f);
        r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h); return r;
    }
    static Image Pic(string name, Transform parent, string sprite, float x,float y,float w,float h, bool hit=false)
    {
        var i = Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();
        if (sprite != null) i.sprite = Sprite(sprite);
        i.raycastTarget=hit; return i;
    }
    static TMP_Text Text(string name,Transform parent,string text,float x,float y,float w,float h,float size,Color? color=null)
    {
        var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font; t.text=text;t.fontSize=size;t.color=color??Color.black;
        t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;
    }
    static Button Button(string name,Transform parent,string sprite,float x,float y,float w,float h,string label=null,float size=40)
    {
        var i=Pic(name,parent,sprite,x,y,w,h,true);var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;
        // All labels and art stay at their authored colours when selected.
        b.transition=Selectable.Transition.None;
        if(label!=null) Text("Label",i.transform,label,w/2,h/2,w,h,size,label=="快捷键"||label=="声音设置"?Color.white:Color.black);
        return b;
    }
    static Slider Slider(string name,Transform parent,float y)
    {
        var root=Rect(name,parent,1091,y,802,90);
        var hit=root.gameObject.AddComponent<Image>();hit.color=Color.clear;
        Pic("Track",root,"条",401,45,776,27);
        var area=Rect("HandleArea",root,401,45,776,84);
        var handle=Pic("Handle",area,"按钮",0,42,26,84,true);
        handle.rectTransform.anchorMin=new Vector2(0,0);handle.rectTransform.anchorMax=new Vector2(0,1);
        handle.rectTransform.sizeDelta=new Vector2(26,0);handle.rectTransform.anchoredPosition=Vector2.zero;
        var slider=root.gameObject.AddComponent<Slider>();slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
        slider.minValue=0;slider.maxValue=1;slider.value=1;slider.transition=Selectable.Transition.None;return slider;
    }
    static void Assign(UnityEditor.SerializedObject so,string name,UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue=value;
    [MenuItem("Tools/UI/Build Settings Artwork Prefab")]
    public static void Build()
    {
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/MiniGameUI SDF.asset");
        var root=new GameObject("Settings",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); root.layer=5;
        root.SetActive(false);
        var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=40;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var bg=Pic("Background",root.transform,"背景",960,540,1920,1080,true);
        bg.rectTransform.anchorMin=Vector2.zero;bg.rectTransform.anchorMax=Vector2.one;bg.rectTransform.sizeDelta=Vector2.zero;bg.rectTransform.anchoredPosition=Vector2.zero;
        // A uniformly scaled design canvas keeps the folder and controls together on other aspect ratios.
        var content=Rect("Design",root.transform,0,0,1920,1080);content.anchorMin=content.anchorMax=new Vector2(.5f,.5f);content.anchoredPosition=Vector2.zero;
        Pic("Folder",content,"窗口",957,539,1432,923);
        var shortcuts=Rect("ShortcutsPage",content,960,540,1920,1080);
        Pic("LeftPaper",shortcuts,"左页",599,561,617,759);
        Pic("RightPaper",shortcuts,"右页",1277,539,617,759);
        var clip=Pic("Paperclip",shortcuts,"曲型针",494,251,75,184);clip.rectTransform.localEulerAngles=new Vector3(0,0,-48);
        Pic("Tape",shortcuts,"胶带",640,213,185,70);
        var leftMouse=Pic("LeftMouse",shortcuts,"右键",482,406,145,175);leftMouse.rectTransform.localScale=new Vector3(-1,1,1);
        Pic("RightMouse",shortcuts,"右键",482,580,145,175);
        Pic("MiddleMouse",shortcuts,"滚轮高亮",482,756,110,133);
        for(int direction=0;direction<4;direction++)
        {
            var angle=direction*90f;var offset=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),-Mathf.Sin(angle*Mathf.Deg2Rad))*62;
            var arrow=Rect("ScrollDirection"+direction,shortcuts,482+offset.x,756+offset.y,35,32).gameObject.AddComponent<SettingsGuideArrow>();
            arrow.color=new Color(1,.47f,0);arrow.raycastTarget=false;arrow.rectTransform.localEulerAngles=new Vector3(0,0,angle);
        }
        string[] mouseHelp={"选择 / 操作","次要操作","缩放视角"};float[] my={419,574,757};
        for(int i=0;i<3;i++){Pic("MouseInfo"+i,shortcuts,"信息栏",708,my[i],245,55);Text("MouseText"+i,shortcuts,mouseHelp[i],708,my[i],238,50,25);}
        string[] actions={"移动","存档","交互","切换目标"};string[] keys={"WASD","暂未开放","E / F","Q"};float[] ys={294,490,582,684};
        for(int i=0;i<4;i++)
        {
            Text("Action"+i,shortcuts,actions[i],1104,ys[i],176,65,i==3?32:40);
            Pic("LeftArrow"+i,shortcuts,"左",1223,ys[i],35,48);
            Pic("KeyField"+i,shortcuts,i==1?"off信息栏":"信息栏",1336,ys[i],153,34);
            Text("Key"+i,shortcuts,keys[i],1336,ys[i],151,48,i==1?23:35);
            Pic("RightArrow"+i,shortcuts,"右",1440,ys[i],35,26);
        }
        var sound=Rect("SoundPage",content,960,540,1920,1080);
        Pic("Paper",sound,"左页",906,571,1436,759);
        var soundClip=Pic("Paperclip",sound,"曲型针",427,241,70,173);soundClip.rectTransform.localEulerAngles=new Vector3(0,0,-48);
        Pic("Tape",sound,"胶带",962,207,185,70);
        Text("EffectsTitle",sound,"音效",490,424,130,65,40);Pic("EffectsIcon",sound,"音效",633,426,89,84);
        Text("MusicTitle",sound,"音乐",490,572,130,65,40);Pic("MusicIcon",sound,"音乐",630,563,80,86);
        Text("AnimationTitle",sound,"动画",490,719,130,65,40);Pic("AnimationIcon",sound,"动画",621,707,94,109);
        var effects=Slider("EffectsSlider",sound,426);var music=Slider("MusicSlider",sound,564);
        var animation=Button("AnimationToggle",sound,"信息栏",1378,738,210,63,"ON");
        var shortcutTab=Button("ShortcutsTab",content,"胶带 · 选中",275,332,181,75,"快捷键");
        var soundTab=Button("SoundTab",content,"胶带 · 灰",275,453,181,75,"声音设置");
        var pin=Pic("SelectedPin",content,"IMG_4803 3",170,285,70,68);
        var back=Button("Back",content,"返回键",99,100,148,148);
        var controller=root.AddComponent<SettingsMenu>();var so=new SerializedObject(controller);
        Assign(so,"shortcutsTab",shortcutTab);Assign(so,"soundTab",soundTab);Assign(so,"shortcutsPage",shortcuts.gameObject);Assign(so,"soundPage",sound.gameObject);
        Assign(so,"effectsSlider",effects);Assign(so,"musicSlider",music);Assign(so,"animationButton",animation);Assign(so,"animationLabel",animation.GetComponentInChildren<TMP_Text>());
        Assign(so,"backButton",back);Assign(so,"selectedTabSprite",Sprite("胶带 · 选中"));Assign(so,"inactiveTabSprite",Sprite("胶带 · 灰"));Assign(so,"selectedPin",pin.rectTransform);so.ApplyModifiedPropertiesWithoutUndo();
        sound.gameObject.SetActive(false);
        Directory.CreateDirectory("Assets/Resources/UI");PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        File.WriteAllText("settings-build-result.txt","SUCCESS");
    }
}
