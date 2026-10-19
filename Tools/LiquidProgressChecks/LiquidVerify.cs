using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TapTapGameJam.UI;
public static class LiquidVerify
{
 public static void Run()
 {
  var shader=Shader.Find("TapTapGameJam/UI/Arc Liquid Progress");
  if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Shader compilation failed");
  var sprite=System.Linq.Enumerable.First(System.Linq.Enumerable.OfType<Sprite>(AssetDatabase.LoadAllAssetsAtPath("Assets/Art/产卵值进度条.png")));
  var root=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));root.layer=31;
  var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
  ((RectTransform)root.transform).sizeDelta=new Vector2(253,239);
  var bg=new GameObject("EmptyPipe",typeof(RectTransform),typeof(Image));bg.layer=31;bg.transform.SetParent(root.transform,false);
  ((RectTransform)bg.transform).sizeDelta=new Vector2(253,239);bg.GetComponent<Image>().sprite=sprite;
  var go=new GameObject("LiquidFill",typeof(RectTransform),typeof(Image));go.SetActive(false);go.layer=31;go.transform.SetParent(bg.transform,false);
  ((RectTransform)go.transform).sizeDelta=new Vector2(253,239);var image=go.GetComponent<Image>();image.sprite=sprite;
  var mat=new Material(shader);image.material=mat;
  var liquid=go.AddComponent<ArcLiquidProgress>();var so=new SerializedObject(liquid);so.FindProperty("liquidMaterial").objectReferenceValue=mat;so.ApplyModifiedPropertiesWithoutUndo();go.SetActive(true);
  var cam=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();cam.transform.position=new Vector3(0,0,-10);cam.orthographic=true;cam.orthographicSize=125;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.13f,.16f);canvas.worldCamera=cam;
  var rt=new RenderTexture(600,600,24);cam.targetTexture=rt;rt.Create();
  var tick=typeof(ArcLiquidProgress).GetMethod("Tick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
  Directory.CreateDirectory("Output");
  var states=new[]{0f,.25f,.55f,.85f,1f};
  foreach(var value in states){liquid.SetProgress(value);tick.Invoke(liquid,new object[]{10.0});Capture(cam,rt,"Output/progress-"+Mathf.RoundToInt(value*100)+".png");}
  liquid.SetProgress(.55f);
  for(int i=0;i<32;i++){tick.Invoke(liquid,new object[]{20.0+i/16.0});Capture(cam,rt,"Output/frame-"+i.ToString("D2")+".png");}
  liquid.SetProgress(-10);if(liquid.progress!=0)throw new Exception("Lower clamp");liquid.SetProgress(10);if(liquid.progress!=1)throw new Exception("Upper clamp");
  var before=EditorJsonUtility.ToJson((RectTransform)go.transform);tick.Invoke(liquid,new object[]{25.0});if(before!=EditorJsonUtility.ToJson((RectTransform)go.transform))throw new Exception("Layout changed");
  liquid.enabled=false;if(image.material!=mat)throw new Exception("Material not restored");
  File.WriteAllText("Output/result.txt","PASS: C# + shader compilation, rendered 0/25/55/85/100%, 32 animation frames, input clamping, layout preservation, material cleanup.");
 }
 static void Capture(Camera cam,RenderTexture rt,string path){Canvas.ForceUpdateCanvases();cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);}
}
