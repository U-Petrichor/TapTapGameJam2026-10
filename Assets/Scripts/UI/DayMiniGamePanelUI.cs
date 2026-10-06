using System.Collections;
using TapTapGameJam.Events;
using UnityEngine.UI;
using UnityEngine;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class DayMiniGamePanelUI : MonoBehaviour
{
    [SerializeField] private int _miniGameIndex = 0;
    [SerializeField] private Vector2 _shownPosition = new Vector2(0f, 100f);
    [SerializeField] private Vector2 _hiddenPosition = new Vector2(0f, -200f);
    [SerializeField, Min(0f)] private float _duration = 0.3f;
    [SerializeField] private Button ExitButton;
    private System.Action<bool> _actionOnFinished;

    private RectTransform _rect;
    private CanvasGroup _canvasGroup;
    private Coroutine _animation;
    

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (ExitButton == null)
        {
            Debug.LogError($"[DayMiniGamePanelUI]：没有绑定ExitButton");
        }
        SetHidden();
    }

    private void OnEnable()
    {
        EventPipeline.Subscribe<EventSwitchMiniGamePanel>(OnSwitchMiniGamePanel);
        ExitButton.onClick.AddListener(OnMiniGamePanelClose);
    }

    private void OnDisable()
    {
        EventPipeline.UnSubscribe<EventSwitchMiniGamePanel>(OnSwitchMiniGamePanel);
        StopAnimation();
        SetHidden();
        ExitButton.onClick.RemoveAllListeners();
    }

    private void OnSwitchMiniGamePanel(EventSwitchMiniGamePanel evt)
    {
        if (evt.MiniGameIndex != _miniGameIndex)
            return;

        if (evt.IsStart)
        {
            _actionOnFinished = evt.OnFinished; // 记录此游戏完成时的处理方法
            Show();
        }
           
        else
            Hide();
    }
    private void OnMiniGamePanelClose()
    {
        if (_actionOnFinished != null)
        {
            // 先执行，然后置为空
            _actionOnFinished(false);
            _actionOnFinished = null;
        }
        else
        {
            // 本身就为空，不需要额外处理
        }
        // 固定关闭当前UI
        Hide();
    }
    private void OnCurrentMiniGameFinish()
    {
        if (_actionOnFinished != null)
        {
            // 先执行，然后置为空
            _actionOnFinished(true);
            _actionOnFinished = null;
        }
        else
        {
            // 理论上不该出现这个问题
            Debug.LogError($"[DayMiniGamePanelUI]:当前小游戏可能没有传FinishAction，或链路出现问题");
        }
        // 固定关闭当前UI
        Hide();
    }

    public void Show() => MoveTo(_shownPosition, true);
    public void Hide() => MoveTo(_hiddenPosition, false);

    private void MoveTo(Vector2 target, bool show)
    {
        if (!isActiveAndEnabled)
            return;

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
