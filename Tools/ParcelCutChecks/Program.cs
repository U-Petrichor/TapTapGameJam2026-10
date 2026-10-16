using System;
using System.Collections.Generic;
using TapTapGameJam.MiniGame;
using UnityEngine;
using Random = System.Random;

internal static class Program
{
    private static int _checks;

    private static void Main()
    {
        Run("每轮选择三个不同的胶带", () =>
        {
            var round = NewRound(new Random(17));
            Check(round.TargetIndices.Count == ParcelCutRound.RequiredTapeCount);
            Check(new HashSet<int>(round.TargetIndices).Count == ParcelCutRound.RequiredTapeCount);
        });
        Run("未选中的胶带不能增加进度", () =>
        {
            var round = NewRound(new Random(17));
            int nonTarget = -1;
            for (int i = 0; i < ParcelCutRound.TapeCount; i++)
                if (!round.IsTarget(i)) { nonTarget = i; break; }
            Check(nonTarget >= 0);
            Check(!round.TryCutSegment(nonTarget, new Vector2(0f, 0f), new Vector2(1f, 1f)));
            Check(round.CompletedCount == 0);
        });
        Run("沿目标路径拖动可以完成三条胶带", () =>
        {
            var round = NewRound(new Random(17));
            foreach (int target in round.TargetIndices)
            {
                Check(round.TryCutSegment(target, new Vector2(0f, 0f), new Vector2(1f, 1f)));
                Check(round.IsCompleted(target));
            }
            Check(round.IsComplete && round.CompletedCount == ParcelCutRound.RequiredTapeCount);
            Check(!round.TryCutSegment(round.TargetIndices[0], new Vector2(0f, 0f), new Vector2(1f, 1f)));
        });
        Run("19 个点不通过，20 个不同点达到 60% 门槛", () =>
        {
            var round = NewRound(new Random(17), 0.0001f);
            int tape = round.TargetIndices[0];
            Check(round.RequiredCheckpointCount == 20);
            round.TryCutSegment(tape, round.CheckpointPosition(tape, 0), round.CheckpointPosition(tape, 18));
            Check(round.CoveredCountFor(tape) == 19 && !round.IsCompleted(tape));
            round.TryCutSegment(tape, round.CheckpointPosition(tape, 18), round.CheckpointPosition(tape, 19));
            Check(round.CoveredCountFor(tape) == 20 && round.IsCompleted(tape) && round.CompletedCount == 1);
        });
        Run("分段补切累积，重复经过不重复计数，点击不计数", () =>
        {
            var round = NewRound(new Random(17), 0.0001f);
            int tape = round.TargetIndices[0];
            var a = round.CheckpointPosition(tape, 0); var b = round.CheckpointPosition(tape, 5);
            Check(!round.TryCutSegment(tape, a, a));
            Check(round.TryCutSegment(tape, a, b) && round.CoveredCountFor(tape) == 6);
            Check(!round.TryCutSegment(tape, b, a) && round.CoveredCountFor(tape) == 6);
            Check(round.TryCutSegment(tape, round.CheckpointPosition(tape, 15), round.CheckpointPosition(tape, 10)));
            Check(round.CoveredCountFor(tape) == 12);
            round.Stop();
            Check(!round.TryCutSegment(tape, a, round.CheckpointPosition(tape, 31)));
        });
        Run("图片自动生成水平中心线；透明区域不在轮廓内", () =>
        {
            var alpha = new byte[100 * 20];
            for (int y = 6; y < 14; y++) for (int x = 0; x < 100; x++) alpha[y * 100 + x] = 255;
            var geometry = ParcelTapeGeometry.Extract(100, 20, alpha);
            foreach (var p in geometry.Path) Check(Math.Abs(p.y - 0.5f) < 0.00001f);
            Check(ParcelTapeGeometry.Contains(geometry.Outline, new Vector2(0.5f, 0.5f)));
            Check(!ParcelTapeGeometry.Contains(geometry.Outline, new Vector2(0.5f, 0.1f)));
        });
        Run("自动中心线跟随斜胶带的真实方向", () =>
        {
            var alpha = new byte[100 * 200];
            for (int y = 0; y < 200; y++)
                for (int x = 80 - y / 4 - 4; x <= 80 - y / 4 + 4; x++) alpha[y * 100 + x] = 255;
            var geometry = ParcelTapeGeometry.Extract(100, 200, alpha);
            Check(geometry.Path[0].x > geometry.Path[geometry.Path.Length - 1].x);
            Check(ParcelTapeGeometry.Contains(geometry.Outline, geometry.Path[20]));
        });
        Run("长宽不同的图片使用实际距离，远离中心线不能误计数", () =>
        {
            var paths = new List<IReadOnlyList<Vector2>>();
            for (int i = 0; i < 6; i++) paths.Add(new[] { new Vector2(0, 0.5f), new Vector2(1, 0.5f) });
            var round = new ParcelCutRound(paths, new Random(17), 0.25f);
            int tape = round.TargetIndices[0];
            Check(!round.TryCutSegment(tape, new Vector2(0, 0.8f), new Vector2(1, 0.8f), new Vector2(800, 80)));
            Check(round.CoveredCountFor(tape) == 0);
            Check(round.TryCutSegment(tape, new Vector2(0, 0.7f), new Vector2(1, 0.7f), new Vector2(800, 80)));
        });
        Console.WriteLine($"Passed {_checks} parcel-cut scenarios.");
    }

    private static ParcelCutRound NewRound(Random random, float tolerance = 0.13f)
    {
        var paths = new List<IReadOnlyList<Vector2>>();
        for (int i = 0; i < ParcelCutRound.TapeCount; i++)
            paths.Add(new[] { new Vector2(0f, 0f), new Vector2(1f, 1f) });
        return new ParcelCutRound(paths, random, tolerance);
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new Exception("check failed");
    }

    private static void Run(string name, Action action)
    {
        action();
        _checks++;
        Console.WriteLine("PASS: " + name);
    }
}
