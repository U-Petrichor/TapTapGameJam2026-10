using System.Collections;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 当切换到商店时的管理中心，管理所有在商店阶段应该处理的事件
    /// </summary>
    public class ShoppingManager : MonoBehaviour
    {
        [Header("Transition references")]
        [SerializeField] private PlayerBasic _player;
        [SerializeField] private Transform _houseSpawn;
        [SerializeField] private CinemachineCamera _houseCamera;
        [SerializeField] private CinemachineBrain _cinemachineBrain;
        [SerializeField] private GameObject _fadeOverlayCanvas;
        [SerializeField] private Image _fadeImage;
        [SerializeField] private TMP_Text _fadeText;

        [Header("Transition timing")]
        [SerializeField, Min(0f)] private float _textDisplayDuration = 0.5f;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.5f;
        [SerializeField, Min(0f)] private float _postCameraBlackDuration = 0.5f;

        private Coroutine _transitionCoroutine;
        private PlayerInput _playerInput;
        private Rigidbody _playerRigidbody;
        private bool _playerInputWasActive;

        private void Awake()
        {
            ResolveReferences();
            SetFadeAlpha(0f);
            if (_fadeOverlayCanvas != null)
            {
                _fadeOverlayCanvas.SetActive(false);
            }
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandleGamePhaseChange);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandleGamePhaseChange);
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }
        }

        /// <summary>
        /// 接受到GamePhaseChangeEvnet之后，如果是Shopping阶段，处理初始化的内容
        /// </summary>
        private void HandleGamePhaseChange(GamePhaseChangedEvent evt)
        {
            if (evt.NewPhase != TapTapGameJam.Types.GamePhase.Shopping ||
                _transitionCoroutine != null)
            {
                return;
            }

            _transitionCoroutine = StartCoroutine(EnterShoppingRoutine());
        }

        private void ResolveReferences()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerBasic>();
            }

            if (_houseSpawn == null)
            {
                foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
                {
                    if (child == transform)
                    {
                        continue;
                    }

                    if (child.name == "HouseSpawnPoint" ||
                        child.name == "HouseSpawn" ||
                        child.name == "ShoppingSpawn")
                    {
                        _houseSpawn = child;
                        break;
                    }
                }
            }

            if (_houseCamera == null)
            {
                _houseCamera = GetComponentInChildren<CinemachineCamera>(true);
            }

            if (_cinemachineBrain == null)
            {
                _cinemachineBrain = FindFirstObjectByType<CinemachineBrain>();
            }

            if (_fadeOverlayCanvas == null)
            {
                Transform overlay = transform.Find("FadeOverlayCanvas");
                _fadeOverlayCanvas = overlay != null ? overlay.gameObject : null;
            }

            if (_fadeImage == null && _fadeOverlayCanvas != null)
            {
                _fadeImage = _fadeOverlayCanvas.GetComponentInChildren<Image>(true);
            }

            if (_fadeText == null && _fadeOverlayCanvas != null)
            {
                _fadeText = _fadeOverlayCanvas.GetComponentInChildren<TMP_Text>(true);
            }

            if (_player != null)
            {
                _playerInput = _player.GetComponent<PlayerInput>();
                _playerRigidbody = _player.GetComponent<Rigidbody>();
            }
        }

        private IEnumerator EnterShoppingRoutine()
        {
            SetPlayerInputActive(false);

            if (_fadeOverlayCanvas != null)
            {
                _fadeOverlayCanvas.SetActive(true);
            }

            if (_fadeText != null)
            {
                _fadeText.gameObject.SetActive(true);
            }

            SetFadeAlpha(0f);
            yield return WaitRealtime(_textDisplayDuration);
            yield return FadeImage(0f, 1f, _fadeDuration);

            MovePlayerToHouseSpawn();

            if (_houseCamera != null)
            {
                // Priority is configured in the scene. Enabling the camera lets
                // Cinemachine select it without changing that configured value.
                _houseCamera.enabled = true;
            }

            yield return WaitForCameraBlend();
            yield return WaitRealtime(_postCameraBlackDuration);
            yield return FadeImage(1f, 0f, _fadeDuration);

            if (_fadeOverlayCanvas != null)
            {
                _fadeOverlayCanvas.SetActive(false);
            }

            SetPlayerInputActive(true);
            _transitionCoroutine = null;
        }

        private void MovePlayerToHouseSpawn()
        {
            if (_player == null || _houseSpawn == null)
            {
                return;
            }

            if (_playerRigidbody == null)
            {
                _playerRigidbody = _player.GetComponent<Rigidbody>();
            }

            if (_playerRigidbody != null)
            {
                _playerRigidbody.position = _houseSpawn.position;
                _playerRigidbody.rotation = _houseSpawn.rotation;
                _playerRigidbody.linearVelocity = Vector3.zero;
                _playerRigidbody.angularVelocity = Vector3.zero;
            }
            else
            {
                _player.transform.SetPositionAndRotation(
                    _houseSpawn.position,
                    _houseSpawn.rotation);
            }
        }

        private void SetPlayerInputActive(bool active)
        {
            if (_playerInput == null && _player != null)
            {
                _playerInput = _player.GetComponent<PlayerInput>();
            }

            if (_playerInput == null)
            {
                return;
            }

            if (!active)
            {
                _playerInputWasActive = _playerInput.enabled;
                if (_player != null && _player.InputManager != null)
                {
                    _player.InputManager.MoveInput = Vector2.zero;
                }

                _playerInput.DeactivateInput();
            }
            else if (_playerInputWasActive)
            {
                _playerInput.ActivateInput();
            }
        }

        private IEnumerator FadeImage(float from, float to, float duration)
        {
            if (_fadeImage == null || duration <= 0f)
            {
                SetFadeAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFadeAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetFadeAlpha(to);
        }

        private IEnumerator WaitForCameraBlend()
        {
            // Let Cinemachine process the newly enabled camera before reading
            // the blend state, then wait until any configured blend is complete.
            yield return null;
            while (_cinemachineBrain != null && _cinemachineBrain.IsBlending)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitRealtime(float duration)
        {
            if (duration <= 0f)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void SetFadeAlpha(float alpha)
        {
            if (_fadeImage == null)
            {
                return;
            }

            Color color = _fadeImage.color;
            color.r = 0f;
            color.g = 0f;
            color.b = 0f;
            color.a = Mathf.Clamp01(alpha);
            _fadeImage.color = color;
        }
    }
}
