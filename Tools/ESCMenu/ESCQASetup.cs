using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TapTapGameJam.Core;
public static class ESCQASetup {
 public static object Prepare() {
  if(EditorApplication.isPlaying) throw new Exception("Already playing");
  string dir="Temp/ESCMenuQA/PlayerSaveBackup"; Directory.CreateDirectory(dir);
  foreach(string suffix in new[]{"", ".bak", ".tmp"}) {
   string src=SaveManager.SavePath+suffix, target=dir+"/save.json"+suffix;
   if(File.Exists(src)) { File.Copy(src,target,true); File.WriteAllText(target+".exists","yes"); }
   else File.WriteAllText(target+".exists","no");
  }
  EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
  return new {prepared=true,savePath=SaveManager.SavePath};
 }
 public static object Restore() {
  if(EditorApplication.isPlaying) throw new Exception("Stop Play before restoring saves");
  foreach(string suffix in new[]{"", ".bak", ".tmp"}) {
   string dst=SaveManager.SavePath+suffix, source="Temp/ESCMenuQA/PlayerSaveBackup/save.json"+suffix;
   if(File.ReadAllText(source+".exists")=="yes") File.Copy(source,dst,true);
   else if(File.Exists(dst)) File.Delete(dst);
  }
  EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
  return "Original player save files and MainMenu editor scene restored.";
 }
}
