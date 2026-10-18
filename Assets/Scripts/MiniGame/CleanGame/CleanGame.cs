using TapTapGameJam.Audio;
using System;
using TapTapGameJam.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 保留当前 CleanGame 脚本及 GUID。
public class CleanGame : MiniGameContent, IPointerDownHandler, IDragHandler, IPointerUpHandler,
    IPointerMoveHandler, IPointerEnterHandler, IPointerExitHandler, IEndDragHandler,
    IInitializePotentialDragHandler
{
    [Header("美术与擦拭工具")]
    [SerializeField] private RawImage _stainImage;
    [SerializeField] private Texture2D _stainTexture;
    [Tooltip("轴心为抹布擦拭中心，手臂和抹布均为其子物体。")]
    [SerializeField] private RectTransform _wipeTool;
    [SerializeField] private TMP_Text _progressText;
    [SerializeField] private Slider _progressBar;
    [SerializeField, Range(0.01f, 1f)] private float _completionThreshold = 0.9f;
    [SerializeField, Min(1f)] private float _brushRadius = 48f;
    private readonly System.Random _layoutRandom = new System.Random();
    private Texture2D _workingTexture;
    private Color32[] _pixels;
    private float _lastWipeMotion = -100f;
    private WipeMask _mask;
    private Action _onCompleted;
    private bool _running, _wiping, _hasLastPoint;
    private int _pointer;
    private Vector2 _last;
    private Vector3 _toolRestPosition;
    private bool _toolCached;

    public bool IsConfigured => _stainImage != null && _stainTexture != null && _wipeTool != null;

    public override void Begin(Action onCompleted)
    {
        if (_wipeTool != null && !_toolCached)
        {
            _toolRestPosition = _wipeTool.localPosition;
            _toolCached = true;
        }
        ResetView();
        if (!IsConfigured) throw new InvalidOperationException("请绑定污渍 RawImage、污渍素材与手臂抹布组合。");
        BuildTexture();
        _onCompleted = onCompleted;
        _running = true;
        _wipeTool.gameObject.SetActive(true);
        RefreshProgress();
    }

    private void BuildTexture()
    {
        ReleaseTexture();
        const int width = 512;
        Rect surface = _stainImage.rectTransform.rect;
        int height = Mathf.Clamp(Mathf.RoundToInt(width * surface.height / Mathf.Max(1f, surface.width)), 64, 512);
        // GPU 读回一次源图，素材不需要开启 Read/Write，也不会改动原始资源。
        const int sourceWidth = 256;
        int sourceHeight = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * (float)_stainTexture.height / _stainTexture.width));
        var source = new Texture2D(sourceWidth, sourceHeight, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        var temp = RenderTexture.GetTemporary(sourceWidth, sourceHeight, 0, RenderTextureFormat.ARGB32);
        try
        {
            Graphics.Blit(_stainTexture, temp);
            RenderTexture.active = temp;
            source.ReadPixels(new Rect(0, 0, sourceWidth, sourceHeight), 0, 0);
            _pixels = StainPattern.Compose(source.GetPixels32(), sourceWidth, sourceHeight,
                width, height, _layoutRandom.Next());
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temp);
            ReleaseObject(source);
        }
        _workingTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "CleanGame generated stains", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        var alpha = new byte[_pixels.Length];
        for (int i = 0; i < alpha.Length; i++) alpha[i] = _pixels[i].a;
        _mask = new WipeMask(width, height, alpha);
        _workingTexture.SetPixels32(_pixels);
        _workingTexture.Apply();
        _stainImage.texture = _workingTexture;
    }

    public void OnPointerDown(PointerEventData evt)
    {
        if (!_running || _wiping || evt.button != PointerEventData.InputButton.Left || !TryPoint(evt, out var point)) return;
        MoveTool(point);
        _wiping = true;
        _pointer = evt.pointerId;
        _last = point;
        _hasLastPoint = true;
        Erase(point);
    }
    public void OnInitializePotentialDrag(PointerEventData evt) => evt.useDragThreshold = false;
    public void OnDrag(PointerEventData evt)
    {
        if (!_running || !_wiping || evt.pointerId != _pointer) return;
        if (!TryPoint(evt, out var point)) { PauseStroke(); return; }
        ContinueStroke(point);
    }
    private void ContinueStroke(Vector2 point)
    {
        MoveTool(point);
        // 保留按住状态，但离开后重入时从当前接触点重新采样，不跨空白连线。
        if (!_hasLastPoint) _last = point;
        _hasLastPoint = true;
        if ((point - _last).sqrMagnitude > 0.01f) _lastWipeMotion = Time.unscaledTime;
        Erase(point);
    }
    public void OnPointerMove(PointerEventData evt)
    {
        if (_running && !_wiping && TryPoint(evt, out var point)) MoveTool(point);
    }
    public void OnPointerEnter(PointerEventData evt)
    {
        if (_running && _wiping && evt.pointerId == _pointer && TryPoint(evt, out var point))
            ContinueStroke(point);
        else OnPointerMove(evt);
    }
    public void OnPointerExit(PointerEventData evt)
    {
        if (_wiping && evt.pointerId != _pointer) return;
        PauseStroke();
    }
    private void PauseStroke()
    {
        _hasLastPoint = false;
        _lastWipeMotion = -100f;
        if (_wipeTool != null) _wipeTool.gameObject.SetActive(false);
    }
    public void OnPointerUp(PointerEventData evt) { if (evt.pointerId == _pointer) StopWiping(); }
    public void OnEndDrag(PointerEventData evt) { if (evt.pointerId == _pointer) StopWiping(); }
    private void StopWiping() { _wiping = false; _hasLastPoint = false; _lastWipeMotion = -100f; }
    private void LateUpdate() => ActionSound.SetPlaying(SoundId.Wipe,
        _running && _wiping && _hasLastPoint && Time.unscaledTime - _lastWipeMotion < 0.08f);
    private bool TryPoint(PointerEventData evt, out Vector2 point)
    {
        point = default;
        if (_mask == null || _stainImage == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _stainImage.rectTransform, evt.position, evt.pressEventCamera ?? evt.enterEventCamera, out var local)) return false;
        Rect rect = _stainImage.rectTransform.rect;
        if (rect.width <= 0 || rect.height <= 0 || !rect.Contains(local)) return false;
        point = new Vector2((local.x - rect.xMin) / rect.width * (_mask.Width - 1),
            (local.y - rect.yMin) / rect.height * (_mask.Height - 1));
        return true;
    }
    private void MoveTool(Vector2 point)
    {
        Rect rect = _stainImage.rectTransform.rect;
        Vector3 local = new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, point.x / (_mask.Width - 1)),
            Mathf.Lerp(rect.yMin, rect.yMax, point.y / (_mask.Height - 1)), 0f);
        _wipeTool.position = _stainImage.rectTransform.TransformPoint(local);
        _wipeTool.gameObject.SetActive(true);
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
    public override void End()
    {
        ActionSound.StopAll();
        StopWiping();
        _running = false;
        _onCompleted = null;
        if (_wipeTool != null) _wipeTool.gameObject.SetActive(false);
    }
    public override void ResetView()
    {
        End();
        ReleaseTexture();
        if (_wipeTool != null && _toolCached) _wipeTool.localPosition = _toolRestPosition;
        if (_progressText != null) _progressText.text = "按住鼠标左键擦除污渍";
        if (_progressBar != null) _progressBar.SetValueWithoutNotify(0f);
    }
    private void ReleaseTexture()
    {
        if (_stainImage != null) _stainImage.texture = null;
        ReleaseObject(_workingTexture);
        _workingTexture = null;
        _mask = null;
        _pixels = null;
    }
    private static void ReleaseObject(UnityEngine.Object item)
    {
        if (item == null) return;
        if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
    }
    private void OnDisable() => End();
    private void OnDestroy() => ReleaseTexture();
    private void OnApplicationFocus(bool focused) { if (!focused) StopWiping(); }
    private void OnApplicationPause(bool paused) { if (paused) StopWiping(); }
}
