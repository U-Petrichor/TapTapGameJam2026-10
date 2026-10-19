using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TapTapGameJam.UI;
public static class ArcLiquidProgressSetup
{
 [MenuItem("Tools/UI/Install Cockroach Liquid Progress")]
 public static void Install()
 {
  if(Application.isPlaying) throw new Exception("Exit Play mode before installing liquid UI");
  var shader=Shader.Find("TapTapGameJam/UI/Arc Liquid Progress");
  if(!shader || ShaderUtil.ShaderHasError(shader))throw new Exception("Liquid shader did not compile");
  const string materialPath="Assets/Resources/UI/ArcLiquidProgress.mat";
  var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
  if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
  var scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
  bool opened=!scene.IsValid()||!scene.isLoaded;
  if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity",OpenSceneMode.Additive);
  var source=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Image>(true)).Single(i=>i.name=="ProgressBar" && i.transform.parent.name=="Cockroach");
  Directory.CreateDirectory("RecoverySnapshots/LiquidProgress");
  string backup="RecoverySnapshots/LiquidProgress/GameScene_before_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
  EditorSceneManager.SaveScene(scene,backup,true);
  var child=source.transform.Find("LiquidFill");
  if(!child){
   var go=new GameObject("LiquidFill",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
   Undo.RegisterCreatedObjectUndo(go,"Add arc liquid fill");
   go.layer=source.gameObject.layer;go.transform.SetParent(source.transform,false);
   var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;
   child=go.transform;
  }
  var image=child.GetComponent<Image>();image.sprite=source.sprite;image.type=Image.Type.Simple;image.color=Color.white;image.raycastTarget=false;image.material=material;
  var liquid=child.GetComponent<ArcLiquidProgress>();if(!liquid)liquid=Undo.AddComponent<ArcLiquidProgress>(child.gameObject);
  // Re-enable once after assigning the authored template so it owns its runtime material.
  liquid.enabled=false;
  var data=new SerializedObject(liquid);data.FindProperty("liquidMaterial").objectReferenceValue=material;data.ApplyModifiedProperties();
  liquid.enabled=true;
  EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
  if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save scene");
  Selection.activeGameObject=child.gameObject;
  Debug.Log("Liquid UI installed at "+child.parent.parent.name+"/ProgressBar/LiquidFill; backup: "+backup);
 }
}
