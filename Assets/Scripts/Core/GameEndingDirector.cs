using System.Collections;
using TapTapGameJam.Buffs;
using TapTapGameJam.Cockroach;
using TapTapGameJam.Events;
using TapTapGameJam.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 结局总控。所有结局都从这里出去：
    /// <code>
    /// 暂停（0.1 倍速） → 等 N 秒 → 黑场淡入 → 记下结局 → 切 MainMenu
    /// </code>
    ///
    /// 【为什么暂停用 0.1 而不是 0】
    /// 结局前那段延迟是给玩家"看清发生了什么"的（被踩、被喷、成片消杀），
    /// 彻底冻住反而看不出戏；0.1 倍速下画面还在动，但已经不可玩。
    /// 暂停通过 <see cref="GamePauseManager.SetGameEnding"/> 走，和其它暂停原因统一在一处生效。
    ///
    /// 【播报不会被暂停】广播视频用的是 <c>VideoTimeUpdateMode.UnscaledGameTime</c>，
    /// 这里改 timeScale 不影响它；延迟计时也全部用 unscaled，否则 0.1 倍速下会慢十倍。
    ///
    /// 【黑场】延迟结束后自己建一块全屏黑 Image 淡入（排序号 32000，盖住所有 UI），
    /// 切到 MainMenu 后由那边的 <see cref="EndingScreenUI"/> 从黑场淡出露出结局图。
    /// 不在 GameScene 里 authored 一块黑幕，是因为要改一个 23MB 的场景文件；
    /// 这只是本局最后 0.5 秒的一次性开销。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameEndingDirector : MonoBehaviour
    {
        /// <summary>结局期间的时间流速：不彻底冻死，画面还在动</summary>
        public const float EndingTimeScale = 0.1f;

        [Header("延迟（秒，从触发算到开始黑场）")]
        [Tooltip("踩死 / 喷死：留给玩家看清自己和播报")]
        [SerializeField, Min(0f)] private float _deathDelay = 1f;

        [Tooltip("区域消杀：留给玩家看清成片暴毙")]
        [SerializeField, Min(0f)] private float _disinfectionDelay = 2f;

        [Tooltip("过劳死：等正常的白天流程把场景呈现出来")]
        [SerializeField, Min(0f)] private float _overworkDelay = 4f;

        [Header("触发条件")]
        [Tooltip("撑到第几天（含）还没赢就过劳死。DayIndex 从 1 开始")]
        [SerializeField, Min(1)] private int _overworkDay = 10;

        [Header("过渡")]
        [Tooltip("黑场淡入时长（秒，真实时间）")]
        [SerializeField, Min(0.01f)] private float _fadeDuration = 0.5f;

        [Tooltip("切到哪个场景")]
        [SerializeField] private string _mainMenuScene = "MainMenu";

        private static GameEndingDirector _instance;

        public static GameEndingDirector Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GameEndingDirector>();
                }

                return _instance;
            }
        }

        /// <summary>本局是否已经进入结局流程（进入后不再接受新的触发）</summary>
        public bool HasTriggered { get; private set; }

        private Canvas _fadeCanvas;
        private Image _fadeImage;

        #region 生命周期

        private void Awake()
        {
            _instance = this;
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<EventNewDay>(HandleNewDay);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventNewDay>(HandleNewDay);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion

        #region 对外接口

        /// <summary>
        /// 进入结局流程。延迟用本组件上按结局配好的值，重复调用只有第一次生效。
        /// </summary>
        public static void Trigger(GameEnding ending)
        {
            GameEndingDirector director = Instance;
            if (director == null)
            {
                Debug.LogError(
                    $"[GameEndingDirector]：场景里没有 GameEndingDirector，结局 {ending} 进不去。" +
                    "检查 GameScene 的 GameManager 物体上有没有挂这个组件。");
                return;
            }

            director.Play(ending);
        }

        #endregion

        #region 触发判定

        /// <summary>
        /// 进入白天时判两件事，顺序不能反：
        /// <list type="number">
        /// <item><b>占领</b>：蟑螂达标 / 拿到「群颚破壁」 → 立刻进结局（不等白天亮起）；</item>
        /// <item><b>过劳死</b>：撑到 <see cref="_overworkDay"/> 天还没赢 → 等一会儿再进。</item>
        /// </list>
        /// 占领判定里带上 <see cref="CockroachManager.HasWon"/>：清晨会先清掉没归巢的蟑螂，
        /// 只看当前数量的话，明明夜里已经达标也会被判成没赢。
        /// </summary>
        private void HandleNewDay(EventNewDay evt)
        {
            if (HasTriggered)
            {
                return;
            }

            if (IsOccupied())
            {
                Play(GameEnding.Occupation);
                return;
            }

            if (evt.DayCount >= _overworkDay)
            {
                Play(GameEnding.Overwork);
            }
        }

        private static bool IsOccupied()
        {
            CockroachManager manager = CockroachManager.Instance;
            if (manager != null)
            {
                if (manager.HasWon)
                {
                    return true;
                }

                if (manager.CockroachTotalCount() >= manager.VictoryPopulation)
                {
                    return true;
                }
            }

            PlayerBuffs buffs = PlayerBuffs.Current;
            return buffs != null && buffs.Has(BuffKey.FlagGroupWallGnawing);
        }

        private float DelayFor(GameEnding ending)
        {
            switch (ending)
            {
                case GameEnding.Crushed:
                case GameEnding.Sprayed:
                    return _deathDelay;
                case GameEnding.Disinfection:
                    return _disinfectionDelay;
                case GameEnding.Overwork:
                    return _overworkDelay;
                default:
                    // 占领：立刻进，只留黑场淡入的时间
                    return 0f;
            }
        }

        #endregion

        public void PlayByButton()
        {
            Play(GameEnding.Disinfection);
        }

        #region 播放流程

        private void Play(GameEnding ending)
        {
            if (HasTriggered)
            {
                return;
            }

            HasTriggered = true;

            if (GamePauseManager.Instance != null)
            {
                GamePauseManager.Instance.SetGameEnding(true);
            }
            else
            {
                // 没有暂停管理器也别把整局卡在正常速度上
                Time.timeScale = EndingTimeScale;
            }

            StartCoroutine(EndingRoutine(ending, DelayFor(ending)));
        }

        private IEnumerator EndingRoutine(GameEnding ending, float delay)
        {
            Debug.Log($"[GameEndingDirector]：进入结局 {ending}（延迟 {delay:F1}s）", this);

            // 延迟：真实时间，0.1 倍速下也不能变慢
            float elapsed = 0f;
            while (elapsed < delay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 黑场淡入
            EnsureFadeOverlay();

            float fade = 0f;
            float duration = Mathf.Max(0.01f, _fadeDuration);
            while (fade < duration)
            {
                fade += Time.unscaledDeltaTime;
                SetFadeAlpha(Mathf.Clamp01(fade / duration));
                yield return null;
            }

            SetFadeAlpha(1f);

            EndingState.Set(ending);
            SceneManager.LoadScene(_mainMenuScene);
        }

        /// <summary>
        /// 建一块全屏黑幕盖住所有 UI。
        /// 只有本局最后那一下会用一次，所以直接建在运行时，不需要场景里 authored。
        /// </summary>
        private void EnsureFadeOverlay()
        {
            if (_fadeImage != null)
            {
                return;
            }

            var canvasObject = new GameObject("EndingFadeCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            _fadeCanvas = canvasObject.GetComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 盖住场景里所有 UI（项目里最高的是 32000 这个量级）
            _fadeCanvas.sortingOrder = 32760;

            var imageObject = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _fadeImage = imageObject.GetComponent<Image>();
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
            // 黑幕只是遮罩，不吃点击
            _fadeImage.raycastTarget = false;
        }

        private void SetFadeAlpha(float alpha)
        {
            if (_fadeImage == null)
            {
                return;
            }

            Color color = _fadeImage.color;
            color.a = alpha;
            _fadeImage.color = color;
        }

        #endregion
    }
}
