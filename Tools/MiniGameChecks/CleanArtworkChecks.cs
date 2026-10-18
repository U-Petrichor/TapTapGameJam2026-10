using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TapTapGameJam.UI;
internal class CommandScript : IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  if(Application.isPlaying)throw new Exception("Run the isolated clean checks outside Play mode.");
  var source=UnityEngine.Object.FindFirstObjectByType<CleanGame>(FindObjectsInactive.Include);
  void Check(bool ok,string text){if(!ok)throw new Exception(text);result.Log("PASS: "+text);}
  Check(source!=null && source.IsConfigured,"scene explicitly binds the stain artwork and wiping tool");
  var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
  try {
   var root=new GameObject("CleanGameChecks",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));root.layer=31;SceneManager.MoveGameObjectToScene(root,preview);
   var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;((RectTransform)root.transform).sizeDelta=new Vector2(1100,600);
   var cameraObject=new GameObject("CleanCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,preview);
   var camera=cameraObject.GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=300;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;camera.cullingMask=1<<31;camera.scene=preview;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(preview);canvas.worldCamera=camera;
   var texture=new RenderTexture(1100,600,24);camera.targetTexture=texture;
   var game=UnityEngine.Object.Instantiate(source,canvas.transform,false);game.gameObject.SetActive(true);var gameRect=(RectTransform)game.transform;gameRect.anchoredPosition3D=Vector3.zero;gameRect.localScale=Vector3.one;gameRect.localRotation=Quaternion.identity;
   foreach(var t in game.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
   var data=new SerializedObject(game);var stain=(RawImage)data.FindProperty("_stainImage").objectReferenceValue;var tool=(RectTransform)data.FindProperty("_wipeTool").objectReferenceValue;
   Check(tool.Find("Cloth").GetComponent<UnityEngine.UI.Image>().sprite!=null && tool.Find("WipingArm").GetComponent<UnityEngine.UI.Image>().sprite!=null,"cloth and arm use supplied sprites");
   Directory.CreateDirectory("Temp/CleanGameChecks");
   void Capture(string name){Canvas.ForceUpdateCanvases();foreach(var graphic in canvas.GetComponentsInChildren<Graphic>()){graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);}camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;var pixels=new Texture2D(1100,600,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1100,600),0,0);pixels.Apply();File.WriteAllBytes("Temp/CleanGameChecks/"+name+".png",pixels.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);}
   int CountDirty(){int n=0;foreach(var p in ((Texture2D)stain.texture).GetPixels32())if(p.a>0)n++;return n;}
   ulong Hash(Color32[] pixels){ulong h=1469598103934665603UL;foreach(var p in pixels)h=unchecked((h^p.a)*1099511628211UL);return h;}
   int completed=0;game.Begin(()=>completed++);int firstDirty=CountDirty();ulong first=Hash(((Texture2D)stain.texture).GetPixels32());Capture("layout-1");
   Check(firstDirty>1000 && firstDirty<((Texture2D)stain.texture).width*((Texture2D)stain.texture).height*.7f,"visible stains cover a bounded portion of the surface");
   game.Begin(()=>completed++);ulong second=Hash(((Texture2D)stain.texture).GetPixels32());Check(first!=second,"reopening generates a different stain layout");Capture("layout-2");
   // Exercise actual pointer handlers, including a fast swipe and hover without erasing.
   var eventObject=new GameObject("CleanEvents",typeof(EventSystem));SceneManager.MoveGameObjectToScene(eventObject,preview);
   var evt=new PointerEventData(eventObject.GetComponent<EventSystem>()){button=PointerEventData.InputButton.Left,pointerId=-1};
   evt.pointerPressRaycast=new RaycastResult{gameObject=stain.gameObject,module=root.GetComponent<GraphicRaycaster>()};
   void Position(float x,float y){evt.position=RectTransformUtility.WorldToScreenPoint(camera,stain.rectTransform.TransformPoint(new Vector3(x,y,0)));}
   Rect bounds=stain.rectTransform.rect;int before=CountDirty();Position(0,0);game.OnPointerMove(evt);Check(before==CountDirty(),"hover moves the tool without cleaning");
   Check(Vector3.Distance(tool.position,stain.rectTransform.TransformPoint(Vector3.zero))<.01f,"cloth contact center follows the cursor");
   Position(bounds.xMin+8,0);game.OnPointerDown(evt);Position(100,0);game.OnDrag(evt);game.OnPointerUp(evt);Check(CountDirty()<before && CountDirty()>0,"one fast swipe erases the full segment and leaves other stains");Capture("wiping");
   game.OnPointerExit(evt);Check(!tool.gameObject.activeSelf,"tool hides when the pointer leaves the surface");
   for(float y=bounds.yMin+8;y<bounds.yMax;y+=55){Position(bounds.xMin+8,y);game.OnPointerDown(evt);Position(bounds.xMax-8,y);game.OnDrag(evt);game.OnPointerUp(evt);}
   Check(completed==1,"wiping the board reaches the threshold and completes exactly once");
   Position(0,0);game.OnPointerDown(evt);game.OnDrag(evt);Check(completed==1,"completed round ignores further input");
   game.Begin(()=>completed++);Check(CountDirty()>1000 && tool.gameObject.activeSelf,"new round restores stains and the tool");game.End();int stopped=CountDirty();game.OnPointerDown(evt);game.OnDrag(evt);Check(CountDirty()==stopped && completed==1,"closing stops input without awarding completion");
   // Determinism and diversity checks use an asymmetric synthetic source, without Unity's global RNG.
   var sample=new Color32[64*32];for(int y=4;y<28;y++)for(int x=4;x<60-y/2;x++)sample[y*64+x]=new Color32(90,45,35,230);
   var expected=StainPattern.Compose(sample,64,32,256,128,73);Check(Hash(expected)==Hash(StainPattern.Compose(sample,64,32,256,128,73)),"a seed reproduces the layout for debugging");
   var fingerprints=new System.Collections.Generic.HashSet<ulong>();for(int seed=0;seed<24;seed++)fingerprints.Add(Hash(StainPattern.Compose(sample,64,32,256,128,seed)));Check(fingerprints.Count==24,"24 seeds generate distinct compositions");
   camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);result.Log("Rendered previews: Temp/CleanGameChecks");
  } finally {EditorSceneManager.CloseScene(preview,true);}
 }
}
