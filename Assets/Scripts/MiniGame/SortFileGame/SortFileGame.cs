using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TapTapGameJam.UI
{
    public class SortFileGame : MiniGameContent
    {
        [Serializable]
        private class DepartmentArea
        {
            public SortFileDepartment Department;
            public RectTransform Area;
            public Color FileColor = Color.white;
            public TMP_Text CountText;
        }

        [SerializeField] private List<DepartmentArea> _departments = new List<DepartmentArea>();
        [SerializeField] private RectTransform _fileStackRoot;
        [SerializeField] private RectTransform _dragRoot;
        [SerializeField] private SortFileCard _fileTemplate;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField, Min(1)] private int _filesPerDepartment = 3;
        [SerializeField] private bool _shuffle = true;
        [SerializeField] private Vector2 _stackOffset = new Vector2(3f, 3f);
        [SerializeField] private Vector2 _sortedOffset = new Vector2(4f, -5f);
        [SerializeField, Min(0f)] private float _moveDuration = 0.18f;

        private readonly List<SortFileCard> _cards = new List<SortFileCard>();
        private readonly DepartmentArea[] _areaByDepartment = new DepartmentArea[4];
        private SortFileRound _round;
        private Action _onCompleted;
        private Coroutine _motion;
        private SortFileCard _activeCard;
        private bool _running;

        public RectTransform DragRoot => _dragRoot;

        private bool ValidateSetup()
        {
            if (_fileStackRoot == null || _dragRoot == null || _fileTemplate == null
                || _fileStackRoot == _dragRoot || _fileTemplate.gameObject.activeSelf
                || _filesPerDepartment < 1)
            {
                Debug.LogError("[SortFileGame] 请绑定文件堆、拖动层和默认禁用的文件模板。", this);
                return false;
            }
            Array.Clear(_areaByDepartment, 0, _areaByDepartment.Length);
            var usedAreas = new HashSet<RectTransform>();
            foreach (var entry in _departments)
            {
                int index = entry == null ? -1 : (int)entry.Department;
                if (index < 0 || index >= 4 || entry.Area == null
                    || _areaByDepartment[index] != null || !usedAreas.Add(entry.Area))
                {
                    Debug.LogError("[SortFileGame] 四个部门需要各自独立、无重复的接收区域。", this);
                    return false;
                }
                _areaByDepartment[index] = entry;
            }
            foreach (var entry in _areaByDepartment)
            {
                if (entry != null) continue;
                Debug.LogError("[SortFileGame] 缺少部门区域绑定。", this);
                return false;
            }
            return true;
        }

        public override void Begin(Action onCompleted)
        {
            ResetView();
            if (!ValidateSetup()) return;
            var random = _shuffle ? new System.Random(UnityEngine.Random.Range(0, int.MaxValue)) : null;
            _round = new SortFileRound(SortFileRound.CreateBalanced(_filesPerDepartment, random));
            _onCompleted = onCompleted;

            for (int i = 0; i < _round.TotalCount; i++)
            {
                var card = Instantiate(_fileTemplate, _fileStackRoot);
                var rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = _stackOffset * (_round.TotalCount - i - 1);
                // 第 0 份是栈顶，放到 UI 绘制顺序的最上面。
                rect.SetAsFirstSibling();
                SortFileDepartment department = _round.DepartmentAt(i);
                card.Initialize(this, i, department, _areaByDepartment[(int)department].FileColor);
                card.gameObject.SetActive(true);
                _cards.Add(card);
            }
            _running = true;
            EnableTopCard();
            UpdateLabels();
        }

        public bool TryBeginDrag(SortFileCard card)
        {
            if (!_running || _activeCard != null || card == null
                || !_round.CanDrag(card.Index) || _cards[card.Index] != card) return false;
            _activeCard = card;
            return true;
        }

        public void Drop(SortFileCard card, Camera eventCamera)
        {
            if (!_running || card != _activeCard || _motion != null) return;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(eventCamera,
                card.Rect.TransformPoint(card.Rect.rect.center));
            DepartmentArea target = null;
            foreach (var entry in _areaByDepartment)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(entry.Area, center, eventCamera)) continue;
                // 重叠区域不猜测目标，按无效投放处理。
                if (target != null) { target = null; break; }
                target = entry;
            }

            bool correct = target != null && target.Department == _round.DepartmentAt(card.Index);
            if (!correct)
            {
                if (_statusText != null)
                    _statusText.text = "请放到" + SortFileRound.DepartmentName(_round.DepartmentAt(card.Index)) + "部门";
                _motion = StartCoroutine(MoveCard(card, card.HomeWorldPosition, () =>
                {
                    card.RestoreHome();
                    _activeCard = null;
                    EnableTopCard();
                }));
                return;
            }

            int count = _round.CountFor(target.Department);
            Vector2 offset = _sortedOffset * count;
            Vector3 destination = target.Area.TransformPoint(target.Area.rect.center + offset);
            _motion = StartCoroutine(MoveCard(card, destination, () =>
            {
                card.PlaceIn(target.Area, offset);
                _round.TrySort(card.Index, target.Department);
                _activeCard = null;
                UpdateLabels();
                if (_round.IsComplete)
                {
                    Action completed = _onCompleted;
                    End();
                    completed?.Invoke();
                }
                else EnableTopCard();
            }));
        }

        private IEnumerator MoveCard(SortFileCard card, Vector3 destination, Action onArrived)
        {
            Vector3 start = card.Rect.position;
            float elapsed = 0f;
            // 至少等待一帧，避免同步回调与 StartCoroutine 返回值相互覆盖。
            do
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                float t = _moveDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / _moveDuration);
                card.Rect.position = Vector3.Lerp(start, destination, 1f - Mathf.Pow(1f - t, 3f));
            } while (elapsed < _moveDuration);
            _motion = null;
            onArrived();
        }

        private void EnableTopCard()
        {
            foreach (var card in _cards)
                card.SetDraggable(_running && _activeCard == null && _round.CanDrag(card.Index));
        }

        private void UpdateLabels()
        {
            if (_statusText != null)
                _statusText.text = $"已整理 {_round?.SortedCount ?? 0} / {_round?.TotalCount ?? 0}";
            foreach (var entry in _departments)
            {
                if (entry?.CountText != null)
                    entry.CountText.text = SortFileRound.DepartmentName(entry.Department)
                        + $"  {_round?.CountFor(entry.Department) ?? 0}";
            }
        }

        public void AbortDrag()
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = null;
            if (_activeCard != null) _activeCard.RestoreHome();
            _activeCard = null;
            EnableTopCard();
        }

        public override void End()
        {
            _running = false;
            _onCompleted = null;
            _round?.Stop();
            AbortDrag();
        }

        public override void ResetView()
        {
            End();
            foreach (var card in _cards)
            {
                if (card == null) continue;
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
            _cards.Clear();
            _round = null;
            UpdateLabels();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _running) AbortDrag();
        }

        private void OnDisable() => End();
    }
}
