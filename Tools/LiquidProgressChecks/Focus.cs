using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
internal class CommandScript : IRunCommand {
 public void Execute(ExecutionResult result) {
  var go=Selection.activeGameObject;
  if(go && go.name=="LiquidFill") {
   var r=(RectTransform)go.transform.parent.parent; var points=new Vector3[4];r.GetWorldCorners(points);
   var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);b.Expand(b.size.magnitude*.15f);
   if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(b,false);
   EditorSceneManager.SaveScene(go.scene);
  }
  result.Log("Liquid preview ready; GameScene saved.");
 }
}
