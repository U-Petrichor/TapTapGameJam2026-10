using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TapTapGameJam.UI;
using TapTapGameJam.Cockroach;
internal class CommandScript : IRunCommand {
 public void Execute(ExecutionResult result) {
  if(Application.isPlaying)throw new Exception("Exit Play mode before binding population UI");
  var scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
  if(!scene.IsValid()||!scene.isLoaded)throw new Exception("Open GameScene first");
  var manager=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CockroachManager>(true)).Single();
  var ui=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ResourcesUI>(true)).Single();
  var liquid=ui.GetComponentsInChildren<ArcLiquidProgress>(true).Single();
  Directory.CreateDirectory("RecoverySnapshots/LiquidProgress");
  EditorSceneManager.SaveScene(scene,"RecoverySnapshots/LiquidProgress/GameScene_before_population_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
  Undo.RecordObject(manager,"Configure population victory target");
  var md=new SerializedObject(manager);md.FindProperty("victoryPopulation").intValue=2000;md.ApplyModifiedProperties();
  Undo.RecordObject(ui,"Bind cockroach population progress");
  var data=new SerializedObject(ui);data.FindProperty("cockroachManager").objectReferenceValue=manager;data.FindProperty("cockroachProgress").objectReferenceValue=liquid;data.ApplyModifiedProperties();
  Undo.RecordObject(liquid,"Use live population progress");liquid.demoLoop=false;liquid.SetProgress(0);EditorUtility.SetDirty(liquid);
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Selection.activeGameObject=ui.gameObject;
  result.Log("Bound population HUD to CockroachManager. Goal = 2000. Demo off, initial progress = 0. Saved GameScene.");
 }
}
