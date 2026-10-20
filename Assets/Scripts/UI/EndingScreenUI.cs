using System;
using System.Collections;
using System.Collections.Generic;
using TapTapGameJam.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// MainMenu 里的结局屏：全屏放一张结局图，3 秒后出现「点击继续」，点一下回到主菜单。
    ///
    /// 数据来自 <see cref="EndingState"/>（GameScene 切场景前写进去的）。没有待播结局时
    /// 自己在 <c>Awake</c> 里关掉，主菜单照常显示 —— 所以从 ESC 退回主菜单不会误弹。
    ///
    /// 【黑场衔接】GameScene 那边是先淡入黑幕再切场景，所以这里开局就是全黑，
    /// 再把黑幕淡出露出结局图：视觉上是从黑场亮起来，不会闪一下白屏。
    ///
    /// 【层级】本物体是独立 Canvas（RenderMode = Screen Space Overlay，sortingOrder 很高），
    /// 显示期间会把 <see cref="_menuRoot"/> 关掉，结束后再打开。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndingScreenUI : MonoBehaviour
    {
        /// <summary>一条「结局 → 图片」的映射。Unity 不能序列化 Dictionary，所以用 List 配。</summary>
        [Serializable]
        public struct EndingPicture
        {
            [Tooltip("结局种类")]
            public GameEnding ending;

            [Tooltip("对应的结局图（Assets/UI/结局/*.PNG）")]
            public Texture picture;
        }

        [Header("引用")]
        [Tooltip("显示结局图的 RawImage")]
        [SerializeField] private RawImage _picture;

        [Tooltip("盖在最上面的纯黑 Image，开局全黑，淡出露出图片")]
        [SerializeField] private Image _blackOverlay;

        [Tooltip("主菜单的根物体：显示结局期间关掉，点完继续再打开")]
        [SerializeField] private GameObject _menuRoot;

        [Tooltip("「点击继续」挂哪个物体下。留空则挂在根上")]
        [SerializeField] private RectTransform _hintParent;

        [Tooltip("「点击继续」的文字对象。留空则运行时建一个（用下面的字体）")]
        [SerializeField] private TMP_Text _hint;

        [Header("文案与节奏")]
        [SerializeField] private string _hintText = "点击继续";

        [Tooltip("「点击继续」的字体。运行时自建文字时用")]
        [SerializeField] private TMP_FontAsset _hintFont;

        [Tooltip("点完继续之后，回到主菜单要不要淡回黑屏")]
        [SerializeField] private bool _fadeOutOnContinue;

        [Header("结局图")]
        [Tooltip("结局 → 图片。没配到的结局会显示一张纯黑（并在 Console 报警）")]
        [SerializeField] private List<EndingPicture> _pictures = new List<EndingPicture>();

        [Header("时间（秒，真实时间）")]
        [Tooltip("黑场淡出、露出结局图的时长")]
        [SerializeField, Min(0.01f)] private float _revealDuration = 0.6f;

        [Tooltip("结局图显示多久之后出现「点击继续」")]
        [SerializeField, Min(0f)] private float _holdBeforeHint = 3f;

        private bool _waitingForClick;

        #region 生命周期

        private void Awake()
        {
            GameEnding? ending = EndingState.Consume();
            if (!ending.HasValue)
            {
                // 不是从结局过来的：整块关掉，主菜单正常显示
                gameObject.SetActive(false);
                return;
            }

            if (_menuRoot != null)
            {
                _menuRoot.SetActive(false);
            }

            ApplyPicture(ending.Value);
            SetBlack(1f);

            if (_hint != null)
            {
                _hint.text = _hintText;
                _hint.gameObject.SetActive(false);
            }

            StartCoroutine(PlayRoutine());
        }

        private void Update()
        {
            if (!_waitingForClick)
            {
                return;
            }

            // 只认鼠标左键（触摸不会走这里，按需求只做鼠标）
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                _waitingForClick = false;
                StartCoroutine(ContinueRoutine());
            }
        }

        #endregion

        #region 流程

        private IEnumerator PlayRoutine()
        {
            // 黑场淡出 → 露出结局图
            yield return FadeBlack(1f, 0f, _revealDuration);

            // 图停留
            float elapsed = 0f;
            while (elapsed < _holdBeforeHint)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 出现「点击继续」
            TMP_Text hint = EnsureHint();
            if (hint != null)
            {
                hint.gameObject.SetActive(true);
            }

            _waitingForClick = true;
        }

        private IEnumerator ContinueRoutine()
        {
            if (_hint != null)
            {
                _hint.gameObject.SetActive(false);
            }

            if (_fadeOutOnContinue)
            {
                yield return FadeBlack(0f, 1f, _revealDuration);
            }

            if (_menuRoot != null)
            {
                _menuRoot.SetActive(true);
            }

            gameObject.SetActive(false);
        }

        private IEnumerator FadeBlack(float from, float to, float duration)
        {
            if (_blackOverlay == null)
            {
                yield break;
            }

            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            SetBlack(from);
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetBlack(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / safeDuration)));
                yield return null;
            }

            SetBlack(to);
        }

        #endregion

        #region 工具

        private void SetBlack(float alpha)
        {
            if (_blackOverlay == null)
            {
                return;
            }

            Color color = _blackOverlay.color;
            color.a = Mathf.Clamp01(alpha);
            _blackOverlay.color = color;

            // 全黑的时候吃掉点击，避免误触到主菜单
            _blackOverlay.raycastTarget = alpha > 0.99f;
        }

        private void ApplyPicture(GameEnding ending)
        {
            if (_picture == null)
            {
                return;
            }

            for (int i = 0; i < _pictures.Count; i++)
            {
                if (_pictures[i].ending != ending)
                {
                    continue;
                }

                _picture.texture = _pictures[i].picture;
                if (_picture.texture == null)
                {
                    Debug.LogWarning($"[EndingScreenUI]：结局 {ending} 的图没配。", this);
                }

                return;
            }

            Debug.LogWarning($"[EndingScreenUI]：映射表里没有结局 {ending}，只能显示黑屏。", this);
        }

        /// <summary>
        /// 「点击继续」的兜底创建：场景里 authored 了就直接用（推荐），
        /// 没 authored 就在运行时建一个居中的像素字，省得为了一个文本框去手写 TMP 的序列化数据。
        /// </summary>
        private TMP_Text EnsureHint()
        {
            if (_hint != null)
            {
                return _hint;
            }

            var hintObject = new GameObject("EndingHint", typeof(RectTransform), typeof(CanvasRenderer));
            Transform parent = _hintParent != null ? _hintParent : transform;
            hintObject.transform.SetParent(parent, false);

            RectTransform rect = hintObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 140f);
            rect.sizeDelta = new Vector2(900f, 90f);

            _hint = hintObject.AddComponent<TextMeshProUGUI>();
            if (_hintFont != null)
            {
                _hint.font = _hintFont;
            }

            _hint.text = _hintText;
            _hint.fontSize = 56f;
            _hint.color = Color.white;
            _hint.alignment = TextAlignmentOptions.Center;
            _hint.raycastTarget = false;
            return _hint;
        }

        #endregion
    }
}
