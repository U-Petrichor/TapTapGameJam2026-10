using UnityEngine;

namespace TapTapGameJam.Coffee
{
    // 挂在滤杯中心的空物体上。绿色圆表示可接收的桌面区域。
    public class CoffeeDropZone : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _radius = 0.25f;
        [SerializeField] private Transform _snapPoint;
        public Transform SnapPoint => _snapPoint != null ? _snapPoint : transform;

        public bool Contains(Vector3 itemPosition)
        {
            // 忽略高度，比较水平距离；滤纸吸附时再调整高度。
            Vector3 difference = itemPosition - transform.position;
            difference.y = 0f;
            return difference.sqrMagnitude <= _radius * _radius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 previous = transform.position + Vector3.right * _radius;
            for (int i = 1; i <= 32; i++)
            {
                float angle = i * Mathf.PI * 2f / 32f;
                Vector3 next = transform.position +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
