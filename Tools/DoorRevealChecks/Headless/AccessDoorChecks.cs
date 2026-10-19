using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using UnityEngine;

public sealed class AccessDoorChecks:MonoBehaviour
{
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Launch()=>new GameObject("Checks").AddComponent<AccessDoorChecks>();
 readonly List<string> checks=new();
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
 void Check(bool ok,string message) { if(!ok) throw new Exception(message); checks.Add(message); }
 IEnumerator Start()
 {
  var tests=Run();
  while(true) {
   object current;
   try { if(!tests.MoveNext()) break; current=tests.Current; }
   catch(Exception e) { File.WriteAllText("result.txt","FAIL: "+e+"\n"+string.Join("\n",checks)); UnityEditor.EditorApplication.Exit(1); yield break; }
   yield return current;
  }
  File.WriteAllText("result.txt","PASS "+checks.Count+"\n"+string.Join("\n",checks));
  UnityEditor.EditorApplication.Exit(0);
 }
 SecurityDoor Door(string name,Vector3 pos,bool card)
 {
  var go=new GameObject(name); go.SetActive(false); go.transform.position=pos;
  var door=go.AddComponent<SecurityDoor>();
  var block=go.AddComponent<BoxCollider>();
  var detect=go.AddComponent<BoxCollider>(); detect.isTrigger=true; detect.size=Vector3.one*4;
  var animator=go.AddComponent<Animator>(); animator.enabled=false;
  Set(door,"_blockCollider",block); Set(door,"_detectCollider",detect); Set(door,"_animator",animator);
  Set(door,"_requiresAccessCard",card); Set(door,"_whichDayOpen",3); Set(door,"_doorAnimationDuration",0.05f);
  go.SetActive(true); return door;
 }
 void Move(GameObject o,Vector3 pos) { o.transform.position=pos; Physics.SyncTransforms(); }
 IEnumerator Run()
 {
  Application.runInBackground=true;
  DoorRepeatRegression.Run(Check);
  var phase=new GameObject("Phase").AddComponent<GamePhaseManager>(); GameManager.Phase=phase;
  yield return null; phase.TimeCouldWork=false;
  var player=new GameObject("Player"); player.tag="Player"; player.AddComponent<PlayerBasic>();
  var interaction=player.AddComponent<PlayerInteractionManager>();
  player.AddComponent<CapsuleCollider>(); var rb=player.AddComponent<Rigidbody>(); rb.isKinematic=true;
  var normal=Door("Normal",new Vector3(100,0,0),false);
  Move(player,normal.transform.position);
  EventPipeline.Publish(new EventNewDay(2));
  yield return new WaitForSeconds(0.15f);
  Check(!Get<bool>(normal,"_doorOpen") && Get<Collider>(normal,"_blockCollider").enabled,"Locked normal door blocks nearby player");
  EventPipeline.Publish(new EventNewDay(3));
  yield return new WaitForSeconds(0.15f);
  Check(Get<bool>(normal,"_doorOpen") && !Get<Collider>(normal,"_blockCollider").enabled,"Date unlock opens for player already in detection area");
  Check(Get<Collider>(normal,"_detectCollider").enabled,"Open door keeps detection enabled");
  Move(player,Vector3.zero);
  yield return new WaitForSeconds(0.15f);
  Check(!Get<bool>(normal,"_doorOpen") && Get<Collider>(normal,"_blockCollider").enabled,"Normal door closes after player leaves");
  EventPipeline.Publish(new EventNewDay(2));
  var npc=new GameObject("NPC"); npc.AddComponent<TapTapGameJam.NPC.NPC>(); npc.AddComponent<BoxCollider>();
  Move(npc,normal.transform.position);
  yield return new WaitForSeconds(0.15f);
  Check(Get<bool>(normal,"_doorOpen") && Get<Collider>(normal,"_blockCollider").enabled,"NPC opens locked door visually without granting player access");
  Move(npc,new Vector3(300,0,0));
  yield return new WaitForSeconds(0.15f);
  Check(!Get<bool>(normal,"_doorOpen"),"Door closes after last NPC leaves");
  Check(!typeof(Interactable).IsAssignableFrom(typeof(SecurityDoor)),"SecurityDoor has no Interactable inheritance");
  foreach(int pickup in new[]{2,4,0})
  {
   var door=Door("CardDoor",new Vector3(200,0,0),true);
   var template=new GameObject("CardTemplate"); template.transform.position=new Vector3(1000,0,0);
   var cardTemplate=template.AddComponent<AccessCard>(); var trigger=template.AddComponent<SphereCollider>(); trigger.isTrigger=true; trigger.radius=2;
   var root=new GameObject("Spawner"); root.SetActive(false); var spawner=root.AddComponent<AccessCardSpawner>();
   var point2=new GameObject("Day2Night").transform;
   var point4=new GameObject("Day4Night").transform; point4.position=new Vector3(10,0,0);
   Set(spawner,"_phaseManager",phase); Set(spawner,"_cardPrefab",cardTemplate); Set(spawner,"_door",door);
   Set(spawner,"_day2Night",point2); Set(spawner,"_day4Night",point4); root.SetActive(true);
   phase.DayIndex=2; phase.EnterPhase(GamePhase.Night); phase.TimeCouldWork=false;
   yield return new WaitForSeconds(0.1f);
   var card=Get<AccessCard>(spawner,"_activeCard");
   Check(card!=null && card.transform.position==point2.position,"Branch "+pickup+": second-night card at configured point");
   Check(!door.IsAvailable,"Branch "+pickup+": card door ignores dates before collection");
   EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Night));
   Check(Get<AccessCard>(spawner,"_activeCard")==card,"Repeated phase event does not duplicate card");
   if(pickup!=2)
   {
    phase.EnterPhase(GamePhase.Day); phase.TimeCouldWork=false;
    yield return null;
    Check(card==null && Get<AccessCard>(spawner,"_activeCard")==null,"Branch "+pickup+": missed card removed at dawn");
    phase.DayIndex=3; phase.EnterPhase(GamePhase.Night); phase.TimeCouldWork=false;
    Check(Get<AccessCard>(spawner,"_activeCard")==null,"No third-night spawn");
    phase.EnterPhase(GamePhase.Day); phase.TimeCouldWork=false;
    phase.DayIndex=4; phase.EnterPhase(GamePhase.Night); phase.TimeCouldWork=false;
    yield return new WaitForSeconds(0.1f);
    card=Get<AccessCard>(spawner,"_activeCard");
    Check(card!=null && card.transform.position==point4.position,"Fourth night uses alternate spawn point");
   }
   if(pickup!=0)
   {
    Move(player,card.transform.position);
    yield return new WaitForSeconds(0.1f);
    Check(interaction.Candidates.Contains(card),"Branch "+pickup+": real physics trigger registers card");
    Check(card.InteractionPrompt=="获得门禁卡" && card.CanInteract(player.GetComponent<PlayerBasic>()),"Card exposes night pickup prompt");
    card.Interact(player.GetComponent<PlayerBasic>(),InteractKey.F);
    Check(!door.IsAvailable && card.CanInteract(player.GetComponent<PlayerBasic>()),"F cannot store card and E stays available");
    int reveals=0; Action<EventSecurityDoorAvailable> onReveal=e=>reveals++; EventPipeline.Subscribe(onReveal);
    card.Interact(player.GetComponent<PlayerBasic>(),InteractKey.E);
    Check(door.IsAvailable && Get<bool>(door,"_opening") && Get<bool>(door,"_isAnimating"),"Remote E pickup immediately starts opening target door");
    Check(!interaction.Candidates.Contains(card) && !card.gameObject.activeSelf,"Consumed card immediately unregisters and disappears");
    Check(!spawner.TryCollect(card),"Duplicate pickup cannot apply twice");
    Check(reveals==0,"Card unlock does not publish date reveal event"); EventPipeline.UnSubscribe(onReveal);
    yield return new WaitForSeconds(0.25f);
    Check(!Get<bool>(door,"_doorOpen"),"Remote door closes again when nobody is nearby");
    Move(player,door.transform.position);
    yield return new WaitForSeconds(0.15f);
    Check(Get<bool>(door,"_doorOpen") && !Get<Collider>(door,"_blockCollider").enabled,"Card door opens on nighttime approach");
    Move(player,new Vector3(-10,0,0));
    yield return new WaitForSeconds(0.15f);
    Check(!Get<bool>(door,"_doorOpen") && Get<Collider>(door,"_blockCollider").enabled,"Card door closes on departure");
    phase.EnterPhase(GamePhase.Day); phase.TimeCouldWork=false;
    Move(player,door.transform.position);
    yield return new WaitForSeconds(0.15f);
    Check(door.IsAvailable && Get<bool>(door,"_doorOpen"),"Card permission persists into daytime and opens on approach");
    Move(player,new Vector3(-10,0,0));
    phase.DayIndex=4; phase.EnterPhase(GamePhase.Night); phase.TimeCouldWork=false;
    Check(Get<AccessCard>(spawner,"_activeCard")==null,"Collected card never respawns on fourth night");
   }
   else
   {
    phase.EnterPhase(GamePhase.Day); phase.TimeCouldWork=false;
    yield return null;
    Check(card==null,"Missed fourth-night card removed at dawn");
    phase.DayIndex=5; phase.EnterPhase(GamePhase.Night); phase.TimeCouldWork=false;
    Check(Get<AccessCard>(spawner,"_activeCard")==null && !door.IsAvailable,"Missing both nights permanently misses card");
    EventPipeline.Publish(new EventNewDay(99));
    Check(!door.IsAvailable,"Later date cannot bypass card requirement");
   }
   Destroy(root); Destroy(template); Destroy(door.gameObject); Destroy(point2.gameObject); Destroy(point4.gameObject);
   yield return null;
  }
 }
}
