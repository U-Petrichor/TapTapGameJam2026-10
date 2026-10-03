using UnityEngine;

namespace TapTapGameJam.Coffee
{
    // 桌面物体只处理自己的移动、倾倒、吸附、复位，不读取鼠标、不推进配方。
    [RequireComponent(typeof(Collider))]
    public class CoffeeDraggable : MonoBehaviour
    {
        [SerializeField] private Vector3 _pourEulerAngles = new Vector3(0f, 0f, -35f);
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Quaternion _beforePourRotation;
        private bool _startActive;
        private bool _initialized;
        private bool _isPouring;
        public bool IsPlaced { get; private set; }

        private void Awake() => RememberInitialPose();

        private void RememberInitialPose()
        {
            if (_initialized) return;
            _initialized = true;
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            _startActive = gameObject.activeSelf;
        }

        public void MoveTo(Vector3 position) => transform.position = position;

        public void SetPouring(bool pouring)
        {
            if (_isPouring == pouring) return;
            if (pouring)
            {
                _beforePourRotation = transform.rotation;
                transform.rotation = _beforePourRotation * Quaternion.Euler(_pourEulerAngles);
            }
            else transform.rotation = _beforePourRotation;
            _isPouring = pouring;
        }

        public void PlaceAt(Transform snapPoint)
        {
            SetPouring(false);
            transform.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);
            IsPlaced = true;
        }

        public void ResetItem()
        {
            // 咖啡粉容器可能初始隐藏，因此也允许在 Awake 之前初始化。
            RememberInitialPose();
            _isPouring = false;
            transform.SetPositionAndRotation(_startPosition, _startRotation);
            IsPlaced = false;
            gameObject.SetActive(_startActive);
        }
    }
}
