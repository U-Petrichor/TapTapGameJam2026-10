using TapTapGameJam.MiniGame;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    // Draw a persistent cutting guide through the rule's actual samples.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ParcelCutCheckpointGraphic : MaskableGraphic
    {
        private ParcelCutRound _round;
        private RectTransform[] _tapes;
        private float _radius = 5f;
        private bool _showCheckpoints;

        private const float DashLength = 12f;
        private const float GapLength = 8f;
        private const float LineWidth = 3f;

        public void Configure(ParcelCutRound round, RectTransform[] tapes, float radius, bool showCheckpoints = false)
        {
            _round = round;
            _tapes = tapes;
            _radius = Mathf.Max(1f, radius);
            _showCheckpoints = showCheckpoints;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_round == null || _tapes == null) return;
            for (int tape = 0; tape < _tapes.Length; tape++)
            {
                var rect = _tapes[tape];
                if (rect == null || !rect.gameObject.activeInHierarchy ||
                    !_round.IsTarget(tape) || _round.IsCompleted(tape)) continue;
                DrawGuide(mesh, tape, rect);
                if (!_showCheckpoints) continue;
                for (int point = 0; point < ParcelCutRound.CheckpointCount; point++)
                {
                    Vector2 center = CheckpointLocalPosition(tape, point, rect);
                    Color fill = _round.IsCheckpointCovered(tape, point)
                        ? new Color(0.15f, 0.85f, 0.35f) : new Color(1f, 0.35f, 0.05f);
                    Circle(mesh, center, _radius + 1.5f, new Color(0.1f, 0.1f, 0.1f));
                    Circle(mesh, center, _radius, fill);
                }
            }
        }

        private Vector2 CheckpointLocalPosition(int tape, int point, RectTransform rect)
        {
            Vector2 normalized = _round.CheckpointPosition(tape, point);
            Vector2 local = rect.rect.min + Vector2.Scale(normalized, rect.rect.size);
            return rectTransform.InverseTransformPoint(rect.TransformPoint(local));
        }

        private void DrawGuide(VertexHelper mesh, int tape, RectTransform rect)
        {
            // Keep the dash phase continuous across checkpoints, including bends.
            float distance = 0f;
            Vector2 from = CheckpointLocalPosition(tape, 0, rect);
            for (int point = 1; point < ParcelCutRound.CheckpointCount; point++)
            {
                Vector2 to = CheckpointLocalPosition(tape, point, rect);
                float length = Vector2.Distance(from, to);
                float offset = 0f;
                while (offset < length)
                {
                    float phase = Mathf.Repeat(distance + offset, DashLength + GapLength);
                    bool dash = phase < DashLength;
                    float step = Mathf.Min(length - offset,
                        (dash ? DashLength : DashLength + GapLength) - phase);
                    if (step <= 0.0001f) step = Mathf.Min(length - offset, 0.0001f);
                    if (dash)
                    {
                        Vector2 start = Vector2.Lerp(from, to, offset / length);
                        Vector2 end = Vector2.Lerp(from, to, (offset + step) / length);
                        Segment(mesh, start, end, LineWidth + 2f, new Color(0.12f, 0.12f, 0.12f, 0.85f));
                        Segment(mesh, start, end, LineWidth, Color.white);
                    }
                    offset += step;
                }
                distance += length;
                from = to;
            }
        }

        private static void Segment(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color fill)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            int start = mesh.currentVertCount;
            mesh.AddVert(from - normal, fill, Vector2.zero);
            mesh.AddVert(from + normal, fill, Vector2.zero);
            mesh.AddVert(to + normal, fill, Vector2.zero);
            mesh.AddVert(to - normal, fill, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Circle(VertexHelper mesh, Vector2 center, float radius, Color fill)
        {
            const int sides = 12;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, fill, Vector2.zero);
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, fill, Vector2.zero);
            }
            for (int side = 0; side < sides; side++)
                mesh.AddTriangle(start, start + 1 + side, start + 1 + (side + 1) % sides);
        }
    }
}
