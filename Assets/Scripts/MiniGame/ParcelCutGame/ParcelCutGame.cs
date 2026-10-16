using System;
using System.Collections.Generic;
using TMPro;
using TapTapGameJam.MiniGame;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// Drag the cutter along three randomly selected tape paths.
    /// The tape artwork remains independent from the rule class so the paths
    /// can be tuned in the editor without changing completion logic.
    /// </summary>
    public sealed class ParcelCutGame : MiniGameContent,
        IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        [Serializable]
        private sealed class TapeEntry
        {
            public RawImage Image;
            public Vector2[] Path;
            public Vector2[] Outline;
        }

        [SerializeField] private RawImage _boxImage;
        [SerializeField] private RawImage _cutterBlade;
        [SerializeField] private RawImage _cutterHandle;
        [SerializeField] private List<TapeEntry> _tapes = new();
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _instructionText;
        [SerializeField] private Slider _progressBar;
        [SerializeField, Range(0.03f, 0.4f)] private float _pathTolerance = 0.25f;
        [Tooltip("覆盖此比例的不同判定点即可剪开；60% 对应 32 个点中的至少 20 个。")]
        [SerializeField, Range(0.1f, 1f)] private float _completionThreshold = ParcelCutRound.DefaultCompletionThreshold;
        [Header("判定点显示")]
        [Tooltip("橙色表示未覆盖，绿色表示已覆盖；显示的点与实际剪切判定一致。")]
        [SerializeField] private bool _showCheckpoints = true;
        [SerializeField, Min(1f)] private float _checkpointRadius = 5f;

        private ParcelCutCheckpointGraphic _checkpointGraphic;
        private RectTransform[] _checkpointTapeRects;

        private ParcelCutRound _round;
        private Action _onCompleted;
        private bool _running;
        private int _draggingTape = -1;
        private int _pointerId;
        private Camera _eventCamera;
        private Vector2 _lastPoint;

        public bool IsConfigured
        {
            get
            {
                if (_boxImage == null || _cutterBlade == null || _cutterHandle == null ||
                    _progressText == null || _instructionText == null || _progressBar == null ||
                    _tapes == null || _tapes.Count != ParcelCutRound.TapeCount)
                    return false;
                foreach (var tape in _tapes)
                    if (tape == null || tape.Image == null || tape.Path == null || tape.Path.Length < 2) return false;
                return true;
            }
        }

        public bool HasTapeGeometry
        {
            get
            {
                if (_tapes == null || _tapes.Count != ParcelCutRound.TapeCount) return false;
                foreach (var tape in _tapes)
                    if (tape == null || tape.Path == null || tape.Path.Length < 2 ||
                        tape.Outline == null || tape.Outline.Length < 3) return false;
                return true;
            }
        }

        public void Configure(RawImage box, RawImage blade, RawImage handle, RawImage[] tapes,
            TMP_Text progress, TMP_Text instruction, Slider progressBar = null)
        {
            _boxImage = box;
            _cutterBlade = blade;
            _cutterHandle = handle;
            _progressText = progress;
            _instructionText = instruction;
            _progressBar = progressBar;
            if (_tapes == null) _tapes = new List<TapeEntry>();
            while (_tapes.Count < ParcelCutRound.TapeCount) _tapes.Add(new TapeEntry());
            if (_tapes.Count > ParcelCutRound.TapeCount)
                _tapes.RemoveRange(ParcelCutRound.TapeCount, _tapes.Count - ParcelCutRound.TapeCount);
            for (int i = 0; i < ParcelCutRound.TapeCount; i++)
            {
                _tapes[i].Image = tapes != null && i < tapes.Length ? tapes[i] : null;
                if (_tapes[i].Image != null) _tapes[i].Image.raycastTarget = true;
            }
#if UNITY_EDITOR
            RebuildTapeGeometry();
#endif
        }

#if UNITY_EDITOR
        // Bake from source PNGs, independent of importer resizing/compression/readability.
        public void RebuildTapeGeometry()
        {
            if (_tapes == null || _tapes.Count != ParcelCutRound.TapeCount)
                throw new InvalidOperationException("请先绑定六张胶带图片。");
            var geometry = new ParcelTapeGeometry[ParcelCutRound.TapeCount];
            for (int i = 0; i < _tapes.Count; i++)
            {
                var image = _tapes[i].Image;
                string path = image != null ? UnityEditor.AssetDatabase.GetAssetPath(image.texture) : null;
                if (string.IsNullOrEmpty(path)) throw new InvalidOperationException($"胶带 {i + 1} 缺少图片资源。");
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!ImageConversion.LoadImage(texture, System.IO.File.ReadAllBytes(path)))
                        throw new InvalidOperationException($"无法读取胶带图片：{path}");
                    Color32[] pixels = texture.GetPixels32();
                    var alpha = new byte[pixels.Length];
                    for (int p = 0; p < pixels.Length; p++) alpha[p] = pixels[p].a;
                    geometry[i] = ParcelTapeGeometry.Extract(texture.width, texture.height, alpha);
                }
                finally { DestroyImmediate(texture); }
            }
            for (int i = 0; i < _tapes.Count; i++)
            {
                _tapes[i].Path = geometry[i].Path;
                _tapes[i].Outline = geometry[i].Outline;
            }
        }
#endif

        public override void Begin(Action onCompleted)
        {
            ResetView();
#if UNITY_EDITOR
            if (!HasTapeGeometry) RebuildTapeGeometry();
#endif
            if (!IsConfigured)
                throw new InvalidOperationException("请配置箱子、剪刀、六条胶带和拆快递文字组件。");

            var paths = new List<IReadOnlyList<Vector2>>(ParcelCutRound.TapeCount);
            var outlines = new List<IReadOnlyList<Vector2>>(ParcelCutRound.TapeCount);
            foreach (var tape in _tapes) { paths.Add(tape.Path); outlines.Add(tape.Outline); }
            if (!HasTapeGeometry) throw new InvalidOperationException("请在编辑器中校准胶带判定路径并保存场景。");
            _round = new ParcelCutRound(paths,
                new System.Random(UnityEngine.Random.Range(0, int.MaxValue)),
                Mathf.Clamp(_pathTolerance, 0.03f, 0.4f),
                Mathf.Clamp(_completionThreshold, 0.1f, 1f), outlines);
            _onCompleted = onCompleted;
            _running = true;
            _draggingTape = -1;
            RenderTapes();
            PrepareCheckpoints();
            RefreshText();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_running || _draggingTape >= 0 || eventData.button != PointerEventData.InputButton.Left) return;
            int tape = FindTape(eventData.position, eventData.pressEventCamera);
            if (tape < 0 || !_round.IsTarget(tape) || _round.IsCompleted(tape)) return;
            if (!TryGetNormalizedPoint(tape, eventData.position, eventData.pressEventCamera, out _lastPoint)) return;
            _draggingTape = tape;
            _pointerId = eventData.pointerId;
            _eventCamera = eventData.pressEventCamera;
            MoveCutter(eventData.position);
            RefreshText();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_running || _draggingTape < 0 || eventData.pointerId != _pointerId) return;
            if (!TryGetNormalizedPoint(_draggingTape, eventData.position, _eventCamera, out Vector2 current)) return;
            bool changed = _round.TryCutSegment(_draggingTape, _lastPoint, current,
                _tapes[_draggingTape].Image.rectTransform.rect.size);
            _lastPoint = current;
            MoveCutter(eventData.position);
            if (changed)
            {
                RefreshText();
                if (_round.IsCompleted(_draggingTape))
                {
                    _tapes[_draggingTape].Image.gameObject.SetActive(false);
                    _tapes[_draggingTape].Image.raycastTarget = false;
                    _draggingTape = -1;
                    RefreshText();
                    if (_round.IsComplete)
                    {
                        var completed = _onCompleted;
                        End();
                        completed?.Invoke();
                    }
                    else
                    {
                        _instructionText.text = "继续剪下一条胶带 · 每条覆盖 60% 即可";
                    }
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) ReleasePointer();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) ReleasePointer();
        }

        private int FindTape(Vector2 screen, Camera camera)
        {
            int best = -1, topSibling = -1;
            for (int i = 0; i < _tapes.Count; i++)
            {
                var image = _tapes[i].Image;
                if (image == null || !image.gameObject.activeInHierarchy || !_round.IsTarget(i) || _round.IsCompleted(i)) continue;
                if (!TryGetNormalizedPoint(i, screen, camera, out var point) ||
                    !ParcelTapeGeometry.Contains(_tapes[i].Outline, point)) continue;
                int sibling = image.transform.GetSiblingIndex();
                if (sibling > topSibling) { best = i; topSibling = sibling; }
            }
            return best;
        }

        private bool TryGetNormalizedPoint(int tapeIndex, Vector2 screen, Camera camera, out Vector2 normalized)
        {
            normalized = default;
            RectTransform rect = _tapes[tapeIndex].Image.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out Vector2 local)) return false;
            Rect bounds = rect.rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return false;
            normalized = new Vector2(
                (local.x - bounds.xMin) / bounds.width,
                (local.y - bounds.yMin) / bounds.height);
            return true;
        }

        private void MoveCutter(Vector2 screen)
        {
            if (_cutterBlade == null || _cutterHandle == null) return;
            RectTransform root = transform as RectTransform;
            if (root == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, _eventCamera, out Vector2 local)) return;
            _cutterBlade.rectTransform.anchoredPosition = local + new Vector2(18f, 12f);
            _cutterHandle.rectTransform.anchoredPosition = local + new Vector2(-4f, -18f);
        }

        private void RenderTapes()
        {
            for (int i = 0; i < _tapes.Count; i++)
            {
                var image = _tapes[i].Image;
                if (image == null) continue;
                bool target = _round != null && _round.IsTarget(i);
                image.gameObject.SetActive(target);
                image.color = Color.white;
                image.raycastTarget = target;
            }
        }

        private void PrepareCheckpoints()
        {
            if (_checkpointGraphic == null)
            {
                var existing = transform.Find("CutCheckpoints");
                if (existing != null) _checkpointGraphic = existing.GetComponent<ParcelCutCheckpointGraphic>();
            }
            if (_checkpointGraphic == null)
            {
                var go = new GameObject("CutCheckpoints", typeof(RectTransform), typeof(CanvasRenderer), typeof(ParcelCutCheckpointGraphic));
                go.layer = gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                _checkpointGraphic = go.GetComponent<ParcelCutCheckpointGraphic>();
            }
            // Repair objects created by the previous version as well.
            if (_checkpointGraphic.GetComponent<CanvasRenderer>() == null)
                _checkpointGraphic.gameObject.AddComponent<CanvasRenderer>();
            _checkpointGraphic.transform.SetAsLastSibling();
            _checkpointTapeRects = new RectTransform[_tapes.Count];
            for (int i = 0; i < _tapes.Count; i++) _checkpointTapeRects[i] = _tapes[i].Image.rectTransform;
            RefreshCheckpoints();
        }

        private void RefreshCheckpoints()
        {
            if (_checkpointGraphic == null) return;
            bool show = _running && _showCheckpoints;
            _checkpointGraphic.gameObject.SetActive(show);
            if (show) _checkpointGraphic.Configure(_round, _checkpointTapeRects, _checkpointRadius);
        }

        // Also follows edits to tape transforms and the display toggle during Play.
        private void LateUpdate() => RefreshCheckpoints();

        private void RefreshText()
        {
            if (_round == null) return;
            if (_progressText != null)
                _progressText.text = $"已拆 {_round.CompletedCount} / {ParcelCutRound.RequiredTapeCount}";
            if (_progressBar != null)
            {
                float progress = 0f;
                foreach (int tape in _round.TargetIndices)
                    progress += _round.IsCompleted(tape) ? 1f : Mathf.Clamp01(_round.CoveredCountFor(tape) / (float)_round.RequiredCheckpointCount);
                _progressBar.SetValueWithoutNotify(progress / ParcelCutRound.RequiredTapeCount);
            }
            if (_instructionText != null)
                _instructionText.text = _draggingTape >= 0
                    ? $"当前胶带：{_round.CoverageFor(_draggingTape):P0} · 目标 {_completionThreshold:P0}"
                    : $"沿胶带拖动剪刀 · 每条覆盖 {_completionThreshold:P0} 即可";
        }

        private void ReleasePointer()
        {
            _draggingTape = -1;
            _eventCamera = null;
        }

        public override void End()
        {
            _running = false;
            _onCompleted = null;
            ReleasePointer();
            _round?.Stop();
            if (_checkpointGraphic != null)
            {
                _checkpointGraphic.Configure(null, null, _checkpointRadius);
                _checkpointGraphic.gameObject.SetActive(false);
            }
        }

        public override void ResetView()
        {
            End();
            _round = null;
            if (_tapes != null)
            {
                foreach (var tape in _tapes)
                {
                    if (tape == null || tape.Image == null) continue;
                    tape.Image.gameObject.SetActive(true);
                    tape.Image.color = Color.white;
                    tape.Image.raycastTarget = true;
                }
            }
            if (_progressText != null) _progressText.text = "已拆 0 / 3";
            if (_instructionText != null) _instructionText.text = "按住鼠标左键，沿黄色胶带拖动剪刀";
            if (_progressBar != null) _progressBar.SetValueWithoutNotify(0f);
        }

        private void OnDisable() => End();
        private void OnDestroy() => End();
        private void OnApplicationFocus(bool focused) { if (!focused) ReleasePointer(); }
    }
}
