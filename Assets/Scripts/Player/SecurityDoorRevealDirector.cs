using System;
using System.Collections;
using System.Collections.Generic;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TapTapGameJam.Player
{
    /// <summary>同日新开放的门连续展示一次，使用独立虚拟相机，不修改玩家镜头的跟随和缩放。</summary>
    [DisallowMultipleComponent]
    public sealed class SecurityDoorRevealDirector : MonoBehaviour
    {
        [Header("场景引用")]
        [SerializeField] private CinemachineCamera _revealCamera;
        [SerializeField] private CinemachineBrain _brain;
        [SerializeField] private GamePhaseManager _phaseManager;
        [SerializeField] private GamePauseManager _pauseManager;
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private PlayerInputManager _inputManager;

        [Header("运镜节奏")]
        [SerializeField, Min(0.01f)] private float _moveDuration = 1f;
        [SerializeField, Min(0f)] private float _holdDuration = 1f;
        [SerializeField, Min(0.01f)] private float _returnDuration = 1f;
        [Tooltip("沿触发时镜头的观察方向后退此距离取景，不受玩家缩放距离影响。")]
        [SerializeField, Min(0.5f)] private float _closeUpDistance = 7f;
        [SerializeField] private int _priority = 100;

        private readonly List<SecurityDoor> _pending = new();
        private readonly HashSet<SecurityDoor> _shown = new();
        private Coroutine _routine;
        private bool _ownsControl;
        private bool _inputWasActive;
        private bool _brainIgnoredTimeScale;
        private CinemachineBlendDefinition _originalBlend;
        private CinemachineBlenderSettings _originalCustomBlends;
        private PrioritySettings _originalPriority;
        private LensSettings _originalLens;

        public bool IsPlaying => _ownsControl;

        private void Awake()
        {
            if (_revealCamera != null)
                _revealCamera.enabled = false;
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<EventSecurityDoorAvailable>(HandleDoorAvailable);
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.Subscribe<EventGameOver>(HandleGameOver);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventSecurityDoorAvailable>(HandleDoorAvailable);
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            EventPipeline.UnSubscribe<EventGameOver>(HandleGameOver);
            CancelReveal();
        }

        private void HandleDoorAvailable(EventSecurityDoorAvailable evt)
        {
            SecurityDoor door = evt.Door;
            if (door == null || door.gameObject.scene != gameObject.scene
                || _shown.Contains(door) || _pending.Contains(door))
                return;

            _pending.Add(door);
            if (_routine == null)
                _routine = StartCoroutine(PlayPending());
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            if (evt.NewPhase != GamePhase.Day)
                CancelReveal();
        }

        private void HandleGameOver(EventGameOver _) => CancelReveal();

        private bool ReferencesReady()
        {
            return _revealCamera != null && _brain != null && _brain.OutputCamera != null
                && _phaseManager != null && _pauseManager != null
                && _playerInput != null && _inputManager != null;
        }

        private IEnumerator PlayPending()
        {
            // 等待这次 EventNewDay 的所有门完成通知，避免依赖事件订阅顺序。
            yield return null;
            if (!ReferencesReady())
            {
                Debug.LogError("[SecurityDoorRevealDirector] 请配置相机、Brain、阶段、暂停和玩家输入引用。", this);
                _pending.Clear();
                _routine = null;
                yield break;
            }

            // 背包、菜单、小游戏或其他相机过渡结束后再接管，不关闭玩家已有的界面。
            while (_pauseManager.IsPaused || _brain.IsBlending || !_playerInput.inputIsActive
                || _playerInput.currentActionMap == null
                || _playerInput.currentActionMap.name != "DayPlayer")
                yield return null;

            if (_phaseManager.CurrentPhase != GamePhase.Day)
            {
                _pending.Clear();
                _routine = null;
                yield break;
            }

            Camera output = _brain.OutputCamera;
            // 玩家可以拉到近乎垂直俯视；门特写保留方位，但限制俯角以看清门面。
            Vector3 forward = output.transform.forward;
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(-forward.y, -1f, 1f)) * Mathf.Rad2Deg, 20f, 65f);
            Quaternion viewingRotation = Quaternion.Euler(pitch, output.transform.eulerAngles.y, 0f);
            Vector3 routeOrigin = _inputManager.transform.position;
            try
            {
                TakeControl(output);
                // 起点与当前实际画面完全重合，先让 Brain 切换到特写相机。
                yield return null;
                while (_pending.Count > 0)
                {
                    SecurityDoor door = TakeNearestDoor(routeOrigin);
                    if (door == null || !door.isActiveAndEnabled || !door.IsAvailable)
                        continue;

                    Vector3 focus = door.RevealFocus;
                    Quaternion rotation = viewingRotation;
                    float distance = door.RevealDistance > 0f ? door.RevealDistance : _closeUpDistance;
                    Vector3 position = focus - rotation * Vector3.forward * distance;
                    if (door.RevealCameraPose != null)
                    {
                        position = door.RevealCameraPose.position;
                        rotation = door.RevealCameraPose.rotation;
                    }

                    yield return MoveCamera(position, rotation);
                    _shown.Add(door);
                    yield return new WaitForSecondsRealtime(_holdDuration);
                    routeOrigin = focus;
                }

                // 解除高优先级后，Brain 平滑返回原来的游戏相机。
                _brain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.EaseInOut, _returnDuration);
                _revealCamera.enabled = false;
                yield return null;
                while (_brain.IsBlending)
                    yield return null;
            }
            finally
            {
                RestoreControl();
                _routine = null;
            }
        }

        private void TakeControl(Camera output)
        {
            _inputWasActive = _playerInput.inputIsActive;
            _brainIgnoredTimeScale = _brain.IgnoreTimeScale;
            _originalBlend = _brain.DefaultBlend;
            _originalCustomBlends = _brain.CustomBlends;
            _originalPriority = _revealCamera.Priority;
            _originalLens = _revealCamera.Lens;
            _ownsControl = true;

            _inputManager.MoveInput = Vector2.zero;
            _playerInput.DeactivateInput();
            _pauseManager.SetDoorRevealPlaying(true);
            _brain.IgnoreTimeScale = true;
            // 接手机位一致，使用 Cut 防止 Brain 和移动插值叠加；返回时单独使用平滑混合。
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            _brain.CustomBlends = null;
            _revealCamera.Lens = LensSettings.FromCamera(output);
            _revealCamera.OutputChannel = _brain.ActiveVirtualCamera is CinemachineVirtualCameraBase source
                ? source.OutputChannel : OutputChannels.Default;
            _revealCamera.ForceCameraPosition(output.transform.position, output.transform.rotation);
            int activePriority = _brain.ActiveVirtualCamera is CinemachineVirtualCameraBase active
                ? active.Priority.Value : 0;
            _revealCamera.Priority = Math.Max(_priority, activePriority < int.MaxValue ? activePriority + 1 : int.MaxValue);
            _revealCamera.enabled = true;
        }

        private IEnumerator MoveCamera(Vector3 destination, Quaternion rotation)
        {
            Vector3 origin = _revealCamera.transform.position;
            Quaternion originRotation = _revealCamera.transform.rotation;
            float elapsed = 0f;
            while (elapsed < _moveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _moveDuration);
                t = t * t * (3f - 2f * t);
                _revealCamera.ForceCameraPosition(Vector3.Lerp(origin, destination, t),
                    Quaternion.Slerp(originRotation, rotation, t));
                yield return null;
            }
            _revealCamera.ForceCameraPosition(destination, rotation);
        }

        private SecurityDoor TakeNearestDoor(Vector3 origin)
        {
            int best = 0;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < _pending.Count; i++)
            {
                SecurityDoor candidate = _pending[i];
                if (candidate == null) continue;
                float distance = (candidate.RevealFocus - origin).sqrMagnitude;
                if (distance < bestDistance || (Mathf.Approximately(distance, bestDistance)
                    && string.CompareOrdinal(candidate.name, _pending[best]?.name) < 0))
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            SecurityDoor result = _pending[best];
            _pending.RemoveAt(best);
            return result;
        }

        private void CancelReveal()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            _pending.Clear();
            RestoreControl();
        }

        private void RestoreControl()
        {
            if (!_ownsControl) return;
            _ownsControl = false;
            if (_revealCamera != null)
            {
                _revealCamera.enabled = false;
                _revealCamera.Priority = _originalPriority;
                _revealCamera.Lens = _originalLens;
            }
            if (_brain != null)
            {
                _brain.DefaultBlend = _originalBlend;
                _brain.CustomBlends = _originalCustomBlends;
                _brain.IgnoreTimeScale = _brainIgnoredTimeScale;
            }
            if (_pauseManager != null)
                _pauseManager.SetDoorRevealPlaying(false);
            if (_playerInput != null && _inputWasActive)
                _playerInput.ActivateInput();
        }
    }
}
