using System.Collections;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TapTapGameJam.Tutorial;
using TapTapGameJam.Types;
using TapTapGameJam.UI;
using TalentTree;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapTapGameJam.Core
{
    /// <summary>Shopping 的 2D 房间和入场过渡，不传送玩家或切换相机。</summary>
    public class ShoppingManager : MonoBehaviour
    {
        [SerializeField] private PlayerBasic _player;
        [SerializeField] private ShoppingRoomView _shoppingRoom;
        [SerializeField] private GameObject _fadeOverlayCanvas;
        [SerializeField] private Image _fadeImage;
        [SerializeField] private TMP_Text _fadeText;
        [SerializeField] private TalentTreeUI _talentTreeUI;
        [SerializeField] private DailySettlementView _settlementView;
        [SerializeField] private GamePhaseManager _phaseManager;
        [Header("过渡时间（秒）")]
        [SerializeField, Min(0f)] private float _fadeDuration = 0.5f;

        private Coroutine _transition;
        private bool _shopping;
        private bool _nightExitPending;
        private bool _controlsCaptured;
        private PlayerInput _playerInput;
        private Rigidbody _body;
        private bool _inputWasActive;
        private string _previousActionMap;
        private bool _movementWasEnabled;
        private bool _interactionWasEnabled;
        private bool _worldInputWasBlocked;
        private bool _bodyWasKinematic;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        private void Awake()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerBasic>();
            if (_player != null)
            {
                _playerInput = _player.GetComponent<PlayerInput>();
                _body = _player.GetComponent<Rigidbody>();
            }
            if (_shoppingRoom != null) _shoppingRoom.gameObject.SetActive(false);
            if (_settlementView != null) _settlementView.gameObject.SetActive(false);
            if (_talentTreeUI != null) _talentTreeUI.enabled = false;
            HideOverlay();
        }

        private void OnEnable() => EventPipeline.Subscribe<GamePhaseChangedEvent>(HandleGamePhaseChange);

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandleGamePhaseChange);
            LeaveShopping();
        }

        private void HandleGamePhaseChange(GamePhaseChangedEvent evt)
        {
            // NightManager 在全黑时隐藏房间，在渐亮结束时才释放世界控制。
            if (evt.NewPhase == GamePhase.Night) return;
            if (evt.NewPhase != GamePhase.Shopping)
            {
                LeaveShopping();
                return;
            }
            if (_nightExitPending) LeaveShopping();
            if (_shopping) return;
            if (_shoppingRoom == null || _fadeOverlayCanvas == null || _fadeImage == null || _fadeText == null ||
                _settlementView == null || _phaseManager == null)
            {
                Debug.LogError("[ShoppingManager] 请配置房间、转场、每日结算和 GamePhaseManager。", this);
                return;
            }
            _shopping = true;
            CaptureControls();
            _transition = StartCoroutine(EnterShoppingRoutine());
        }

        public void OpenTalentTree()
        {
            if (_shopping && !_nightExitPending && _transition == null && _talentTreeUI != null)
                _talentTreeUI.Open();
        }

        public void FinishShopping()
        {
            if (!_shopping || _nightExitPending || _transition != null ||
                (_talentTreeUI != null && _talentTreeUI.IsOpen)) return;
            _nightExitPending = true;
            _shoppingRoom.SetInteractionEnabled(false);
            EventPipeline.Publish(new ShoppingFinishedEvent());
        }

        public void BeginNightTransition()
        {
            _nightExitPending = true;
            if (_transition != null) StopCoroutine(_transition);
            _transition = null;
            if (_settlementView != null) _settlementView.gameObject.SetActive(false);
            if (!_controlsCaptured) CaptureControls();
            if (_shoppingRoom != null) _shoppingRoom.SetInteractionEnabled(false);
            if (_talentTreeUI != null) _talentTreeUI.enabled = false;
            if (_playerInput != null) _playerInput.DeactivateInput();
        }

        public void HideRoomForNight()
        {
            if (_shoppingRoom != null) _shoppingRoom.gameObject.SetActive(false);
        }

        public void CompleteNightTransition() => LeaveShopping();

        private IEnumerator EnterShoppingRoutine()
        {
            _shoppingRoom.SetInteractionEnabled(false);
            SetFadeAlpha(0f);
            _fadeText.gameObject.SetActive(false);
            _settlementView.Show(_phaseManager.DailyCoinsEarned, _phaseManager.DailyTalentEarned,
                _phaseManager.DailyCoinsGiftAmount, _phaseManager.DailyTalentGiftAmount);
            if (_playerInput != null && _inputWasActive) _playerInput.ActivateInput();
            // 等待确认期间不启用上层淡黑遮罩，让结算按钮接收点击。
            yield return new WaitUntil(() => _settlementView.Confirmed);

            // 结算先，教程后：等玩家关闭结算说明再继续切到家园。
            bool tutorialDone = TutorialDirector.Instance == null;
            if (TutorialDirector.Instance != null)
                TutorialDirector.Instance.RequestBlocking(TutorialSignals.DaySettled, () => tutorialDone = true);
            else
                tutorialDone = true;
            yield return new WaitUntil(() => tutorialDone);

            if (_playerInput != null) _playerInput.DeactivateInput();
            _fadeOverlayCanvas.SetActive(true);
            yield return Fade(0f, 1f);

            // Fade 最后一帧已经以 alpha=1 绘制过，在全黑下替换界面。
            _fadeText.gameObject.SetActive(false);
            _settlementView.gameObject.SetActive(false);
            _shoppingRoom.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            yield return Fade(1f, 0f);

            HideOverlay();
            _shoppingRoom.SetInteractionEnabled(true);
            if (_talentTreeUI != null) _talentTreeUI.enabled = true;
            // UI 接收鼠标，世界操作保持锁定，直到离开 Shopping。
            if (_playerInput != null && _inputWasActive) _playerInput.ActivateInput();
            TutorialSignal.Send(TutorialSignals.HomeEntered);
            _transition = null;
        }

        private void CaptureControls()
        {
            _controlsCaptured = true;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_playerInput != null)
            {
                _inputWasActive = _playerInput.inputIsActive;
                _previousActionMap = _playerInput.currentActionMap?.name;
                _playerInput.DeactivateInput();
            }
            if (_player == null) return;
            if (_player.InputManager != null)
            {
                _worldInputWasBlocked = _player.InputManager.WorldInputBlocked;
                _player.InputManager.WorldInputBlocked = true;
                _player.InputManager.MoveInput = Vector2.zero;
            }
            if (_player.PlayerMovement != null)
            {
                _movementWasEnabled = _player.PlayerMovement.enabled;
                _player.PlayerMovement.SetMoveInput(Vector2.zero);
                _player.PlayerMovement.IsSprinting = false;
                _player.PlayerMovement.enabled = false;
            }
            if (_player.PlayerInteractionManager != null)
            {
                _interactionWasEnabled = _player.PlayerInteractionManager.enabled;
                _player.PlayerInteractionManager.enabled = false;
            }
            if (_body != null)
            {
                _bodyWasKinematic = _body.isKinematic;
                if (!_body.isKinematic)
                {
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                    _body.isKinematic = true;
                }
            }
        }

        private void LeaveShopping()
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = null;
            _shopping = false;
            _nightExitPending = false;
            if (_settlementView != null) _settlementView.gameObject.SetActive(false);
            if (_talentTreeUI != null) _talentTreeUI.enabled = false;
            if (_shoppingRoom != null)
            {
                _shoppingRoom.SetInteractionEnabled(false);
                _shoppingRoom.gameObject.SetActive(false);
            }
            HideOverlay();
            if (!_controlsCaptured) return;
            _controlsCaptured = false;
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
            if (_body != null) _body.isKinematic = _bodyWasKinematic;
            if (_player != null)
            {
                if (_player.InputManager != null)
                {
                    _player.InputManager.MoveInput = Vector2.zero;
                    _player.InputManager.WorldInputBlocked = _worldInputWasBlocked;
                }
                if (_player.PlayerMovement != null) _player.PlayerMovement.enabled = _movementWasEnabled;
                if (_player.PlayerInteractionManager != null) _player.PlayerInteractionManager.enabled = _interactionWasEnabled;
            }
            if (_playerInput != null)
            {
                if (!string.IsNullOrEmpty(_previousActionMap)) _playerInput.SwitchCurrentActionMap(_previousActionMap);
                if (_inputWasActive) _playerInput.ActivateInput();
                else _playerInput.DeactivateInput();
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            float elapsed = 0f;
            SetFadeAlpha(from);
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFadeAlpha(Mathf.Lerp(from, to, elapsed / _fadeDuration));
                yield return null;
            }
            SetFadeAlpha(to);
        }

        private void HideOverlay()
        {
            SetFadeAlpha(0f);
            if (_fadeOverlayCanvas != null) _fadeOverlayCanvas.SetActive(false);
        }

        private void SetFadeAlpha(float alpha)
        {
            if (_fadeImage != null) _fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        }
    }
}
