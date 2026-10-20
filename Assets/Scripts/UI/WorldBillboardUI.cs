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
    /// - 勾上 <see cref="_yawOnly"/> 时只驱动水平朝向（yaw），俯仰 / 翻滚保持作者摆好的样子，
    ///   给平躺在地面上的 UI 用：它会绕着竖直轴转向相机，但不会立起来。
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldBillboardUI : MonoBehaviour
    {
        [Tooltip("相机。留空则用 Camera.main")]
        [SerializeField] private Camera _camera;

        [Header("朝向")]
        [Tooltip("只转 Yaw。勾上后只改水平朝向，物体保留作者摆好的俯仰 / 翻滚（平躺的地面 UI 用它）")]
        [SerializeField] private bool _yawOnly = false;

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

        /// <summary>作者摆好的局部欧拉角。yaw-only 模式下只改它的 y，x / z 原样保留</summary>
        private Vector3 _authoredEuler;

        /// <summary>目标 yaw（世界空间）。相机正上正下看时水平方向会退化，那时保持上一次的值</summary>
        private float _targetYaw;

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

        private void Awake()
        {
            // 记下作者摆好的俯仰 / 翻滚：yaw-only 模式下每帧只覆盖 yaw，这两个原样保留
            _authoredEuler = transform.localEulerAngles;
            _targetYaw = transform.eulerAngles.y;
        }

        /// <summary>
        /// 只驱动 yaw：物体会保留作者摆好的俯仰 / 翻滚（平躺的地面 UI 不会立起来），
        /// 水平朝向则跟着相机转。
        ///
        /// 父节点带 yaw 时会先减掉它，才能换算回 local；
        /// 父节点如果本身是倾斜的（不常见），这里的换算只是近似。
        /// </summary>
        private void ApplyYawOnlyRotation(Transform camTransform, Transform parent)
        {
            // 目标朝向 = 相机 forward 拍扁到水平面。
            // 不用 LookRotation(相机位置 - 自己)：UI 偏离画面中心时会歪，理由和全量对齐那段一样。
            Vector3 forward = camTransform.forward;
            forward.y = 0f;

            // 相机正上 / 正下看时水平方向退化（此时任何 yaw 都算"正对"），保持上一次的朝向别乱转
            if (forward.sqrMagnitude > 0.000001f)
            {
                _targetYaw = Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;
            }

            float parentYaw = parent != null ? parent.eulerAngles.y : 0f;
            float localYaw = Mathf.DeltaAngle(0f, _targetYaw - parentYaw);

            transform.localRotation = Quaternion.Euler(_authoredEuler.x, localYaw, _authoredEuler.z);
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
            if (_yawOnly)
            {
                // 平躺的地面 UI：只改水平朝向，俯仰 / 翻滚保持作者摆好的样子
                ApplyYawOnlyRotation(camTransform, parent);
            }
            else
            {
                // 直接抄相机的世界旋转，让 UI 平面和近裁剪面共面。
                // 注意不要用 Quaternion.LookRotation(自己 - 相机)：那是让 UI 朝向"相机所在的位置"，
                // 只有 UI 正好在画面中心时两者才等价。一旦偏离中心，UI 平面就与近裁剪面不平行，
                // 透视投影会把它投成梯形 —— 屏幕上看起来就是"斜过来"。
                // 对齐相机 forward 相当于把相机当正交用，屏幕任意位置都是正的。
                Quaternion worldRotation = camTransform.rotation;

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

            float worldScale = _baseScale * (Vector3.Distance(transform.position, camTransform.position) / _referenceDistance);

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
