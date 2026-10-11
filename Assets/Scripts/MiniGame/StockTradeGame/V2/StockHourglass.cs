using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class StockHourglass : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private Sprite _sprite;
        private StockHourglassSand _sand;

        public void Configure(Sprite sprite)
        {
            _sprite = sprite;
            ApplyVisual(0f);
        }

        public void SetProgress(float progress)
        {
            ApplyVisual(Mathf.Clamp01(progress));
        }

        public void ResetProgress() => ApplyVisual(0f);

        private void Awake()
        {
            if (_image == null) _image = GetComponent<Image>();
            EnsureSand();
            ApplyVisual(0f);
        }

        private void ApplyVisual(float progress)
        {
            if (_image == null) _image = GetComponent<Image>();
            EnsureSand();
            if (_image != null)
            {
                _image.sprite = _sprite;
                _image.color = Color.white;
                _image.preserveAspect = true;
                _image.raycastTarget = false;
            }
            _sand?.SetProgress(progress);
        }

        private void EnsureSand()
        {
            if (_sand != null) return;
            var go = new GameObject("ColoredSand", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _sand = go.AddComponent<StockHourglassSand>();
            _sand.ConfigureColor(new Color(0.96f, 0.63f, 0.16f, 1f));
            go.transform.SetAsLastSibling();
        }
    }
}
