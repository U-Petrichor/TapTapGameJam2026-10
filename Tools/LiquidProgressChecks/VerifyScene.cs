using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TapTapGameJam.UI;
internal class CommandScript : IRunCommand {
 public void Execute(ExecutionResult result) {
  var scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
  var liquid=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ArcLiquidProgress>(true)).Single();
  if(liquid.transform.parent.name!="ProgressBar"||!liquid.GetComponent<Image>().sprite)throw new Exception("Broken liquid hierarchy or sprite");
  var template=new SerializedObject(liquid).FindProperty("liquidMaterial").objectReferenceValue as Material;
  if(!template||ShaderUtil.ShaderHasError(template.shader))throw new Exception("Missing material or shader compilation error");
  var disk=EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
  try{var saved=disk.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ArcLiquidProgress>(true)).Single();if(!new SerializedObject(saved).FindProperty("liquidMaterial").objectReferenceValue)throw new Exception("Saved material reference missing");}finally{EditorSceneManager.ClosePreviewScene(disk);}
  var holder=new GameObject("LiquidPreviewCanvas",typeof(RectTransform),typeof(Canvas));
  var cameraGo=new GameObject("LiquidPreviewCamera",typeof(Camera));
  RenderTexture rt=null;Texture2D tex=null;var old=RenderTexture.active;
  try{
   var canvas=holder.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;holder.transform.position=new Vector3(10000,10000,0);((RectTransform)holder.transform).sizeDelta=new Vector2(300,300);
   var copy=UnityEngine.Object.Instantiate(liquid.transform.parent.parent.gameObject,holder.transform,false);copy.SetActive(true);
   var rect=(RectTransform)copy.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;
   foreach(var t in holder.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
   var preview=copy.GetComponentInChildren<ArcLiquidProgress>(true);preview.demoLoop=false;
   var cam=cameraGo.GetComponent<Camera>();cam.transform.position=new Vector3(10000,10000,-10);cam.orthographic=true;cam.orthographicSize=145;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.15f,.16f,.19f);canvas.worldCamera=cam;
   rt=new RenderTexture(720,720,24);rt.Create();cam.targetTexture=rt;tex=new Texture2D(720,720,TextureFormat.RGB24,false);
   Directory.CreateDirectory("Temp/LiquidProgressChecks");
   var tick=typeof(ArcLiquidProgress).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic);
   foreach(float value in new[]{0f,.55f,1f}){preview.SetProgress(value);tick.Invoke(preview,new object[]{20.0});Capture(cam,rt,tex,"Temp/LiquidProgressChecks/hud-"+Mathf.RoundToInt(value*100)+".png");}
   preview.SetProgress(.55f);
   for(int i=0;i<32;i++){tick.Invoke(preview,new object[]{20.0+i/16.0});Capture(cam,rt,tex,"Temp/LiquidProgressChecks/frame-"+i.ToString("D2")+".png");}
  }finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(holder);UnityEngine.Object.DestroyImmediate(cameraGo);if(rt)UnityEngine.Object.DestroyImmediate(rt);if(tex)UnityEngine.Object.DestroyImmediate(tex);}
  Selection.activeGameObject=liquid.gameObject;
  if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(liquid.transform.position,new Vector3(330,330,1)),false);
  File.WriteAllText("Temp/LiquidProgressChecks/result.txt","PASS: GameScene saved/reloaded liquid hierarchy, material, sprite, shader; actual Cockroach HUD rendered at 0/55/100%; 32 animation frames.");
  result.Log("PASS liquid scene integration and HUD renders");
 }
 static void Capture(Camera camera,RenderTexture rt,Texture2D tex,string path){Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
}
