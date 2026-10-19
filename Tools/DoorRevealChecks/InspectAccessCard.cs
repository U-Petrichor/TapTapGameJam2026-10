using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TapTapGameJam.Core;
using TapTapGameJam.Interactables;
using TapTapGameJam.UI;
using TMPro;
public static class InspectAccessCard
{
    public static object Main()
    {
        var root=GameObject.Find("CardSpawnPoint");
        var spawner=root.GetComponent<AccessCardSpawner>();
        var so=new SerializedObject(spawner);
        var door=(SecurityDoor)so.FindProperty("_door").objectReferenceValue;
        var card=(AccessCard)so.FindProperty("_cardPrefab").objectReferenceValue;
        var bubble=card.GetComponentInChildren<BubbleHint>(true);
        var texts=card.GetComponentsInChildren<TMP_Text>(true);
        bool label=texts.Any(t=>t.text=="获得门禁卡");
        bool noF=!card.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Trim()=="F");
        bool bubbleLink=new SerializedObject(bubble).FindProperty("_interactable").objectReferenceValue==card;
        bool points=so.FindProperty("_day2Night").objectReferenceValue==root.transform.Find("Day2Night") && so.FindProperty("_day4Night").objectReferenceValue==root.transform.Find("Day4Night");
        bool cardMode=new SerializedObject(door).FindProperty("_requiresAccessCard").boolValue;
        if(!label||!noF||!bubbleLink||!points||!cardMode||door.name!="SecurityDoor (5)"||typeof(Interactable).IsAssignableFrom(typeof(SecurityDoor))) throw new Exception("Configuration invalid");
        var text=texts.Single(t=>t.text=="获得门禁卡");
        text.ForceMeshUpdate(true);
        return new {passed=true,label=text.text,noF,bubbleLink,points,cardMode,trigger=card.GetComponent<Collider>().isTrigger,fontSize=text.fontSize,width=text.rectTransform.rect.width,preferredWidth=text.preferredWidth,scene=spawner.gameObject.scene.path};
    }
}
