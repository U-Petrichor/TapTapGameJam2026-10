using TapTapGameJam.Audio;
using System;
using System.Collections.Generic;
using TapTapGameJam.Cockroach;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using TapTapGameJam.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TapTapGameJam.Core.Disinfection
{
    /// <summary>
    /// Coordinates the once-per-day daytime disinfection warning and execution.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DisinfectionManager : MonoBehaviour
    {
        private enum DisinfectionState
        {
            Locked,
            Waiting,
            Warning,
            Finished
        }

        private struct RoomCandidate
        {
            public DisinfectionRoom Room;
            public int Weight;

            public RoomCandidate(DisinfectionRoom room, int weight)
            {
                Room = room;
                Weight = weight;
            }
        }

        [Header("解锁与时间")]
        [Tooltip("EventNewDay.DayCount 的解锁阈值。当前项目第一天是 0，第二天是 1。")]
        [SerializeField] private int _unlockDayCount = 1;
        [SerializeField, Min(0f)] private float _warningDuration = 40f;
        [SerializeField] private Vector2 _randomDelayRange = new Vector2(30f, 45f);

        [Header("房间数量")]
        [SerializeField, Min(1)] private int _minimumRooms = 1;
        [SerializeField, Min(1)] private int _maximumRooms = 3;

        [Header("箭头")]
        [SerializeField] private GameObject _arrowPrefab;
        [SerializeField] private RectTransform _indicatorRoot;
        [SerializeField] private Camera _worldCamera;
        [SerializeField, Min(0f)] private float _edgePadding = 20f;

        [Header("调试")]
        [Tooltip("忽略第二天解锁限制，并跳过随机等待。仍需在白天且房间内有蟑螂。")]
        [SerializeField] private bool _startImmediatelyForDebug;

        private readonly List<DisinfectionRoom> _rooms = new List<DisinfectionRoom>();
        private readonly List<DisinfectionRoom> _selectedRooms = new List<DisinfectionRoom>();
        private readonly List<DisinfectionArrowView> _arrows = new List<DisinfectionArrowView>();

        private DisinfectionState _state = DisinfectionState.Locked;
        private bool _isDaytime;
        private bool _hasRunToday;
        private float _waitRemaining;
        private float _warningRemaining;

        private void Awake()
        {
            DiscoverRooms();
            ResolveIndicatorDefaults();
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<EventNewDay>(HandleNewDay);
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.Subscribe<EventGameOver>(HandleGameOver);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventNewDay>(HandleNewDay);
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.UnSubscribe<EventGameOver>(HandleGameOver);
            CancelVisuals();
        }

        private void Update()
        {
            if (!_isDaytime)
            {
                return;
            }

            if (_state == DisinfectionState.Waiting)
            {
                _waitRemaining -= Time.deltaTime;
                if (_waitRemaining <= 0f)
                {
                    StartWarning();
                }

                return;
            }

            if (_state != DisinfectionState.Warning)
            {
                return;
            }

            _warningRemaining -= Time.deltaTime;
            UpdateWarningViews();
            if (_warningRemaining <= 0f)
            {
                ExecuteDisinfection();
            }
        }

        private void HandleNewDay(EventNewDay evt)
        {
            _isDaytime = _startImmediatelyForDebug || evt.DayCount >= _unlockDayCount;
            CancelVisuals();
            _selectedRooms.Clear();
            _hasRunToday = false;

            if (!_isDaytime)
            {
                _state = DisinfectionState.Locked;
                return;
            }

            DiscoverRooms();
            _waitRemaining = _startImmediatelyForDebug
                ? 0f
                : Random.Range(Mathf.Min(_randomDelayRange.x, _randomDelayRange.y),
                    Mathf.Max(_randomDelayRange.x, _randomDelayRange.y));
            _state = DisinfectionState.Waiting;
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            _isDaytime = evt.NewPhase == GamePhase.Day && _state != DisinfectionState.Locked;

            if (evt.NewPhase != GamePhase.Day && _state == DisinfectionState.Warning)
            {
                // Disinfection is a daytime event. A phase transition cancels an unfinished warning.
                CancelVisuals();
                _selectedRooms.Clear();
                _state = _hasRunToday ? DisinfectionState.Finished : DisinfectionState.Locked;
            }
        }

        private void HandleGameOver(EventGameOver evt)
        {
            _isDaytime = false;
            CancelVisuals();
            _selectedRooms.Clear();
            _state = DisinfectionState.Locked;
        }

        private void DiscoverRooms()
        {
            _rooms.Clear();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                DisinfectionRoom room = child.GetComponent<DisinfectionRoom>();
                if (room == null)
                {
                    room = child.gameObject.AddComponent<DisinfectionRoom>();
                }

                room.CacheComponents();
                _rooms.Add(room);
            }
        }

        private void ResolveIndicatorDefaults()
        {
            if (_worldCamera == null)
            {
                _worldCamera = Camera.main;
            }

            if (_indicatorRoot == null)
            {
                TaskIndicatorUI existingIndicator = FindFirstObjectByType<TaskIndicatorUI>();
                if (existingIndicator != null)
                {
                    _indicatorRoot = existingIndicator.transform as RectTransform;
                }
            }
        }

        private void StartWarning()
        {
            if (_hasRunToday)
            {
                _state = DisinfectionState.Finished;
                return;
            }

            DiscoverRooms();
            List<CockNest> nests = FindNests();
            var candidates = new List<RoomCandidate>();

            for (int i = 0; i < _rooms.Count; i++)
            {
                int weight = _rooms[i].CountOccupiedNests(nests);
                if (weight > 0)
                {
                    candidates.Add(new RoomCandidate(_rooms[i], weight));
                }
            }

            _hasRunToday = true;
            if (candidates.Count == 0)
            {
                Debug.Log("[DisinfectionManager] 没有包含蟑螂巢穴的可用房间，今天跳过消杀。", this);
                _state = DisinfectionState.Finished;
                return;
            }

            int minRooms = Mathf.Clamp(_minimumRooms, 1, candidates.Count);
            int maxRooms = Mathf.Clamp(_maximumRooms, minRooms, candidates.Count);
            int roomCount = Random.Range(minRooms, maxRooms + 1);

            _selectedRooms.Clear();
            for (int i = 0; i < roomCount && candidates.Count > 0; i++)
            {
                int selectedIndex = WeightedIndex(candidates);
                DisinfectionRoom selected = candidates[selectedIndex].Room;
                _selectedRooms.Add(selected);
                candidates.RemoveAt(selectedIndex);
            }

            _warningRemaining = Mathf.Max(0f, _warningDuration);
            _state = DisinfectionState.Warning;
            Sfx.Play(SoundId.Warning, this);

            for (int i = 0; i < _selectedRooms.Count; i++)
            {
                DisinfectionRoom room = _selectedRooms[i];
                room.SetWarningVisible(true);
                room.SetCountdown(_warningRemaining);
                CreateArrow(room);
            }

            UpdateWarningViews();
        }

        private int WeightedIndex(List<RoomCandidate> candidates)
        {
            int totalWeight = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += Mathf.Max(0, candidates[i].Weight);
            }

            if (totalWeight <= 0)
            {
                return Random.Range(0, candidates.Count);
            }

            int roll = Random.Range(0, totalWeight);
            int accumulated = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                accumulated += Mathf.Max(0, candidates[i].Weight);
                if (roll < accumulated)
                {
                    return i;
                }
            }

            return candidates.Count - 1;
        }

        private void ExecuteDisinfection()
        {
            // 播报：消杀时刻。放在开头，玩家先看到"消杀开始"，再看到蟑螂成片死掉
            BroadcastUI.Broadcast(BroadcastKind.Disinfection);

            List<CockNest> nests = FindNests();
            for (int i = 0; i < _selectedRooms.Count; i++)
            {
                List<CockNest> roomNests = _selectedRooms[i].GetOccupiedNests(nests);
                for (int j = 0; j < roomNests.Count; j++)
                {
                    DestroyNestCockroaches(roomNests[j]);
                }
            }

            Debug.Log($"[DisinfectionManager] 消杀完成，处理房间数：{_selectedRooms.Count}。", this);
            CancelVisuals();
            _selectedRooms.Clear();
            _state = DisinfectionState.Finished;
        }

        private static void DestroyNestCockroaches(CockNest nest)
        {
            if (nest == null || nest.CockroachList == null)
            {
                return;
            }

            TapTapGameJam.Cockroach.Cockroach[] snapshot = nest.CockroachList.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] != null)
                {
                    snapshot[i].Die();
                }
            }
        }

        private List<CockNest> FindNests()
        {
            CockroachManager manager = CockroachManager.Instance;
            if (manager != null && manager.CockNestList != null)
            {
                return new List<CockNest>(manager.CockNestList);
            }

            CockNest[] found = FindObjectsByType<CockNest>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return new List<CockNest>(found);
        }

        private void CreateArrow(DisinfectionRoom room)
        {
            if (_arrowPrefab == null || _indicatorRoot == null)
            {
                return;
            }

            GameObject instance = Instantiate(_arrowPrefab, _indicatorRoot, false);
            DisinfectionArrowView view = instance.GetComponent<DisinfectionArrowView>();
            if (view == null)
            {
                view = instance.AddComponent<DisinfectionArrowView>();
            }

            view.Bind(room, new Color(1f, 0.2f, 0.15f, 1f));
            _arrows.Add(view);
        }

        private void UpdateWarningViews()
        {
            for (int i = 0; i < _selectedRooms.Count; i++)
            {
                _selectedRooms[i].SetCountdown(_warningRemaining);
            }

            for (int i = _arrows.Count - 1; i >= 0; i--)
            {
                if (_arrows[i] == null)
                {
                    _arrows.RemoveAt(i);
                    continue;
                }

                _arrows[i].UpdatePosition(_indicatorRoot, _worldCamera, _edgePadding, _warningRemaining);
            }
        }

        private void CancelVisuals()
        {
            for (int i = 0; i < _selectedRooms.Count; i++)
            {
                if (_selectedRooms[i] != null)
                {
                    _selectedRooms[i].SetWarningVisible(false);
                    _selectedRooms[i].HideCountdown();
                }
            }

            for (int i = 0; i < _arrows.Count; i++)
            {
                if (_arrows[i] != null)
                {
                    Destroy(_arrows[i].gameObject);
                }
            }

            _arrows.Clear();
        }

        [ContextMenu("Debug/Start Disinfection Warning")]
        private void DebugStartWarning()
        {
            _isDaytime = true;
            _hasRunToday = false;
            _state = DisinfectionState.Waiting;
            _waitRemaining = 0f;
            StartWarning();
        }
    }
}
