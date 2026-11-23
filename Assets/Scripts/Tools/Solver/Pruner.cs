using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Sheepft.Core;

namespace Sheepft.Tools
{
    /// <summary>prune 결과. Lines는 사람이 읽는 기록이고, 찾았으면 Spec에 헤더에 붙일 shift 표기가 있다.</summary>
    public sealed class PruneResult
    {
        public readonly List<string> Lines = new List<string>();
        /// <summary>조건을 맞춘 첫 조합의 shift 표기. 못 찾았으면 null.</summary>
        public string Spec;
        public bool Found => Spec != null;

        public string Text => string.Join("\n", Lines);
    }

    /// <summary>
    /// shift 포인트 정하기. 헤더의 shift는 무시하고, 실제 칸이 둘 이상인 모든 줄을 양방향으로 연 상태에서 시작해
    /// 의도와 다른 해를 만드는 포인트만 뺀다. 예전 Solver~의 `prune` 명령과 같다.
    /// "의도와 다른 해"는 @intent의 기믹 없이 최적 길이 이하로 깨는 해, 그리고 @optimal의 기믹 없이 최적 길이로 깨는 해다.
    /// 최적보다 느린 우회해는 괜찮다(StageChecker.BypassTooFast).
    ///
    /// 그런 해가 하나 나오면, 그 해가 쓰는 shift 포인트 중 적어도 하나는 빼야 한다.
    /// 그래서 그 해의 포인트를 하나씩 빼 보는 식으로 가지를 치고, 가장 적게 빼는 조합을 모두 찾는다.
    /// </summary>
    public static class Pruner
    {
        private sealed class Verdict
        {
            public bool Pass;
            public string Why;
            public ShiftPoint[] Witness;
            public Board Variant;
            public SolveReport Report;
            public string Spec;
        }

        /// <param name="slack">제한 턴을 최적 + slack으로 잡는다. 음수면 헤더의 제한 − 별3 기준, 별 기준도 없으면 4.</param>
        /// <param name="maxRemove">뺄 포인트의 최대 개수.</param>
        public static PruneResult Run(Board level, int slack = -1, int maxRemove = 4, int maxNodes = StageChecker.DefaultMaxNodes,
            CancellationToken cancel = default(CancellationToken))
        {
            var result = new PruneResult();
            var rules = level.Rules;
            var points = AllPoints(level);
            if (points.Count > 60) { result.Lines.Add("shift 포인트가 너무 많다 (" + points.Count + "개)."); return result; }
            if (slack < 0) slack = rules.Star3Turns > 0 ? rules.TurnLimit - rules.Star3Turns : 4;
            int cap = Math.Max(rules.TurnLimit, 12);

            result.Lines.Add(rules.Name + " — shift 포인트 " + points.Count + "개에서 시작, 제한 턴 = 최적 + " + slack);
            result.Lines.Add("@intent " + MechanicText.Describe(rules.Intent)
                + (rules.OptimalIntent != Mechanic.None ? "   @optimal " + MechanicText.Describe(rules.OptimalIntent) : ""));

            var level0 = new List<ulong> { 0UL };
            var seen = new HashSet<ulong> { 0UL };
            for (int k = 0; k <= maxRemove; k++)
            {
                var passed = new List<Verdict>();
                var passedMasks = new List<ulong>();
                var next = new List<ulong>();
                foreach (ulong mask in level0)
                {
                    cancel.ThrowIfCancellationRequested();
                    var v = Evaluate(level, points, mask, slack, cap, maxNodes, cancel);
                    if (v.Pass) { passed.Add(v); passedMasks.Add(mask); continue; }
                    if (k == 0) result.Lines.Add("모두 열면: " + v.Why);
                    if (v.Witness == null) continue;
                    foreach (var m in v.Witness)
                    {
                        int pi = points.IndexOf(m);
                        ulong child = mask | (1UL << pi);
                        if (seen.Add(child)) next.Add(child);
                    }
                }
                if (passed.Count > 0)
                {
                    result.Lines.Add(k == 0 ? "뺄 포인트 없음." : k + "개를 빼면 된다. 조합 " + passed.Count + "가지:");
                    for (int i = 0; i < passed.Count; i++)
                    {
                        var v = passed[i];
                        if (k > 0)
                        {
                            var removed = new List<string>();
                            for (int p = 0; p < points.Count; p++)
                                if ((passedMasks[i] & (1UL << p)) != 0) removed.Add(points[p].ToString());
                            result.Lines.Add("- 빼는 것: " + string.Join(", ", removed));
                        }
                        result.Lines.Add("  shift: " + v.Spec);
                        result.Lines.Add("  최적 " + v.Report.OptimalTurns + "턴, 최적 수순 " + v.Report.OptimalSequences + "개 (상태 경로 "
                            + v.Report.OptimalStatePaths + "개), 제한 " + v.Variant.Rules.TurnLimit + "턴  예: " + v.Report.Solutions[0].MovesText);
                    }
                    result.Spec = passed[0].Spec;
                    return result;
                }
                level0 = next;
                if (level0.Count == 0) break;
            }
            result.Lines.Add(maxRemove + "개까지 빼 봐도 조건을 맞추지 못했다. 맵을 고쳐야 한다.");
            return result;
        }

        /// <summary>실제 칸이 둘 이상인 모든 행(R, L)과 열(D, U).</summary>
        private static List<ShiftPoint> AllPoints(Board level)
        {
            var points = new List<ShiftPoint>();
            for (int r = 0; r < level.Height; r++)
            {
                if (level.Line(Axis.Row, r).Count < 2) continue;
                points.Add(new ShiftPoint(Axis.Row, r, Dir.Right));
                points.Add(new ShiftPoint(Axis.Row, r, Dir.Left));
            }
            for (int c = 0; c < level.Width; c++)
            {
                if (level.Line(Axis.Col, c).Count < 2) continue;
                points.Add(new ShiftPoint(Axis.Col, c, Dir.Down));
                points.Add(new ShiftPoint(Axis.Col, c, Dir.Up));
            }
            return points;
        }

        private static Verdict Evaluate(Board level, List<ShiftPoint> points, ulong mask, int slack, int cap, int maxNodes, CancellationToken cancel)
        {
            var verdict = new Verdict();
            var remaining = new List<ShiftPoint>();
            for (int p = 0; p < points.Count; p++)
                if ((mask & (1UL << p)) == 0) remaining.Add(points[p]);
            if (remaining.Count == 0) { verdict.Why = "남은 포인트가 없다"; return verdict; }
            verdict.Spec = LevelWriter.ShiftSpec(remaining);

            // 최적 턴 수를 먼저 구하고, 제한 턴을 최적 + 여유로 잡아 다시 검사한다.
            var variant = level.Clone();
            variant.Rules = Solver.WithRules(level.Rules, remaining, cap);
            verdict.Variant = variant;
            var first = Solver.Solve(variant, new SolveOptions { MaxDepth = cap, MaxSolutions = 1, MaxNodes = maxNodes, Intent = Mechanic.None, Cancel = cancel });
            if (first.OptimalTurns < 0) { verdict.Why = "해 없음"; return verdict; }
            variant.Rules = Solver.WithRules(level.Rules, remaining, first.OptimalTurns + slack);

            var r = Solver.Solve(variant, new SolveOptions { MaxSolutions = 1, MaxNodes = maxNodes, Cancel = cancel });
            verdict.Report = r;
            if (r.Truncated) { verdict.Why = "탐색 한도에 걸림"; return verdict; }
            if (StageChecker.BypassTooFast(r))
            {
                verdict.Why = "최적 " + r.OptimalTurns + "턴. " + MechanicText.Describe(r.BypassMissing) + " 없이 " + r.BypassTurns + "턴에 깨짐: " + r.Bypass.MovesText;
                verdict.Witness = r.Bypass.Moves;
                return verdict;
            }
            if (r.OptimalMissing != Mechanic.None)
            {
                verdict.Why = "최적 " + r.OptimalTurns + "턴. " + MechanicText.Describe(r.OptimalMissing) + " 없이도 최적 길이로 깨짐: " + r.OptimalBypass.MovesText;
                verdict.Witness = r.OptimalBypass.Moves;
                return verdict;
            }
            verdict.Pass = true;
            return verdict;
        }

    }
}
