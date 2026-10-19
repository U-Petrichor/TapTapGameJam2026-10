using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using UnityEngine;

/// <summary>只在第二、第四晚提供门禁卡；天亮清理，拾取后本局不再刷新。</summary>
public sealed class AccessCardSpawner : MonoBehaviour
{
    [SerializeField] private GamePhaseManager _phaseManager;
    [SerializeField] private AccessCard _cardPrefab;
    [SerializeField] private Transform _day2Night;
    [SerializeField] private Transform _day4Night;
    [SerializeField] private SecurityDoor _door;
    private AccessCard _activeCard;
    private bool _collected;
    private int _lastSpawnDay;
    private void OnEnable() => EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
    private void Start()
    {
        if (_phaseManager == null || _cardPrefab == null || _day2Night == null || _day4Night == null || _door == null)
        {
            Debug.LogError("[AccessCardSpawner] 请配置阶段、卡片、两个刷新点及目标门。", this);
            enabled = false;
            return;
        }
        RefreshCard();
    }
    private void OnDisable()
    {
        EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        ClearCard();
    }
    private void HandlePhaseChanged(GamePhaseChangedEvent _) => RefreshCard();
    private void RefreshCard()
    {
        if (_phaseManager == null) return;
        if (_phaseManager.CurrentPhase != GamePhase.Night)
        {
            ClearCard();
            return;
        }
        int day = _phaseManager.DayIndex;
        if (_collected || _activeCard != null || _lastSpawnDay == day || (day != 2 && day != 4)) return;
        Transform point = day == 2 ? _day2Night : _day4Night;
        if (_cardPrefab == null || point == null || _door == null) return;
        _lastSpawnDay = day;
        _activeCard = Instantiate(_cardPrefab, point.position, point.rotation, transform);
        _activeCard.Initialize(this);
    }
    public bool CanCollect(AccessCard card) => !_collected && card != null && card == _activeCard
        && _door != null && _phaseManager != null && _phaseManager.CurrentPhase == GamePhase.Night;
    public bool TryCollect(AccessCard card)
    {
        if (!CanCollect(card)) return false;
        _collected = true;
        _activeCard = null;
        _door.GrantAccessCard();
        return true;
    }
    private void ClearCard()
    {
        if (_activeCard == null) return;
        _activeCard.gameObject.SetActive(false);
        Destroy(_activeCard.gameObject);
        _activeCard = null;
    }
}
