using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>守卫身上的哪一块 trigger 报上来的。两块的回调不同：视野收"看见的"，身体收"贴脸的"。</summary>
    public enum GuardPart
    {
        /// <summary>视野扇形</summary>
        Vision,

        /// <summary>身体球（贴脸了）</summary>
        Body
    }

    /// <summary>
    /// 挂在守卫的 trigger 子物体上（视野扇形 / 身体球），只干一件事：
    /// 把 Unity 的碰撞回调转给 <see cref="Guard"/>，并说明是哪一块报上来的。
    /// 这里不做过滤、不判层、不认蟑螂，那些都是 Guard 的职责。
    /// </summary>
    public class GuardTrigger : MonoBehaviour
    {
        private Guard _guard;
        private GuardPart _part;

        /// <summary>由 <see cref="Guard"/> 在搭建时调用，绑定归属和部位</summary>
        public void BindGuard(Guard guard, GuardPart part)
        {
            _guard = guard;
            _part = part;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_guard == null)
            {
                return;
            }

            _guard.OnPartEnter(other, _part);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_guard == null)
            {
                return;
            }

            _guard.OnPartExit(other, _part);
        }
    }
}
