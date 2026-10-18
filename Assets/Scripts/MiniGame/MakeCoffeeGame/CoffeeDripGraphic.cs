using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    // 使用本地 UI 坐标绘制细流和下落液滴，随咖啡机缩放，无需额外贴图。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CoffeeDripGraphic : MaskableGraphic
    {
        private float _elapsed;

        protected override void OnEnable()
        {
            base.OnEnable();
            _elapsed = 0f;
            raycastTarget = false;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            const int segments = 16;
            float halfWidth = bounds.width * (0.19f + 0.025f * Mathf.Sin(_elapsed * 19f));
            // 细流占上半段，底部液滴留出断续过滤的间隙。
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float y = Mathf.Lerp(bounds.yMax, bounds.yMin + bounds.height * 0.3f, t);
                float x = bounds.center.x + Mathf.Sin(t * 9f - _elapsed * 13f) * bounds.width * 0.08f * t;
                mesh.AddVert(new Vector3(x - halfWidth, y), color, Vector2.zero);
                mesh.AddVert(new Vector3(x + halfWidth, y), color, Vector2.zero);
                if (i == 0) continue;
                int start = (i - 1) * 2;
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start + 1, start + 3, start + 2);
            }
            for (int drop = 0; drop < 3; drop++)
            {
                float t = Mathf.Repeat(_elapsed * 2.2f + drop / 3f, 1f);
                float y = Mathf.Lerp(bounds.yMin + bounds.height * 0.4f, bounds.yMin, t);
                float radius = bounds.width * 0.3f;
                int start = mesh.currentVertCount;
                mesh.AddVert(new Vector3(bounds.center.x, y), color, Vector2.zero);
                const int sides = 8;
                for (int i = 0; i <= sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    mesh.AddVert(new Vector3(bounds.center.x + Mathf.Cos(angle) * radius,
                        y + Mathf.Sin(angle) * radius * 1.5f), color, Vector2.zero);
                    if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
                }
            }
        }
    }
}
