using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class DoorChecksEditor
{
 static DoorChecksEditor() { EditorApplication.update+=()=> { if(EditorApplication.isPlaying && EditorApplication.isPaused) EditorApplication.isPaused=false; EditorApplication.QueuePlayerLoopUpdate(); }; }
 public static void Run() {
  PlayerSettings.runInBackground=true;
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  EditorSceneManager.SaveScene(scene,"Assets/Checks.unity");
  EditorApplication.EnterPlaymode();
 }
}
