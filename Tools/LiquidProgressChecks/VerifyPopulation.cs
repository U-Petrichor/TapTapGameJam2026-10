using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TapTapGameJam.UI;
using TapTapGameJam.Cockroach;
using TapTapGameJam.Events;
using TMPro;
internal class CommandScript : IRunCommand {
 static void Call(MonoBehaviour o,string method){o.SendMessage(method,SendMessageOptions.RequireReceiver);}
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 public void Execute(ExecutionResult result){
  Check(!Application.isPlaying,"Run outside Play mode");
  var live=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
  var source=live.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ResourcesUI>(true)).Single();
  var saved=EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
  try{var bound=saved.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ResourcesUI>(true)).Single();var d=new SerializedObject(bound);var m=(CockroachManager)d.FindProperty("cockroachManager").objectReferenceValue;var l=(ArcLiquidProgress)d.FindProperty("cockroachProgress").objectReferenceValue;Check(m&&l&&m.VictoryPopulation==2000&&!l.demoLoop&&l.progress==0,"Saved GameScene binding/target/demo state");}finally{EditorSceneManager.ClosePreviewScene(saved);}
  var preview=EditorSceneManager.NewPreviewScene();ResourcesUI ui=null;CockroachManager manager=null;Action<EventGameOver> watch=null;
  try{
   var root=new GameObject("PopulationCheck");SceneManager.MoveGameObjectToScene(root,preview);
   var managerGo=new GameObject("Manager");managerGo.transform.SetParent(root.transform);manager=managerGo.AddComponent<CockroachManager>();Call(manager,"OnEnable");
   CockNest Nest(string name){var go=new GameObject(name);go.transform.SetParent(root.transform);return go.AddComponent<CockNest>();}
   var a=Nest("NestA");var b=Nest("NestB");manager.CockNestList.Add(a);manager.CockNestList.Add(b);
   ui=UnityEngine.Object.Instantiate(source.gameObject,root.transform,false).GetComponent<ResourcesUI>();
   var data=new SerializedObject(ui);data.FindProperty("cockroachManager").objectReferenceValue=manager;data.ApplyModifiedPropertiesWithoutUndo();
   var liquid=(ArcLiquidProgress)data.FindProperty("cockroachProgress").objectReferenceValue;
   var text=(TMP_Text)data.FindProperty("cockroachNumText").objectReferenceValue;
   var rect=EditorJsonUtility.ToJson((RectTransform)liquid.transform);var color=liquid.liquidColor;
   int wins=0;watch=e=>{if(e.IsWin)wins++;};EventPipeline.Subscribe(watch);
   void Count(int first,int second){a.CockroachList.Clear();b.CockroachList.Clear();for(int i=0;i<first;i++)a.CockroachList.Add(null);for(int i=0;i<second;i++)b.CockroachList.Add(null);Call(manager,"LateUpdate");}
   // Initial population can exist before HUD subscribes; Start must not overwrite it with zero.
   for(int i=0;i<500;i++)a.CockroachList.Add(null);
   liquid.demoLoop=true;Call(ui,"OnEnable");Call(ui,"Start");Check(liquid.progress==.25f&&text.text=="500"&&!liquid.demoLoop,"Initial population and demo override");
   Count(500,500);Check(liquid.progress==.5f&&text.text=="1000"&&wins==0,"1000 => 50%, sum all nests");
   Count(250,250);Check(liquid.progress==.25f&&text.text=="500","Deaths reduce progress");
   Count(1999,0);Check(liquid.progress<1&&wins==0,"1999 must not win");
   Count(2000,0);Check(liquid.progress==1&&text.text=="2000"&&wins==1&&manager.HasWon,"2000 wins exactly once");
   Count(2100,0);Check(liquid.progress==1&&text.text=="2100"&&wins==1,"Over target clamps and does not repeat victory");
   Count(0,0);Check(liquid.progress==0&&text.text=="0"&&wins==1,"Zero is empty");
   Call(ui,"OnDisable");Count(100,0);Check(text.text=="0","Disabled HUD should unsubscribe");Call(ui,"OnEnable");Check(text.text=="100"&&Mathf.Abs(liquid.progress-.05f)<.00001f,"Re-enable reads current population");
   Count(100,100);manager.CockNestList.Remove(b);Call(manager,"LateUpdate");Check(text.text=="100","Nest removal refresh");
   Check(rect==EditorJsonUtility.ToJson((RectTransform)liquid.transform)&&color==liquid.liquidColor,"Authored layout and liquid colors preserved");
   // A previous loss must prevent a later victory event.
   var lossGo=new GameObject("LossManager");lossGo.transform.SetParent(root.transform);var loss=lossGo.AddComponent<CockroachManager>();Call(loss,"OnEnable");loss.CockNestList.Add(a);a.CockroachList.Clear();for(int i=0;i<2000;i++)a.CockroachList.Add(null);EventPipeline.Publish(new EventGameOver(false,"check"));Call(loss,"LateUpdate");Check(!loss.HasWon&&wins==1,"No victory after loss");Call(loss,"OnDisable");
   string message="PASS: production C# compilation; saved bindings and 2000 goal; initial 500/2000; 1000=50%; death regression; 1999 no win; 2000 wins; >2000 clamped; one-shot victory; zero; HUD disable/re-enable; nest removal; loss guard; authored layout/colors preserved. Counts simulated in a disposable preview scene, live population unchanged.";
   Directory.CreateDirectory("Temp/LiquidProgressChecks");File.WriteAllText("Temp/LiquidProgressChecks/population-result.txt",message);result.Log(message);
  } finally {
   if(ui)Call(ui,"OnDisable");if(manager)Call(manager,"OnDisable");
   EditorSceneManager.ClosePreviewScene(preview);if(watch!=null)EventPipeline.UnSubscribe(watch);
  }
 }
}
