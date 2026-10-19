using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand {
 public void Execute(ExecutionResult result){
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
 result.Log("Scene="+scene.path+" playing="+Application.isPlaying+" dirty="+scene.isDirty);
 var root=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="DayCount");
 foreach(var rect in root.GetComponentsInChildren<RectTransform>(true))result.Log(rect.name+" anchor="+rect.anchorMin+"/"+rect.anchorMax+" position="+rect.anchoredPosition+" size="+rect.sizeDelta+" pivot="+rect.pivot);
 }
}
