using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TapTapGameJam.Audio;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AudioSmokeRunner : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        Application.runInBackground = true;
        new GameObject("AudioSmokeRunner").AddComponent<AudioSmokeRunner>();
    }
    private SoundManager manager;
    private readonly List<string> passed = new();
    private readonly List<SoundId> published = new();
    private AudioSource[] Playing(SoundId id) => manager.GetComponentsInChildren<AudioSource>()
        .Where(s => s.clip != null && Resources.Load<SoundLibrary>("Audio/SoundLibrary").Find(id).clips.Contains(s.clip)).ToArray();
    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        passed.Add(label);
    }
    private void Record(PlaySoundEvent e) => published.Add(e.Sound);
    private IEnumerator Start()
    {
        var test = Run();
        while (true)
        {
            object current;
            try
            {
                if (!test.MoveNext()) break;
                current = test.Current;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Application.dataPath, "../smoke-result.txt"), "FAIL: " + error + "\nPassed:\n" + string.Join("\n", passed));
                UnityEditor.EditorApplication.Exit(1);
                yield break;
            }
            yield return current;
        }
        File.WriteAllText(Path.Combine(Application.dataPath, "../smoke-result.txt"), "PASS\n" + string.Join("\n", passed));
        UnityEditor.EditorApplication.Exit(0);
    }
    private IEnumerator Run()
    {
        manager = SoundManager.Instance;
        var library = Resources.Load<SoundLibrary>("Audio/SoundLibrary");
        Check(manager != null && library != null, "Runtime bootstrap and Resources library");
        Check(library.sounds.Length == 44 && library.sounds.All(s => s.clips.Length > 0 && s.clips.All(c => c != null)), "44 definitions resolve all clip references");
        Check(!library.Find(SoundId.CoffeeMachine).enabled && !library.Find(SoundId.PrinterQte).enabled, "S11/S15 disabled");
        Check(library.Find(SoundId.Click).clips.Length == 3 && library.Find(SoundId.Click).clips.All(c => c.length < 0.3f),
            "Click variants contain individual clicks rather than a three-click recording");
        Check(library.Find(SoundId.Panel).clips.Length == 3 && library.Find(SoundId.Panel).clips.All(c => c.length < 2f),
            "Panel variants contain individual page turns rather than a three-take recording");
        Check(library.Find(SoundId.HumanFootstep).clips.All(c => c.length < 0.5f) &&
            library.Find(SoundId.GuardFootstep).clips.All(c => c.length < 0.6f),
            "Animation footstep events use individual steps rather than walking recordings");
        foreach (var definition in library.sounds) { definition.cooldown = 0; definition.fadeOut = 0; }
        library.voiceLimit = 2;
        DoorRepeatRegression.Run(Check);
        manager.enabled = false; manager.enabled = true;
        Sfx.Play(SoundId.Panel);
        Check(Playing(SoundId.Panel).Length == 1 && !Playing(SoundId.Panel)[0].loop,
            "One page-turn request creates exactly one non-looping sample");
        yield return new WaitForSecondsRealtime(2.2f);
        Check(Playing(SoundId.Panel).Length == 0, "One page turn finishes without later recorded takes or replay");
        var player = new GameObject("Player").AddComponent<TapTapGameJam.Player.PlayerBasic>();
        var camera = new GameObject("MainCamera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.gameObject.AddComponent<AudioListener>();
        yield return new WaitForSecondsRealtime(0.6f);
        var a = new GameObject("A").AddComponent<SoundEmitter>();
        var b = new GameObject("B").AddComponent<SoundEmitter>();
        a.SetPlaying(SoundId.Printer, true);
        b.SetPlaying(SoundId.Printer, true);
        a.SetPlaying(SoundId.Printer, true);
        Check(Playing(SoundId.Printer).Length == 2, "Independent owners and duplicate start idempotence");
        a.Stop(SoundId.Printer);
        Check(Playing(SoundId.Printer).Length == 1, "Stopping A leaves B playing");
        b.gameObject.SetActive(false);
        Check(Playing(SoundId.Printer).Length == 0, "Disabled owner releases its loop");
        b.gameObject.SetActive(true);
        var old = SoundPlaybackId.New();
        EventPipeline.Publish(new PlaySoundEvent(SoundId.Printer, SoundBus.MiniGame, a, playback: old, controlled: true));
        EventPipeline.Publish(new StopSoundEvent(old));
        var next = SoundPlaybackId.New();
        EventPipeline.Publish(new PlaySoundEvent(SoundId.Printer, SoundBus.MiniGame, a, playback: next, controlled: true));
        EventPipeline.Publish(new StopSoundEvent(old));
        Check(manager.IsPlaying(next), "Stale stop cannot stop a new playback");
        EventPipeline.Publish(new StopSoundEvent(next));
        a.transform.position = new Vector3(5, 0, 0);
        a.SetPlaying(SoundId.Spray, true, SoundBus.World, true);
        yield return null;
        var spatial = Playing(SoundId.Spray).Single();
        float gain = spatial.volume;
        Check(spatial.panStereo > 0.9f && gain > 0, "Right-hand source pans right");
        camera.transform.rotation = Quaternion.Euler(0, 180, 0);
        yield return null;
        Check(spatial.panStereo < -0.9f, "Camera rotation updates stereo direction");
        camera.transform.position = new Vector3(0, 100, -100);
        yield return null;
        Check(Mathf.Abs(spatial.volume - gain) < 0.001f, "Camera zoom does not change distance gain");
        player.transform.position = new Vector3(100, 0, 0);
        yield return null;
        Check(spatial.volume == 0, "Beyond maximum player distance is silent");
        player.transform.position = Vector3.zero;
        b.SetPlaying(SoundId.Printer, true);
        Time.timeScale = 0;
        yield return null;
        Check(!spatial.isPlaying && Playing(SoundId.Printer).Single().isPlaying, "World pauses while minigame loop continues");
        Time.timeScale = 1;
        yield return null;
        Check(spatial.isPlaying, "World loop resumes after pause");
        a.StopAll(); b.StopAll();
        Time.timeScale = 0;
        a.SetPlaying(SoundId.Spray, true, SoundBus.World, true);
        yield return null;
        Check(Playing(SoundId.Spray).Length == 1 && !Playing(SoundId.Spray).Single().isPlaying, "Loop requested during pause remains resumable");
        Time.timeScale = 1;
        yield return null;
        Check(Playing(SoundId.Spray).Single().isPlaying, "Paused start resumes correctly");
        a.StopAll();
        a.SetPlaying(SoundId.Printer, true); b.SetPlaying(SoundId.Spray, true);
        Sfx.Play(SoundId.Defeat);
        Check(Playing(SoundId.Defeat).Length == 1 && manager.MusicGain < manager.MasterVolume * manager.MusicVolume, "Defeat preempts full pool and ducks music");
        manager.enabled = false; manager.enabled = true;
        a.StopAll(); b.StopAll();
        EventPipeline.Subscribe<PlaySoundEvent>(Record);
        var button = new GameObject("Button", typeof(RectTransform)).AddComponent<Button>();
        button.gameObject.AddComponent<SoundButton>();
        button.onClick.AddListener(() => Sfx.Play(SoundId.Coin));
        published.Clear(); button.onClick.Invoke();
        yield return null; yield return null;
        Check(published.Contains(SoundId.Coin) && !published.Contains(SoundId.Click), "Specific UI feedback suppresses generic click");
        button.onClick.RemoveAllListeners();
        // Recreate a component so its generic callback is registered again.
        Destroy(button.gameObject);
        var ordinary = new GameObject("OrdinaryButton", typeof(RectTransform)).AddComponent<Button>();
        ordinary.gameObject.AddComponent<SoundButton>();
        published.Clear(); ordinary.onClick.Invoke();
        yield return null; yield return null;
        Check(published.Contains(SoundId.Click), "Ordinary button gets click feedback");
        published.Clear();
        EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
        Check(!published.Contains(SoundId.NightEnd), "First day does not play night settlement");
        EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Shopping));
        Check(published.Count(s => s == SoundId.DayEnd) == 1, "Day end cue once on actual transition");
        EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Night));
        EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Day));
        Check(published.Count(s => s == SoundId.NightEnd) == 1, "Night end cue once on night-to-day");
        EventPipeline.UnSubscribe<PlaySoundEvent>(Record);
        manager.enabled = false; manager.enabled = true;
        a.SetPlaying(SoundId.Printer, true);
        var previous = SceneManager.GetActiveScene();
        var menu = SceneManager.CreateScene("MainMenu");
        SceneManager.SetActiveScene(menu);
        DontDestroyOnLoad(gameObject);
        yield return SceneManager.UnloadSceneAsync(previous);
        Check(manager.GetComponentsInChildren<AudioSource>().All(s => s.clip == null), "Scene unload clears old playback instances");
        Check(FindObjectsByType<SoundManager>(FindObjectsSortMode.None).Length == 1, "Single persistent manager");
    }
}
