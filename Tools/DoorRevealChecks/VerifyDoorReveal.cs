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
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public static class VerifyDoorReveal
{
    static readonly List<string> Results = new();
    static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Results.Add(message);
    }
    static async Task Wait(Func<bool> condition, string message, int milliseconds = 18000)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > milliseconds) throw new Exception("Timeout: " + message);
            await Task.Delay(20);
        }
    }

    public static async Task<object> Main()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play Mode with GameScene loaded.");
        Results.Clear();
        var director = Object.FindFirstObjectByType<SecurityDoorRevealDirector>();
        var phase = Object.FindFirstObjectByType<GamePhaseManager>();
        var pause = Object.FindFirstObjectByType<GamePauseManager>();
        var doors = Object.FindObjectsByType<SecurityDoor>(FindObjectsSortMode.None);
        var input = Field<PlayerInput>(director, "_playerInput");
        var brain = Field<CinemachineBrain>(director, "_brain");
        var reveal = Field<CinemachineCamera>(director, "_revealCamera");
        bool originalBackground = Application.runInBackground;
        Application.runInBackground = true;
        try
        {
            Check(phase.DayIndex == 1, "Startup is day 1");
            Check(doors.Length == 12, "GameScene contains 12 security doors");
            Check(doors.All(d => !d.IsAvailable), "Doors remain locked before their dates");
            Check(!reveal.enabled && !director.IsPlaying, "Reveal camera is idle before an unlock");

            int expectedShown = 0;
            foreach (int day in new[] { 3, 5, 7 })
            {
                phase.DayIndex = day - 1;
                phase.EnterPhase(GamePhase.Day);
                var expected = doors.Where(d => Field<int>(d, "_whichDayOpen") >= 0
                    && Field<int>(d, "_whichDayOpen") <= day).ToArray();
                Check(doors.Count(d => d.IsAvailable) == expected.Length, "Correct door permissions on day " + day);
                await Wait(() => director.IsPlaying, "Reveal starts on day " + day);
                float remaining = phase.RemainingTime;
                Vector3 playerPosition = input.transform.position;
                var originalCamera = brain.ActiveVirtualCamera;
                Check(Time.timeScale == 0f && pause.IsPaused && !input.inputIsActive,
                    "World and input pause during day " + day + " reveal");
                await Task.Delay(1450);
                Check(Mathf.Abs(remaining - phase.RemainingTime) < 0.001f, "Countdown is frozen on day " + day);
                Check((input.transform.position - playerPosition).sqrMagnitude < 0.0001f, "Player stays still on day " + day);
                ScreenCapture.CaptureScreenshot("Temp/DoorReveal_Day" + day + ".png");
                await Wait(() => !director.IsPlaying, "Reveal finishes on day " + day);
                expectedShown = expected.Length;
                Check(Field<HashSet<SecurityDoor>>(director, "_shown").Count == expectedShown,
                    "Every newly unlocked door shown exactly once through day " + day);
                Check(!reveal.enabled && input.inputIsActive && !pause.IsPaused && Time.timeScale == 1f,
                    "Camera, input and time restored after day " + day);
                EventPipeline.Publish(new EventNewDay(day));
                await Task.Delay(80);
                Check(!director.IsPlaying, "Repeated day event does not replay day " + day);
            }

            // 用 Play Mode 临时复制的一扇门验证中断，退出 Play Mode 后副本自动丢弃。
            var template = doors.First(d => Field<int>(d, "_whichDayOpen") < 0);
            var extra = Object.Instantiate(template);
            extra.GetType().GetField("_whichDayOpen", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(extra, 9);
            phase.DayIndex = 8;
            phase.EnterPhase(GamePhase.Day);
            await Wait(() => director.IsPlaying, "Interruption fixture starts");
            EventPipeline.Publish(new ESCMenuEvent(true));
            director.enabled = false;
            Check(input.inputIsActive && !reveal.enabled && !director.IsPlaying, "Disabling director releases camera and input");
            Check(pause.IsPaused && Time.timeScale == 0f, "Interruption preserves another pause reason");
            EventPipeline.Publish(new ESCMenuEvent(false));
            Check(!pause.IsPaused && Time.timeScale == 1f, "Remaining pause reason can be released");
            return new { passed = true, checks = Results.ToArray() };
        }
        finally
        {
            Application.runInBackground = originalBackground;
            System.IO.File.WriteAllText("Temp/DoorRevealVerification.txt", string.Join("\n", Results));
        }
    }
}
