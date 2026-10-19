using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TapTapGameJam.UI;
using TMPro;

internal class CommandScript : IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  if(Application.isPlaying)throw new System.Exception("Exit Play mode before configuring key hints");
  const string prefabPath="Assets/Prefabs/UI/HUD/Key.prefab";
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
  if(!prefab)throw new System.Exception("Missing Key.prefab");
  foreach(string path in new[]{"Assets/Scenes/GameScene.unity","Assets/Scenes/GameScene_lx.unity"}) {
   var scene=SceneManager.GetSceneByPath(path);
   bool opened=!scene.IsValid()||!scene.isLoaded;
   if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try {
    foreach(var root in scene.GetRootGameObjects())
     foreach(var ui in root.GetComponentsInChildren<TaskInteractionPromptUI>(true)) {
      var data=new SerializedObject(ui);
      var previous=data.FindProperty("interactionKeyHint").objectReferenceValue as GameObject;
      var row=new GameObject("TaskKeyHints",typeof(RectTransform));
      Undo.RegisterCreatedObjectUndo(row,"Create task key hints");
      row.transform.SetParent(ui.transform,false);
      var rect=(RectTransform)row.transform;
      rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;
      // Match the reference: full-size keycaps, aligned just below the artwork baseline.
      rect.anchoredPosition=new Vector2(614,60);rect.sizeDelta=new Vector2(500,100);
      void AddKey(string name,string key,string label,float x) {
       var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,row.transform);
       Undo.RegisterCreatedObjectUndo(go,"Instantiate key hint");
       go.name=name;
       var keyRect=(RectTransform)go.transform;
       keyRect.anchorMin=keyRect.anchorMax=Vector2.zero;
       keyRect.pivot=new Vector2(.5f,.5f);
       keyRect.anchoredPosition=new Vector2(x,50);
       keyRect.sizeDelta=new Vector2(100,100);
       keyRect.localScale=Vector3.one;
       var keyText=go.transform.Find("KeyText").GetComponent<TMP_Text>();
       keyText.text=key;
       keyText.color=Color.black;
       var labelText=go.transform.Find("Text").GetComponent<TMP_Text>();
       labelText.text=label;
       labelText.fontSize=40;
       foreach(var graphic in go.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))graphic.raycastTarget=false;
       PrefabUtility.RecordPrefabInstancePropertyModifications(keyRect);
       foreach(var text in go.GetComponentsInChildren<TMP_Text>(true))PrefabUtility.RecordPrefabInstancePropertyModifications(text);
      }
      AddKey("InteractKey","E","交互",50);
      AddKey("SwitchKey","Q","切换",260);
      row.SetActive(false);
      data.FindProperty("interactionKeyHint").objectReferenceValue=row;
      data.ApplyModifiedProperties();
      if(previous)Undo.DestroyObjectImmediate(previous);
     }
    EditorSceneManager.MarkSceneDirty(scene);
    if(!EditorSceneManager.SaveScene(scene))throw new System.Exception("Scene save failed");
    result.Log("Saved "+path+": linked E / Q Key.prefab instances beside task artwork.");
   } finally {if(opened)EditorSceneManager.CloseScene(scene,true);}
  }
 }
}
