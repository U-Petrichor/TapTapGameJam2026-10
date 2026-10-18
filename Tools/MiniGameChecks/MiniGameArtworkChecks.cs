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
  if (Application.isPlaying) throw new Exception("Exit Play mode before artwork checks.");
  AssetDatabase.Refresh();
  var coffee = UnityEngine.Object.FindFirstObjectByType<CoffeeGame>(FindObjectsInactive.Include);
  var sort = UnityEngine.Object.FindFirstObjectByType<SortFileGame>(FindObjectsInactive.Include);
  var dog = UnityEngine.Object.FindFirstObjectByType<PourDogFoodGame>(FindObjectsInactive.Include);
  void Check(bool ok,string message) {if(!ok)throw new Exception(message); result.Log("PASS: "+message);}
  Check(coffee != null && sort != null && dog != null,"all three scene games exist");
  Check(coffee.IsConfigured,"coffee interaction references are authored");
  var dogData=new SerializedObject(dog);
  Check(dogData.FindProperty("_progressBar").objectReferenceValue == null,"dog-food slider is optional and unbound");
  var panel=UnityEngine.Object.FindFirstObjectByType<MiniGamePanelUI>(FindObjectsInactive.Include);
  var mappings=new SerializedObject(panel).FindProperty("_miniGames");
  Check(mappings.arraySize==7,"all seven minigame mappings are saved in the scene");
  for(int i=0;i<mappings.arraySize;i++) Check(mappings.GetArrayElementAtIndex(i).FindPropertyRelative("Content").objectReferenceValue != null,"authored minigame mapping "+i);
  var data=new SerializedObject(sort);var areas=data.FindProperty("_departments");
  for(int i=0;i<4;i++) {
   var area=areas.GetArrayElementAtIndex(i);
   var sprite=area.FindPropertyRelative("FileSprite").objectReferenceValue as Sprite;
   var target=area.FindPropertyRelative("Area").objectReferenceValue as RectTransform;
   Check(sprite != null && target != null && target.GetComponent<UnityEngine.UI.Image>().sprite != null,"department "+i+" file and folder sprites imported");
   Check(AssetDatabase.GetAssetPath(sprite).StartsWith("Assets/Model/MiniGame/SortFile/"),"department "+i+" references organized artwork");
  }
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MiniGame/CoffeeGame.prefab");
  foreach(var image in prefab.GetComponentsInChildren<UnityEngine.UI.Image>(true)) {
   var serialized=new SerializedObject(image);var property=serialized.FindProperty("m_Sprite");
   Check(property.objectReferenceInstanceIDValue == 0 || image.sprite != null,"coffee prefab image reference: "+image.name);
  }
  var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
  try {
   var canvasObject=new GameObject("MiniGameArtworkPreview",typeof(RectTransform),typeof(Canvas));
   SceneManager.MoveGameObjectToScene(canvasObject,preview);
   canvasObject.layer=31;var canvas=canvasObject.GetComponent<Canvas>();
   var cameraObject=new GameObject("PreviewCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,preview);
   var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=300;
   camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.48f,.48f,.48f);camera.cullingMask=1<<31;camera.scene=preview;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(preview);
   canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;((RectTransform)canvas.transform).sizeDelta=new Vector2(1100,600);
   var texture=new RenderTexture(1100,600,24);camera.targetTexture=texture;
   string output=Path.GetFullPath("Temp/MiniGameArtworkChecks");Directory.CreateDirectory(output);
   void SetLayer(GameObject root) {foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;}
   void Capture(string name) {
    Canvas.ForceUpdateCanvases();foreach(var graphic in canvas.GetComponentsInChildren<Graphic>()) {graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);}camera.Render();var old=RenderTexture.active;RenderTexture.active=texture;
    var pixels=new Texture2D(1100,600,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1100,600),0,0);pixels.Apply();
    File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);
   }
   var coffeeClone=UnityEngine.Object.Instantiate(coffee,canvas.transform,false);coffeeClone.gameObject.SetActive(true);((RectTransform)coffeeClone.transform).anchoredPosition3D=Vector3.zero;coffeeClone.transform.localScale=Vector3.one;coffeeClone.transform.localRotation=Quaternion.identity;SetLayer(coffeeClone.gameObject);
   coffeeClone.Begin(null);
   var coffeeData=new SerializedObject(coffeeClone);var drip=coffeeData.FindProperty("_coffeeEffect").objectReferenceValue as GameObject;
   Check(drip != null && drip.GetComponent<CoffeeDripGraphic>() != null,"coffee filter has the animated stream bound");
   Check(coffeeClone.transform.Find("FilterClamp/CoffeeCup").GetComponent<UnityEngine.UI.Image>().sprite != null,"cup artwork sits under the machine filter");
   var kettle=(RectTransform)coffeeData.FindProperty("_kettle").objectReferenceValue;
   var spout=(RectTransform)coffeeData.FindProperty("_kettleSpout").objectReferenceValue;
   var landing=(RectTransform)coffeeData.FindProperty("_waterLandingPoint").objectReferenceValue;
   var water=(GameObject)coffeeData.FindProperty("_waterEffect").objectReferenceValue;
   var stream=(RectTransform)water.transform;
   Canvas.ForceUpdateCanvases();
   kettle.localRotation=Quaternion.Euler(0,0,25);
   kettle.position+=landing.TransformPoint(new Vector3(0,50,0))-spout.position;
   void CheckStreamEnds() {
    var start=stream.TransformPoint(new Vector3(stream.rect.center.x,stream.rect.yMax,0));
    var end=stream.TransformPoint(new Vector3(stream.rect.center.x,stream.rect.yMin,0));
    Check(Vector3.Distance(start,spout.position)<0.01f,"water starts exactly at the tilted kettle spout");
    Check(Vector3.Distance(end,landing.position)<0.01f,"water ends at the filter receiving point");
   }
   coffeeClone.SendMessage("SetPourEffects",true);Check(drip.activeSelf,"valid pour enables coffee stream");
   CheckStreamEnds();Capture("coffee-pouring");
   kettle.position+=new Vector3(18,12,0);kettle.localRotation=Quaternion.Euler(0,0,38);
   coffeeClone.SendMessage("LateUpdate");CheckStreamEnds();Capture("coffee-pouring-moved");
   canvas.transform.localScale=new Vector3(1.2f,0.8f,1f);
   coffeeClone.SendMessage("LateUpdate");CheckStreamEnds();
   canvas.transform.localScale=Vector3.one;

   coffeeClone.End();Check(!drip.activeSelf,"ending hides stream");coffeeClone.ResetView();Check(!drip.activeSelf,"restarting clears stream");
   coffeeClone.gameObject.SetActive(false);
   var sortClone=UnityEngine.Object.Instantiate(sort,canvas.transform,false);sortClone.gameObject.SetActive(true);((RectTransform)sortClone.transform).anchoredPosition3D=Vector3.zero;sortClone.transform.localScale=Vector3.one;sortClone.transform.localRotation=Quaternion.identity;
   sortClone.Begin(null);SetLayer(sortClone.gameObject);Check(sortClone.GetComponentsInChildren<SortFileCard>().Length==12,"sorting creates twelve artwork cards");Capture("sort-file");sortClone.End();
   camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);
   result.Log("Rendered previews: "+output);
  } finally {EditorSceneManager.CloseScene(preview,true);}
 }
}
