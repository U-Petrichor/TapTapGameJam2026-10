using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Run in Play mode with: unity command run_script --file Tools/MainMenu/MainMenuArtworkChecks.cs --entry MainMenuArtworkChecks.Verify
public static class MainMenuArtworkChecks
{
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static void Require(bool condition, string description) { if (!condition) throw new Exception(description); }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Instance).GetValue(owner);
    static MainMenuUIInteract Menu => UnityEngine.Object.FindFirstObjectByType<MainMenuUIInteract>();
    static Button Button(string field) => Field<Button>(Menu, field);
    static void Available(bool value) => typeof(MainMenuUIInteract).GetMethod("SetContinueAvailable", Instance).Invoke(Menu, new object[] { value });

    public static async Task<string> Verify()
    {
        Require(Application.isPlaying, "Checks require Play mode.");
        var menu = Menu;
        var results = new List<string>();
        bool originalAvailable = Button("continueGameBtn").interactable;
        var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singletonType.GetProperty("instance", Static).GetValue(null);
        var groupType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = sizesType.GetMethod("GetGroup", Instance).Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
        var viewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        var selected = viewType.GetProperty("selectedSizeIndex", Instance);
        int originalSize = (int)selected.GetValue(view);
        // Remove only stale sizes made by an interrupted earlier run. RemoveCustomSize expects
        // the absolute group index, including built-in entries.
        int previousCount = (int)group.GetType().GetMethod("GetTotalCount", Instance).Invoke(group, null);
        int builtinCount = (int)group.GetType().GetMethod("GetBuiltinCount", Instance).Invoke(group, null);
        for (int i = previousCount - 1; i >= builtinCount; i--)
        {
            var size = group.GetType().GetMethod("GetGameViewSize", Instance).Invoke(group, new object[] { i });
            var name = (string)size.GetType().GetProperty("baseText", Instance).GetValue(size);
            if (name == "MainMenu artwork QA") group.GetType().GetMethod("RemoveCustomSize", Instance).Invoke(group, new object[] { i });
        }
        var customIndices = new List<int>();
        Directory.CreateDirectory("Temp/MainMenuArtwork");
        try
        {
            foreach (var pair in new[] { new[] { 1920, 1080 }, new[] { 1280, 720 }, new[] { 1280, 960 }, new[] { 2560, 1080 } })
            {
                int w = pair[0], h = pair[1];
                var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
                var resolutionType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                var custom = Activator.CreateInstance(sizeType, Instance, null, new object[] { Enum.Parse(resolutionType, "FixedResolution"), w, h, "MainMenu artwork QA" }, null);
                int index = (int)group.GetType().GetMethod("GetTotalCount", Instance).Invoke(group, null);
                group.GetType().GetMethod("AddCustomSize", Instance).Invoke(group, new[] { custom });
                customIndices.Add(index);
                selected.SetValue(view, index); view.Repaint();
                await Task.Delay(450);
                Canvas.ForceUpdateCanvases();
                var design = (RectTransform)menu.transform;
                var canvas = menu.GetComponentInParent<Canvas>();
                Vector3[] corners = new Vector3[4]; design.GetWorldCorners(corners);
                var canvasRect = ((RectTransform)canvas.transform).rect;
                // Validate in canvas coordinates so GUI point/pixel scaling cannot mask clipping.
                foreach (var corner in corners)
                {
                    var local = canvas.transform.InverseTransformPoint(corner);
                    Require(local.x >= canvasRect.xMin - 1 && local.x <= canvasRect.xMax + 1 && local.y >= canvasRect.yMin - 1 && local.y <= canvasRect.yMax + 1, "Design clipped at " + w + "x" + h);
                }
                foreach (var button in menu.GetComponentsInChildren<Button>())
                {
                    var pos = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                    var pointer = new PointerEventData(EventSystem.current) { position = pos };
                    var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                    Require(hits.Count > 0 && hits[0].gameObject == button.gameObject, "Incorrect hit target: " + button.name + " at " + w + "x" + h);
                }
                // Transparent parts of full-frame artwork must not consume input.
                var blank = design.TransformPoint(new Vector3(0, -350));
                var blankHits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, blank) }, blankHits);
                Require(blankHits.Count == 0, "Decorative artwork intercepts background clicks.");
                Capture("main-" + w + "x" + h + ".png", w, h);
                results.Add(w + "x" + h + ": design fits, five local hit areas, decorative artwork ignores raycasts.");
            }

            foreach (string field in new[] { "settingsBtn", "galleryBtn", "exitBtn" })
            {
                string panelField = field == "settingsBtn" ? "settingsCanvas" : field == "galleryBtn" ? "galleryCanvas" : "exitConfirmation";
                string backField = field == "settingsBtn" ? "settingsBackBtn" : field == "galleryBtn" ? "galleryBackBtn" : "cancelExitBtn";
                Button(field).onClick.Invoke();
                Require(Field<GameObject>(menu, panelField).activeInHierarchy, "Panel did not open: " + panelField);
                Require(!menu.GetComponent<CanvasGroup>().interactable, "Menu remained interactive behind modal.");
                await Task.Delay(100);
                Require(EventSystem.current.currentSelectedGameObject == Button(backField).gameObject, "Initial modal focus incorrect.");
                Button(backField).onClick.Invoke();
                Require(!Field<GameObject>(menu, panelField).activeSelf && menu.GetComponent<CanvasGroup>().interactable, "Panel did not close.");
                Require(EventSystem.current.currentSelectedGameObject == Button(field).gameObject, "Return focus incorrect.");
                results.Add(panelField + ": opens, blocks menu, returns using bound button, restores focus.");
            }
            Available(false);
            Require(!Button("continueGameBtn").interactable && Field<GameObject>(menu, "continueUnavailableHint").activeSelf, "No-save disabled state missing.");
            Require(Button("startGameBtn").FindSelectableOnDown() == Button("settingsBtn"), "Navigation does not skip unavailable continue.");
            Require(Button("settingsBtn").FindSelectableOnUp() == Button("startGameBtn"), "Reverse navigation does not skip unavailable continue.");
            selected.SetValue(view, customIndices[0]); view.Repaint();
            EventSystem.current.SetSelectedGameObject(Button("startGameBtn").gameObject);
            await Task.Delay(450);
            Capture("no-save.png", 1920, 1080);
            Available(true);
            Require(Button("continueGameBtn").interactable && !Field<GameObject>(menu, "continueUnavailableHint").activeSelf, "Available continue state missing.");
            Require(Button("startGameBtn").FindSelectableOnDown() == Button("continueGameBtn"), "Available continue is not navigable.");
            results.Add("Save availability: disabled/hint/dimming, both navigation directions, available state restored. Save files untouched.");
            var start = Button("startGameBtn"); var art = (Image)start.targetGraphic;
            EventSystem.current.SetSelectedGameObject(null);
            ExecuteEvents.Execute(start.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Require(art.overrideSprite == start.spriteState.highlightedSprite, "Hover artwork did not switch.");
            ExecuteEvents.Execute(start.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            Require(art.overrideSprite == art.sprite, "Normal artwork did not restore.");
            EventSystem.current.SetSelectedGameObject(start.gameObject);
            Require(art.overrideSprite == start.spriteState.selectedSprite, "Keyboard artwork did not switch.");
            results.Add("Pointer hover/exit and keyboard selection swap white/yellow artwork.");
            var intro = Field<IntroVideoTransition>(menu, "introTransition");
            Require(intro != null && Field<UnityEngine.Video.VideoPlayer>(intro, "videoPlayer") != null, "Missing intro video reference.");
            Require(UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/GameScene.unity") >= 0, "GameScene missing from Build Settings.");
            results.Add("Start/continue retain existing intro/video and enabled GameScene references; full gameplay transition not executed by these UI checks.");
            File.WriteAllLines("Temp/MainMenuArtwork/verification.txt", results);
            return string.Join("\n", results);
        }
        finally
        {
            Available(originalAvailable); menu.ClosePanel();
            EventSystem.current.SetSelectedGameObject(Button("startGameBtn").gameObject);
            selected.SetValue(view, originalSize);
            foreach (int index in customIndices.AsEnumerable().Reverse()) group.GetType().GetMethod("RemoveCustomSize", Instance).Invoke(group, new object[] { index });
            view.Repaint();
        }
    }

    static void Capture(string name, int width, int height)
    {
        var captureType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t => t != null);
        // Encode the actual Game view render target without adding QA PNGs to Assets.
        var bytes = (byte[])captureType.GetMethod("EncodeScreenToPng", Static).Invoke(null, new object[] { width, height });
        File.WriteAllBytes("Temp/MainMenuArtwork/" + name, bytes);
    }

    public static async Task<string> VerifyEscape()
    {
        Menu.ClosePanel(); Menu.OpenSettings();
        var previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var keyboard = InputSystem.AddDevice<Keyboard>("MainMenuArtworkQA");
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.EnableDevice(keyboard);
            keyboard.MakeCurrent();
            // Let the real player Update handle Escape on its next frame, with background
            // keyboard input enabled only for this automated check in an unfocused editor.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            await Task.Delay(300);
            Require(!Field<GameObject>(Menu, "settingsCanvas").activeSelf, "Esc did not close settings.");
            Require(Menu.GetComponent<CanvasGroup>().interactable, "Esc did not restore menu input.");
            Require(EventSystem.current.currentSelectedGameObject == Button("settingsBtn").gameObject, "Esc did not restore focus.");
            File.AppendAllText("Temp/MainMenuArtwork/verification.txt", "Esc: injected Input System keyboard input closes settings through the player Update and restores input/focus.\n");
            return "Esc: injected Input System keyboard input closes settings through the player Update, restores main menu input and source-button focus.";
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
            InputSystem.settings.backgroundBehavior = previousBackground;
            Menu.ClosePanel();
        }
    }

    public static string PreviewFont()
    {
        var labels = Menu.GetComponentsInChildren<TMPro.TMP_Text>(true);
        foreach (var label in labels)
        {
            Require(label.font.name == "fusion-pixel-12px-monospaced-zh_hans 2 SDF", "Unexpected font: " + label.name);
            Require(label.font.HasCharacters(label.text), "Missing font characters: " + label.text);
            label.ForceMeshUpdate(true);
            Require(!label.isTextOverflowing, "Label overflows: " + label.text);
        }
        Capture("pixel-font.png", 1920, 1080);
        return labels.Length + " labels use the requested font; all glyphs present and no text overflow.";
    }
}
