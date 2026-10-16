using System;
using TapTapGameJam.UI;
internal static class Program
{
    static int passed;
    static void Check(bool value) { if (!value) throw new Exception("Printer check failed"); }
    static void Run(string name, Action body) { body(); passed++; Console.WriteLine("PASS " + name); }
    static void Main()
    {
        Run("错误时机不增加次数，正确5次才完成，错误不清空已完成次数", () => {
            var round = new PrinterRepairRound(5, 1, 0.25, () => 0);
            for (int i = 0; i < 5; i++) {
                Check(round.Hit() == (i == 4 ? PrinterHitResult.Complete : PrinterHitResult.Hit));
                Check(round.Successes == i + 1);
                if (i == 4) break;
                Check(!round.IsComplete); round.Tick(0.5);
                Check(round.Hit() == PrinterHitResult.Miss && round.Successes == i + 1);
                round.Tick(1.5);
            }
            Check(round.IsComplete && !round.IsRunning);
        });
        Run("完成后所有按键均忽略，不会重复计数", () => {
            var round = new PrinterRepairRound(1, 1, 1, () => 0);
            Check(round.Hit() == PrinterHitResult.Complete);
            for (int i = 0; i < 10; i++) { round.Tick(1); Check(round.Hit() == PrinterHitResult.Ignored); }
            Check(round.Successes == 1);
        });
        Run("取消后旧回合停止，重新打开从零开始", () => {
            var round = new PrinterRepairRound(5, 1, 1, () => 0); round.Hit(); round.Stop();
            double position = round.Position; round.Tick(1);
            Check(round.Position == position && round.Hit() == PrinterHitResult.Ignored && round.Successes == 1);
            Check(new PrinterRepairRound(5, 1, 1, () => 0).Successes == 0);
        });
        Run("成功区域两侧边界算命中，区域外算失败", () => {
            foreach (double t in new[] { 0.375, 0.625 }) {
                var round = new PrinterRepairRound(5, 1, 0.25, () => 0.5); round.Tick(t);
                Check(round.Hit() == PrinterHitResult.Hit);
            }
            var miss = new PrinterRepairRound(5, 1, 0.25, () => 0.5); miss.Tick(0.626);
            Check(miss.Hit() == PrinterHitResult.Miss);
        });
        Run("指针在轨道中往返且不同时间步保持相同位置", () => {
            var fine = new PrinterRepairRound(5, 1, 0.25, () => 0);
            var coarse = new PrinterRepairRound(5, 1, 0.25, () => 0);
            for (int i = 0; i < 200; i++) { fine.Tick(0.125); Check(fine.Position >= 0 && fine.Position <= 1); }
            coarse.Tick(25); Check(Math.Abs(fine.Position - coarse.Position) < 0.00001);
            coarse.Tick(0.25); Check(Math.Abs(coarse.Position - 0.75) < 0.00001);
        });
        Console.WriteLine($"Passed {passed} printer round checks.");
    }
}
