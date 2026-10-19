using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.NPC;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

// Run each branch in a fresh GameScene Play Mode session; never save runtime state.
public static class VerifyAccessCard
{
    static readonly List<string> Checks=new();
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static void Check(bool ok,string text) { if(!ok) throw new Exception(text); Checks.Add("PASS: "+text); }
    static GamePhaseManager phase;
    static AccessCardSpawner spawner;
    static PlayerBasic player;
    static Rigidbody body;
    static SecurityDoor door;
    static void Move(Vector3 position) { body.position=position; player.transform.position=position; Physics.SyncTransforms(); }
    static async Task Stage(int day,GamePhase stage)
    {
        phase.DayIndex=stage==GamePhase.Day ? day-1 : day;
        phase.EnterPhase(stage);
        phase.TimeCouldWork=false;
        await Task.Delay(stage==GamePhase.Night?2100:150);
        phase.TimeCouldWork=false;
        foreach(var npc in Object.FindObjectsByType<NPC>(FindObjectsSortMode.None)) npc.gameObject.SetActive(false);
    }
    public static async Task<object> Main(int pickupNight)
    {
        Check(EditorApplication.isPlaying,"Play Mode active");
        Application.runInBackground=true;
        phase=Object.FindFirstObjectByType<GamePhaseManager>();
        spawner=Object.FindFirstObjectByType<AccessCardSpawner>();
        player=Object.FindFirstObjectByType<PlayerBasic>();
        body=player.GetComponent<Rigidbody>();
        Object.FindFirstObjectByType<SecurityDoorRevealDirector>().enabled=false;
        var npcController=Object.FindFirstObjectByType<DayTimeNpcController>();
        if(npcController!=null) npcController.enabled=false;
        player.GetComponent<PlayerMovement>().enabled=false;
        body.isKinematic=true;
        door=Object.FindObjectsByType<SecurityDoor>(FindObjectsSortMode.None).Single(d=>d.name=="SecurityDoor (5)");
        var normal=Object.FindObjectsByType<SecurityDoor>(FindObjectsSortMode.None).Single(d=>d.name=="SecurityDoor (10)");
        var interaction=player.GetComponent<PlayerInteractionManager>();
        Check(!typeof(Interactable).IsAssignableFrom(typeof(SecurityDoor)),"Doors have no Interactable inheritance");
        try
        {
            await Stage(2,GamePhase.Day);
            Move(Field<Collider>(normal,"_detectCollider").bounds.center);
            await Task.Delay(600);
            Check(!normal.IsAvailable && Field<Collider>(normal,"_blockCollider").enabled,"Locked normal door blocks nearby player");
            EventPipeline.Publish(new EventNewDay(3));
            await Task.Delay(650);
            Check(!Field<Collider>(normal,"_blockCollider").enabled,"Date unlock opens for player already nearby");
            Check(Field<Collider>(normal,"_detectCollider").enabled,"Open door keeps proximity detection active");
            Move(new Vector3(-100,1,-100));
            await Task.Delay(650);
            Check(!Field<bool>(normal,"_doorOpen") && Field<Collider>(normal,"_blockCollider").enabled,"Normal door closes after player leaves");
            await Stage(2,GamePhase.Night);
            var card=Field<AccessCard>(spawner,"_activeCard");
            Check(card!=null,"Second night spawns card");
            Check(Vector3.Distance(card.transform.position,Field<Transform>(spawner,"_day2Night").position)<0.001f,"Second-night spawn uses configured world point");
            Check(card.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text=="获得门禁卡"),"Card bubble displays 获得门禁卡");
            Check(!card.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Trim()=="F"),"No bag pickup hint on card");
            Check(!door.IsAvailable,"Card door remains locked before pickup");
            if(pickupNight!=2)
            {
                await Stage(3,GamePhase.Day);
                Check(card==null && Field<AccessCard>(spawner,"_activeCard")==null,"Uncollected card disappears at dawn");
                await Stage(3,GamePhase.Night);
                Check(Field<AccessCard>(spawner,"_activeCard")==null,"No card on third night");
                await Stage(4,GamePhase.Day);
                await Stage(4,GamePhase.Night);
                card=Field<AccessCard>(spawner,"_activeCard");
                Check(card!=null,"Fourth night offers missed card again");
                Check(Vector3.Distance(card.transform.position,Field<Transform>(spawner,"_day4Night").position)<0.001f,"Fourth-night spawn uses alternate point");
            }
            if(pickupNight!=0)
            {
                Move(card.transform.position);
                await Task.Delay(180);
                Check(interaction.Candidates.Contains(card),"Real player trigger registers card");
                Check(interaction.SelectInteractionTarget(card),"Card receives real interaction focus");
                interaction.HandleInteract(InteractKey.F);
                Check(!door.IsAvailable && card.gameObject.activeSelf,"F cannot store or consume card");
                Check(card.CanInteract(player),"E remains available after unsupported F");
                interaction.HandleInteract(InteractKey.E);
                Check(door.IsAvailable,"E immediately grants door permission");
                Check(!interaction.Candidates.Contains(card),"Consumed card unregisters immediately");
                Check(!card.gameObject.activeSelf && Field<AccessCard>(spawner,"_activeCard")==null,"Card consumed without inventory storage");
                Check(Vector3.Distance(player.transform.position,door.transform.position)>5f,"Pickup fixture is far from target door");
                Check(Field<bool>(door,"_isAnimating") && Field<bool>(door,"_opening"),"Remote pickup immediately starts target door opening");
                Move(Field<Collider>(door,"_detectCollider").bounds.center);
                await Task.Delay(650);
                Check(Field<bool>(door,"_doorOpen") && !Field<Collider>(door,"_blockCollider").enabled,"Card door is passable while player nearby at night");
                Move(new Vector3(-100,1,-100));
                await Task.Delay(650);
                Check(!Field<bool>(door,"_doorOpen") && Field<Collider>(door,"_blockCollider").enabled,"Card door closes after player leaves");
                await Stage(pickupNight+1,GamePhase.Day);
                Move(Field<Collider>(door,"_detectCollider").bounds.center);
                await Task.Delay(650);
                Check(door.IsAvailable && !Field<Collider>(door,"_blockCollider").enabled,"Card access persists and proximity opens door in daytime");
                Move(new Vector3(-100,1,-100));
                await Stage(4,GamePhase.Night);
                Check(Field<AccessCard>(spawner,"_activeCard")==null,"Collected card never respawns on fourth night");
                Check(!Object.FindFirstObjectByType<SecurityDoorRevealDirector>().IsPlaying,"Card unlock never starts reveal camera");
            }
            else
            {
                await Stage(5,GamePhase.Day);
                Check(card==null,"Missed fourth-night card disappears at dawn");
                await Stage(5,GamePhase.Night);
                Check(Field<AccessCard>(spawner,"_activeCard")==null && !door.IsAvailable,"Missing both nights permanently misses access");
                await Stage(7,GamePhase.Day);
                Check(!door.IsAvailable,"Later date unlocks cannot bypass card requirement");
            }
            return new {passed=true,branch=pickupNight,checks=Checks.ToArray()};
        }
        finally { System.IO.File.WriteAllText("Temp/AccessCardVerification-"+pickupNight+".txt",string.Join("\n",Checks)); }
    }
}
