using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫的固定巡逻路线：贴着行动区域边缘的一圈"圆角矩形"。
    ///
    /// 圆角不是画着好看的：直角处切线会突变，守卫只能原地急转；
    /// 换成 90° 圆弧之后切线连续变化，守卫顺着走就能平滑地把方向转过来。
    ///
    /// 这是个纯几何对象——不知道守卫、网格、日夜。
    /// 调用方只按"沿路线走了多远"来问位置和切线（<see cref="Sample"/>），
    /// 顺 / 逆时针在构造时定好，对外的"距离递增方向"永远是前进方向。
    /// </summary>
    public sealed class GuardPatrolPath
    {
        /// <summary>
        /// 一段路线：直线或 90° 圆弧。
        /// 整条路线只由这两类拼成，所以采样和投影都能解析算，不用采样成折线。
        /// </summary>
        private readonly struct Piece
        {
            public readonly bool IsArc;

            /// <summary>这一段在路线上的起点距离</summary>
            public readonly float StartDistance;

            public readonly float Length;

            /// <summary>直线：起点；圆弧：圆心</summary>
            public readonly Vector2 A;

            /// <summary>直线：终点；圆弧：不用</summary>
            public readonly Vector2 B;

            /// <summary>圆弧：起点极角（弧度，0 = +X）</summary>
            public readonly float StartAngle;

            /// <summary>圆弧：扫过的角，带符号（本类里只有 +π/2）</summary>
            public readonly float Turn;

            /// <summary>圆弧半径</summary>
            public readonly float Radius;

            private Piece(bool isArc, float startDistance, float length, Vector2 a, Vector2 b, float startAngle,
                float turn, float radius)
            {
                IsArc = isArc;
                StartDistance = startDistance;
                Length = length;
                A = a;
                B = b;
                StartAngle = startAngle;
                Turn = turn;
                Radius = radius;
            }

            public static Piece Line(float startDistance, Vector2 from, Vector2 to)
            {
                return new Piece(false, startDistance, Vector2.Distance(from, to), from, to, 0f, 0f, 0f);
            }

            public static Piece Arc(float startDistance, Vector2 center, float radius, float startAngle, float turn)
            {
                return new Piece(true, startDistance, Mathf.Abs(turn) * radius, center, center, startAngle, turn, radius);
            }
        }

        /// <summary>圆角矩形的最大内缩比例：内缩到这个份上再大就没路线了</summary>
        private const float MaxInsetRatio = 0.8f;

        private const float MinPieceLength = 1e-4f;

        private readonly List<Piece> _pieces;
        private readonly bool _reverse;

        /// <summary>整条路线的长度（世界单位）</summary>
        public float Length { get; }

        private GuardPatrolPath(List<Piece> pieces, float length, bool reverse)
        {
            _pieces = pieces;
            Length = length;
            _reverse = reverse;
        }

        /// <summary>
        /// 沿行动区域边缘拼一圈巡逻路线。
        /// </summary>
        /// <param name="region">行动区域</param>
        /// <param name="inset">路线离区域边界的内缩量（也用来避开贴墙）</param>
        /// <param name="cornerRadius">四个角的圆角半径，0 = 直角</param>
        /// <param name="clockwise">true = 俯视顺时针，false = 俯视逆时针</param>
        public static bool TryBuild(GuardRegion region, float inset, float cornerRadius, bool clockwise,
            out GuardPatrolPath path)
        {
            path = null;

            if (!region.IsValid)
            {
                return false;
            }

            Vector2 half = region.Size * 0.5f;
            float maxInset = Mathf.Min(half.x, half.y) * MaxInsetRatio;
            float clampedInset = Mathf.Clamp(inset, 0f, maxInset);
            Vector2 innerHalf = half - Vector2.one * clampedInset;

            if (innerHalf.x <= MinPieceLength || innerHalf.y <= MinPieceLength)
            {
                return false;
            }

            float radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(innerHalf.x, innerHalf.y));
            Vector2 center = region.Center;
            Vector2 min = center - innerHalf;
            Vector2 max = center + innerHalf;

            var pieces = new List<Piece>(8);
            float distance = 0f;

            if (radius <= MinPieceLength)
            {
                // 直角：四条边首尾相接，转向完全交给守卫自己的转向速度
                Vector2 bottomLeft = new Vector2(min.x, min.y);
                Vector2 bottomRight = new Vector2(max.x, min.y);
                Vector2 topRight = new Vector2(max.x, max.y);
                Vector2 topLeft = new Vector2(min.x, max.y);

                AddLine(pieces, ref distance, bottomLeft, bottomRight);
                AddLine(pieces, ref distance, bottomRight, topRight);
                AddLine(pieces, ref distance, topRight, topLeft);
                AddLine(pieces, ref distance, topLeft, bottomLeft);
            }
            else
            {
                // 圆角矩形，逆时针：底边向右 → 右下角弧 → 右边向上 → 右上角弧 → …
                AddLine(pieces, ref distance,
                    new Vector2(min.x + radius, min.y), new Vector2(max.x - radius, min.y));
                AddArc(pieces, ref distance, new Vector2(max.x - radius, min.y + radius), radius,
                    -Mathf.PI * 0.5f);
                AddLine(pieces, ref distance,
                    new Vector2(max.x, min.y + radius), new Vector2(max.x, max.y - radius));
                AddArc(pieces, ref distance, new Vector2(max.x - radius, max.y - radius), radius, 0f);
                AddLine(pieces, ref distance,
                    new Vector2(max.x - radius, max.y), new Vector2(min.x + radius, max.y));
                AddArc(pieces, ref distance, new Vector2(min.x + radius, max.y - radius), radius, Mathf.PI * 0.5f);
                AddLine(pieces, ref distance,
                    new Vector2(min.x, max.y - radius), new Vector2(min.x, min.y + radius));
                AddArc(pieces, ref distance, new Vector2(min.x + radius, min.y + radius), radius, Mathf.PI);
            }

            if (pieces.Count == 0 || distance <= MinPieceLength)
            {
                return false;
            }

            // 上面拼的是俯视逆时针的一圈；要顺时针就把遍历方向反过来
            path = new GuardPatrolPath(pieces, distance, clockwise);
            return true;
        }

        /// <summary>把距离折回 [0, Length)。调用方可以让它一直累加，不用自己回绕</summary>
        public float Wrap(float distance)
        {
            return Mathf.Repeat(distance, Length);
        }

        /// <summary>沿路线前进 <paramref name="distance"/> 处的位置和前进方向（单位向量）</summary>
        public void Sample(float distance, out Vector2 position, out Vector2 tangent)
        {
            float raw = Wrap(distance);

            // 反向遍历 = 从终点那头倒着走：位置取对称点，切线也取反
            if (_reverse)
            {
                raw = Wrap(Length - raw);
            }

            Evaluate(raw, out position, out tangent);

            if (_reverse)
            {
                tangent = -tangent;
            }
        }

        public Vector2 PositionAt(float distance)
        {
            Sample(distance, out Vector2 position, out _);
            return position;
        }

        /// <summary>
        /// 某个世界坐标在路线上最近的位置（返回沿路线前进的距离，已算上方向）。
        /// 守卫从区域外回来时用它"就地"接上路线。
        /// </summary>
        public float Project(Vector2 point)
        {
            float bestRaw = 0f;
            float bestSqr = float.PositiveInfinity;

            foreach (Piece piece in _pieces)
            {
                Vector2 closest = ClosestOnPiece(piece, point, out float localDistance);
                float sqr = (closest - point).sqrMagnitude;

                if (sqr >= bestSqr)
                {
                    continue;
                }

                bestSqr = sqr;
                bestRaw = piece.StartDistance + localDistance;
            }

            return _reverse ? Wrap(Length - bestRaw) : Wrap(bestRaw);
        }

        private static void AddLine(List<Piece> pieces, ref float distance, Vector2 from, Vector2 to)
        {
            float length = Vector2.Distance(from, to);
            if (length <= MinPieceLength)
            {
                return;
            }

            pieces.Add(Piece.Line(distance, from, to));
            distance += length;
        }

        /// <summary>加一段 90° 圆弧，从 <paramref name="startAngle"/> 逆时针扫 π/2</summary>
        private static void AddArc(List<Piece> pieces, ref float distance, Vector2 center, float radius,
            float startAngle)
        {
            const float turn = Mathf.PI * 0.5f;
            if (radius * turn <= MinPieceLength)
            {
                return;
            }

            pieces.Add(Piece.Arc(distance, center, radius, startAngle, turn));
            distance += radius * turn;
        }

        private void Evaluate(float rawDistance, out Vector2 position, out Vector2 tangent)
        {
            int index = FindPiece(rawDistance);
            Piece piece = _pieces[index];
            float local = rawDistance - piece.StartDistance;

            if (!piece.IsArc)
            {
                Vector2 direction = (piece.B - piece.A) / piece.Length;
                position = piece.A + direction * local;
                tangent = direction;
                return;
            }

            // 圆弧：local 是弧长，除以本段总长才是推进比例（不能直接除以半径，那会把扫角放大）
            float angle = piece.StartAngle + piece.Turn * (local / piece.Length);
            Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            position = piece.A + radial * piece.Radius;
            tangent = piece.Turn > 0f ? new Vector2(-radial.y, radial.x) : new Vector2(radial.y, -radial.x);
        }

        private int FindPiece(float rawDistance)
        {
            for (int i = 0; i < _pieces.Count - 1; i++)
            {
                Piece piece = _pieces[i];
                if (rawDistance < piece.StartDistance + piece.Length)
                {
                    return i;
                }
            }

            return _pieces.Count - 1;
        }

        private static Vector2 ClosestOnPiece(Piece piece, Vector2 point, out float localDistance)
        {
            if (!piece.IsArc)
            {
                Vector2 direction = piece.B - piece.A;
                float lengthSqr = direction.sqrMagnitude;
                float t = lengthSqr > 1e-8f
                    ? Mathf.Clamp01(Vector2.Dot(point - piece.A, direction) / lengthSqr)
                    : 0f;

                localDistance = t * piece.Length;
                return piece.A + direction * t;
            }

            float angle = Mathf.Atan2(point.y - piece.A.y, point.x - piece.A.x);
            float delta = NormAng(angle - piece.StartAngle);
            float clamped = piece.Turn >= 0f
                ? Mathf.Clamp(delta, 0f, piece.Turn)
                : Mathf.Clamp(delta, piece.Turn, 0f);

            localDistance = Mathf.Abs(clamped) * piece.Radius;

            float clampedAngle = piece.StartAngle + clamped;
            return piece.A + new Vector2(Mathf.Cos(clampedAngle), Mathf.Sin(clampedAngle)) * piece.Radius;
        }

        /// <summary>把角度归一化到 (-π, π]</summary>
        private static float NormAng(float angle)
        {
            return Mathf.Repeat(angle + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
        }
    }
}
