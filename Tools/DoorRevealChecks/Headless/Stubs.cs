using UnityEngine;
using System.Collections.Generic;
namespace TapTapGameJam.Types { public enum GamePhase { Day, Shopping, Night } }
namespace TapTapGameJam.Events {
 public interface IEvent {}
 public readonly struct EventNewDay:IEvent { public int DayCount{get;} public EventNewDay(int day)=>DayCount=day; }
 public readonly struct EventSecurityDoorAvailable:IEvent { public SecurityDoor Door{get;} public EventSecurityDoorAvailable(SecurityDoor door)=>Door=door; }
 public readonly struct ShoppingFinishedEvent:IEvent {}
 public readonly struct NightTransitionCompletedEvent:IEvent {}
 public readonly struct GamePhaseChangedEvent:IEvent { public TapTapGameJam.Types.GamePhase NewPhase{get;} public GamePhaseChangedEvent(TapTapGameJam.Types.GamePhase phase)=>NewPhase=phase; }
}
namespace TapTapGameJam.Core {
 public static class GameManager { public static GamePhaseManager Phase; public static bool IsNight=>Phase!=null && Phase.CurrentPhase==TapTapGameJam.Types.GamePhase.Night; }
}
namespace TapTapGameJam.Player {
 public class PlayerBasic:MonoBehaviour {}
 public class PlayerInteractionManager:MonoBehaviour {
  public HashSet<TapTapGameJam.Interactables.Interactable> Candidates=new();
  public void Register(TapTapGameJam.Interactables.Interactable item)=>Candidates.Add(item);
  public void Unregister(TapTapGameJam.Interactables.Interactable item)=>Candidates.Remove(item);
 }
}
namespace TapTapGameJam.NPC { public class NPC:MonoBehaviour {} }
namespace TapTapGameJam.Audio {
 public enum SoundId { Door,DoorClose,Pickup }
 public enum SoundBus { Interface }
 public readonly struct PlaySoundEvent:TapTapGameJam.Events.IEvent { public SoundId Sound{get;} public PlaySoundEvent(SoundId sound)=>Sound=sound; }
 public static class Sfx {
  public static void At(SoundId id,Vector3 pos,Object owner,SoundBus bus=SoundBus.Interface)=>TapTapGameJam.Events.EventPipeline.Publish(new PlaySoundEvent(id));
 }
}
