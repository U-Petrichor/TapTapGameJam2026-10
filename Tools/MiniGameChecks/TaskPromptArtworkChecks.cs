using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TapTapGameJam.UI;
using TapTapGameJam.Interactables;
using System.Linq;


internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        string[] names = { "制作咖啡", "喂狗粮", "打印文件", "打扫", "拆快递", "炒股", "送快递" };
        foreach (string name in names)
        {
            string path = "Assets/Model/UI/UI/所有任务提示/" + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new System.Exception("Sprite failed to import: " + path);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id);
            if (id != 21300000) throw new System.Exception("Unexpected sprite file ID: " + name + " " + id);
            result.Log("Imported sprite: " + name + " " + sprite.rect.size);
        }
        foreach (string scenePath in new[] { "Assets/Scenes/GameScene.unity", "Assets/Scenes/GameScene_lx.unity" })
        {
            var scene = EditorSceneManager.OpenPreviewScene(scenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                var ui = roots.SelectMany(r => r.GetComponentsInChildren<TaskInteractionPromptUI>(true)).Single();
                var serialized = new SerializedObject(ui);
                var image = serialized.FindProperty("taskPromptImage").objectReferenceValue as UnityEngine.UI.Image;
                var key = serialized.FindProperty("interactionKeyHint").objectReferenceValue as GameObject;
                if (!image || !key) throw new System.Exception("Missing HUD binding: " + scenePath);
                var targets = roots.SelectMany(r => r.GetComponentsInChildren<Interactable>(true)).ToArray();
                ui.SendMessage("Awake");
                var pictured = targets.Where(t => t.InteractionPromptSprite != null).ToArray();
                if (pictured.Length == 0) throw new System.Exception("No configured targets");
                foreach (var target in pictured)
                {
                    ui.SendMessage("OnTargetChanged", target);
                    if (image.sprite != target.InteractionPromptSprite || !image.gameObject.activeSelf || !key.activeSelf || !image.preserveAspect)
                        throw new System.Exception("Image presentation failed: " + target.name);
                }
                foreach (var ordinary in targets.Where(t => t.InteractionPromptSprite == null))
                {
                    ui.SendMessage("OnTargetChanged", ordinary);
                    if (image.gameObject.activeSelf || key.activeSelf || ui.GetComponent<CanvasGroup>().alpha != 0)
                        throw new System.Exception("Ordinary interaction showed task HUD: " + ordinary.name);
                }
                ui.SendMessage("OnDisable");
                if (image.sprite != null || image.gameObject.activeSelf || key.activeSelf)
                    throw new System.Exception("Target cleanup failed");
                result.Log("PASS " + scenePath + ": " + pictured.Length + " targets; sprite switching, aspect ratio, E hint and no fallback.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}




