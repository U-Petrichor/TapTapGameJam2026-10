using UnityEngine;
using UnityEditor;
using TapTapGameJam.UI;
using TapTapGameJam.Player;
using TapTapGameJam.Interactables;
using System.Linq;
internal class CommandScript : IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  if(!Application.isPlaying)throw new System.Exception("Requires Play mode");
  var ui=Object.FindFirstObjectByType<TaskInteractionPromptUI>(FindObjectsInactive.Include);
  var data=new SerializedObject(ui);
  var manager=data.FindProperty("interactionManager").objectReferenceValue as PlayerInteractionManager;
  var image=data.FindProperty("taskPromptImage").objectReferenceValue as UnityEngine.UI.Image;
  var key=data.FindProperty("interactionKeyHint").objectReferenceValue as GameObject;
  if(!manager||!image||!key)throw new System.Exception("Missing live HUD references");
  if(manager.Candidates.Count!=0)throw new System.Exception("Player is near an interactable; move to an empty area before this check.");
  var targets=Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include,FindObjectsSortMode.None)
   .Where(t=>t.InteractionPromptSprite!=null).GroupBy(t=>t.InteractionPromptSprite).Select(g=>g.First()).ToArray();
  if(targets.Length!=7)throw new System.Exception("Expected seven artwork variants, got "+targets.Length);
  var group=ui.GetComponent<CanvasGroup>();
  foreach(var target in targets)
  {
   try {
    // Exercise the actual manager -> focus event -> subscribed HUD path.
    manager.Register(target);
    if(manager.CurrentTarget!=target||image.sprite!=target.InteractionPromptSprite||!image.gameObject.activeInHierarchy||!key.activeInHierarchy||group.alpha!=1f)
     throw new System.Exception("Live focus presentation failed: "+target.GetType().Name);
    result.Log("PASS live focus: "+target.GetType().Name+" -> "+image.sprite.name+"; image and E visible, legacy panel hidden.");
   } finally { manager.Unregister(target); }
   if(group.alpha!=0||image.gameObject.activeSelf||key.activeSelf)throw new System.Exception("Live exit cleanup failed");
  }
  foreach(var ordinary in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.InteractionPromptSprite==null))
  {
   try {
    manager.Register(ordinary);
    if(group.alpha!=0 || image.gameObject.activeSelf || key.activeSelf)
     throw new System.Exception("Ordinary item showed task HUD: "+ordinary.GetType().Name);
   } finally { manager.Unregister(ordinary); }
  }
  result.Log("PASS ordinary interactions keep task HUD hidden; no legacy fallback.");
  var other=Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.InteractionPromptSprite==null);
  try {
   manager.Register(targets[0]);manager.Register(other);
   manager.SelectInteractionTarget(targets[0]);
   if(group.alpha!=1)throw new System.Exception("Task focus did not show artwork");
   manager.SelectInteractionTarget(other);
   if(group.alpha!=0||image.gameObject.activeSelf||key.activeSelf)
    throw new System.Exception("Switching from task to ordinary interaction left HUD visible");
   manager.SelectInteractionTarget(targets[0]);
   if(group.alpha!=1)throw new System.Exception("Switching back to task did not restore artwork");
  } finally {manager.Unregister(targets[0]);manager.Unregister(other);}
  if(ui.transform.Find("Background")||ui.transform.Find("Text (TMP)"))throw new System.Exception("Legacy UI nodes still exist");
  result.Log("PASS task -> ordinary -> task focus switching and legacy nodes removed.");
  result.Log("PASS all seven sprites via live manager events, including leaving interaction range.");
 }
}
