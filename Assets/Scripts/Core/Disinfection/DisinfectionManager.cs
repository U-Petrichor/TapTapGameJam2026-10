using TapTapGameJam.Audio;
using System;
using System.Collections;
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

        [Header("解锁与时间")]
        [Tooltip("EventNewDay.DayCount 的解锁阈值。第一天是 1，第二天是 2。")]
        [SerializeField] private int _unlockDayCount = 2;
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
        [Tooltip("忽略第二天解锁限制，并跳过随机等待。无需房间内存在巢穴。")]
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
            var roomsWithNests = new List<DisinfectionRoom>();
            var roomsWithoutNests = new List<DisinfectionRoom>();
            for (int i = 0; i < _rooms.Count; i++)
            {
                var group = _rooms[i].CountNests(nests) > 0 ? roomsWithNests : roomsWithoutNests;
                group.Add(_rooms[i]);
            }

            _hasRunToday = true;
            if (_rooms.Count == 0)
            {
                Debug.LogWarning("[DisinfectionManager] 没有可用的消杀房间，请检查场景配置。", this);
                _state = DisinfectionState.Finished;
                return;
            }

            int minRooms = Mathf.Clamp(_minimumRooms, 1, _rooms.Count);
            int maxRooms = Mathf.Clamp(_maximumRooms, minRooms, _rooms.Count);
            int roomCount = Random.Range(minRooms, maxRooms + 1);
            _selectedRooms.Clear();
            SelectRooms(roomsWithNests, roomsWithoutNests, roomCount, _selectedRooms);

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

        private static void SelectRooms(List<DisinfectionRoom> roomsWithNests,
            List<DisinfectionRoom> roomsWithoutNests, int roomCount, List<DisinfectionRoom> selected)
        {
            for (int i = 0; i < roomCount; i++)
            {
                // First slot is guaranteed; later slots use group probabilities, not nest counts.
                float nestChance = i == 0 ? 1f : i == 1 ? 0.5f : 0.25f;
                bool preferNests = i == 0 || Random.value < nestChance;
                var group = preferNests ? roomsWithNests : roomsWithoutNests;
                if (group.Count == 0)
                {
                    group = preferNests ? roomsWithoutNests : roomsWithNests;
                }

                if (group.Count == 0)
                {
                    break;
                }

                int index = Random.Range(0, group.Count);
                selected.Add(group[index]);
                group.RemoveAt(index);
            }
        }

        private void ExecuteDisinfection()
        {
            // 播报：消杀时刻。放在开头，玩家先看到"消杀开始"，再看到蟑螂成片死掉
            BroadcastUI.Broadcast(BroadcastKind.Disinfection);

            // 结局判定要"消杀前 > 0 且消杀后 == 0"，所以先把消杀前的数量记下来
            CockroachManager cockroachManager = CockroachManager.Instance;
            int cockroachesBefore = cockroachManager != null ? cockroachManager.CockroachTotalCount() : 0;

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

            StartCoroutine(CheckDisinfectionEnding(cockroachesBefore));
        }

        /// <summary>
        /// 区域消杀结局：消杀把场上蟑螂清成 0。
        ///
        /// 必须等一帧再判：蟑螂的 <c>Die()</c> 走的是 <c>Destroy</c>（帧末才真正销毁），
        /// 巢穴名单要到蟑螂的 <c>OnDisable</c> 才摘除，所以这一帧查 <c>CockroachTotalCount()</c>
        /// 拿到的还是消杀前的数字。
        /// </summary>
        private IEnumerator CheckDisinfectionEnding(int cockroachesBefore)
        {
            yield return null;

            if (cockroachesBefore <= 0)
            {
                // 消杀前就没有蟑螂：这不是"消杀导致的清零"，不结算
                yield break;
            }

            CockroachManager manager = CockroachManager.Instance;
            if (manager == null || manager.CockroachTotalCount() > 0)
            {
                yield break;
            }

            GameEndingDirector.Trigger(GameEnding.Disinfection);
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
