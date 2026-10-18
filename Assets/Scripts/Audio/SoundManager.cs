using System.Collections.Generic;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TapTapGameJam.Audio
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SoundManager : MonoBehaviour
    {
        private sealed class Voice
        {
            public AudioSource source;
            public SoundDefinition definition;
            public PlaySoundEvent request;
            public bool active, paused, hadFollow, fading;
            public float elapsed, fadeRemaining, baseVolume;
            public int scene;
            public int priority;
        }
        public static SoundManager Instance { get; private set; }
        [SerializeField] private SoundLibrary library;
        private readonly List<Voice> voices = new();
        private readonly Dictionary<(SoundId, int), float> lastPlayed = new();
        private readonly Dictionary<SoundId, int> lastVariant = new();
        private readonly HashSet<SoundId> warned = new();
        private Transform listenerTarget;
        private Camera view;
        private float nextDiscovery;
        private int specificFeedbackFrame = -1;
        private GamePhase previousPhase;
        private bool hasPhase;
        private float master = 1f, music = 1f, effects = 1f;

        public float MasterVolume { get => master; set => master = Mathf.Clamp01(value); }
        public float MusicVolume { get => music; set => music = Mathf.Clamp01(value); }
        public float EffectsVolume { get => effects; set => effects = Mathf.Clamp01(value); }
        public float MusicGain => master * music * DuckGain;
        public bool HasSpecificFeedbackThisFrame => specificFeedbackFrame == Time.frameCount;
        public bool IsPlaying(SoundPlaybackId id)
        {
            foreach (var voice in voices)
                if (voice.active && voice.request.Playback.Value == id.Value) return id.IsValid;
            return false;
        }
        private bool SupportedScene => IsSupported(SceneManager.GetActiveScene().name);
        private static bool IsSupported(string name) => name == "MainMenu" || name == "GameScene";
        private float DuckGain
        {
            get
            {
                foreach (var voice in voices)
                    if (voice.active && voice.definition.id == SoundId.Defeat)
                        return library != null ? library.defeatDuckVolume : 0.25f;
                return 1f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null) new GameObject("SoundManager").AddComponent<SoundManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (library == null) library = Resources.Load<SoundLibrary>("Audio/SoundLibrary");
            if (library == null) Debug.LogError("[Audio] Missing Resources/Audio/SoundLibrary.", this);
            else { master = library.masterVolume; music = library.musicVolume; effects = library.effectsVolume; }
            music = Mathf.Clamp01(PlayerPrefs.GetFloat("Settings.Music", music));
            effects = Mathf.Clamp01(PlayerPrefs.GetFloat("Settings.Effects", effects));
        }
        private void OnEnable()
        {
            if (Instance != this) return;
            EventPipeline.Subscribe<PlaySoundEvent>(Play);
            EventPipeline.Subscribe<StopSoundEvent>(Stop);
            EventPipeline.Subscribe<GamePhaseChangedEvent>(PhaseChanged);
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<PlaySoundEvent>(Play);
            EventPipeline.UnSubscribe<StopSoundEvent>(Stop);
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(PhaseChanged);
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            foreach (var voice in voices) Release(voice);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            view = null; listenerTarget = null; nextDiscovery = 0f;
            if (mode == LoadSceneMode.Single) { hasPhase = false; lastPlayed.Clear(); }
            Discover();
        }
        private void SceneUnloaded(Scene scene)
        {
            foreach (var voice in voices)
                if (voice.active && voice.scene == scene.handle)
                {
                    if (voice.definition.id == SoundId.Click) voice.scene = -1;
                    else Release(voice);
                }
        }
        private void PhaseChanged(GamePhaseChangedEvent evt)
        {
            // Stop controlled old-phase sounds before playing the transition cue.
            foreach (var voice in voices)
                if (voice.active && voice.request.Controlled) Release(voice);
            if (hasPhase && previousPhase == GamePhase.Day && evt.NewPhase == GamePhase.Shopping)
                Sfx.Play(SoundId.DayEnd);
            if (hasPhase && previousPhase == GamePhase.Night && evt.NewPhase == GamePhase.Day)
                Sfx.Play(SoundId.NightEnd);
            previousPhase = evt.NewPhase; hasPhase = true;
        }
        private void Discover()
        {
            if (!SupportedScene) return;
            view = Camera.main;
            var player = FindFirstObjectByType<PlayerBasic>();
            listenerTarget = player != null ? player.transform : null;
            foreach (var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (button.GetComponent<SoundButton>() == null) button.gameObject.AddComponent<SoundButton>();
            // Animation callbacks must live beside the Animator, including alternate player models.
            foreach (var animator in FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (animator.GetComponentInParent<PlayerBasic>() == null &&
                    animator.GetComponentInParent<TapTapGameJam.NPC.NPC>() == null &&
                    animator.GetComponentInParent<TapTapGameJam.Cockroach.Guard>() == null) continue;
                if (animator.GetComponent<SoundAnimationEvents>() == null)
                    animator.gameObject.AddComponent<SoundAnimationEvents>();
            }
        }
        private void Play(PlaySoundEvent request)
        {
            if (!SupportedScene || library == null) return;
            var definition = library.Find(request.Sound);
            if (definition == null || !definition.enabled) return;
            if (request.Sound != SoundId.Click && request.Bus != SoundBus.World) specificFeedbackFrame = Time.frameCount;
            if (request.Controlled && (!request.Playback.IsValid || request.Owner == null)) return;
            if (!request.Controlled && request.Bus == SoundBus.World && Time.timeScale <= 0f) return;
            foreach (var voice in voices)
                if (voice.active && request.Playback.IsValid && voice.request.Playback.Value == request.Playback.Value) return;
            int owner = definition.cooldownPerOwner && request.Owner != null ? request.Owner.GetInstanceID() : 0;
            if (request.Sound == SoundId.Hit && request.Bus == SoundBus.World) owner = 0;
            var key = (request.Sound, owner);
            if (lastPlayed.TryGetValue(key, out float last) && Time.unscaledTime - last < definition.cooldown) return;
            int count = 0;
            foreach (var voice in voices) if (voice.active && voice.definition.id == request.Sound) count++;
            if (count >= Mathf.Max(1, definition.maxInstances))
            {
                // A swarm death must never crowd out the player's own hit feedback.
                Voice replace = null;
                if (request.Sound == SoundId.Hit && request.Bus != SoundBus.World)
                    foreach (var voice in voices)
                        if (voice.active && voice.definition.id == SoundId.Hit && voice.request.Bus == SoundBus.World)
                        { replace = voice; break; }
                if (replace == null) return;
                Release(replace);
            }
            var clips = definition.clips;
            if (clips == null || clips.Length == 0)
            {
                if (warned.Add(request.Sound)) Debug.LogWarning($"[Audio] No clips configured: {request.Sound}", this);
                return;
            }
            int variant = Random.Range(0, clips.Length);
            if (clips.Length > 1 && lastVariant.TryGetValue(request.Sound, out int previous) && previous == variant)
                variant = (variant + Random.Range(1, clips.Length)) % clips.Length;
            if (clips[variant] == null)
            {
                if (warned.Add(request.Sound)) Debug.LogWarning($"[Audio] Missing clip: {request.Sound}", this);
                return;
            }
            Voice slot = null;
            foreach (var voice in voices) if (!voice.active) { slot = voice; break; }
            if (slot == null && voices.Count < Mathf.Max(1, library.voiceLimit))
            {
                var child = new GameObject("Voice"); child.transform.SetParent(transform);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false; source.spatialBlend = 0f; source.dopplerLevel = 0f;
                slot = new Voice { source = source }; voices.Add(slot);
            }
            int priority = definition.priority + (request.Sound == SoundId.Hit && request.Bus != SoundBus.World ? 20 : 0);
            if (slot == null)
            {
                foreach (var voice in voices)
                    if (voice.priority < priority && (slot == null || voice.priority < slot.priority)) slot = voice;
                if (slot == null) return;
                Release(slot);
            }
            slot.request = request; slot.definition = definition;
            slot.priority = priority;
            slot.active = true; slot.paused = slot.fading = false; slot.elapsed = 0f;
            slot.hadFollow = request.Follow != null;
            slot.scene = SceneManager.GetActiveScene().handle;
            int level = Mathf.Max(0, request.Level - 1);
            slot.baseVolume = Mathf.Clamp01(definition.volume + level * definition.volumePerLevel);
            slot.source.clip = clips[variant];
            slot.source.pitch = Mathf.Clamp(Random.Range(definition.pitch.x, definition.pitch.y) + level * definition.pitchPerLevel, 0.1f, 3f);
            slot.source.loop = request.Controlled && definition.loop;
            slot.source.transform.position = request.Follow != null ? request.Follow.position : request.Position;
            ApplyGain(slot);
            slot.source.Play();
            if (request.Bus == SoundBus.World && Time.timeScale <= 0f)
            { slot.source.Pause(); slot.paused = true; }
            lastPlayed[key] = Time.unscaledTime; lastVariant[request.Sound] = variant;
        }
        private void Stop(StopSoundEvent request)
        {
            if (!request.Playback.IsValid) return;
            foreach (var voice in voices)
            {
                if (!voice.active || voice.request.Playback.Value != request.Playback.Value) continue;
                if (voice.definition.fadeOut <= 0f || voice.paused || request.Reason == SoundStopReason.OwnerRemoved) Release(voice);
                else if (!voice.fading) { voice.fading = true; voice.fadeRemaining = voice.definition.fadeOut; }
            }
        }
        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextDiscovery)
            {
                Discover(); nextDiscovery = Time.unscaledTime + 0.5f;
            }
            foreach (var voice in voices)
            {
                if (!voice.active) continue;
                if (voice.request.Controlled && !OwnerActive(voice.request.Owner) || voice.hadFollow && voice.request.Follow == null)
                { Release(voice); continue; }
                bool pause = voice.request.Bus == SoundBus.World && Time.timeScale <= 0f;
                if (pause != voice.paused)
                {
                    voice.paused = pause;
                    if (pause) voice.source.Pause(); else voice.source.UnPause();
                }
                if (voice.paused) continue;
                voice.elapsed += Time.unscaledDeltaTime;
                if (!voice.source.isPlaying || voice.definition.maxDuration > 0f && voice.elapsed >= voice.definition.maxDuration)
                { Release(voice); continue; }
                if (voice.request.Follow != null) voice.source.transform.position = voice.request.Follow.position;
                if (voice.fading)
                {
                    voice.fadeRemaining -= Time.unscaledDeltaTime;
                    if (voice.fadeRemaining <= 0f) { Release(voice); continue; }
                }
                ApplyGain(voice);
            }
        }
        private static bool OwnerActive(Object owner)
        {
            if (owner == null) return false;
            if (owner is Behaviour behaviour) return behaviour.isActiveAndEnabled;
            if (owner is Component component) return component.gameObject.activeInHierarchy;
            if (owner is GameObject go) return go.activeInHierarchy;
            return true;
        }
        private void ApplyGain(Voice voice)
        {
            float gain = master * effects * voice.baseVolume;
            if (voice.definition.id != SoundId.Defeat) gain *= DuckGain;
            voice.source.panStereo = 0f;
            if (voice.request.Spatial)
            {
                if (listenerTarget == null || view == null) gain = 0f;
                else
                {
                    Vector3 delta = voice.source.transform.position - listenerTarget.position;
                    delta.y = 0f;
                    float distance = delta.magnitude;
                    gain *= 1f - Mathf.InverseLerp(voice.definition.nearDistance,
                        Mathf.Max(voice.definition.nearDistance + 0.01f, voice.definition.farDistance), distance);
                    Vector3 right = view.transform.right; right.y = 0f;
                    voice.source.panStereo = distance > 0.01f ? Mathf.Clamp(Vector3.Dot(delta / distance, right.normalized), -1f, 1f) : 0f;
                }
            }
            if (voice.fading) gain *= Mathf.Clamp01(voice.fadeRemaining / voice.definition.fadeOut);
            voice.source.volume = Mathf.Clamp01(gain);
        }
        private static void Release(Voice voice)
        {
            if (voice.source != null) { voice.source.Stop(); voice.source.clip = null; }
            voice.active = voice.paused = voice.fading = false;
            voice.request = default; voice.definition = null;
        }
    }
}

