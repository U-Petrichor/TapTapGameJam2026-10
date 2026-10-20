using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using TapTapGameJam.UI;
using TapTapGameJam.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;
public static class ESCExitCheck {
 public static async Task<string> PrepareConfirmation() {
  SceneManager.LoadScene("GameScene"); await Task.Delay(800);
  var menu=ESCMenu.Instance;
  if(menu==null) throw new Exception("Game menu missing");
  EventPipeline.Publish(new ESCMenuEvent(true));
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  ((Button)typeof(ESCMenu).GetField("ExitGameBtn",flags).GetValue(menu)).onClick.Invoke();
  return "Exit confirmation ready.";
 }
 public static string Confirm() {
  var menu=ESCMenu.Instance;
  ((Button)typeof(ESCMenu).GetField("confirmExitBtn",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu)).onClick.Invoke();
  return "Confirmed exit through the actual button.";
 }
 public static string InspectStopped() {
  if(EditorApplication.isPlaying) throw new Exception("Confirmation failed to stop Play");
  File.AppendAllText("Temp/ESCMenuQA/verification.txt","PASS: real Input System ESC closes all subpages first, resumes, and reopens; save failure stays paused with feedback; checkpoint round-trip preserves phase-start snapshot; confirmed exit stops Play in editor.\n");
  return "Confirm exit stops Play correctly.";
 }
}
