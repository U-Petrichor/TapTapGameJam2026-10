using System;
using System.Collections.Generic;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TapTapGameJam.UI;
using UnityEngine;

namespace TapTapGameJam.Tutorial
{
    /// <summary>
    /// 教程调度器：把“第一次事件”信号变成有序、只出现一次的弹窗/高亮。
    /// 只负责判断与流程，不直接持有 UI 层级；UI 由 TutorialDialogueView 适配。
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed class TutorialDirector : MonoBehaviour
    {
        private const string AfterPrefix = "after:";

        public static TutorialDirector Instance { get; private set; }

        [SerializeField] private TutorialSequence sequence;
        [SerializeField] private TutorialDialogueView dialogueView;
        [SerializeField] private TutorialHighlightView highlightView;
        [SerializeField] private RectTransform coinTarget;
        [SerializeField] private RectTransform countdownTarget;
        [SerializeField, Min(0f)] private float stepGap = 0.15f;

        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly HashSet<string> receivedSignals = new HashSet<string>();
        private readonly Queue<TutorialStep> queue = new Queue<TutorialStep>();
        private readonly HashSet<string> queuedIds = new HashSet<string>();
        private readonly Dictionary<string, List<Action>> blocking = new Dictionary<string, List<Action>>();

        private TutorialStep current;
        private bool highlightRunning;
        private float highlightEndsAt;
        private float nextAllowedAt;
        private bool modalHeld;
        private string savedActionMap;
        private bool subscribed;
        private bool warnedMissingView;

        public bool IsShowing => current != null;

        private void Awake()
        {
            Instance = this;
            ResolveReferences();
            LoadSeen();
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<EventTutorialSignal>(HandleSignal);
            EventPipeline.Subscribe<EventNewDay>(HandleNewDay);
            EventPipeline.Subscribe<NightTransitionCompletedEvent>(HandleNightCompleted);
            subscribed = true;
        }

        private void Start()
        {
            Evaluate();
        }

        private void Update()
        {
            if (highlightRunning && Time.unscaledTime >= highlightEndsAt)
            {
                FinishCurrent();
                return;
            }

            if (current == null && queue.Count > 0 &&
                Time.unscaledTime >= nextAllowedAt && CanShow())
            {
                ShowNext();
            }
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                EventPipeline.UnSubscribe<EventTutorialSignal>(HandleSignal);
                EventPipeline.UnSubscribe<EventNewDay>(HandleNewDay);
                EventPipeline.UnSubscribe<NightTransitionCompletedEvent>(HandleNightCompleted);
                subscribed = false;
            }

            Shutdown();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 结算这类流程需要“等教程弹完再继续”。如果该步骤已看过或没有可展示内容，立即执行。
        /// </summary>
        public void RequestBlocking(string signalKey, Action onDone)
        {
            if (onDone == null) return;
            if (dialogueView == null || sequence == null)
            {
                onDone();
                return;
            }

            ReceiveSignal(signalKey);

            TutorialStep last = null;
            foreach (TutorialStep step in sequence.Steps)
            {
                if (step == null || step.trigger != signalKey) continue;
                if (seen.Contains(step.id)) continue;
                if (!RequiresMet(step)) continue;
                last = step;
            }

            if (last == null)
            {
                onDone();
                return;
            }

            bool pending = queuedIds.Contains(last.id) ||
                (current != null && current.id == last.id);
            if (!pending)
            {
                // 被更早的步骤挡住时不要卡住玩法；等它真正排到队首再说。
                onDone();
                return;
            }

            if (!blocking.TryGetValue(last.id, out List<Action> callbacks))
            {
                callbacks = new List<Action>();
                blocking[last.id] = callbacks;
            }
            callbacks.Add(onDone);
        }

        /// <summary>ESC 等关闭类输入只由教程弹窗拦截；弹窗本身只由空格推进。</summary>
        public static bool IsConsumingDismissInput()
        {
            return Instance != null && Instance.current != null;
        }

        /// <summary>QA：清空本安装的所有教程记录。</summary>
        public void ResetProgress()
        {
            PlayerProfile.ResetTutorialProgress();
            TapTapGameJam.Player.MoveState.ResetFirstMoveTracking();
            seen.Clear();
            receivedSignals.Clear();
            queue.Clear();
            queuedIds.Clear();
            blocking.Clear();
            highlightRunning = false;
            highlightView?.Hide();
            dialogueView?.HideImmediate();
            if (current != null)
            {
                current = null;
                ReleaseModal();
            }
        }

        /// <summary>QA：绕过信号，直接把某一步排进队列。</summary>
        public bool ForceShow(string id)
        {
            if (sequence == null || !sequence.TryGet(id, out TutorialStep step)) return false;
            if (queuedIds.Contains(id) || (current != null && current.id == id)) return true;
            queue.Enqueue(step);
            queuedIds.Add(id);
            return true;
        }

        private void ShowNext()
        {
            TutorialStep step = queue.Dequeue();
            queuedIds.Remove(step.id);
            current = step;

            if (step.presentation == TutorialPresentation.HighlightOnly)
            {
                highlightRunning = true;
                float duration = step.autoCloseSeconds > 0f ? step.autoCloseSeconds : 2.5f;
                RectTransform target = ResolveHighlightTarget(step.highlight);
                if (highlightView != null && target != null)
                    highlightView.Show(target, duration);
                else
                    Debug.LogWarning($"[Tutorial] 高亮步骤 {step.id} 缺少组件或目标，{duration:0.0}s 后自动跳过。", this);

                highlightEndsAt = Time.unscaledTime + duration;
                if (step.markOnShow) MarkSeen(step.id);
                return;
            }

            AcquireModal();
            if (dialogueView == null)
            {
                FinishCurrent();
                return;
            }

            dialogueView.Show(step, FinishCurrent);
        }

        private void FinishCurrent()
        {
            TutorialStep step = current;
            if (step == null) return;

            current = null;
            bool wasHighlight = highlightRunning;
            highlightRunning = false;
            highlightView?.Hide();
            if (!wasHighlight) ReleaseModal();

            if (!step.markOnShow) MarkSeen(step.id);
            if (!string.IsNullOrEmpty(step.emitOnClose))
                TutorialSignal.Send(step.emitOnClose);

            if (blocking.TryGetValue(step.id, out List<Action> callbacks))
            {
                blocking.Remove(step.id);
                foreach (Action callback in callbacks)
                    callback?.Invoke();
            }

            nextAllowedAt = Time.unscaledTime + stepGap;
            Evaluate();
        }

        private void Evaluate()
        {
            if (sequence == null) return;

            foreach (TutorialStep step in sequence.Steps)
            {
                if (step == null || string.IsNullOrEmpty(step.id)) continue;
                if (seen.Contains(step.id) || queuedIds.Contains(step.id)) continue;
                if (current != null && current.id == step.id) continue;
                if (!TriggerMet(step) || !RequiresMet(step)) continue;

                queue.Enqueue(step);
                queuedIds.Add(step.id);
            }
        }

        private bool TriggerMet(TutorialStep step)
        {
            if (string.IsNullOrEmpty(step.trigger)) return false;
            if (step.trigger.StartsWith(AfterPrefix, StringComparison.Ordinal))
                return seen.Contains(step.trigger.Substring(AfterPrefix.Length));
            return receivedSignals.Contains(step.trigger);
        }

        private bool RequiresMet(TutorialStep step)
        {
            if (step.requires == null) return true;
            foreach (string required in step.requires)
            {
                if (!string.IsNullOrEmpty(required) && !seen.Contains(required))
                    return false;
            }
            return true;
        }

        private void ReceiveSignal(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            receivedSignals.Add(key);
            Evaluate();
        }

        private void HandleSignal(EventTutorialSignal evt) => ReceiveSignal(evt.Key);

        private void HandleNewDay(EventNewDay evt)
        {
            if (evt.DayCount == 1) ReceiveSignal(TutorialSignals.DayStart);
        }

        private void HandleNightCompleted(NightTransitionCompletedEvent evt)
        {
            ReceiveSignal(TutorialSignals.NightStarted);
        }

        private void MarkSeen(string id)
        {
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) return;
            if (!PlayerProfile.MarkSeen(id))
                Debug.LogError($"[Tutorial] 无法写入教程进度：{id}", this);
        }

        private bool CanShow()
        {
            if (IntroVideoTransition.IsTransitioning) return false;
            if (ESCMenu.Instance != null && ESCMenu.Instance.IsOpen) return false;
            if (!GameManager.HasInstance) return false;

            if (dialogueView == null && !warnedMissingView)
            {
                warnedMissingView = true;
                Debug.LogError("[Tutorial] 找不到 TutorialDialogueView，教程无法显示。", this);
            }
            return dialogueView != null;
        }

        private void AcquireModal()
        {
            if (modalHeld) return;
            modalHeld = true;

            PlayerInputManager input = GameManager.PlayerInputManager;
            if (input != null)
            {
                savedActionMap = input.CurrentActionMapName;
                input.SetTutorialBlocked(true);
                input.SwitchActionMap("ESCMenu");
            }

            if (GamePauseManager.Instance != null)
                GamePauseManager.Instance.SetTutorialOpen(true);
        }

        private void ReleaseModal()
        {
            if (!modalHeld) return;
            modalHeld = false;

            PlayerInputManager input = GameManager.PlayerInputManager;
            if (input != null)
            {
                input.SetTutorialBlocked(false);
                input.SwitchActionMap(string.IsNullOrEmpty(savedActionMap)
                    ? PlayerInputManager.DefaultActionMapName
                    : savedActionMap);
            }

            if (GamePauseManager.Instance != null)
                GamePauseManager.Instance.SetTutorialOpen(false);
        }

        private RectTransform ResolveHighlightTarget(TutorialHighlightTarget target)
        {
            switch (target)
            {
                case TutorialHighlightTarget.Coins:
                    return coinTarget;
                case TutorialHighlightTarget.Countdown:
                    return countdownTarget;
                default:
                    return null;
            }
        }

        private void ResolveReferences()
        {
            if (sequence == null)
                sequence = Resources.Load<TutorialSequence>("Tutorial/TutorialSequence");

            if (sequence == null)
            {
                // 资产还没生成时用默认内容兜底，保证系统可运行。
                var runtime = ScriptableObject.CreateInstance<TutorialSequence>();
                runtime.SetSteps(TutorialDefaultContent.CreateSteps());
                sequence = runtime;
            }

            if (dialogueView == null) dialogueView = FindFirstObjectByType<TutorialDialogueView>();
            if (highlightView == null) highlightView = FindFirstObjectByType<TutorialHighlightView>();

            if (coinTarget == null)
            {
                GameObject coin = GameObject.Find("HUDCanvas/ResourcesUI/Coins");
                if (coin != null) coinTarget = coin.GetComponent<RectTransform>();
            }
            if (countdownTarget == null)
            {
                GameObject timer = GameObject.Find("HUDCanvas/RemainingTimeUI");
                if (timer != null) countdownTarget = timer.GetComponent<RectTransform>();
            }
        }

        private void LoadSeen()
        {
            seen.Clear();
            if (sequence == null) return;

            foreach (TutorialStep step in sequence.Steps)
            {
                if (step != null && !string.IsNullOrEmpty(step.id) && PlayerProfile.HasSeen(step.id))
                    seen.Add(step.id);
            }
        }

        private void Shutdown()
        {
            queue.Clear();
            queuedIds.Clear();
            blocking.Clear();
            highlightRunning = false;
            highlightView?.Hide();
            dialogueView?.HideImmediate();

            if (current != null)
            {
                current = null;
                ReleaseModal();
            }
        }
    }
}
