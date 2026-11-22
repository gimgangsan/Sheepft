using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Sheepft.Core;

namespace Sheepft.Tools
{
    /// <summary>스테이지 하나의 검증 결과.</summary>
    public sealed class StageCheck
    {
        /// <summary>파일 안의 순서. 1부터 센다.</summary>
        public int Number;
        public Board Level;
        public SolveReport Report;
        /// <summary>문제 목록. 비어 있으면 통과다.</summary>
        public readonly List<string> Problems = new List<string>();
        public long Milliseconds;

        public bool Ok => Problems.Count == 0;
        public string Title => Level.Rules.Name;
    }

    /// <summary>
    /// 스테이지 검증 규칙. 예전 Solver~의 `check` 명령과 같은 기준이다.
    /// 최적해가 의도 기믹을 쓰는지, 의도 기믹을 피해 가는 해(우회해)가 없는지, 별 기준이 맞는지,
    /// 풀이 줄이 게임 규칙으로 실제로 승리하는지 본다.
    /// </summary>
    public static class StageChecker
    {
        public const int DefaultMaxNodes = 4000000;

        public static StageCheck Check(Board level, int number, int maxNodes = DefaultMaxNodes, int maxSolutions = 5,
            CancellationToken cancel = default(CancellationToken))
        {
            var watch = Stopwatch.StartNew();
            var check = new StageCheck { Number = number, Level = level };
            var rules = level.Rules;
            var r = Solver.Solve(level, new SolveOptions { MaxSolutions = maxSolutions, MaxNodes = maxNodes, Cancel = cancel });
            check.Report = r;
            var problems = check.Problems;

            if (r.OptimalTurns < 0) problems.Add("해 없음");
            if (r.Truncated) problems.Add("탐색 한도에 걸림 (결과를 믿을 수 없음)");
            if (rules.Intent == Mechanic.None) problems.Add("@intent 없음");
            else if (r.OptimalTurns > 0)
            {
                if (r.IntendedTurns < 0) problems.Add("의도해 없음");
                else if (r.IntendedTurns != r.OptimalTurns) problems.Add("최적해가 의도 기믹을 안 씀");
                if (r.BypassTurns >= 0) problems.Add("우회해 " + r.BypassTurns + "턴 (" + MechanicText.Describe(r.BypassMissing) + " 없이)");
            }
            if (r.OptimalMissing != Mechanic.None) problems.Add("최적해가 " + MechanicText.Describe(r.OptimalMissing) + "을 안 써도 됨");
            if (rules.Star3Turns <= 0) problems.Add("@stars 없음");
            else
            {
                if (rules.Star3Turns != r.OptimalTurns) problems.Add("별3 기준(" + rules.Star3Turns + ")이 최적과 다름");
                if (!(rules.Star3Turns < rules.Star2Turns && rules.Star2Turns <= rules.TurnLimit)) problems.Add("별 기준 순서 (별3 < 별2 ≤ 제한)");
            }
            if (Math.Max(0, level.Height - 7) + Math.Max(0, level.Width - 7) > 4) problems.Add("맵이 7×7보다 행·열 합쳐 4줄 넘게 큼");
            if (r.OptimalTurns > 20) problems.Add("최적 20턴 초과");
            if (rules.Solution.Count > 0)
            {
                string why = SolutionProblem(level);
                if (why != null) problems.Add("풀이 줄: " + why);
            }

            check.Milliseconds = watch.ElapsedMilliseconds;
            return check;
        }

        /// <summary>`# 풀이:` 줄의 수순을 게임 규칙(제한 턴 포함) 그대로 두어 승리하는지 본다. 문제가 없으면 null.</summary>
        public static string SolutionProblem(Board level)
        {
            var board = level.Clone();
            foreach (var move in level.Rules.Solution)
            {
                if (board.Result != GameResult.InProgress) return "수순이 끝나기 전에 판이 끝남";
                if (!TurnRunner.PlayTurn(board, move)) return "'" + move + "'는 둘 수 없는 수";
            }
            return board.Result == GameResult.Won ? null : "승리하지 못함";
        }

        /// <summary>표의 머리줄. Row와 열을 맞춘다.</summary>
        public const string TableHeader = "#   최적  수순  경로  의도해  우회해  제한  별3/별2  판정  제목";

        /// <summary>예전 `check` 명령과 같은 모양의 한 줄.</summary>
        public static string Row(StageCheck c)
        {
            var r = c.Report;
            var rules = c.Level.Rules;
            return c.Number.ToString().PadRight(4) +
                   Num(r.OptimalTurns).PadRight(6) +
                   r.OptimalSequences.ToString().PadRight(6) +
                   r.OptimalStatePaths.ToString().PadRight(6) +
                   Num(r.IntendedTurns).PadRight(8) +
                   Num(r.BypassTurns).PadRight(8) +
                   rules.TurnLimit.ToString().PadRight(6) +
                   (Num(rules.Star3Turns) + "/" + Num(rules.Star2Turns)).PadRight(9) +
                   (c.Ok ? "OK    " : "확인  ") +
                   c.Title +
                   (c.Ok ? "" : "  ← " + string.Join("; ", c.Problems));
        }

        /// <summary>예전 `solve` 명령처럼 자세한 내용. 검증 창의 상세 칸에 쓴다.</summary>
        public static string Details(StageCheck c)
        {
            var r = c.Report;
            var rules = c.Level.Rules;
            var sb = new StringBuilder();
            sb.Append('[').Append(c.Number).Append("] ").Append(rules.Name)
              .Append("  (제한 ").Append(rules.TurnLimit).Append("턴, 목표 양 ").Append(rules.TargetSheep).AppendLine(")");
            sb.AppendLine("shift: " + string.Join(", ", rules.ShiftPoints) + "  (포인트 " + rules.ShiftPoints.Count + "개)");
            sb.AppendLine(LevelWriter.Write(c.Level));
            sb.AppendLine();

            if (r.OptimalTurns < 0)
            {
                sb.AppendLine("해 없음 (" + r.Depth + "턴까지 탐색)");
            }
            else
            {
                sb.AppendLine("최적 " + r.OptimalTurns + "턴. 최적 수순 " + r.OptimalSequences + "개, 상태 경로 " + r.OptimalStatePaths + "개");
                for (int i = 0; i < r.Solutions.Count; i++)
                {
                    var s = r.Solutions[i];
                    sb.AppendLine("  " + (i + 1) + ". " + s.MovesText);
                    sb.AppendLine("     기믹: " + MechanicText.Describe(s.Mechanics));
                }
                if (r.OptimalSequences > r.Solutions.Count) sb.AppendLine("  ... (" + r.Solutions.Count + "개만 보임)");
            }

            if (rules.Intent == Mechanic.None)
            {
                sb.AppendLine("의도 기믹: 지정 안 됨 (@intent)");
            }
            else if (r.OptimalTurns > 0)
            {
                sb.AppendLine("의도 기믹: " + MechanicText.Describe(rules.Intent));
                if (r.IntendedTurns < 0) sb.AppendLine("  의도 기믹을 모두 쓰는 해: 없음");
                else sb.AppendLine("  의도 기믹을 모두 쓰는 최단 해: " + r.IntendedTurns + "턴  " + r.Intended.MovesText);
                if (r.BypassTurns < 0) sb.AppendLine("  의도 기믹 없이 깨는 해: 없음 (" + r.Depth + "턴까지 탐색)");
                else
                {
                    sb.AppendLine("  의도 기믹 없이 깨는 최단 해: " + r.BypassTurns + "턴  " + r.Bypass.MovesText);
                    sb.AppendLine("     쓰지 않은 기믹: " + MechanicText.Describe(r.BypassMissing));
                }
            }
            if (rules.OptimalIntent != Mechanic.None && r.OptimalTurns > 0)
            {
                sb.AppendLine("최적해에 나와야 하는 기믹: " + MechanicText.Describe(rules.OptimalIntent));
                if (r.OptimalMissing == Mechanic.None) sb.AppendLine("  최적 길이의 모든 해가 쓴다");
                else sb.AppendLine("  " + MechanicText.Describe(r.OptimalMissing) + " 없이도 최적 길이로 깨짐: " + r.OptimalBypass.MovesText);
            }
            if (rules.Solution.Count > 0) sb.AppendLine("풀이 줄: " + Solver.MovesText(rules.Solution));
            if (rules.Star3Turns > 0) sb.AppendLine("별 기준: 3개 " + rules.Star3Turns + "턴, 2개 " + rules.Star2Turns + "턴, 1개 " + rules.TurnLimit + "턴");
            sb.Append("탐색 상태 " + r.Nodes + "개, " + c.Milliseconds + "ms");
            if (r.Truncated) sb.Append("  ※ 상태 수 한도에 걸려 끝까지 탐색하지 못함");
            sb.AppendLine();

            if (!c.Ok)
            {
                sb.AppendLine();
                sb.AppendLine("문제:");
                foreach (var p in c.Problems) sb.AppendLine("  - " + p);
            }
            return sb.ToString();
        }

        private static string Num(int v)
        {
            return v < 0 ? "-" : v.ToString();
        }
    }
}
