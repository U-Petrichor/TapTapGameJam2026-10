using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapTapGameJam.Tutorial
{
    /// <summary>
    /// 绑定场景里已有的 GameScene/Tutorial/Player|Boss/TextArea，不新建预制体。
    /// 只负责显示与推进；暂停、输入拦截和“已看”记录在 TutorialDirector。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialDialogueView : MonoBehaviour
    {
        private const int PopupSortingOrder = 210;
        private const string ClickCatcherName = "ClickCatcher";

        [SerializeField] private Canvas tutorialCanvas;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private GameObject playerRoot;
        [SerializeField] private TMP_Text playerText;
        [SerializeField] private Image taskHintIcon;
        [SerializeField] private Image nestHintIcon;
        [SerializeField] private GameObject bossRoot;
        [SerializeField] private TMP_Text bossText;
        [SerializeField, Min(0f)] private float fadeDuration = 0.15f;

        private int originalSortingOrder;
        private Action onClosed;
        private GameObject clickCatcher;
        private bool showing;
        private bool closing;
        private bool canAdvance;
        private bool advanceRequested;

        public bool IsShowing => showing;

        private void Awake()
        {
            ResolveReferences();
            originalSortingOrder = tutorialCanvas != null ? tutorialCanvas.sortingOrder : PopupSortingOrder;

            // 场景里的 Tutorial 根节点 Scale 是 0,0,0，先兜底修正，否则运行时看不见。
            if (transform.localScale == Vector3.zero)
                transform.localScale = Vector3.one;

            ConfigureText(playerText);
            ConfigureText(bossText);
            EnsureClickCatcher();
            HideImmediate();
        }

        private void Update()
        {
            if (!showing || closing) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                Advance();
                return;
            }
        }

        public void Show(TutorialStep step, Action closed)
        {
            if (step == null)
            {
                closed?.Invoke();
                return;
            }

            if (showing) HideImmediate();
            ResolveReferences();
            if (tutorialCanvas == null)
            {
                Debug.LogError("[Tutorial] 找不到 Tutorial Canvas。", this);
                closed?.Invoke();
                return;
            }

            bool boss = step.speaker == TutorialSpeaker.Boss;
            if (playerRoot != null) playerRoot.SetActive(!boss);
            if (bossRoot != null) bossRoot.SetActive(boss);
            SetTaskHintIcons(!boss && step.id == TutorialStepIds.Day1TaskGuide);

            TMP_Text target = boss ? bossText : playerText;
            if (target != null)
                target.text = step.text ?? string.Empty;

            onClosed = closed;
            showing = true;
            closing = false;
            canAdvance = false;
            advanceRequested = false;

            tutorialCanvas.sortingOrder = PopupSortingOrder;
            tutorialCanvas.enabled = true;
            if (EventSystem.current != null && clickCatcher != null)
                EventSystem.current.SetSelectedGameObject(clickCatcher);
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                // 淡入期间就要挡住背景 UI，避免 0.15 秒内点到下层按钮。
                group.blocksRaycasts = true;
            }

            StartCoroutine(ShowRoutine());
        }

        public void Advance()
        {
            if (!showing || closing) return;
            if (!canAdvance)
            {
                advanceRequested = true;
                return;
            }

            closing = true;
            canAdvance = false;
            advanceRequested = false;
            StartCoroutine(CloseRoutine());
        }

        public void HideImmediate()
        {
            StopAllCoroutines();
            showing = false;
            closing = false;
            canAdvance = false;
            advanceRequested = false;
            onClosed = null;

            if (playerRoot != null) playerRoot.SetActive(false);
            if (bossRoot != null) bossRoot.SetActive(false);
            SetTaskHintIcons(false);
            if (tutorialCanvas != null)
            {
                tutorialCanvas.enabled = false;
                tutorialCanvas.sortingOrder = originalSortingOrder;
            }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == clickCatcher)
                EventSystem.current.SetSelectedGameObject(null);
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        private IEnumerator ShowRoutine()
        {
            yield return Fade(0f, 1f);
            if (!showing || closing) yield break;
            canAdvance = true;
            if (group != null)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            if (advanceRequested) Advance();
        }

        private IEnumerator CloseRoutine()
        {
            yield return Fade(1f, 0f);
            showing = false;
            closing = false;
            canAdvance = false;
            if (playerRoot != null) playerRoot.SetActive(false);
            if (bossRoot != null) bossRoot.SetActive(false);
            SetTaskHintIcons(false);
            if (tutorialCanvas != null)
            {
                tutorialCanvas.enabled = false;
                tutorialCanvas.sortingOrder = originalSortingOrder;
            }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == clickCatcher)
                EventSystem.current.SetSelectedGameObject(null);
            if (group != null)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        private IEnumerator Fade(float from, float to)
        {
            if (group == null) yield break;
            if (fadeDuration <= 0f)
            {
                group.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }
            group.alpha = to;
        }

        private void ResolveReferences()
        {
            if (tutorialCanvas == null) tutorialCanvas = GetComponent<Canvas>();
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            if (playerRoot == null)
            {
                Transform child = transform.Find("Player");
                if (child != null) playerRoot = child.gameObject;
            }
            if (bossRoot == null)
            {
                Transform child = transform.Find("Boss");
                if (child != null) bossRoot = child.gameObject;
            }
            if (playerText == null) playerText = FindTextArea(playerRoot);
            if (bossText == null) bossText = FindTextArea(bossRoot);
            if (taskHintIcon == null) taskHintIcon = FindImage(playerRoot, "TaskHintIcon");
            if (nestHintIcon == null) nestHintIcon = FindImage(playerRoot, "NestHintIcon");
        }

        private static Image FindImage(GameObject root, string childName)
        {
            if (root == null) return null;
            Transform child = root.transform.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private void SetTaskHintIcons(bool visible)
        {
            if (taskHintIcon != null) taskHintIcon.gameObject.SetActive(visible);
            if (nestHintIcon != null) nestHintIcon.gameObject.SetActive(visible);
        }

        private static TMP_Text FindTextArea(GameObject root)
        {
            if (root == null) return null;
            Transform child = root.transform.Find("TextArea");
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static void ConfigureText(TMP_Text text)
        {
            if (text == null) return;
            text.text = string.Empty;
            // 表格里最长的段落会超过当前 177 高度，开启自动缩放避免溢出。
            text.enableAutoSizing = true;
            text.fontSizeMin = 28f;
            text.fontSizeMax = 44f;
        }

        private void EnsureClickCatcher()
        {
            Transform existing = transform.Find(ClickCatcherName);
            if (existing != null)
            {
                clickCatcher = existing.gameObject;
                Button existingButton = existing.GetComponent<Button>();
                if (existingButton != null)
                {
                    existingButton.onClick.RemoveListener(Advance);
                }
                return;
            }

            var catcher = new GameObject(ClickCatcherName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            catcher.transform.SetParent(transform, false);
            clickCatcher = catcher;

            var rect = catcher.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var image = catcher.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            var button = catcher.GetComponent<Button>();
            button.transition = Selectable.Transition.None;

            // 全屏透明层只拦截鼠标点击，教程统一由空格关闭。
            catcher.transform.SetAsLastSibling();
        }
    }
}
