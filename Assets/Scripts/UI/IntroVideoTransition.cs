using System.Collections;
using System.Collections.Generic;
using TapTapGameJam.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TapTapGameJam.UI
{
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(Canvas))]
    public sealed class IntroVideoTransition : MonoBehaviour
    {
        [Header("视频引用")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private CanvasGroup videoGroup;
        [Header("进入场景")]
        [SerializeField] private string gameSceneName = "GameScene";
        [Header("淡出：从指定秒数持续到视频结束")]
        [Min(0f)] [SerializeField] private float fadeStartTime = 27f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Header("异常兜底（真实时间秒数）")]
        [Min(1f)] [SerializeField] private float videoTimeout = 15f;
        [SerializeField] private TMP_FontAsset loadingFont;

        private static IntroVideoTransition active;
        public static bool IsTransitioning => active != null;

        private readonly List<EventSystem> blockedEventSystems = new List<EventSystem>();
        private Canvas canvas;
        private Image backdrop;
        private TMP_Text loadingLabel;
        private AsyncOperation loading;
        private bool videoFinished;
        private bool gameLoaded;
        private bool ownsPause;
        private float previousTimeScale;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvas.enabled = false;
            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.Stop();
            }
        }

        // Called by the existing main-menu button. Validate before locking the menu.
        public bool Begin()
        {
            if (IsTransitioning) return false;
            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError($"开场过渡：Build Settings 中找不到场景 {gameSceneName}。", this);
                return false;
            }

            try
            {
                loading = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
            if (loading == null) return false;
            loading.allowSceneActivation = false;

            active = this;
            previousTimeScale = Time.timeScale;
            ownsPause = true;
            Time.timeScale = 0f;
            transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
            BlockEventSystems();
            SceneManager.sceneLoaded += OnSceneLoaded;
            CreateLoadingUI();
            canvas.enabled = true;
            StartCoroutine(RunTransition());
            return true;
        }

        private IEnumerator RunTransition()
        {
            videoFinished = false;
            if (videoPlayer == null || videoGroup == null ||
                (videoPlayer.source == VideoSource.VideoClip && videoPlayer.clip == null))
            {
                FailVideo("没有配置视频或 CanvasGroup，跳过开场视频。");
            }
            else
            {
                videoGroup.alpha = 0f;
                videoGroup.interactable = false;
                videoPlayer.isLooping = false;
                // The game is paused while its first frame is revealed under the video.
                videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                videoPlayer.loopPointReached += OnVideoEnded;
                videoPlayer.errorReceived += OnVideoError;
                videoPlayer.Prepare();

                float prepareStarted = Time.realtimeSinceStartup;
                while (!videoFinished && !videoPlayer.isPrepared)
                {
                    if (Time.realtimeSinceStartup - prepareStarted >= videoTimeout)
                        FailVideo("视频准备超时，跳过开场视频。");
                    yield return null;
                }
                if (!videoFinished)
                {
                    loadingLabel.gameObject.SetActive(false);
                    videoGroup.alpha = 1f;
                    videoPlayer.Play();
                }
            }

            double lastVideoTime = -1;
            float lastProgressAt = Time.realtimeSinceStartup;
            while (!videoFinished)
            {
                double time = videoPlayer.time;
                videoGroup.alpha = EvaluateAlpha(time, videoPlayer.length, fadeStartTime, fadeCurve);
                if (time != lastVideoTime || !Application.isFocused)
                {
                    lastVideoTime = time;
                    lastProgressAt = Time.realtimeSinceStartup;
                }
                else if (Time.realtimeSinceStartup - lastProgressAt >= videoTimeout)
                    FailVideo("视频播放长时间没有进展，跳过开场视频。");

                if (time >= fadeStartTime)
                    TryActivateGame();
                yield return null;
            }

            // End events can arrive before the last Update has sampled the final frame.
            if (videoGroup != null) videoGroup.alpha = 0f;
            if (videoPlayer != null) videoPlayer.Stop();
            while (!gameLoaded)
            {
                backdrop.gameObject.SetActive(true);
                loadingLabel.gameObject.SetActive(true);
                TryActivateGame();
                yield return null;
            }

            ReleasePauseAndInput();
            canvas.enabled = false;
            Destroy(gameObject);
        }

        internal static float EvaluateAlpha(double time, double length, float start, AnimationCurve curve)
        {
            if (time >= length && length > 0) return 0f;
            if (time <= start || length <= start) return 1f;
            float progress = Mathf.Clamp01((float)((time - start) / (length - start)));
            return 1f - Mathf.Clamp01(curve.Evaluate(progress));
        }

        private void TryActivateGame()
        {
            if (loading.progress >= 0.9f)
                loading.allowSceneActivation = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != gameSceneName) return;
            gameLoaded = true;
            BlockEventSystems();
            backdrop.gameObject.SetActive(false);
            loadingLabel.gameObject.SetActive(false);
        }

        private void BlockEventSystems()
        {
            foreach (var system in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (!system.enabled) continue;
                system.enabled = false;
                blockedEventSystems.Add(system);
            }
        }

        private void OnVideoEnded(VideoPlayer player) => videoFinished = true;
        private void OnVideoError(VideoPlayer player, string message) => FailVideo(message);

        private void FailVideo(string message)
        {
            Debug.LogWarning($"开场过渡：{message}", this);
            videoFinished = true;
        }

        private void CreateLoadingUI()
        {
            // A separate sibling: fading the video must not fade the loading fallback.
            backdrop = new GameObject("LoadingBackdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            backdrop.transform.SetParent(transform, false);
            backdrop.transform.SetAsFirstSibling();
            Stretch(backdrop.rectTransform);
            backdrop.color = Color.black;
            backdrop.raycastTarget = true;

            loadingLabel = new GameObject("LoadingLabel", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            loadingLabel.transform.SetParent(transform, false);
            Stretch(loadingLabel.rectTransform);
            if (loadingFont != null) loadingLabel.font = loadingFont;
            loadingLabel.text = loadingFont != null ? "加载中…" : "Loading...";
            loadingLabel.alignment = TextAlignmentOptions.Center;
            loadingLabel.fontSize = 32f;
            loadingLabel.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void ReleasePauseAndInput()
        {
            if (!ownsPause) return;
            ownsPause = false;
            if (active == this) active = null;
            foreach (var system in blockedEventSystems)
                if (system != null) system.enabled = true;
            blockedEventSystems.Clear();
            if (GamePauseManager.Instance != null)
                GamePauseManager.Instance.RefreshPause();
            else
                Time.timeScale = previousTimeScale;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnVideoEnded;
                videoPlayer.errorReceived -= OnVideoError;
            }
            // Do not leave Unity's async queue stalled if the transition is removed early.
            if (loading != null && !loading.isDone) loading.allowSceneActivation = true;
            ReleasePauseAndInput();
        }
    }
}
