using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>Animates an authored Image without changing its layout or sprite.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class ArcLiquidProgress : MonoBehaviour
    {
        [Header("Progress (0 = empty, 1 = full)")]
        [Range(0, 1)] public float progress = .55f;
        [Tooltip("Visual demonstration only. Disable when driving progress from gameplay.")]
        public bool demoLoop;
        [Min(1)] public float demoDuration = 8;
        [Min(0)] public float followSpeed = 7;

        [Header("Liquid appearance")]
        public Color liquidColor = new Color(1, .19f, .055f, 1);
        public Color highlightColor = new Color(1, .79f, .28f, 1);
        [Range(0, .12f)] public float waveAmplitude = .035f;
        [Range(0, 4)] public float flowSpeed = 1;
        [Range(0, 1)] public float bubbleStrength = .8f;

        [Header("Arc mapping in sprite coordinates")]
        public Vector2 arcCenter = new Vector2(.5f, .439f);
        public float startAngle = 206;
        [Range(1, 359)] public float sweepAngle = 172;
        public Vector2 innerOuterRadius = new Vector2(.232f, .34f);
        [SerializeField] private Material liquidMaterial;

        private Image image;
        private Material instance;
        private Material previousMaterial;
        private float displayed;
        private float surge;
        private double previousTime;
        private double demoStarted;

        public float DisplayedProgress => displayed;
        public void SetProgress(float value) { progress = Mathf.Clamp01(value); }

        private void OnEnable()
        {
            image = GetComponent<Image>();
            previousMaterial = image.material;
            displayed = Mathf.Clamp01(progress);
            previousTime = demoStarted = CurrentTime;
            if (liquidMaterial != null)
            {
                instance = new Material(liquidMaterial) { hideFlags = HideFlags.HideAndDontSave };
                image.material = instance;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += EditorTick;
#endif
            Tick(CurrentTime);
        }

        private double CurrentTime
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) return UnityEditor.EditorApplication.timeSinceStartup;
#endif
                return Time.unscaledTimeAsDouble;
            }
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (Application.isPlaying || this == null || !isActiveAndEnabled) return;
            double now = CurrentTime;
            if (now - previousTime < 1.0 / 30) return;
            Tick(now);
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
#endif
        private void Update() { if (Application.isPlaying) Tick(CurrentTime); }

        private void Tick(double now)
        {
            if (instance == null || image == null) return;
            float dt = Mathf.Clamp((float)(now - previousTime), 0, .1f);
            previousTime = now;
            float target = demoLoop ? Mathf.PingPong((float)(now - demoStarted) / Mathf.Max(1, demoDuration), 1) : Mathf.Clamp01(progress);
            float before = displayed;
            displayed = !Application.isPlaying || followSpeed <= 0 ? target : Mathf.Lerp(displayed, target, 1 - Mathf.Exp(-followSpeed * dt));
            if (Mathf.Abs(displayed - target) < .0001f) displayed = target;
            surge = Mathf.Max(surge * Mathf.Exp(-5 * dt), Mathf.Clamp01(Mathf.Abs(displayed - before) * 14));
            instance.SetFloat("_Progress", displayed);
            instance.SetFloat("_LiquidTime", (float)(now % 3600));
            instance.SetColor("_LiquidColor", liquidColor);
            instance.SetColor("_HighlightColor", highlightColor);
            instance.SetVector("_Arc", new Vector4(arcCenter.x, arcCenter.y, startAngle * Mathf.Deg2Rad, sweepAngle * Mathf.Deg2Rad));
            instance.SetVector("_Radii", new Vector4(innerOuterRadius.x, innerOuterRadius.y, waveAmplitude, flowSpeed));
            instance.SetFloat("_Bubbles", bubbleStrength);
            instance.SetFloat("_Surge", surge);
            var sprite = image.overrideSprite;
            Vector4 uv = sprite != null ? DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);
            instance.SetVector("_SpriteUV", uv);
            instance.SetFloat("_Aspect", sprite != null ? sprite.rect.height / sprite.rect.width : 1);
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
            if (image != null && image.material == instance) image.material = previousMaterial;
            if (instance != null)
            {
                if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
            }
            instance = null;
        }
    }
}
