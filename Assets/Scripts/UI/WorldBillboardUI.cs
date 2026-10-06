using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 挂在 World Space Canvas 上，让它始终正对着相机。
    ///
    /// - target 就是自己挂在的那个 GameObject（自动取，不用配）。
    /// - 不处理任何 offset：要抬高 / 偏移，在 Canvas 下面挂一层子物体自己调。
    /// - 本脚本只改自己的 localRotation / localScale，并且会把父节点的旋转、缩放换算进去，
    ///   所以可以直接挂在会旋转、会缩放的物体下面，父物体和兄弟节点都不受影响。
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldBillboardUI : MonoBehaviour
    {
        [Tooltip("相机。留空则用 Camera.main")]
        [SerializeField] private Camera _camera;

        [Header("自动缩放")]
        [Tooltip("开启后按相机距离自动缩放，保证不同距离下看起来一样大")]
        [SerializeField] private bool _autoScale = false;

        [Tooltip("基准距离：相机离本物体这么远时，世界缩放正好是下面那个值")]
        [SerializeField, Min(0.01f)] private float _referenceDistance = 10f;

        [Tooltip("基准缩放：基准距离下的世界缩放")]
        [SerializeField] private float _baseScale = 1f;

        // 每帧只查一次相机，避免每个 billboard 各自去找
        private static Camera _cachedCamera;
        private static int _cachedFrame = -1;

        private Camera Cam
        {
            get
            {
                if (_camera != null)
                {
                    return _camera;
                }

                if (_cachedFrame != Time.frameCount)
                {
                    _cachedFrame = Time.frameCount;
                    _cachedCamera = Camera.main;
                }

                return _cachedCamera;
            }
        }

        // 用 LateUpdate：相机（Cinemachine）也是在 LateUpdate 里动的，
        // 放在 Update 里对齐会晚一帧，看起来会抖。
        private void LateUpdate()
        {
            Camera cam = Cam;
            if (cam == null)
            {
                return;
            }

            Transform camTransform = cam.transform;
            Transform parent = transform.parent;

            // ---- 旋转 ----
            // 想要的世界朝向：UI 的 +Z 指向相机，屏幕上看就是正视图
            Vector3 toCamera = transform.position - camTransform.position;
            if (toCamera.sqrMagnitude > 0.000001f)
            {
                Quaternion worldRotation = Quaternion.LookRotation(toCamera);

                // 换算回 local 空间：父节点转了、缩放了，这里自动抵消掉。
                // 直接写 transform.rotation 会把父节点的旋转算进去，父物体一动 UI 就歪。
                transform.localRotation = parent != null
                    ? Quaternion.Inverse(parent.rotation) * worldRotation
                    : worldRotation;
            }

            // ---- 自动缩放 ----
            if (!_autoScale)
            {
                return;
            }

            float worldScale = _baseScale * (toCamera.magnitude / _referenceDistance);

            // 注意：必须读 parent.lossyScale，不能读 transform.lossyScale。
            // transform.lossyScale 把自身的 localScale 也算在内，用它反解等于"读自己写自己"，
            // 会每帧来回震荡（表现就是相机不动也在抖）。
            if (parent == null)
            {
                transform.localScale = Vector3.one * worldScale;
                return;
            }

            // parent.lossyScale = 父节点自身缩放 × 父链，正好是"本节点 localScale 之外"的那部分
            Vector3 parentScale = parent.lossyScale;
            transform.localScale = new Vector3(
                parentScale.x > 0.000001f ? worldScale / parentScale.x : worldScale,
                parentScale.y > 0.000001f ? worldScale / parentScale.y : worldScale,
                parentScale.z > 0.000001f ? worldScale / parentScale.z : worldScale);
        }
    }
}
