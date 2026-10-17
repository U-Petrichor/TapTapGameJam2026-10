using UnityEngine;
using UnityEngine.Assertions;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 喷雾守卫的表现层：一层"雾壳" + 视野灯的预警 / 闪光。
    ///
    /// 职责边界：只管画。什么时候喷、喷到谁，全在 <see cref="SprayGuard"/> 里。
    /// 雾壳的锥和判定锥同源（都用 <see cref="GuardConeMesh"/> + 同一套 range / halfAngle），
    /// 所以玩家看到的就是真正会打到的范围。
    ///
    /// 时间轴：
    ///   蓄势：雾淡入到 <see cref="Settings.AimAlpha"/>，灯光染成预警色并提亮
    ///   释放：_Sweep 从 0 推到 1.2（雾从喷嘴推出去），随后 alpha 淡回 0、灯光回落
    /// 释放的总时长由 <see cref="PlaySpray"/> 返回给逻辑层，两边不会对不上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuardSprayView : MonoBehaviour
    {
        /// <summary>外观参数。挂在 SprayGuard 上，方便在 Inspector 里现场调</summary>
        [System.Serializable]
        public sealed class Settings
        {
            [Header("时间轴（秒）")]
            [Tooltip("蓄势时雾淡入的时间")]
            public float AimFadeDuration = 0.18f;

            [Tooltip("雾从喷嘴推到远端的时间")]
            public float ReleaseSweepDuration = 0.22f;

            [Tooltip("推完之后淡出的时间")]
            public float ReleaseFadeDuration = 0.22f;

            [Header("浓度")]
            [Range(0f, 1f)] public float AimAlpha = 0.09f;

            [Range(0f, 1f)] public float ReleaseAlpha = 0.5f;

            [Header("颜色")]
            public Color SprayColor = new Color(0.62f, 1f, 0.55f, 1f);

            public Color AimLightColor = new Color(0.75f, 1f, 0.55f, 1f);

            [Header("预警灯光")]
            [Tooltip("蓄势时灯光亮度倍率（相对 prefab 里的原始亮度）")]
            public float AimLightIntensityMul = 1.6f;

            [Tooltip("喷出去那一下的灯光亮度倍率")]
            public float ReleaseLightIntensityMul = 3f;
        }

        /// <summary>雾的材质放在 Resources 里：不用手拖引用，也不会被打包时剔除</summary>
        private const string MaterialResourcePath = "GuardSprayCone";

        /// <summary>雾壳网格的横向分段数，和视野锥一致</summary>
        private const int ShellSegments = 24;

        /// <summary>_Sweep 推到这里就完全越过锥的远端（锥的轴向 uv 最大是 1）</summary>
        private const float SweepEnd = 1.2f;

        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int SweepId = Shader.PropertyToID("_Sweep");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private enum Mode
        {
            Hidden,
            Aiming,
            Releasing
        }

        private Settings _settings;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Light _light;

        private Color _baseLightColor;
        private float _baseLightIntensity;

        /// <summary>材质缺失时整层雾都不画（灯光预警照旧），错误已经在 Bind 里报过了</summary>
        private bool _canDraw;

        private Mode _mode = Mode.Hidden;
        private float _clock;

        /// <summary>释放阶段的总时长（雾推出 + 淡出），逻辑层用它决定"定住多久"</summary>
        public float ReleaseDuration => _settings == null
            ? 0f
            : _settings.ReleaseSweepDuration + _settings.ReleaseFadeDuration;

        /// <summary>
        /// 搭好雾壳并接管视野灯。只调一次（由 <see cref="SprayGuard"/> 在 Awake 里调）。
        /// </summary>
        /// <param name="visionLight">视野灯，用来做蓄势预警和释放闪光</param>
        /// <param name="nozzleLocalPosition">喷嘴位置（守卫本地坐标），雾壳的底边会落在这个高度</param>
        /// <param name="range">喷雾距离，和视野距离同源</param>
        /// <param name="halfAngle">喷雾半角，和视野半角同源</param>
        /// <param name="height">雾壳的竖直厚度</param>
        /// <param name="settings">外观参数</param>
        public void Bind(Light visionLight, Vector3 nozzleLocalPosition, float range, float halfAngle, float height,
            Settings settings)
        {
            Assert.IsNotNull(settings, "喷雾外观参数没有实例");
            Assert.IsNotNull(visionLight, "喷雾特效要用视野灯做预警 / 闪光，但守卫身上没有 Light");

            if (settings == null || visionLight == null)
            {
                enabled = false;
                return;
            }

            _settings = settings;
            _light = visionLight;
            _baseLightColor = visionLight.color;
            _baseLightIntensity = visionLight.intensity;

            // 雾壳的底边对齐喷嘴，往上长 height：雾贴着地面铺开，不会插进地板里
            transform.localPosition = nozzleLocalPosition + Vector3.up * (height * 0.5f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            MeshFilter filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            if (filter == null)
            {
                filter = gameObject.AddComponent<MeshFilter>();
            }

            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<MeshRenderer>();
            }

            filter.sharedMesh = GuardConeMesh.Build(range, halfAngle, height, ShellSegments, "GuardSprayCone");

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _renderer.sharedMaterial = Resources.Load<Material>(MaterialResourcePath);
            _renderer.enabled = false;

            _canDraw = _renderer.sharedMaterial != null;
            Assert.IsTrue(_canDraw, $"找不到喷雾材质 Resources/{MaterialResourcePath}：雾会看不见（判定不受影响）");

            _block = new MaterialPropertyBlock();
            _block.SetColor(ColorId, _settings.SprayColor);
        }

        /// <summary>开始蓄势：雾淡入 + 灯光转预警色。重复调用只重置时间轴</summary>
        public void ShowAiming()
        {
            if (_light == null)
            {
                return;
            }

            _mode = Mode.Aiming;
            _clock = 0f;
            _renderer.enabled = _canDraw;
            ApplyCone(0f, SweepEnd);
        }

        /// <summary>释放喷雾：雾向前推出再淡出。返回释放阶段总时长</summary>
        public float PlaySpray()
        {
            if (_light == null)
            {
                return 0f;
            }

            _mode = Mode.Releasing;
            _clock = 0f;
            _renderer.enabled = _canDraw;
            ApplyCone(_settings.AimAlpha, 0f);

            return ReleaseDuration;
        }

        /// <summary>立刻收起雾并还原灯光（白天、销毁前都走这里）</summary>
        public void Hide()
        {
            _mode = Mode.Hidden;

            if (_renderer != null)
            {
                _renderer.enabled = false;
            }

            RestoreLight();
        }

        private void Update()
        {
            switch (_mode)
            {
                case Mode.Aiming:
                    TickAiming();
                    break;

                case Mode.Releasing:
                    TickReleasing();
                    break;
            }
        }

        private void TickAiming()
        {
            _clock += Time.deltaTime;

            float t = Progress(_clock, _settings.AimFadeDuration);
            ApplyCone(Mathf.Lerp(0f, _settings.AimAlpha, t), SweepEnd);
            ApplyLight(Mathf.Lerp(1f, _settings.AimLightIntensityMul, t),
                Color.Lerp(_baseLightColor, _settings.AimLightColor, t));
        }

        private void TickReleasing()
        {
            _clock += Time.deltaTime;

            float sweepT = Progress(_clock, _settings.ReleaseSweepDuration);
            float fadeT = Progress(_clock - _settings.ReleaseSweepDuration, _settings.ReleaseFadeDuration);

            float sweep = Mathf.Lerp(0f, SweepEnd, sweepT);
            float alpha = sweepT < 1f
                ? Mathf.Lerp(_settings.AimAlpha, _settings.ReleaseAlpha, sweepT)
                : Mathf.Lerp(_settings.ReleaseAlpha, 0f, fadeT);

            ApplyCone(alpha, sweep);
            ApplyLight(Mathf.Lerp(_settings.ReleaseLightIntensityMul, 1f, fadeT),
                Color.Lerp(_settings.AimLightColor, _baseLightColor, fadeT));

            if (fadeT >= 1f)
            {
                Hide();
            }
        }

        private static float Progress(float elapsed, float duration)
        {
            return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
        }

        private void ApplyCone(float alpha, float sweep)
        {
            if (!_canDraw)
            {
                return;
            }

            // 颜色也走 MPB：同一份材质可以被多个守卫共享，各自有自己的 tint
            _block.SetColor(ColorId, _settings.SprayColor);
            _block.SetFloat(AlphaId, alpha);
            _block.SetFloat(SweepId, sweep);
            _renderer.SetPropertyBlock(_block);
        }

        private void ApplyLight(float intensityMultiplier, Color color)
        {
            _light.intensity = _baseLightIntensity * intensityMultiplier;
            _light.color = color;
        }

        private void RestoreLight()
        {
            if (_light == null)
            {
                return;
            }

            _light.intensity = _baseLightIntensity;
            _light.color = _baseLightColor;
        }
    }
}
