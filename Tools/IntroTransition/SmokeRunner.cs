using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TapTapGameJam.Core;
using TapTapGameJam.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class IntroSmokeRunner : MonoBehaviour
{
    private readonly List<string> passed = new List<string>();
    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        passed.Add(label);
        Debug.Log("PASS: " + label);
    }
    private static void Set(object obj, string field, object value) => obj.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        var routine = Run();
        while (true)
        {
            object value;
            try
            {
                if (!routine.MoveNext()) break;
                value = routine.Current;
            }
            catch (Exception exception)
            {
                File.WriteAllText("smoke-result.txt", "FAIL: " + exception + "\n" + string.Join("\n", passed));
                UnityEditor.EditorApplication.Exit(1);
                yield break;
            }
            yield return value;
        }
        File.WriteAllText("smoke-result.txt", "PASS\n" + string.Join("\n", passed));
        UnityEditor.EditorApplication.Exit(0);
    }

    private IntroVideoTransition Create(out VideoPlayer player, out CanvasGroup group)
    {
        var root = new GameObject("VideoCanvas", typeof(RectTransform), typeof(Canvas));
        var image = new GameObject("VideoImage", typeof(RectTransform), typeof(RawImage), typeof(CanvasGroup));
        image.transform.SetParent(root.transform, false);
        group = image.GetComponent<CanvasGroup>();
        player = image.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.renderMode = VideoRenderMode.RenderTexture;
        var target = new RenderTexture(320, 180, 0);
        player.targetTexture = target;
        image.GetComponent<RawImage>().texture = target;
        var controller = root.AddComponent<IntroVideoTransition>();
        Set(controller, "videoPlayer", player);
        Set(controller, "videoGroup", group);
        return controller;
    }

    private IEnumerator Run()
    {
        var curve = AnimationCurve.Linear(0, 0, 1, 1);
        Check(IntroVideoTransition.EvaluateAlpha(26, 33, 27, curve) == 1, "Opaque before 27 seconds");
        Check(IntroVideoTransition.EvaluateAlpha(27, 33, 27, curve) == 1, "Opaque at fade start");
        Check(Mathf.Abs(IntroVideoTransition.EvaluateAlpha(30, 33, 27, curve) - .5f) < .001f,
            "Half alpha halfway between 27 seconds and actual clip end");
        Check(IntroVideoTransition.EvaluateAlpha(33, 33, 27, curve) == 0, "Exactly transparent at end");
        Check(IntroVideoTransition.EvaluateAlpha(10, 20, 27, curve) == 1 &&
              IntroVideoTransition.EvaluateAlpha(20, 20, 27, curve) == 0,
            "Short clips have safe endpoint behavior");
        var invalid = Create(out var unused, out var unusedGroup);
        Set(invalid, "gameSceneName", "MissingScene");
        Check(!invalid.Begin() && !IntroVideoTransition.IsTransitioning && Time.timeScale == 1,
            "Invalid destination leaves input and time unchanged");
        Destroy(invalid.gameObject);
        yield return null;

        var intro = Create(out var player, out var group);
        player.clip = Resources.Load<VideoClip>("Intro");
        Check(player.clip != null && player.clip.length > 27, "Actual project video imports and exceeds 27 seconds");
        Debug.Log("Actual video duration: " + player.clip.length);
        var menuEvents = FindFirstObjectByType<EventSystem>();
        Check(intro.Begin(), "Begin accepts configured destination");
        Check(!intro.Begin(), "Duplicate start is rejected");
        Check(IntroVideoTransition.IsTransitioning && Time.timeScale == 0 && !menuEvents.enabled,
            "Intro immediately locks simulation and menu input");

        float deadline = Time.realtimeSinceStartup + 45;
        while (!player.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
        Check(player.isPlaying, "Video prepares and plays while timeScale is zero");
        double before = player.time;
        float waitUntil = Time.realtimeSinceStartup + .4f;
        while (Time.realtimeSinceStartup < waitUntil) yield return null;
        Check(player.time > before && group.alpha == 1 && SceneManager.GetActiveScene().name == "Menu",
            "Unscaled playback advances; preloaded game stays inactive before fade");

        player.time = 27.1;
        deadline = Time.realtimeSinceStartup + 30;
        while (SceneManager.GetActiveScene().name != "GameScene" && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check(SceneManager.GetActiveScene().name == "GameScene" && intro != null,
            "Game activates during fade while video canvas survives the scene change");
        // Let Start execute on the newly activated scene, including pause refresh.
        yield return null;
        Check(Time.timeScale == 0 && GamePauseManager.Instance.IsPaused &&
              !FindFirstObjectByType<EventSystem>().enabled,
            "Game Start cannot unpause or enable UI input during intro");
        double midpoint = (27 + player.length) / 2;
        player.time = midpoint;
        deadline = Time.realtimeSinceStartup + 8;
        while ((Math.Abs(player.time - midpoint) > .4 || group.alpha >= .8f) &&
               Time.realtimeSinceStartup < deadline) yield return null;
        Check(group.alpha > 0 && group.alpha < .8f, "Actual video time drives visible partial transparency");

        player.time = player.length - .6;
        deadline = Time.realtimeSinceStartup + 15;
        while (IntroVideoTransition.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!IntroVideoTransition.IsTransitioning && Time.timeScale == 1 &&
              FindFirstObjectByType<EventSystem>().enabled,
            "Natural video end releases simulation and input");
        yield return null;
        Check(intro == null, "Finished persistent video layer is destroyed");

        intro = Create(out player, out group);
        Check(intro.Begin(), "Missing video still starts scene preload");
        deadline = Time.realtimeSinceStartup + 15;
        while (IntroVideoTransition.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!IntroVideoTransition.IsTransitioning && Time.timeScale == 1,
            "Missing video falls through to game without a stuck pause");
        yield return null;

        intro = Create(out player, out group);
        player.source = VideoSource.Url;
        player.url = "file:///Z:/missing-intro-video.mp4";
        Check(intro.Begin(), "Decoder-error path starts");
        deadline = Time.realtimeSinceStartup + 25;
        while (IntroVideoTransition.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!IntroVideoTransition.IsTransitioning && Time.timeScale == 1,
            "Decoder error falls through to game without a stuck pause");
    }
}
