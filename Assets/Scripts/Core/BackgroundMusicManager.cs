using TapTapGameJam.Audio;
using UnityEngine;

using TapTapGameJam.Events;
using TapTapGameJam.Types;

namespace TapTapGameJam.Core
{
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public class BackgroundMusicManager : MonoBehaviour
    {
        [Header("阶段管理器（留空时自动查找）")]
        [SerializeField] private GamePhaseManager phaseManager;

        [Header("阶段背景音乐")]
        [SerializeField] private AudioClip dayMusic;
        [SerializeField] private AudioClip shoppingMusic;
        [SerializeField] private AudioClip nightMusic;

        private AudioSource _musicSource;

        private void Reset()
        {
            ConfigureAudioSource();
            phaseManager = GetComponent<GamePhaseManager>();
        }

        private void Awake()
        {
            ConfigureAudioSource();
        }

        private void ConfigureAudioSource()
        {
            _musicSource = GetComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            SyncCurrentPhase();
        }

        private void Start()
        {
            // 兼容阶段管理器的 Start 在本组件之前或之后执行。
            SyncCurrentPhase();
        }

        private void LateUpdate()
        {
            if (_musicSource != null && SoundManager.Instance != null)
                _musicSource.volume = SoundManager.Instance.MusicGain;
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            _musicSource.Stop();
        }

        private void SyncCurrentPhase()
        {
            if (phaseManager == null)
                phaseManager = GetComponent<GamePhaseManager>();

            if (phaseManager == null)
                phaseManager = FindFirstObjectByType<GamePhaseManager>();

            if (phaseManager != null)
                PlayPhaseMusic(phaseManager.CurrentPhase);
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            PlayPhaseMusic(evt.NewPhase);
        }

        private void PlayPhaseMusic(GamePhase phase)
        {
            AudioClip nextMusic = phase switch
            {
                GamePhase.Day => dayMusic,
                GamePhase.Shopping => shoppingMusic,
                GamePhase.Night => nightMusic,
                _ => null
            };

            // 重复请求同一音乐时，保持播放进度。
            if (_musicSource.clip == nextMusic && _musicSource.isPlaying)
                return;

            _musicSource.Stop();
            _musicSource.clip = nextMusic;

            // 未配置音乐的阶段保持静音。
            if (nextMusic != null)
                _musicSource.Play();
        }
    }
}
