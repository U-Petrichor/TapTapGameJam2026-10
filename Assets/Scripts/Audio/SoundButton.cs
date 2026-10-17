using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.Audio
{
    // Defer generic feedback until business handlers have had a chance to play a specific cue.
    [RequireComponent(typeof(Button)), DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class SoundButton : MonoBehaviour
    {
        private Button button;
        private bool pending;
        private void Awake() { button = GetComponent<Button>(); button.onClick.AddListener(Clicked); }
        private void Clicked() => pending = true;
        private void LateUpdate()
        {
            if (!pending) return;
            pending = false;
            if (SoundManager.Instance != null && !SoundManager.Instance.HasSpecificFeedbackThisFrame)
                Sfx.Play(SoundId.Click, this);
        }
        private void OnDisable() => pending = false;
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Clicked); }
    }
}
