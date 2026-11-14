using System;
using System.Collections.Generic;
using System.Text;
using Sheepft.Core;

namespace Sheepft.Cli
{
    /// <summary>
    /// 모든 줄을 양방향으로 연 상태에서 시작해, 의도와 다른 해를 만드는 shift 포인트만 뺀다.
    /// "의도와 다른 해"는 @intent의 기믹 없이 제한 턴 안에 깨는 해, 그리고 @optimal의 기믹 없이 최적 길이로 깨는 해다.
    ///
    /// 그런 해가 하나 나오면, 그 해가 쓰는 shift 포인트 중 적어도 하나는 빼야 한다.
    /// 그래서 그 해의 포인트를 하나씩 빼 보는 식으로 가지를 치고, 가장 적게 빼는 조합을 모두 찾는다.
    /// </summary>
    internal static class Pruner
    {
        private struct Point
        {
            public bool IsRow;
            public int Index;
            public bool Forward;

            public string Text()
            {
                if (IsRow) return "R" + Index + (Forward ? "→" : "←");
                return "C" + Index + (Forward ? "↓" : "↑");
            }
        }

        private sealed class Verdict
        {
            public bool Pass;
            public string Why;
            public Move[] Witness;
            public Level Variant;
            public SolveReport Report;
        }

        public static int Run(Level lv, int number, int slack, int maxRemove, int nodes)
        {
            var full = lv.WithShift(lv.AllShiftSpec());
            var points = new List<Point>();
            foreach (var m in full.Moves)
            {
                var line = full.Lines[m.Line];
                points.Add(new Point { IsRow = line.IsRow, Index = line.Index, Forward = m.Forward });
            }
            if (points.Count > 60) { Console.WriteLine("shift 포인트가 너무 많다."); return 2; }
            if (slack < 0) slack = lv.Star3 > 0 ? lv.TurnLimit - lv.Star3 : 4;
            int cap = Math.Max(lv.TurnLimit, 12);

            Console.WriteLine("[" + number + "] " + lv.Title + "  — shift 포인트 " + points.Count + "개에서 시작, 제한 턴 = 최적 + " + slack);
            Console.WriteLine("  @intent " + EvText.Describe(lv.Intent) + (lv.OptimalIntent != Ev.None ? "   @optimal " + EvText.Describe(lv.OptimalIntent) : ""));

            var level = new List<ulong> { 0UL };
            var seen = new HashSet<ulong> { 0UL };
            for (int k = 0; k <= maxRemove; k++)
            {
                var passed = new List<Verdict>();
                var passedMasks = new List<ulong>();
                var next = new List<ulong>();
                foreach (ulong mask in level)
                {
                    var v = Evaluate(lv, full, points, mask, slack, cap, nodes);
                    if (v.Pass) { passed.Add(v); passedMasks.Add(mask); continue; }
                    if (k == 0) Console.WriteLine("  모두 열면: " + v.Why);
                    if (v.Witness == null) continue;
                    foreach (var m in v.Witness)
                    {
                        var line = v.Variant.Lines[m.Line];
                        int pi = points.FindIndex(p => p.IsRow == line.IsRow && p.Index == line.Index && p.Forward == m.Forward);
                        ulong child = mask | (1UL << pi);
                        if (seen.Add(child)) next.Add(child);
                    }
                }
                if (passed.Count > 0)
                {
                    Console.WriteLine(k == 0 ? "  뺄 포인트 없음." : "  " + k + "개를 빼면 된다. 조합 " + passed.Count + "가지:");
                    for (int i = 0; i < passed.Count; i++)
                    {
                        var v = passed[i];
                        var removed = new StringBuilder();
                        for (int p = 0; p < points.Count; p++)
                            if ((passedMasks[i] & (1UL << p)) != 0) removed.Append(removed.Length > 0 ? ", " : "").Append(points[p].Text());
                        if (k > 0) Console.WriteLine("  - 빼는 것: " + removed);
                        Console.WriteLine("    shift: " + v.Variant.ShiftSpec());
                        Console.WriteLine("    최적 " + v.Report.OptimalTurns + "턴, 최적 수순 " + v.Report.OptimalSequences + "개 (상태 경로 " + v.Report.OptimalStatePaths
                            + "개), 제한 " + v.Variant.TurnLimit + "턴  예: " + v.Variant.MovesText(v.Report.Solutions[0].Moves));
                    }
                    return 0;
                }
                level = next;
                if (level.Count == 0) break;
            }
            Console.WriteLine("  " + maxRemove + "개까지 빼 봐도 조건을 맞추지 못했다. 맵을 고쳐야 한다.");
            return 1;
        }

        private static Verdict Evaluate(Level lv, Level full, List<Point> points, ulong mask, int slack, int cap, int nodes)
        {
            var verdict = new Verdict();

            // 남은 포인트로 shift 표기를 만든다.
            var spec = new StringBuilder();
            foreach (var line in full.Lines)
            {
                bool fwd = false, back = false;
                for (int p = 0; p < points.Count; p++)
                {
                    if ((mask & (1UL << p)) != 0) continue;
                    if (points[p].IsRow != line.IsRow || points[p].Index != line.Index) continue;
                    if (points[p].Forward) fwd = true; else back = true;
                }
                if (!fwd && !back) continue;
                if (spec.Length > 0) spec.Append(", ");
                spec.Append(line.IsRow ? "R" : "C").Append(line.Index).Append(" : ");
                if (line.IsRow) spec.Append(fwd ? "R" : "").Append(back ? "L" : "");
                else spec.Append(back ? "U" : "").Append(fwd ? "D" : "");
            }
            if (spec.Length == 0) { verdict.Why = "남은 포인트가 없다"; return verdict; }

            var variant = lv.WithShift(spec.ToString());
            verdict.Variant = variant;

            // 최적 턴 수를 먼저 구하고, 제한 턴을 최적 + 여유로 잡아 다시 검사한다.
            var first = Solver.Solve(variant, new SolveOptions { MaxDepth = cap, MaxSolutions = 1, MaxNodes = nodes, Intent = Ev.None });
            if (first.OptimalTurns < 0) { verdict.Why = "해 없음"; return verdict; }
            variant.TurnLimit = first.OptimalTurns + slack;

            var r = Solver.Solve(variant, new SolveOptions { MaxSolutions = 1, MaxNodes = nodes });
            verdict.Report = r;
            if (r.Truncated) { verdict.Why = "탐색 한도에 걸림"; return verdict; }
            if (r.BypassTurns >= 0)
            {
                verdict.Why = "최적 " + r.OptimalTurns + "턴. " + EvText.Describe(r.BypassMissing) + " 없이 " + r.BypassTurns + "턴에 깨짐: " + variant.MovesText(r.Bypass.Moves);
                verdict.Witness = r.Bypass.Moves;
                return verdict;
            }
            if (r.OptimalMissing != Ev.None)
            {
                verdict.Why = "최적 " + r.OptimalTurns + "턴. " + EvText.Describe(r.OptimalMissing) + " 없이도 최적 길이로 깨짐: " + variant.MovesText(r.OptimalBypass.Moves);
                verdict.Witness = r.OptimalBypass.Moves;
                return verdict;
            }
            verdict.Pass = true;
            return verdict;
        }
    }
}
