using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public static class SettingsVerify
{
 public static void Run()
 {
  // Verify the authored production prefab, preserving its existing scene references.
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>(SettingsArtworkBuilder.PrefabPath);
  var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
  var controller=instance.GetComponent<SettingsMenu>();
  var guide=instance.transform.Find("Design/ShortcutsPage");
  string[] actions={"移动","交互","切换目标","丢下（仅夜晚）","背包","天赋（仅家园）","切换镜头角度"};
  string[] keys={"WASD","E","Q","X","B","T","[、]"};
  if(instance.transform.Find("Design/ShortcutsTab/Label").GetComponent<TMP_Text>().text!="按键介绍")throw new Exception("Guide tab label mismatch");
  if(guide.Cast<Transform>().Any(t=>t.name.StartsWith("LeftArrow")||t.name.StartsWith("RightArrow")))throw new Exception("Guide still has arrows");
  for(int i=0;i<actions.Length;i++)
  {
   var action=guide.Find("Action"+i).GetComponent<TMP_Text>();
   var key=guide.Find("Key"+i).GetComponent<TMP_Text>();
   if(action.text!=actions[i]||key.text!=keys[i])throw new Exception("Guide row mismatch: "+i);
   foreach(var text in new[]{action,key})
    if(text.GetPreferredValues().x>text.rectTransform.rect.width)throw new Exception("Guide text overflow: "+text.name);
  }
  var probe=new GameObject("References").AddComponent<SettingsReferenceProbe>();probe.settings=instance;probe.back=controller.BackButton;
  EditorSceneManager.SaveScene(scene,"Assets/SettingsIntegration.unity");
  foreach(var slider in instance.GetComponentsInChildren<Slider>(true))
   if(slider.handleRect==null || slider.maxValue!=1 || slider.minValue!=0) throw new Exception("Invalid slider");
  foreach(var graphic in instance.GetComponentsInChildren<Graphic>(true))
   if(graphic is Image i && i.sprite==null && i.color.a!=0) throw new Exception("Missing sprite: "+i.name);
  var method=typeof(SettingsMenu).GetMethod("ShowPage",BindingFlags.NonPublic|BindingFlags.Instance);
  method.Invoke(controller,new object[]{false});
  if(!instance.transform.Find("Design/ShortcutsPage").gameObject.activeSelf)throw new Exception("Shortcuts hidden");
  method.Invoke(controller,new object[]{true});
  if(!instance.transform.Find("Design/SoundPage").gameObject.activeSelf)throw new Exception("Sound hidden");
  var prefs=new[]{"Settings.Effects","Settings.Music","Settings.Animations"};
  var had=prefs.Select(PlayerPrefs.HasKey).ToArray();
  var oldEffects=PlayerPrefs.GetFloat(prefs[0]);var oldMusic=PlayerPrefs.GetFloat(prefs[1]);var oldAnimation=PlayerPrefs.GetInt(prefs[2]);
  try
  {
   typeof(SettingsMenu).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(controller,null);
   typeof(SettingsMenu).GetMethod("OnEnable",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(controller,null);
   var effects=instance.GetComponentsInChildren<Slider>(true).Single(s=>s.name=="EffectsSlider");
   var music=instance.GetComponentsInChildren<Slider>(true).Single(s=>s.name=="MusicSlider");
   effects.value=.27f;music.value=.63f;
   if(Mathf.Abs(PlayerPrefs.GetFloat(prefs[0])-.27f)>.001f || Mathf.Abs(PlayerPrefs.GetFloat(prefs[1])-.63f)>.001f)throw new Exception("Volume preferences failed");
   var before=SettingsMenu.AnimationsEnabled;
   instance.GetComponentsInChildren<Button>(true).Single(b=>b.name=="AnimationToggle").onClick.Invoke();
   if(SettingsMenu.AnimationsEnabled==before)throw new Exception("Animation toggle failed");
   typeof(SettingsMenu).GetMethod("OnEnable",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(controller,null);
   if(Mathf.Abs(effects.value-.27f)>.001f || Mathf.Abs(music.value-.63f)>.001f)throw new Exception("Volume reload failed");
   instance.GetComponentsInChildren<Button>(true).Single(b=>b.name=="AnimationToggle").onClick.Invoke();
  }
  finally
  {
   PlayerPrefs.SetFloat(prefs[0],oldEffects);PlayerPrefs.SetFloat(prefs[1],oldMusic);PlayerPrefs.SetInt(prefs[2],oldAnimation);
   for(int i=0;i<3;i++)if(!had[i])PlayerPrefs.DeleteKey(prefs[i]);PlayerPrefs.Save();
  }
  bool returned=false;SettingsMenu.OpenInGame(()=>returned=true);
  if(UnityEngine.Object.FindObjectsByType<SettingsMenu>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>s!=controller).Single().GetComponent<Canvas>().sortingOrder<=100)throw new Exception("Settings is behind gameplay UI");
  if(!SettingsMenu.IsGameSettingsOpen || !SettingsMenu.CloseGameSettings() || !returned || SettingsMenu.IsGameSettingsOpen)throw new Exception("Game settings navigation failed");
  foreach(var other in UnityEngine.Object.FindObjectsByType<SettingsMenu>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(other!=controller)UnityEngine.Object.DestroyImmediate(other.gameObject);
  // Render on a background graphics device; no editor window is opened.
  UnityEngine.Object.DestroyImmediate(probe.gameObject);
  var canvas=instance.GetComponent<Canvas>();var cam=new GameObject("RenderCamera").AddComponent<Camera>();
  cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.orthographic=true;cam.orthographicSize=540;
  cam.transform.position=new Vector3(0,0,-1000);cam.nearClipPlane=.1f;cam.farClipPlane=2000;
  canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=100;
  instance.SetActive(true);
  foreach(var pair in new[]{("shortcuts",false),("sound",true),("shortcuts-4x3",false),("shortcuts-ultrawide",false)})
  {
   method.Invoke(controller,new object[]{pair.Item2});
   int width=pair.Item1=="shortcuts-4x3"?1440:pair.Item1=="shortcuts-ultrawide"?2560:1920;
   var rt=new RenderTexture(width,1080,24);cam.targetTexture=rt;rt.Create();
   Canvas.ForceUpdateCanvases();
   foreach(var slider in instance.GetComponentsInChildren<Slider>(true))
    if(Mathf.Abs(slider.handleRect.rect.height-84)>1) throw new Exception("Handle height: "+slider.handleRect.rect.height);
   cam.Render();RenderTexture.active=rt;
   var tex=new Texture2D(width,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,1080),0,0);tex.Apply();
   File.WriteAllBytes(pair.Item1+".png",tex.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
  }
  File.WriteAllText("settings-verify-result.txt","PASS: prefab references, slider ranges/handle height, page switching, volume persistence/reload, animation preference, in-game open/close callback, background rendering");
 }
}
