using System.Collections;
using TapTapGameJam.Events;
using UnityEngine.UI;
using UnityEngine;
using System.Collections.Generic;

namespace TapTapGameJam.UI
{

    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class MiniGamePanelUI : MonoBehaviour
    {
        [System.Serializable]
        private class MiniGameEntry
        {
            public int Index;
            public MiniGameContent Content;
        }
        [SerializeField] private List<MiniGameEntry> _miniGames = new List<MiniGameEntry>();
        [SerializeField] private Vector2 _shownPosition = new Vector2(0f, 0f);
        [SerializeField] private Vector2 _hiddenPosition = new Vector2(0f, -1080f);
        [SerializeField, Min(0f)] private float _duration = 0.3f;
        [SerializeField] private Button ExitButton;
        private System.Action<bool> _actionOnFinished; // 小游戏管理器传来的方法，完成或者未完成时通过传入bool值执行对应的策略

        private RectTransform _rect;
        private CanvasGroup _canvasGroup;
        private Coroutine _animation;
        private MiniGameContent _currentContent;


        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (ExitButton == null)
            {
                Debug.LogError($"[MiniGamePanelUI]：没有绑定ExitButton");
            }
            else
            {
                // 总管一直启用，按钮监听只在初始化时绑定一次。
                ExitButton.onClick.AddListener(OnMiniGamePanelClose);
            }
            foreach (var entry in _miniGames)
            {
                if (entry.Content != null)
                    entry.Content.gameObject.SetActive(false);
            }
            SetHidden();
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<EventOpenMiniGamePanel>(OnOpenMiniGamePanel);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventOpenMiniGamePanel>(OnOpenMiniGamePanel);
        }

        private void OnOpenMiniGamePanel(EventOpenMiniGamePanel evt)
        {
            MiniGameContent content = FindContent(evt.MiniGameIndex);
            if (content == null)
            {
                Debug.LogError($"[MiniGamePanelUI] 小游戏编号 {evt.MiniGameIndex} 未正确配置。", this);
                evt.OnFinished?.Invoke(false);
                EventPipeline.Publish(new EventCloseMiniGamePanel());
                return;
            }

            _currentContent = content;
            _actionOnFinished = evt.OnFinished;
            content.gameObject.SetActive(true);
            Show();
            // 内容立即初始化；弹出动画期间 CanvasGroup 会禁止玩家操作。
            content.Begin(OnCurrentMiniGameFinish);
        }

        private MiniGameContent FindContent(int index)
        {
            foreach (var entry in _miniGames)
            {
                if (entry.Index == index)
                    return entry.Content;
            }
            return null;
        }

        /// <summary>
        /// ExitButton调用的关闭UI方法
        /// </summary>
        private void OnMiniGamePanelClose() => FinishCurrent(false);
        private void OnCurrentMiniGameFinish() => FinishCurrent(true);
        public void OnEscKeyPressed()
        {
            FinishCurrent(false);
        }
        private void FinishCurrent(bool success)
        {
            if (_currentContent == null) return;

            MiniGameContent content = _currentContent;
            var callback = _actionOnFinished;
            _currentContent = null;
            _actionOnFinished = null;

            // 先处理结果，再清理内容、隐藏窗口，最后通知玩家恢复操作。
            callback?.Invoke(success);
            content.ResetView();
            content.gameObject.SetActive(false);
            Hide();
            EventPipeline.Publish(new EventCloseMiniGamePanel());
        }

        public void Show() => MoveTo(_shownPosition, true);
        public void Hide() => MoveTo(_hiddenPosition, false);

        private void MoveTo(Vector2 target, bool show)
        {
            // 打断旧动画，从当前位置继续，避免快速切换时跳回起点。
            StopAnimation();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            if (show)
                _canvasGroup.alpha = 1f;

            if (_duration <= 0f)
            {
                CompleteAnimation(target, show);
                return;
            }

            _animation = StartCoroutine(Animate(target, show));
        }

        private IEnumerator Animate(Vector2 target, bool show)
        {
            Vector2 start = _rect.anchoredPosition;
            float elapsed = 0f;
            float duration = _duration;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 缓出：开始较快，接近终点时逐渐减速。
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                _rect.anchoredPosition = Vector2.Lerp(start, target, eased);
                yield return null;
            }

            CompleteAnimation(target, show);
            _animation = null;
        }

        private void CompleteAnimation(Vector2 target, bool show)
        {
            _rect.anchoredPosition = target;
            _canvasGroup.alpha = show ? 1f : 0f;
            _canvasGroup.interactable = show;
            _canvasGroup.blocksRaycasts = show;
        }

        private void StopAnimation()
        {
            if (_animation == null)
                return;

            StopCoroutine(_animation);
            _animation = null;
        }

        private void SetHidden()
        {
            CompleteAnimation(_hiddenPosition, false);
        }
    }

}
