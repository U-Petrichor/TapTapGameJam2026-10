using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using PlayerInputManager = TapTapGameJam.Player.PlayerInputManager;
using TapTapGameJam.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ESCMenuChecks
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static ESCMenu Menu => ESCMenu.Instance;
 static T Field<T>(string name) => (T)typeof(ESCMenu).GetField(name,Flags).GetValue(Menu);
 static Button Btn(string name) => Field<Button>(name);
 static void Require(bool ok,string message) { if(!ok) throw new Exception(message); }
 static void Capture(string name,int w,int h)
 {
  var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t=>t!=null);
  File.WriteAllBytes("Temp/ESCMenuQA/"+name,(byte[])type.GetMethod("EncodeScreenToPng",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{w,h}));
 }
 public static async Task<string> Verify()
 {
  Require(Application.isPlaying && Menu!=null,"Play GameScene first.");
  Directory.CreateDirectory("Temp/ESCMenuQA");
  var input=UnityEngine.Object.FindFirstObjectByType<PlayerInputManager>();
  input.SwitchActionMap("ESCMenu");
  typeof(PlayerInputManager).GetField("_isESCMenuOpen",Flags).SetValue(input,true);
  EventPipeline.Publish(new ESCMenuEvent(true));
  Require(Time.timeScale==0,"Game did not pause.");
  Require(!Field<GameObject>("exitConfirmation").activeSelf,"Exit popup opened with menu.");
  string[] fields={"SettingsBtn","GalleryBtn","SaveAndExitBtn","ExitGameBtn"};
  string[] labels={"系统设置","游戏图鉴","保存并退出","退出至桌面"};
  for(int i=0;i<fields.Length;i++) {
   Require(Btn(fields[i]).GetComponentInChildren<TMP_Text>().text==labels[i],"Wrong label");
   Require(AssetDatabase.GetAssetPath(Btn(fields[i]).image.sprite)=="Assets/Model/UI/菜单组件/系统设置/信息栏.png","Wrong button sprite");
  }
  var sizesType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
  var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
  var sizes=singleton.GetProperty("instance").GetValue(null);
  var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});
  var view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
  var selected=view.GetType().GetProperty("selectedSizeIndex",Flags);
  int original=(int)selected.GetValue(view); var added=new List<int>();
  try {
   foreach(var pair in new[]{new[]{1920,1080},new[]{1280,720},new[]{1280,960},new[]{2560,1080}}) {
    int w=pair[0],h=pair[1];
    var sizeType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
    var resolutionType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
    var custom=Activator.CreateInstance(sizeType,Flags,null,new object[]{Enum.Parse(resolutionType,"FixedResolution"),w,h,"ESC artwork QA"},null);
    int index=(int)group.GetType().GetMethod("GetTotalCount",Flags).Invoke(group,null);
    group.GetType().GetMethod("AddCustomSize",Flags).Invoke(group,new[]{custom}); added.Add(index);
    selected.SetValue(view,index); view.Repaint(); await Task.Delay(400); Canvas.ForceUpdateCanvases();
    foreach(var name in fields.Concat(new[]{"ContinueGameBtn"})) {
     var button=Btn(name); var corners=new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
     Require(corners.All(c=>c.x>=-1&&c.x<=Screen.width+1&&c.y>=-1&&c.y<=Screen.height+1),"Button clipped: "+name);
     var point=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
     var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
     Require(hits.Count>0&&hits[0].gameObject==button.gameObject,"Wrong hit: "+name+" first="+(hits.Count>0?hits[0].gameObject.name:"none"));
    }
    Capture("menu-"+w+"x"+h+".png",w,h);
   }
  } finally {
   selected.SetValue(view,original);
   foreach(int index in added.AsEnumerable().Reverse()) group.GetType().GetMethod("RemoveCustomSize",Flags).Invoke(group,new object[]{index});
   view.Repaint();
  }
  Btn("ExitGameBtn").onClick.Invoke();
  Require(Field<GameObject>("exitConfirmation").activeSelf,"Exit confirmation missing");
  Require(EventSystem.current.currentSelectedGameObject==Btn("cancelExitBtn").gameObject,"Cancel is not default selection");
  Require(!Field<CanvasGroup>("menuGroup").interactable,"Menu click-through while confirming");
  Require(Time.timeScale==0,"Confirmation resumed game");
  await Task.Delay(400); Canvas.ForceUpdateCanvases();
  foreach(var button in new[]{Btn("cancelExitBtn"),Btn("confirmExitBtn")}) {
   var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)},hits);
   Require(hits.Count>0&&hits[0].gameObject==button.gameObject,"Confirmation button blocked");
  }
  Capture("exit-confirmation.png",1920,1080);
  Btn("cancelExitBtn").onClick.Invoke();
  Require(!Field<GameObject>("exitConfirmation").activeSelf&&Field<CanvasGroup>("menuGroup").interactable,"Cancel failed");
  Require(EventSystem.current.currentSelectedGameObject==Btn("ExitGameBtn").gameObject,"Cancel focus failed");
  Btn("GalleryBtn").onClick.Invoke();
  Require(Field<GameObject>("galleryPanel").activeSelf,"Gallery missing");
  Btn("galleryBackBtn").onClick.Invoke();
  Require(!Field<GameObject>("galleryPanel").activeSelf&&Time.timeScale==0,"Gallery return failed");
  Btn("SettingsBtn").onClick.Invoke();
  Require(SettingsMenu.IsGameSettingsOpen&&!Field<CanvasGroup>("menuGroup").interactable,"Settings missing");
  Require(Menu.TryCloseSubPanel()&&!SettingsMenu.IsGameSettingsOpen&&Time.timeScale==0,"Settings back failed");
  Require(EventSystem.current.currentSelectedGameObject==Btn("SettingsBtn").gameObject,"Settings focus failed");
  Btn("ContinueGameBtn").onClick.Invoke();
  Require(!Field<GameObject>("menuRoot").activeSelf&&Time.timeScale==1,"Arrow did not resume");
  Require(!FieldInputOpen(input),"Input pause state out of sync");
  input.SwitchActionMap("ESCMenu"); typeof(PlayerInputManager).GetField("_isESCMenuOpen",Flags).SetValue(input,true); EventPipeline.Publish(new ESCMenuEvent(true));
  File.WriteAllText("Temp/ESCMenuQA/verification.txt","PASS: exact sprites and labels; 4 resolutions and button raycasts; exit confirmation/cancel/default focus; gallery/back; settings/back; arrow resumes and restores input state.\n");
  return File.ReadAllText("Temp/ESCMenuQA/verification.txt");
 }
 static bool FieldInputOpen(PlayerInputManager input)=>(bool)typeof(PlayerInputManager).GetField("_isESCMenuOpen",Flags).GetValue(input);
 public static async Task<string> VerifyEscape()
 {
  var input=UnityEngine.Object.FindFirstObjectByType<PlayerInputManager>();
  var playerInput=input.GetComponent<PlayerInput>();
  var oldBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
  var oldBackground=InputSystem.settings.backgroundBehavior;
  var keyboard=Keyboard.current;
  try {
   InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
   InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
   InputSystem.EnableDevice(keyboard); keyboard.MakeCurrent();
   foreach(string name in new[]{"SettingsBtn","GalleryBtn","ExitGameBtn"}) {
    Btn(name).onClick.Invoke();
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); await Task.Delay(250);
    InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await Task.Delay(100);
    Require(FieldInputOpen(input)&&Time.timeScale==0,"ESC resumed instead of closing "+name);
    Require(!SettingsMenu.IsGameSettingsOpen&&!Field<GameObject>("galleryPanel").activeSelf&&!Field<GameObject>("exitConfirmation").activeSelf,"ESC did not close "+name);
   }
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); await Task.Delay(250);
   InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await Task.Delay(100);
   Require(!FieldInputOpen(input)&&!Field<GameObject>("menuRoot").activeSelf,"ESC failed to resume");
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); await Task.Delay(250);
   InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await Task.Delay(100);
   Require(FieldInputOpen(input)&&Field<GameObject>("menuRoot").activeSelf&&Time.timeScale==0,"ESC failed to reopen");
   File.AppendAllText("Temp/ESCMenuQA/verification.txt","PASS: real Input System ESC closes each subpage first; next ESC resumes, then ESC reopens.\n");
   return "ESC input integration passed.";
  } finally { InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.settings.editorInputBehaviorInPlayMode=oldBehavior; InputSystem.settings.backgroundBehavior=oldBackground; }
 }
 public static string VerifySaveFailure()
 {
  var field=typeof(SaveManager).GetField("_latestCheckpoint",BindingFlags.Static|BindingFlags.NonPublic);
  var snapshot=field.GetValue(null);
  try {
   field.SetValue(null,null); Btn("SaveAndExitBtn").onClick.Invoke();
   Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="GameScene"&&Time.timeScale==0,"Failed save exited game");
   Require(Field<TMP_Text>("saveStatus").text.Contains("保存失败"),"Missing failure feedback");
  } finally {field.SetValue(null,snapshot);}
  Require(SaveManager.SaveLatestCheckpoint(),"Checkpoint persistence failed");
  Require(SaveManager.TryRead(out var saved),"Saved checkpoint unreadable");
  Require(JsonUtility.ToJson(saved)==JsonUtility.ToJson(snapshot),"Checkpoint changed");
  File.AppendAllText("Temp/ESCMenuQA/verification.txt","PASS: failed save stays paused with visible feedback; checkpoint write/read preserves phase-start snapshot.\n");
  return "Save failure and checkpoint round-trip passed.";
 }
 public static string SaveAndReturn()
 {
  Btn("SaveAndExitBtn").onClick.Invoke(); return "Save and return button invoked; inspect MainMenu next.";
 }
 public static object InspectReturn()
 {
  Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu","Did not return to MainMenu");
  Require(Time.timeScale==1,"Time scale not restored on scene exit");
  File.AppendAllText("Temp/ESCMenuQA/verification.txt","PASS: save-and-exit returns to MainMenu and restores time scale.\n");
  return "Save and return integration passed.";
 }
}



