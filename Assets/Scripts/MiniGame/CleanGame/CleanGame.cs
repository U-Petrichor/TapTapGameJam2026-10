using TapTapGameJam.Audio;
using System;
using TapTapGameJam.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 保留当前 CleanGame 脚本及 GUID。
public class CleanGame : MiniGameContent, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RawImage _stainImage;
    [SerializeField] private Texture2D _stainTexture;
    [SerializeField] private TMP_Text _progressText;
    [SerializeField] private Slider _progressBar;
    [SerializeField, Range(0.01f, 1f)] private float _completionThreshold = 0.9f;
    [SerializeField, Min(1f)] private float _brushRadius = 35f;
    private Texture2D _workingTexture;
    private Color32[] _pixels;
    private float _lastWipeMotion = -100f;
    private WipeMask _mask;
    private Action _onCompleted;
    private bool _running, _wiping;
    private int _pointer;
    private Vector2 _last;

    public bool IsConfigured => _stainImage != null;
    public void Configure(RawImage stain, TMP_Text text, Slider progress)
    {
        _stainImage = stain; _progressText = text; _progressBar = progress;
    }

    public override void Begin(Action onCompleted)
    {
        ResetView();
        if (_stainImage == null) throw new InvalidOperationException("请绑定污渍 RawImage。");
        BuildTexture();
        _onCompleted = onCompleted;
        _running = true;
        RefreshProgress();
        CheckComplete();
    }

    private void BuildTexture()
    {
        ReleaseTexture();
        const int width = 384, height = 216;
        _workingTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        if (_stainTexture != null)
        {
            var previous = RenderTexture.active;
            var temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(_stainTexture, temp);
                RenderTexture.active = temp;
                _workingTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                _workingTexture.Apply();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temp); }
            _pixels = _workingTexture.GetPixels32();
        }
        else
        {
            _pixels = new Color32[width * height];
            // 无美术资源时也能试玩；使用局部随机源，不影响任务和股价随机序列。
            var random = new System.Random(73);
            for (int spot = 0; spot < 22; spot++)
            {
                float cx = random.Next(25, width - 25), cy = random.Next(20, height - 20);
                float rx = random.Next(15, 48), ry = random.Next(12, 32);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if ((x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) < 1f)
                            _pixels[y * width + x] = new Color32(92, 75, 52, 220);
            }
        }
        var alpha = new byte[_pixels.Length];
        for (int i = 0; i < alpha.Length; i++) alpha[i] = _pixels[i].a;
        _mask = new WipeMask(width, height, alpha);
        _workingTexture.SetPixels32(_pixels);
        _workingTexture.Apply();
        _stainImage.texture = _workingTexture;
    }

    public void OnPointerDown(PointerEventData evt)
    {
        if (!_running || evt.button != PointerEventData.InputButton.Left || !TryPoint(evt, out var point)) return;
        _wiping = true;
        _pointer = evt.pointerId;
        _last = point;
        Erase(point);
    }
    public void OnDrag(PointerEventData evt)
    {
        if (_wiping && evt.pointerId == _pointer && TryPoint(evt, out var point))
        {
            if ((point - _last).sqrMagnitude > 0.01f) _lastWipeMotion = Time.unscaledTime;
            Erase(point);
        }
    }
    public void OnPointerUp(PointerEventData evt) { if (evt.pointerId == _pointer) _wiping = false; }
    private void LateUpdate() => ActionSound.SetPlaying(SoundId.Wipe,
        _running && _wiping && Time.unscaledTime - _lastWipeMotion < 0.08f);
    private bool TryPoint(PointerEventData evt, out Vector2 point)
    {
        point = default;
        if (_stainImage == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _stainImage.rectTransform, evt.position, evt.pressEventCamera, out var local)) return false;
        Rect rect = _stainImage.rectTransform.rect;
        if (rect.width <= 0 || rect.height <= 0) return false;
        point = new Vector2((local.x - rect.xMin) / rect.width * (_mask.Width - 1),
            (local.y - rect.yMin) / rect.height * (_mask.Height - 1));
        return true;
    }
    private void Erase(Vector2 point)
    {
        if (!_running) return;
        float radius = _brushRadius * _mask.Width / _stainImage.rectTransform.rect.width;
        bool changed = _mask.EraseSegment(_last.x, _last.y, point.x, point.y, radius);
        _last = point;
        if (!changed) return;
        for (int i = 0; i < _pixels.Length; i++) if (!_mask.IsDirty(i)) _pixels[i].a = 0;
        _workingTexture.SetPixels32(_pixels);
        _workingTexture.Apply(false);
        RefreshProgress();
        CheckComplete();
    }
    private void CheckComplete()
    {
        if (!_running || _mask.Progress + 0.000001f < Mathf.Clamp(_completionThreshold, 0.01f, 1f)) return;
        var completed = _onCompleted;
        End();
        completed?.Invoke();
    }
    private void RefreshProgress()
    {
        if (_progressBar != null) _progressBar.SetValueWithoutNotify(_mask.Progress);
        if (_progressText != null) _progressText.text = $"已清理 {_mask.Progress:P0} · 目标 {_completionThreshold:P0}";
    }
    public override void End() { ActionSound.StopAll(); _lastWipeMotion = -100f; _running = _wiping = false; _onCompleted = null; }
    public override void ResetView()
    {
        End();
        ReleaseTexture();
        if (_progressText != null) _progressText.text = "按住鼠标左键擦除污渍";
        if (_progressBar != null) _progressBar.SetValueWithoutNotify(0f);
    }
    private void ReleaseTexture()
    {
        if (_stainImage != null) _stainImage.texture = null;
        if (_workingTexture != null) Destroy(_workingTexture);
        _workingTexture = null;
        _mask = null;
        _pixels = null;
    }
    private void OnDisable() => End();
    private void OnDestroy() => ReleaseTexture();
    private void OnApplicationFocus(bool focused) { if (!focused) _wiping = false; }
}
