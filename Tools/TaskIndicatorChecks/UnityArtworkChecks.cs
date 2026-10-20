using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TapTapGameJam.UI;
using TapTapGameJam.MiniGame;

internal class CommandScript : IRunCommand
{
    public sealed class PreviewTask : MiniGameInstanceBasic
    {
        public PreviewTask(Transform target) : base(target, 125, 2) { }
        protected override void Release() { }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public void Execute(ExecutionResult result)
    {
        Require(!Application.isPlaying, "Run this isolated check outside Play mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        var texture = new RenderTexture(1280, 720, 24);
        var previousTexture = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("IndicatorPreviewCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.24f, 0.28f, 0.33f);
            camera.cullingMask = 1 << 5;
            camera.targetTexture = texture;
            var canvasObject = new GameObject("IndicatorPreviewCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.layer = 5;
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var area = (RectTransform)canvas.transform;
            var target = new GameObject("PreviewTarget");
            SceneManager.MoveGameObjectToScene(target, scene);
            var prefab = AssetDatabase.LoadAssetAtPath<Arrow>("Assets/Prefabs/UI/HUD/Arrow.prefab");
            Require(prefab != null, "Arrow prefab missing");
            var view = UnityEngine.Object.Instantiate(prefab, area, false);
            view.SendMessage("Awake");
            var data = new SerializedObject(view);
            foreach (string name in new[] {"_deliveryIcon", "_coffeeIcon", "_dogFoodIcon", "_printerIcon", "_cleanIcon", "_parcelIcon", "_stockIcon"})
                Require(data.FindProperty(name).objectReferenceValue != null, "Missing sprite: " + name);
            data.FindProperty("_directionSmoothTime").floatValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            var icon = (UnityEngine.UI.Image)data.FindProperty("_taskIcon").objectReferenceValue;
            var pointer = (RectTransform)data.FindProperty("_arrowImage").objectReferenceValue;
            var center = (RectTransform)data.FindProperty("_orbitCenter").objectReferenceValue;
            var radiusPoint = (RectTransform)data.FindProperty("_orbitRadiusPoint").objectReferenceValue;
            Require(pointer.GetComponent<UnityEngine.UI.Image>().sprite != null, "Missing arrow sprite");
            Require(view.transform.Find("TaskName") == null, "Old task-name text still present");
            Type[] types = {typeof(DeliveryFileInstance), typeof(MakeCoffeeInstance), typeof(PourDogFoodInstance), typeof(PrinterRepairInstance), typeof(CleanInstance), typeof(ParcelCutInstance), typeof(StockInstance), typeof(SortFileInstance)};
            string[] fields = {"_deliveryIcon", "_coffeeIcon", "_dogFoodIcon", "_printerIcon", "_cleanIcon", "_parcelIcon", "_stockIcon", "_deliveryIcon"};
            for (int i = 0; i < types.Length; i++)
            {
                // Only exercise presentation: no task gameplay or scene state is started.
                var task = (MiniGameInstanceBasic)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(types[i]);
                view.Bind(task);
                Require(icon.sprite == data.FindProperty(fields[i]).objectReferenceValue, "Wrong icon: " + types[i].Name);
            }
            var previewTask = new PreviewTask(target.transform);
            view.Bind(previewTask);
            var reward = (TMPro.TMP_Text)data.FindProperty("_taskReward").objectReferenceValue;
            Require(reward.text == "125 金币 · 2 天赋点", "Reward changed");
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            for (int degrees = 0; degrees < 360; degrees += 2)
            {
                float angle = degrees * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                target.transform.position = camera.ViewportToWorldPoint(new Vector3(0.5f + direction.x * 2, 0.5f + direction.y * 2, 10));
                view.UpdatePosition(area, camera, 20);
                Require(view.GetComponent<CanvasGroup>().alpha == 1, "Offscreen indicator hidden");
                float radius = Vector3.Distance(center.position, radiusPoint.position);
                Require(Mathf.Abs(Vector3.Distance(pointer.position, center.position) - radius) < 0.001f, "Orbit is not circular");
                Require(Quaternion.Angle(center.localRotation, Quaternion.identity) < 0.01f, "Content rotated");
                foreach (var graphic in view.GetComponentsInChildren<Graphic>())
                {
                    graphic.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var p = area.InverseTransformPoint(corner);
                        Require(p.x >= area.rect.xMin + 19.9f && p.x <= area.rect.xMax - 19.9f &&
                            p.y >= area.rect.yMin + 19.9f && p.y <= area.rect.yMax - 19.9f, "Visual clipped at " + degrees);
                    }
                }
            }
            target.transform.position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 10));
            view.UpdatePosition(area, camera, 20);
            Require(view.GetComponent<CanvasGroup>().alpha == 0, "Onscreen task did not hide");
            target.transform.position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, -10));
            view.UpdatePosition(area, camera, 20);
            Require(view.GetComponent<CanvasGroup>().alpha == 1, "Behind-camera task did not show");

            // Cross the -180/180 boundary: interpolation must take the short arc.
            view.SendMessage("UpdateOrbit", new Vector2(-1, 0.01f));
            var before = (pointer.localPosition - center.localPosition).normalized;
            data.FindProperty("_directionSmoothTime").floatValue = 0.08f;
            data.ApplyModifiedPropertiesWithoutUndo();
            view.SendMessage("UpdateOrbit", new Vector2(-1, -0.01f));
            var after = (pointer.localPosition - center.localPosition).normalized;
            Require(Vector3.Angle(before, after) <= 1.15f && after.x < -0.99f, "Angle wrap took the long arc");
            view.Unbind();
            UnityEngine.Object.DestroyImmediate(view.gameObject);

            // Eight authored views demonstrate all quadrants with no changes to gameplay scenes.
            for (int i = 0; i < 8; i++)
            {
                var sample = UnityEngine.Object.Instantiate(prefab, area, false);
                sample.SendMessage("Awake");
                var task = (MiniGameInstanceBasic)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(types[i]);
                float angle = (i * 45) * Mathf.Deg2Rad;
                target.transform.position = camera.ViewportToWorldPoint(new Vector3(0.5f + Mathf.Cos(angle) * 2, 0.5f + Mathf.Sin(angle) * 2, 10));
                task.TargetPosition = target.transform;
                sample.Bind(task);
                if (!task.IsDailyActivity)
                {
                    var sampleData = new SerializedObject(sample);
                    var sampleReward = (TMPro.TMP_Text)sampleData.FindProperty("_taskReward").objectReferenceValue;
                    sampleReward.text = "125 金币 · 2 天赋点";
                }
                sample.UpdatePosition(area, camera, 20);
            }
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in canvasObject.GetComponentsInChildren<Graphic>())
            {
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            var png = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            png.Apply();
            int foreground = 0;
            var pixels = png.GetPixels32();
            var background = pixels[pixels.Length / 2];
            foreach (var pixel in pixels)
                if (Mathf.Abs(pixel.r - background.r) + Mathf.Abs(pixel.g - background.g) + Mathf.Abs(pixel.b - background.b) > 30)
                    foreground++;
            Require(foreground > 1000, "Preview contains no rendered UI");
            System.IO.File.WriteAllBytes("Temp/TaskIndicatorPreview.png", png.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(png);
            result.Log("PASS: 8 task icon mappings, reward text, 180 circular positions, all graphic bounds, upright content, onscreen hiding and behind-camera fallback. Preview: Temp/TaskIndicatorPreview.png");
        }
        finally
        {
            RenderTexture.active = previousTexture;
            EditorSceneManager.ClosePreviewScene(scene);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
