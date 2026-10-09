using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>挂在守卫的 trigger 子物体上，把碰撞事件转回 Guard。</summary>
    public class GuardTrigger : MonoBehaviour
    {
        Guard _guard;
        bool _isVision;

        public void BindGuard(Guard guard, bool isVision)
        {
            _guard = guard;
            _isVision = isVision;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_guard != null) _guard.OnPartEnter(other, _isVision);
        }

        void OnTriggerExit(Collider other)
        {
            if (_guard != null) _guard.OnPartExit(other, _isVision);
        }
    }
}
