using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Linq;
internal class CommandScript : IRunCommand {
 public void Execute(ExecutionResult result) {
  if(Application.isPlaying)throw new System.Exception("Exit Play mode before migration.");
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  if(scene.path!="Assets/Scenes/GameScene.unity")throw new System.Exception("Expected GameScene.");
  var root=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="DayCount");
  var day=root.Find("DayUI") as RectTransform;
  if(!day) {
   var background=(RectTransform)root.Find("Background");
   var number=(RectTransform)background.Find("DayNumber");
   var oldPosition=number.anchoredPosition;var oldSize=number.sizeDelta;
   var go=new GameObject("DayUI",typeof(RectTransform));go.layer=root.gameObject.layer;
   Undo.RegisterCreatedObjectUndo(go,"Create editable day group");
   day=(RectTransform)go.transform;day.SetParent(root,false);
   day.anchorMin=background.anchorMin;day.anchorMax=background.anchorMax;day.pivot=background.pivot;
   day.sizeDelta=background.sizeDelta;day.anchoredPosition=background.anchoredPosition;
   day.localRotation=background.localRotation;day.localScale=background.localScale;
   Undo.SetTransformParent(number,day,"Preserve authored day number");
   // New parent has exactly the original image's geometry; retain designer coordinates.
   number.anchoredPosition=oldPosition;number.sizeDelta=oldSize;
   Undo.SetTransformParent(background,day,"Move day artwork");
   background.anchorMin=Vector2.zero;background.anchorMax=Vector2.one;background.pivot=new Vector2(.5f,.5f);
   background.anchoredPosition=Vector2.zero;background.sizeDelta=Vector2.zero;background.localScale=Vector3.one;background.localRotation=Quaternion.identity;
   background.SetAsFirstSibling();
   result.Log("Preserved day number designer offset "+oldPosition+" and size "+oldSize);
  }
  var night=root.Find("NightUI") as RectTransform;
  if(!night) {
   var go=Object.Instantiate(day.gameObject,root,false);go.name="NightUI";Undo.RegisterCreatedObjectUndo(go,"Create editable night group");night=(RectTransform)go.transform;
   var image=night.Find("Background").GetComponent<UnityEngine.UI.Image>();
   image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Model/UI/UI/白天夜晚状态/夜晚.png");
   night.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,night.rect.width*image.sprite.rect.height/image.sprite.rect.width);
   var nr=(RectTransform)night.Find("DayNumber");nr.anchorMin=nr.anchorMax=new Vector2(.20f,.30f);
  }
  var ui=root.GetComponent<DayCountUI>();var data=new SerializedObject(ui);
  data.FindProperty("dayUI").objectReferenceValue=day.gameObject;
  data.FindProperty("nightUI").objectReferenceValue=night.gameObject;
  data.FindProperty("dayNumber").objectReferenceValue=day.Find("DayNumber").GetComponent<TextMeshProUGUI>();
  data.FindProperty("nightNumber").objectReferenceValue=night.Find("DayNumber").GetComponent<TextMeshProUGUI>();
  data.ApplyModifiedProperties();
  Undo.RecordObject(day.gameObject,"Preview day UI");Undo.RecordObject(night.gameObject,"Preview night UI");
  day.gameObject.SetActive(true);night.gameObject.SetActive(false);
  EditorSceneManager.MarkSceneDirty(scene);
  if(!EditorSceneManager.SaveScene(scene))throw new System.Exception("Failed saving GameScene");
  Selection.activeGameObject=root.gameObject;
  result.Log("Saved DayCount/DayUI/{Background,DayNumber} and DayCount/NightUI/{Background,DayNumber}. Inspector references bound. No runtime layout writes.");
 }
}
