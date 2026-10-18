using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TapTapGameJam.UI;
internal class CommandScript : IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  if(Application.isPlaying)throw new Exception("Run the isolated visual check outside Play mode.");
  var source=UnityEngine.Object.FindFirstObjectByType<StockHourglass>(FindObjectsInactive.Include);
  if(source==null)throw new Exception("No stock hourglass in scene.");
  var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
  try {
   var root=new GameObject("HourglassChecks",typeof(RectTransform),typeof(Canvas));root.layer=31;
   SceneManager.MoveGameObjectToScene(root,preview);
   var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;((RectTransform)root.transform).sizeDelta=new Vector2(600,200);
   var cameraObject=new GameObject("HourglassCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,preview);
   var camera=cameraObject.GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=100;
   camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.65f,.66f,.65f);camera.cullingMask=1<<31;camera.scene=preview;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(preview);canvas.worldCamera=camera;
   float[] stages={0f,.3f,.65f,1f};
   for(int i=0;i<stages.Length;i++) {
    var icon=UnityEngine.Object.Instantiate(source,canvas.transform,false);icon.gameObject.SetActive(true);
    var rect=(RectTransform)icon.transform;rect.anchoredPosition3D=new Vector3(-225+i*150,0,0);rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;rect.sizeDelta=new Vector2(120,145);
    icon.SetProgress(stages[i]);icon.SetProgress(stages[i]);
    var sands=icon.GetComponentsInChildren<StockHourglassSand>(true);
    if(sands.Length!=1)throw new Exception("Expected one reused sand layer; got "+sands.Length);
    foreach(var transform in icon.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=31;
    result.Log("PASS: progress "+stages[i]+" keeps a single sand layer");
   }
   var texture=new RenderTexture(600,200,24);camera.targetTexture=texture;
   Canvas.ForceUpdateCanvases();foreach(var graphic in canvas.GetComponentsInChildren<Graphic>()){graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);}
   camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;
   var pixels=new Texture2D(600,200,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,600,200),0,0);pixels.Apply();
   Directory.CreateDirectory("Temp/StockHourglassChecks");File.WriteAllBytes("Temp/StockHourglassChecks/progress.png",pixels.EncodeToPNG());
   RenderTexture.active=previous;camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(pixels);
   result.Log("Rendered 0%, 30%, 65%, 100%: Temp/StockHourglassChecks/progress.png");
  } finally {EditorSceneManager.CloseScene(preview,true);}
 }
}
