using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 巢穴范围虚线圈。自己生成网格和材质、自己按当前指令旋转与换色，
    /// 什么时候显示由 CockNest 决定（只在夜间 + 已解锁 + 没被玩家搬着时显示）。
    ///
    /// 注意：旋转和网格都作用在它自己的 transform 上，所以要挂在一个专用的子物体上。
    /// CockNest 在找不到时会运行时建一个 "RangeRing" 子物体挂上，预制体不用改。
    /// </summary>
    [DisallowMultipleComponent]
    public class CockNestRing : MonoBehaviour
    {
        [Header("形状")]
        [Tooltip("环的线宽（世界单位）")]
        [SerializeField, Min(0f)] private float _width = 0.2f;

        [Tooltip("期望的单段实线弧长（世界单位）。段数由半径反推，半径越大段数越多")]
        [SerializeField, Min(0f)] private float _targetDashLength = 0.3f;

        [Tooltip("环的不透明度。颜色本身跟着当前指令走（见 CockNest.HatchColor / ForagingColor）")]
        [SerializeField, Range(0f, 1f)] private float _alpha = 0.35f;

        [Header("自转")]
        [Tooltip("孵化指令下的自转速度（度/秒），顺时针")]
        [SerializeField] private float _hatchSpinSpeed = 4f;

        [Tooltip("觅食指令下的自转速度（度/秒），逆时针")]
        [SerializeField] private float _foragingSpinSpeed = 16f;

        [Header("渲染")]
        [Tooltip("画范围圈的层。需要在 UI 相机的 Culling Mask 里，才能盖在 3D 物体上层")]
        [SerializeField] private string _layerName = "WorldUI";

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _material;
        private float _builtRadius = -1f;
        private CockroachCommand _command = CockroachCommand.Hatch;
        private bool _visible;

        /// <summary>已经生成过网格的半径；-1 表示还没生成过</summary>
        public float BuiltRadius => _builtRadius;

        /// <summary>当前是否显示</summary>
        public bool IsVisible => _visible;

        private void Awake()
        {
            EnsureComponents();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            // 运行时生成的网格和材质要自己释放，否则换场景会一直漏
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        private void Update()
        {
            if (!_visible) return;

            // 顺时针为正角度，所以孵化给正值、觅食给负值
            float speed = _command == CockroachCommand.Hatch ? _hatchSpinSpeed : -_foragingSpinSpeed;
            transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
        }

        /// <summary>
        /// 按半径生成（或重建）网格。半径和上次一样就跳过；
        /// 巢穴范围可能变化，所以 CockNest 在每次进入夜晚时会调一次。
        /// </summary>
        public void Regenerate(float radius)
        {
            EnsureComponents();

            radius = Mathf.Max(0.0001f, radius);
            if (_mesh != null && Mathf.Approximately(radius, _builtRadius)) return;

            if (_mesh != null) Destroy(_mesh);

            _builtRadius = radius;
            int dashCount = CockroachNestRangeRing.DashesFor(radius, _targetDashLength);
            _mesh = CockroachNestRangeRing.Build(radius, _width, dashCount);
            _meshFilter.sharedMesh = _mesh;

            if (_material == null)
            {
                _material = CockroachNestRangeRing.CreateUnlitMaterial(ColorOf(_command, _alpha));
            }
            else
            {
                ApplyColor();
            }

            _meshRenderer.sharedMaterial = _material;
        }

        /// <summary>切换当前指令：颜色跟着在孵化色/觅食色之间换</summary>
        public void SetCommand(CockroachCommand command)
        {
            if (_command == command) return;

            _command = command;
            ApplyColor();
        }

        /// <summary>显示/隐藏。隐藏时不再自转，网格和材质保留着下次直接用</summary>
        public void SetVisible(bool visible)
        {
            EnsureComponents();

            _visible = visible;
            _meshRenderer.enabled = visible;
        }

        /// <summary>把当前指令对应的颜色套到材质上</summary>
        private void ApplyColor()
        {
            if (_material == null) return;

            Color color = ColorOf(_command, _alpha);

            // 显式写两个名字：URP Unlit 用的是 _BaseColor，_Color 是为了兼容老材质
            if (_material.HasProperty("_BaseColor"))
            {
                _material.SetColor("_BaseColor", color);
            }

            _material.SetColor("_Color", color);
        }

        /// <summary>取某个指令的颜色，alpha 换成环专用的透明度。颜色源头在 CockNest</summary>
        private static Color ColorOf(CockroachCommand command, float alpha)
        {
            Color color = command == CockroachCommand.Hatch ? CockNest.HatchColor : CockNest.ForagingColor;
            color.a = alpha;
            return color;
        }

        private void EnsureComponents()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();

            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            int layer = LayerMask.NameToLayer(_layerName);
            if (layer >= 0) gameObject.layer = layer;
        }
    }
}
