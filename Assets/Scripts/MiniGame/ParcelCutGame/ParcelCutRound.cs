using System;
using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    /// <summary>
    /// Pure-ish rules for the parcel tape cutting mini-game. Coordinates are
    /// normalized to each tape image: (0, 0) is its bottom-left corner.
    /// </summary>
    public sealed class ParcelCutRound
    {
        public const int TapeCount = 6;
        public const int RequiredTapeCount = 3;
        public const int CheckpointCount = 32;
        public const float DefaultCompletionThreshold = 0.6f;

        private readonly Vector2[][] _paths;
        private readonly bool[] _targets = new bool[TapeCount];
        private readonly bool[] _completed = new bool[TapeCount];
        private readonly bool[][] _covered = new bool[TapeCount][];
        private readonly List<int> _targetIndices = new List<int>(RequiredTapeCount);
        private readonly float _tolerance;
        private readonly Vector2[][] _checkpoints = new Vector2[TapeCount][];
        private readonly Vector2[][] _outlines;
        private readonly float[][] _radii = new float[TapeCount][];
        private readonly Vector2[] _metricSizes = new Vector2[TapeCount];
        private readonly int[] _coveredCounts = new int[TapeCount];
        private bool _running = true;

        public int RequiredCheckpointCount { get; }

        public IReadOnlyList<int> TargetIndices => _targetIndices;
        public int CompletedCount { get; private set; }
        public bool IsComplete => CompletedCount >= RequiredTapeCount;

        public ParcelCutRound(IReadOnlyList<IReadOnlyList<Vector2>> paths,
            System.Random random = null, float tolerance = 0.25f,
            float completionThreshold = DefaultCompletionThreshold,
            IReadOnlyList<IReadOnlyList<Vector2>> outlines = null)
        {
            if (paths == null || paths.Count != TapeCount)
                throw new ArgumentException($"需要 {TapeCount} 条胶带路径。", nameof(paths));
            if (random == null) random = new System.Random();
            if (float.IsNaN(tolerance) || float.IsInfinity(tolerance) || tolerance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(tolerance));
            if (float.IsNaN(completionThreshold) || completionThreshold <= 0 || completionThreshold > 1)
                throw new ArgumentOutOfRangeException(nameof(completionThreshold));
            if (outlines != null && outlines.Count != TapeCount) throw new ArgumentException("胶带轮廓数量不匹配。", nameof(outlines));
            RequiredCheckpointCount = (int)Math.Ceiling(CheckpointCount * (double)completionThreshold - 0.000001);

            _tolerance = tolerance;
            _paths = new Vector2[TapeCount][];
            _outlines = new Vector2[TapeCount][];
            for (int i = 0; i < TapeCount; i++)
            {
                if (paths[i] == null || paths[i].Count < 2)
                    throw new ArgumentException("每条胶带至少需要两个路径点。", nameof(paths));
                _paths[i] = new Vector2[paths[i].Count];
                for (int p = 0; p < paths[i].Count; p++)
                {
                    Vector2 point = paths[i][p];
                    if (float.IsNaN(point.x) || float.IsNaN(point.y) ||
                        float.IsInfinity(point.x) || float.IsInfinity(point.y))
                        throw new ArgumentException("路径点必须是有限坐标。", nameof(paths));
                    _paths[i][p] = new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
                }
                _covered[i] = new bool[CheckpointCount];
                _checkpoints[i] = new Vector2[CheckpointCount];
                _radii[i] = new float[CheckpointCount];
                for (int p = 0; p < CheckpointCount; p++)
                    _checkpoints[i][p] = PointOnPath(_paths[i], p / (float)(CheckpointCount - 1));
                if (outlines != null)
                {
                    if (outlines[i] == null || outlines[i].Count < 3) throw new ArgumentException("缺少胶带轮廓。", nameof(outlines));
                    _outlines[i] = new Vector2[outlines[i].Count];
                    for (int p = 0; p < outlines[i].Count; p++)
                    {
                        if (!Finite(outlines[i][p])) throw new ArgumentException("胶带轮廓坐标无效。", nameof(outlines));
                        _outlines[i][p] = outlines[i][p];
                    }
                }
            }

            var pool = new List<int>(TapeCount);
            for (int i = 0; i < TapeCount; i++) pool.Add(i);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                int value = pool[i];
                pool[i] = pool[other];
                pool[other] = value;
            }
            for (int i = 0; i < RequiredTapeCount; i++)
            {
                int tape = pool[i];
                _targets[tape] = true;
                _targetIndices.Add(tape);
            }
        }

        public bool IsTarget(int tapeIndex) => ValidIndex(tapeIndex) && _targets[tapeIndex];
        public bool IsCompleted(int tapeIndex) => ValidIndex(tapeIndex) && _completed[tapeIndex];

        public Vector2 CheckpointPosition(int tapeIndex, int checkpoint)
        {
            if (!ValidIndex(tapeIndex)) throw new ArgumentOutOfRangeException(nameof(tapeIndex));
            if (checkpoint < 0 || checkpoint >= CheckpointCount)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            return _checkpoints[tapeIndex][checkpoint];
        }

        public bool IsCheckpointCovered(int tapeIndex, int checkpoint) =>
            ValidIndex(tapeIndex) && checkpoint >= 0 && checkpoint < CheckpointCount &&
            _covered[tapeIndex][checkpoint];

        public float ProgressFor(int tapeIndex)
        {
            if (!ValidIndex(tapeIndex) || !_targets[tapeIndex]) return 0f;
            if (_completed[tapeIndex]) return 1f;
            return CoverageFor(tapeIndex);
        }

        public int CoveredCountFor(int tapeIndex) => ValidIndex(tapeIndex) ? _coveredCounts[tapeIndex] : 0;
        public float CoverageFor(int tapeIndex) => CoveredCountFor(tapeIndex) / (float)CheckpointCount;

        /// <summary>
        /// Covers unique samples touched by the continuous pointer segment.
        /// Coordinates stay normalized for storage; distances use the UI dimensions.
        /// </summary>
        public bool TryCutSegment(int tapeIndex, Vector2 from, Vector2 to)
            => TryCutSegment(tapeIndex, from, to, new Vector2(1f, 1f));

        public bool TryCutSegment(int tapeIndex, Vector2 from, Vector2 to, Vector2 uiSize)
        {
            if (!_running || IsComplete || !IsTarget(tapeIndex) || IsCompleted(tapeIndex)) return false;
            if (!Finite(from) || !Finite(to) || !Finite(uiSize) || uiSize.x <= 0 || uiSize.y <= 0) return false;
            // Do not clamp outside positions onto the tape edge, or count a click as a cut.
            if ((to - from).sqrMagnitude <= 0.00000001f) return false;
            PrepareMetrics(tapeIndex, uiSize);
            bool changed = MarkCovered(tapeIndex, Scale(from, uiSize), Scale(to, uiSize), uiSize);

            if (_coveredCounts[tapeIndex] >= RequiredCheckpointCount)
            {
                _completed[tapeIndex] = true;
                CompletedCount++;
                changed = true;
            }
            return changed;
        }

        public void Stop() => _running = false;

        private bool MarkCovered(int tapeIndex, Vector2 from, Vector2 to, Vector2 uiSize)
        {
            bool changed = false;
            for (int checkpoint = 0; checkpoint < CheckpointCount; checkpoint++)
            {
                if (_covered[tapeIndex][checkpoint]) continue;
                Vector2 sample = Scale(CheckpointPosition(tapeIndex, checkpoint), uiSize);
                if (DistanceToSegment(sample, from, to) <= _radii[tapeIndex][checkpoint])
                {
                    _covered[tapeIndex][checkpoint] = true;
                    _coveredCounts[tapeIndex]++;
                    changed = true;
                }
            }
            return changed;
        }

        private void PrepareMetrics(int tapeIndex, Vector2 size)
        {
            if (_metricSizes[tapeIndex].x == size.x && _metricSizes[tapeIndex].y == size.y) return;
            _metricSizes[tapeIndex] = size;
            var outline = _outlines[tapeIndex];
            for (int point = 0; point < CheckpointCount; point++)
            {
                float radius = Math.Min(size.x, size.y) * _tolerance;
                if (outline != null)
                {
                    var center = Scale(_checkpoints[tapeIndex][point], size);
                    float halfWidth = float.MaxValue;
                    for (int i = 0; i < outline.Length; i++)
                        halfWidth = Math.Min(halfWidth, DistanceToSegment(center,
                            Scale(outline[i], size), Scale(outline[(i + 1) % outline.Length], size)));
                    // The tolerance is a fraction of the actual tape width on each side.
                    radius = Math.Max(1f, halfWidth * 2f * _tolerance);
                }
                _radii[tapeIndex][point] = radius;
            }
        }

        private static Vector2 PointOnPath(Vector2[] path, float normalized)
        {
            if (normalized <= 0f) return path[0];
            if (normalized >= 1f) return path[path.Length - 1];
            float total = 0f;
            for (int i = 1; i < path.Length; i++) total += Vector2.Distance(path[i - 1], path[i]);
            if (total <= 0.0001f) return path[0];
            float target = total * normalized;
            float accumulated = 0f;
            for (int i = 1; i < path.Length; i++)
            {
                float segment = Vector2.Distance(path[i - 1], path[i]);
                if (accumulated + segment >= target)
                    return Vector2.Lerp(path[i - 1], path[i], (target - accumulated) / segment);
                accumulated += segment;
            }
            return path[path.Length - 1];
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float lengthSquared = delta.sqrMagnitude;
            if (lengthSquared <= 0.000001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, delta) / lengthSquared);
            return Vector2.Distance(point, start + delta * t);
        }

        private static Vector2 Scale(Vector2 point, Vector2 size) => new Vector2(point.x * size.x, point.y * size.y);
        private static bool Finite(Vector2 point) =>
            !float.IsNaN(point.x) && !float.IsNaN(point.y) &&
            !float.IsInfinity(point.x) && !float.IsInfinity(point.y);
        private static bool ValidIndex(int index) => index >= 0 && index < TapeCount;
    }
}
