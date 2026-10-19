using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using TapTapGameJam.Core;
using TapTapGameJam.Types;
using TapTapGameJam.Events;
using System.Linq;
using System.IO;
internal class CommandScript : IRunCommand {
 public void Execute(ExecutionResult result) {
  var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
  DayCountUI ui=null;
  try {
   ui=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DayCountUI>(true)).Single();
   var data=new SerializedObject(ui);
   var manager=(GamePhaseManager)data.FindProperty("phaseManager").objectReferenceValue;
   var day=(GameObject)data.FindProperty("dayUI").objectReferenceValue;
   var night=(GameObject)data.FindProperty("nightUI").objectReferenceValue;
   var dn=(TextMeshProUGUI)data.FindProperty("dayNumber").objectReferenceValue;
   var nn=(TextMeshProUGUI)data.FindProperty("nightNumber").objectReferenceValue;
   if(day.name!="DayUI"||night.name!="NightUI"||dn.transform.parent!=day.transform||nn.transform.parent!=night.transform)throw new System.Exception("Hierarchy binding failed");
   manager.DayIndex=10;
   SetPhase(manager,GamePhase.Day);ui.SendMessage("OnEnable");ui.SendMessage("Start");
   Check(day.activeSelf&&!night.activeSelf&&dn.text=="10"&&nn.text=="10","day initialization");Capture(ui,"day");
   SetPhase(manager,GamePhase.Night);ui.SendMessage("OnPhaseChanged",new GamePhaseChangedEvent(GamePhase.Night));
   Check(!day.activeSelf&&night.activeSelf&&nn.text=="10","night switch");Capture(ui,"night");
   // Deliberate designer edits in the disposable preview scene.
   dn.rectTransform.anchoredPosition+=new Vector2(13,-7);nn.rectTransform.anchoredPosition+=new Vector2(-17,9);
   dn.rectTransform.sizeDelta+=new Vector2(11,3);nn.rectTransform.localScale=new Vector3(.85f,1.1f,1);
   var rects=ui.GetComponentsInChildren<RectTransform>(true);
   var before=rects.Select(r=>EditorJsonUtility.ToJson(r)).ToArray();
   var images=ui.GetComponentsInChildren<UnityEngine.UI.Image>(true);
   var artwork=images.Select(i=>EditorJsonUtility.ToJson(i)).ToArray();
   ui.SendMessage("OnDisable");ui.SendMessage("OnEnable");ui.SendMessage("Start");
   foreach(var phase in new[]{GamePhase.Day,GamePhase.Shopping,GamePhase.Night,GamePhase.Day}) {
    SetPhase(manager,phase);ui.SendMessage("OnPhaseChanged",new GamePhaseChangedEvent(phase));
    Check(day.activeSelf==(phase==GamePhase.Day)&&night.activeSelf==(phase==GamePhase.Night),"phase visibility "+phase);
    Check(ui.gameObject.activeSelf,"controller stays active");
   }
   manager.DayIndex=11;ui.SendMessage("OnNewDay",new EventNewDay(11));
   Check(dn.text=="11"&&nn.text=="11","both numbers updated");
   ui.SendMessage("OnDisable");manager.DayIndex=123;SetPhase(manager,GamePhase.Night);ui.SendMessage("OnEnable");
   Check(nn.text=="123"&&dn.text=="123"&&night.activeSelf&&!day.activeSelf,"restore on enable");
   for(int i=0;i<rects.Length;i++)Check(before[i]==EditorJsonUtility.ToJson(rects[i]),"designer RectTransform retained: "+rects[i].name);
   for(int i=0;i<images.Length;i++)Check(artwork[i]==EditorJsonUtility.ToJson(images[i]),"authored artwork retained: "+images[i].name);
   result.Log("PASS saved hierarchy, both day numbers, day/shopping/night visibility, new day, restored state, initialization and re-enable. All RectTransform and Image properties retained after deliberate preview edits and full phase cycle.");
  } finally {if(ui)ui.SendMessage("OnDisable");EditorSceneManager.ClosePreviewScene(scene);}
 }
 void Check(bool ok,string what){if(!ok)throw new System.Exception("FAIL "+what);}
 void SetPhase(GamePhaseManager manager,GamePhase phase){var data=new SerializedObject(manager);data.FindProperty("<CurrentPhase>k__BackingField").enumValueIndex=(int)phase;data.ApplyModifiedPropertiesWithoutUndo();}
 void Capture(DayCountUI source,string name) {
  var holder=new GameObject("DayCountPreview",typeof(Canvas));
  var camGo=new GameObject("DayCountPreviewCamera",typeof(Camera));
  RenderTexture rt=null;Texture2D tex=null;var previous=RenderTexture.active;
  try {
   var canvas=holder.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
   holder.layer=31;var cr=(RectTransform)holder.transform;cr.sizeDelta=new Vector2(500,260);cr.position=new Vector3(10000,10000,0);
   var copy=Object.Instantiate(source.gameObject,holder.transform,false);Object.DestroyImmediate(copy.GetComponent<DayCountUI>());
   foreach(var t in copy.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
   var rect=(RectTransform)copy.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(-200,-100);
   copy.SetActive(true);foreach(var text in copy.GetComponentsInChildren<TextMeshProUGUI>(true))text.ForceMeshUpdate(true);
   var camera=camGo.GetComponent<Camera>();camera.transform.position=new Vector3(10000,10000,-10);camera.orthographic=true;camera.orthographicSize=130;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.white;
   rt=new RenderTexture(1000,520,24);camera.targetTexture=rt;canvas.worldCamera=camera;
   Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
   tex=new Texture2D(1000,520,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1000,520),0,0);tex.Apply();
   Directory.CreateDirectory("Temp/DayCountChecks");File.WriteAllBytes("Temp/DayCountChecks/"+name+".png",tex.EncodeToPNG());
  } finally {RenderTexture.active=previous;Object.DestroyImmediate(holder);Object.DestroyImmediate(camGo);if(rt)Object.DestroyImmediate(rt);if(tex)Object.DestroyImmediate(tex);}
 }
}
