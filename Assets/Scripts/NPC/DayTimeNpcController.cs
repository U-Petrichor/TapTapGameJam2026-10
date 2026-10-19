using System.Collections.Generic;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using UnityEngine;

namespace TapTapGameJam.NPC
{
    /// <summary>挂在始终启用的管理对象上，统一控制白天 NPC。</summary>
    public sealed class DayTimeNpcController : MonoBehaviour
    {
        [SerializeField] private GameObject _dayTimeNpcRoot;
        [SerializeField] private GamePhaseManager _phaseManager;
        private bool _isDaytime;
        private bool _started;
        private readonly Dictionary<Animator, Pose> _passiveSpawnPoses = new Dictionary<Animator, Pose>();

        private void Awake()
        {
            if (_dayTimeNpcRoot == null) return;
            foreach (NPC npc in _dayTimeNpcRoot.GetComponentsInChildren<NPC>(true))
                npc.CaptureSpawnPose();
            // 善良同事目前只有 Animator，没有 NPC 行为脚本，也需要重置。
            foreach (Animator animator in _dayTimeNpcRoot.GetComponentsInChildren<Animator>(true))
            {
                if (animator.GetComponentInParent<NPC>(true) == null)
                    _passiveSpawnPoses.Add(animator, new Pose(animator.transform.position, animator.transform.rotation));
            }
            _dayTimeNpcRoot.SetActive(false);
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            if (_started) SyncCurrentPhase();
        }

        private void Start()
        {
            _started = true;
            SyncCurrentPhase();
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            _isDaytime = false;
            if (_dayTimeNpcRoot != null) _dayTimeNpcRoot.SetActive(false);
        }

        private void SyncCurrentPhase()
        {
            if (_phaseManager == null)
                _phaseManager = FindFirstObjectByType<GamePhaseManager>();
            if (_phaseManager != null) ApplyPhase(_phaseManager.CurrentPhase);
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt) => ApplyPhase(evt.NewPhase);

        private void ApplyPhase(GamePhase phase)
        {
            if (_dayTimeNpcRoot == null) return;
            if (phase != GamePhase.Day)
            {
                _isDaytime = false;
                _dayTimeNpcRoot.SetActive(false);
                return;
            }

            if (_isDaytime) return;
            _dayTimeNpcRoot.SetActive(false);
            foreach (var pair in _passiveSpawnPoses)
            {
                if (pair.Key != null)
                    pair.Key.transform.SetPositionAndRotation(pair.Value.position, pair.Value.rotation);
            }
            foreach (NPC npc in _dayTimeNpcRoot.GetComponentsInChildren<NPC>(true))
                npc.ResetForNewDay();
            _isDaytime = true;
            _dayTimeNpcRoot.SetActive(true);
            foreach (var pair in _passiveSpawnPoses)
            {
                if (pair.Key != null && pair.Key.isActiveAndEnabled) pair.Key.Rebind();
            }
        }
    }
}
