using TapTapGameJam.MiniGame;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    // Draw the rule's actual samples above all tape images, including overlaps.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ParcelCutCheckpointGraphic : MaskableGraphic
    {
        private ParcelCutRound _round;
        private RectTransform[] _tapes;
        private float _radius = 5f;

        public void Configure(ParcelCutRound round, RectTransform[] tapes, float radius)
        {
            _round = round;
            _tapes = tapes;
            _radius = Mathf.Max(1f, radius);
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
                for (int point = 0; point < ParcelCutRound.CheckpointCount; point++)
                {
                    Vector2 normalized = _round.CheckpointPosition(tape, point);
                    Vector2 local = rect.rect.min + Vector2.Scale(normalized, rect.rect.size);
                    Vector2 center = rectTransform.InverseTransformPoint(rect.TransformPoint(local));
                    Color fill = _round.IsCheckpointCovered(tape, point)
                        ? new Color(0.15f, 0.85f, 0.35f) : new Color(1f, 0.35f, 0.05f);
                    Circle(mesh, center, _radius + 1.5f, new Color(0.1f, 0.1f, 0.1f));
                    Circle(mesh, center, _radius, fill);
                }
            }
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
