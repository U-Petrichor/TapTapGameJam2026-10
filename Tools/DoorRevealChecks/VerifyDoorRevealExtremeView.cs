using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using Unity.Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyDoorRevealExtremeView
{
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    static async Task Wait(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > 20000) throw new Exception("Timed out waiting for reveal");
            await Task.Delay(20);
        }
    }
    public static async Task<object> Main()
    {
        if (!Application.isPlaying) throw new Exception("Start a fresh Play Mode session");
        var director = Object.FindFirstObjectByType<SecurityDoorRevealDirector>();
        var playerCamera = Object.FindFirstObjectByType<PlayerCameraManager>();
        var phase = Object.FindFirstObjectByType<GamePhaseManager>();
        var brain = Get<CinemachineBrain>(director, "_brain");
        var reveal = Get<CinemachineCamera>(director, "_revealCamera");
        var shown = Get<HashSet<SecurityDoor>>(director, "_shown");
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        try
        {
            Set(playerCamera, "_currentOrbitAngle", 90f);
            Set(playerCamera, "_targetOrbitAngle", 90f);
            Set(playerCamera, "_currentScale", 40f / Mathf.Sin(89f * Mathf.Deg2Rad) / Get<float>(playerCamera, "_initialDistance"));
            Set(playerCamera, "_targetScale", Get<float>(playerCamera, "_currentScale"));
            await Task.Delay(250);
            float zoom = Get<float>(playerCamera, "_currentScale");
            Vector3 beforePosition = brain.OutputCamera.transform.position;
            Quaternion beforeRotation = brain.OutputCamera.transform.rotation;
            Set(director, "_moveDuration", 0.25f);
            Set(director, "_holdDuration", 0.15f);
            Set(director, "_returnDuration", 0.25f);
            phase.DayIndex = 6;
            phase.EnterPhase(GamePhase.Day);
            await Wait(() => director.IsPlaying);
            int captured = 0;
            var shotChecks = new List<object>();
            while (director.IsPlaying)
            {
                if (shown.Count > captured)
                {
                    captured = shown.Count;
                    Vector3 pos = brain.OutputCamera.transform.position;
                    var door = shown.OrderBy(d => (d.RevealFocus - pos).sqrMagnitude).First();
                    float pitch = Mathf.Asin(-reveal.transform.forward.y) * Mathf.Rad2Deg;
                    if (pitch > 65.1f || pitch < 19.9f) throw new Exception("Door shot pitch out of bounds");
                    ScreenCapture.CaptureScreenshot("Temp/DoorReveal_Extreme_" + captured + ".png");
                    shotChecks.Add(new { count = captured, pitch });
                }
                await Task.Delay(20);
            }
            await Task.Delay(150);
            if (shown.Count != 10 || captured != 10) throw new Exception("Expected all 10 doors when jumping to day 7");
            if (Mathf.Abs(Get<float>(playerCamera, "_currentScale") - zoom) > 0.0001f) throw new Exception("Zoom was not preserved");
            if (Mathf.Abs(Get<float>(playerCamera, "_currentOrbitAngle") - 90f) > 0.0001f) throw new Exception("Orbit was not preserved");
            if (Vector3.Distance(brain.OutputCamera.transform.position, beforePosition) > 0.05f
                || Quaternion.Angle(brain.OutputCamera.transform.rotation, beforeRotation) > 0.1f)
                throw new Exception("Output did not return to the saved player view");
            return new { passed = true, shots = shotChecks, returnedToOriginalView = true, preservedZoom = zoom, preservedOrbit = 90 };
        }
        finally { Application.runInBackground = background; }
    }
}
