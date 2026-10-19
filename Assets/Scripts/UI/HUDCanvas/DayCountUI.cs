using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using TMPro;
using UnityEngine;

public class DayCountUI : MonoBehaviour
{
    [SerializeField] private GamePhaseManager phaseManager;
    [SerializeField] private GameObject dayUI;
    [SerializeField] private GameObject nightUI;
    [SerializeField] private TextMeshProUGUI dayNumber;
    [SerializeField] private TextMeshProUGUI nightNumber;

    // Layout and artwork belong to the scene. Only visibility and text change here.
    private void OnEnable()
    {
        EventPipeline.Subscribe<GamePhaseChangedEvent>(OnPhaseChanged);
        EventPipeline.Subscribe<EventNewDay>(OnNewDay);
        Refresh();
    }

    private void Start() => Refresh();

    private void OnDisable()
    {
        EventPipeline.UnSubscribe<GamePhaseChangedEvent>(OnPhaseChanged);
        EventPipeline.UnSubscribe<EventNewDay>(OnNewDay);
    }

    private void Refresh()
    {
        if (!phaseManager) phaseManager = FindFirstObjectByType<GamePhaseManager>();
        if (!phaseManager) return;
        SetDay(phaseManager.DayIndex);
        ApplyPhase(phaseManager.CurrentPhase);
    }

    private void OnPhaseChanged(GamePhaseChangedEvent evt)
    {
        if (phaseManager) SetDay(phaseManager.DayIndex);
        ApplyPhase(evt.NewPhase);
    }

    private void OnNewDay(EventNewDay evt) => SetDay(evt.DayCount);

    private void SetDay(int day)
    {
        string value = Mathf.Max(1, day).ToString();
        if (dayNumber) dayNumber.text = value;
        if (nightNumber) nightNumber.text = value;
    }

    private void ApplyPhase(GamePhase phase)
    {
        if (dayUI) dayUI.SetActive(phase == GamePhase.Day);
        if (nightUI) nightUI.SetActive(phase == GamePhase.Night);
    }
}
