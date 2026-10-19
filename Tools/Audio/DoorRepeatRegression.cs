using System;
using System.Reflection;
using TapTapGameJam.Audio;
using TapTapGameJam.Events;
using UnityEngine;

// Runs the real SecurityDoor methods; only unrelated gameplay dependencies are stubbed.
public static class DoorRepeatRegression
{
    public static void Run(Action<bool, string> check)
    {
        var go = new GameObject("DoorRegression");
        go.SetActive(false);
        var animator = go.AddComponent<Animator>();
        animator.enabled = false; // Test the state machine without an animation controller.
        var block = go.AddComponent<BoxCollider>();
        var detect = go.AddComponent<BoxCollider>();
        detect.isTrigger = true;
        var door = go.AddComponent<SecurityDoor>();
        Field(door, "_animator", animator);
        Field(door, "_blockCollider", block);
        Field(door, "_detectCollider", detect);
        go.SetActive(true);
        door.enabled = false; // Explicitly advance the state machine, independently of frame timing.
        int requests = 0;
        int closeRequests = 0;
        Action<PlaySoundEvent> record = e => { if (e.Sound == SoundId.Door) requests++; if (e.Sound == SoundId.DoorClose) closeRequests++; };
        EventPipeline.Subscribe(record);
        try
        {
            Request(door, true);
            for (int i = 0; i < 60; i++) Request(door, true);
            check(requests == 1, "NPC requests during opening issue one door sound");
            Call(door, "FinishDoorAnimation");
            for (int i = 0; i < 60; i++) Request(door, true);
            check(requests == 1, "NPC staying beside an already-open door never replays its sound");
            Request(door, false);
            for (int i = 0; i < 60; i++) Request(door, false);
            check(closeRequests == 1, "Closing uses its own sound once despite repeated requests");
            Call(door, "FinishDoorAnimation");
            for (int i = 0; i < 60; i++) Request(door, false);
            check(closeRequests == 1, "An already-closed door never replays its closing sound");
            Request(door, true);
            check(requests == 2, "A genuinely new opening after closing still plays once");
            Call(door, "FinishDoorAnimation");
            Request(door, false);
            Request(door, true);
            check(requests == 3, "Reversing a closing door can play a new opening cue");
            check(closeRequests == 2, "A new closing transition plays one closing cue");
        }
        finally
        {
            EventPipeline.UnSubscribe(record);
            UnityEngine.Object.Destroy(go);
        }
    }
    private static void Request(SecurityDoor door, bool opening) =>
        typeof(SecurityDoor).GetMethod("StartDoorAnimation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(door, new object[] { opening });
    private static void Field(SecurityDoor door, string name, object value) =>
        typeof(SecurityDoor).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(door, value);
    private static void Call(SecurityDoor door, string name) =>
        typeof(SecurityDoor).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(door, null);
}
