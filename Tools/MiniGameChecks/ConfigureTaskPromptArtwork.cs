using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TapTapGameJam.UI;
using TapTapGameJam.Interactables;
using TMPro;
using System.Collections.Generic;

// Run through Invoke-UnityCommand.ps1 while the gameplay scene is open in Edit mode.
// Author the loaded scene through Unity so a later scene save retains the bindings.
internal class CommandScript : IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  if(Application.isPlaying) throw new System.Exception("Exit Play mode before configuring scene artwork.");
  var mapping=new Dictionary<string,string> {
   {"MakeCoffeePoint","制作咖啡"},{"PourDogFoodPoint","喂狗粮"},
   {"PrinterRepairPoint","打印文件"},{"CleanPoint","打扫"},
   {"ParcelCutPoint","拆快递"},{"StockPoint","炒股"},
   {"FileSourcePoint","送快递"},{"FileDestinationPoint","送快递"}
  };
  foreach(var ui in Object.FindObjectsByType<TaskInteractionPromptUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
  {
   var scene=ui.gameObject.scene;
   var data=new SerializedObject(ui);
   var root=(RectTransform)ui.transform;
   Undo.RecordObject(root,"Anchor task prompt");
   root.anchorMin=root.anchorMax=Vector2.zero;
   root.anchoredPosition=root.sizeDelta*root.pivot;
   var imageTransform=ui.transform.Find("TaskPromptImage");
   if(!imageTransform) {
    var go=new GameObject("TaskPromptImage",typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));
    Undo.RegisterCreatedObjectUndo(go,"Create task prompt image");
    go.transform.SetParent(root,false); imageTransform=go.transform;
   }
   var image=imageTransform.GetComponent<UnityEngine.UI.Image>();
   var imageRect=(RectTransform)imageTransform;
   imageRect.anchorMin=imageRect.anchorMax=imageRect.pivot=Vector2.zero;
   imageRect.anchoredPosition=new Vector2(24,60);imageRect.sizeDelta=new Vector2(500,240);
   image.color=Color.white;image.preserveAspect=true;image.raycastTarget=false;
   imageTransform.gameObject.SetActive(false);
   var key=data.FindProperty("interactionKeyHint").objectReferenceValue as GameObject;
   if(!key)throw new System.Exception("Run ConfigureTaskKeyHints.cs to configure Key.prefab instances first.");
   key.SetActive(false);
   data.FindProperty("taskPromptImage").objectReferenceValue=image;
   data.FindProperty("interactionKeyHint").objectReferenceValue=key;
   data.ApplyModifiedProperties();
   int count=0;
   foreach(var sceneRoot in scene.GetRootGameObjects())
    foreach(var target in sceneRoot.GetComponentsInChildren<Interactable>(true))
     if(mapping.TryGetValue(target.GetType().Name,out string name)) {
      var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Model/UI/UI/所有任务提示/"+name+".png");
      if(!sprite) throw new System.Exception("Missing sprite: "+name);
      var targetData=new SerializedObject(target);
      targetData.FindProperty("_interactionPromptSprite").objectReferenceValue=sprite;
      targetData.ApplyModifiedProperties();count++;
     }
   EditorSceneManager.MarkSceneDirty(scene);
   if(!EditorSceneManager.SaveScene(scene))throw new System.Exception("Scene save failed");
   result.Log("Saved loaded scene "+scene.path+": HUD image and E hint bound, "+count+" task targets configured.");
  }
 }
}
