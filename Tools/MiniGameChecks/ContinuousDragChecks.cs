using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TapTapGameJam.UI;
internal class CommandScript : IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  if(Application.isPlaying)throw new Exception("Run these isolated checks outside Play mode.");
  void Check(bool ok,string text){if(!ok)throw new Exception(text);result.Log("PASS: "+text);}
  var authored=UnityEngine.Object.FindFirstObjectByType<CleanGame>(FindObjectsInactive.Include);
  Check(authored!=null && authored.IsConfigured,"clean scene is configured");
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
  try {
   var canvasObject=new GameObject("ContinuousDragChecks",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));SceneManager.MoveGameObjectToScene(canvasObject,scene);
   var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;((RectTransform)canvasObject.transform).sizeDelta=new Vector2(1100,600);
   var cameraObject=new GameObject("PointerCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
   var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=300;camera.transform.position=new Vector3(0,0,-10);canvas.worldCamera=camera;
   var eventsObject=new GameObject("PointerEvents",typeof(EventSystem));SceneManager.MoveGameObjectToScene(eventsObject,scene);
   var events=eventsObject.GetComponent<EventSystem>();var raycaster=canvasObject.GetComponent<GraphicRaycaster>();
   PointerEventData Pointer(GameObject hit)=>new PointerEventData(events){button=PointerEventData.InputButton.Left,pointerId=-1,pointerPressRaycast=new RaycastResult{gameObject=hit,module=raycaster}};
   var clean=UnityEngine.Object.Instantiate(authored,canvas.transform,false);clean.gameObject.SetActive(true);((RectTransform)clean.transform).anchoredPosition3D=Vector3.zero;clean.transform.localScale=Vector3.one;clean.transform.localRotation=Quaternion.identity;
   clean.Begin(null);var data=new SerializedObject(clean);var stain=(RawImage)data.FindProperty("_stainImage").objectReferenceValue;
   var evt=Pointer(stain.gameObject);var texture=(Texture2D)stain.texture;var rect=stain.rectTransform;
   void PixelPosition(Vector2 pixel){var p=new Vector3(Mathf.Lerp(rect.rect.xMin,rect.rect.xMax,pixel.x/(texture.width-1)),Mathf.Lerp(rect.rect.yMin,rect.rect.yMax,pixel.y/(texture.height-1)),0);evt.position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(p));}
   Vector2 DirtyPoint(bool right){var pixels=texture.GetPixels32();for(int y=texture.height/6;y<texture.height*5/6;y++)for(int x=0;x<texture.width;x++){int px=right?texture.width-1-x:x;if((right?px>texture.width*2/3:px<texture.width/3) && pixels[y*texture.width+px].a>0)return new Vector2(px,y);}throw new Exception("No test stain in expected region");}
   Check(ExecuteEvents.GetEventHandler<IPointerDownHandler>(stain.gameObject)==clean.gameObject && ExecuteEvents.GetEventHandler<IDragHandler>(stain.gameObject)==clean.gameObject,"clean press and drag are captured by the same stable game root");
   evt.useDragThreshold=true;clean.OnInitializePotentialDrag(evt);Check(!evt.useDragThreshold,"clean starts dragging without the default movement threshold");
   Vector2 a=DirtyPoint(false),b=DirtyPoint(true);PixelPosition(a);clean.OnPointerDown(evt);var afterPress=texture.GetPixels32();
   evt.position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(new Vector3(rect.rect.xMax+150,0,0)));clean.OnPointerExit(evt);clean.OnDrag(evt);
   var outside=texture.GetPixels32();Check(Same(afterPress,outside),"cleaning pauses outside the surface");
   PixelPosition(b);clean.OnPointerEnter(evt);clean.OnDrag(evt);var returned=texture.GetPixels32();int bIndex=(int)b.y*texture.width+(int)b.x;Check(afterPress[bIndex].a>0 && returned[bIndex].a==0,"held pointer resumes cleaning after reentry without pressing again");
   float radius=data.FindProperty("_brushRadius").floatValue*texture.width/rect.rect.width;bool bridged=false;
   for(int i=0;i<returned.Length;i++)if(outside[i].a!=returned[i].a && Vector2.Distance(new Vector2(i%texture.width,i/texture.width),b)>radius+1f){bridged=true;break;}
   Check(!bridged,"reentry does not erase a bridge from the previous contact point");
   clean.OnPointerExit(evt);clean.OnPointerUp(evt);var released=texture.GetPixels32();PixelPosition(DirtyPoint(false));clean.OnPointerEnter(evt);clean.OnDrag(evt);Check(Same(released,texture.GetPixels32()),"release outside prevents cleaning when the pointer returns");
   clean.OnPointerDown(evt);clean.SendMessage("OnApplicationFocus",false);released=texture.GetPixels32();PixelPosition(DirtyPoint(true));clean.OnDrag(evt);Check(Same(released,texture.GetPixels32()),"focus loss cancels the captured clean gesture");clean.End();

   // Known tape geometry isolates pointer capture and tape transitions from random art layouts.
   var parcelObject=new GameObject("ParcelContinuousDrag",typeof(RectTransform));parcelObject.transform.SetParent(canvas.transform,false);((RectTransform)parcelObject.transform).sizeDelta=new Vector2(1100,600);
   var parcel=parcelObject.AddComponent<ParcelCutGame>();
   RawImage Image(string name,Vector2 position,Vector2 size){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));go.transform.SetParent(parcel.transform,false);var r=(RectTransform)go.transform;r.anchoredPosition=position;r.sizeDelta=size;return go.GetComponent<RawImage>();}
   var box=Image("Box",Vector2.zero,new Vector2(900,400));var blade=Image("Blade",Vector2.zero,new Vector2(20,20));var arm=Image("Arm",Vector2.zero,new Vector2(20,20));
   var parcelData=new SerializedObject(parcel);parcelData.FindProperty("_boxImage").objectReferenceValue=box;parcelData.FindProperty("_cutterBlade").objectReferenceValue=blade;parcelData.FindProperty("_cutterHandle").objectReferenceValue=arm;parcelData.FindProperty("_showCheckpoints").boolValue=false;
   var tapes=parcelData.FindProperty("_tapes");tapes.arraySize=6;var images=new RawImage[6];
   for(int i=0;i<6;i++){
    images[i]=Image("Tape"+i,new Vector2((i%3-1)*300,(i/3*2-1)*100),new Vector2(180,60));var item=tapes.GetArrayElementAtIndex(i);item.FindPropertyRelative("Image").objectReferenceValue=images[i];var path=item.FindPropertyRelative("Path");path.arraySize=2;path.GetArrayElementAtIndex(0).vector2Value=new Vector2(.1f,.5f);path.GetArrayElementAtIndex(1).vector2Value=new Vector2(.9f,.5f);var outline=item.FindPropertyRelative("Outline");outline.arraySize=4;var corners=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};for(int j=0;j<4;j++)outline.GetArrayElementAtIndex(j).vector2Value=corners[j];
   }
   parcelData.ApplyModifiedPropertiesWithoutUndo();int completed=0;parcel.Begin(()=>completed++);
   var targets=new System.Collections.Generic.List<int>();for(int i=0;i<6;i++)if(images[i].gameObject.activeSelf)targets.Add(i);Check(targets.Count==3,"parcel round selects three tapes");
   var cut=Pointer(images[targets[0]].gameObject);
   void TapePosition(int tape,float x){var r=images[tape].rectTransform;cut.position=RectTransformUtility.WorldToScreenPoint(camera,r.TransformPoint(new Vector3(Mathf.Lerp(r.rect.xMin,r.rect.xMax,x),0,0)));}
   void Outside(){cut.position=RectTransformUtility.WorldToScreenPoint(camera,parcel.transform.TransformPoint(new Vector3(900,500,0)));parcel.OnPointerExit(cut);parcel.OnDrag(cut);}
   Check(ExecuteEvents.GetEventHandler<IPointerDownHandler>(images[targets[0]].gameObject)==parcel.gameObject && ExecuteEvents.GetEventHandler<IDragHandler>(images[targets[0]].gameObject)==parcel.gameObject,"parcel press and drag remain on the game root when tape images disappear");
   cut.useDragThreshold=true;parcel.OnInitializePotentialDrag(cut);Check(!cut.useDragThreshold,"parcel starts dragging without a movement threshold");
   int firstTape=targets[0];TapePosition(firstTape,.1f);parcel.OnPointerDown(cut);TapePosition(firstTape,.25f);parcel.OnDrag(cut);Check(images[firstTape].gameObject.activeSelf,"short cut leaves the first tape incomplete");
   Outside();TapePosition(firstTape,.9f);parcel.OnDrag(cut);Check(images[firstTape].gameObject.activeSelf,"reentry starts a fresh tape segment without cutting across the gap");
   TapePosition(firstTape,.35f);parcel.OnDrag(cut);Check(!images[firstTape].gameObject.activeSelf,"held pointer resumes and completes tape after leaving the box");
   for(int i=1;i<targets.Count;i++){TapePosition(targets[i],.1f);parcel.OnDrag(cut);TapePosition(targets[i],.9f);parcel.OnDrag(cut);Check(!images[targets[i]].gameObject.activeSelf,"same held gesture cuts subsequent tape "+i);}
   Check(completed==1,"one continuous gesture completes the parcel exactly once");
   parcel.Begin(()=>completed++);int target=-1;for(int i=0;i<6;i++)if(images[i].gameObject.activeSelf){target=i;break;}
   TapePosition(target,.1f);parcel.OnPointerDown(cut);Outside();parcel.OnPointerUp(cut);var stoppedPosition=blade.transform.position;TapePosition(target,.9f);parcel.OnDrag(cut);Check(images[target].gameObject.activeSelf && blade.transform.position==stoppedPosition,"release outside ends the parcel gesture");
   TapePosition(target,.1f);parcel.OnPointerDown(cut);parcel.SendMessage("OnApplicationPause",true);stoppedPosition=blade.transform.position;TapePosition(target,.9f);parcel.OnDrag(cut);Check(blade.transform.position==stoppedPosition,"application pause cancels the parcel gesture");parcel.End();
  } finally {EditorSceneManager.CloseScene(scene,true);}
 }
 private static bool Same(Color32[] a,Color32[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i].a!=b[i].a)return false;return true;}
}
