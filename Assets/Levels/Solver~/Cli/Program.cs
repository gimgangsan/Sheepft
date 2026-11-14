using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Sheepft.Core;

namespace Sheepft.Cli
{
    internal static class Program
    {
        private const string Usage = @"사용법:
  sheepft solve <레벨파일> [--stage N] [--solutions K] [--depth D] [--nodes N]
      최적 턴 수, 최적해의 개수와 수순, 이벤트, 의도 기믹을 쓰지 않는 해를 구한다.
  sheepft check <레벨파일> [--nodes N]
      모든 스테이지를 한 줄씩 검증한다. 문제가 있으면 종료 코드 1.
  sheepft play <레벨파일> [--stage N] --moves ""R1> C2^""
      수순을 따라 두며 턴마다 맵과 일어난 일을 보여 준다.
  sheepft prune <레벨파일> [--stage N] [--slack S] [--remove K]
      헤더의 shift는 무시하고 모든 줄을 양방향으로 연 뒤, 의도와 다른 해를 만드는 포인트만 뺀 조합을 찾는다.
      제한 턴은 최적 + S로 잡는다 (생략하면 헤더의 제한 - 별3 기준, 그것도 없으면 4). K는 뺄 포인트의 최대 개수.

  스테이지 번호는 파일 안의 순서이고 1부터 센다. 생략하면 solve는 전부, play는 1번.
  수 표기: R1→ R1← C2↑ C2↓ 또는 R1> R1< C2^ C2v";

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length < 2) { Console.WriteLine(Usage); return 2; }

            string command = args[0], file = args[1];
            int stage = 0, solutions = 10, depth = 0, nodes = 4000000, slack = -1, remove = 4;
            string moves = null;
            for (int i = 2; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--stage" && i + 1 < args.Length) stage = int.Parse(args[++i]);
                else if (a == "--solutions" && i + 1 < args.Length) solutions = int.Parse(args[++i]);
                else if (a == "--depth" && i + 1 < args.Length) depth = int.Parse(args[++i]);
                else if (a == "--nodes" && i + 1 < args.Length) nodes = int.Parse(args[++i]);
                else if (a == "--moves" && i + 1 < args.Length) moves = args[++i];
                else if (a == "--slack" && i + 1 < args.Length) slack = int.Parse(args[++i]);
                else if (a == "--remove" && i + 1 < args.Length) remove = int.Parse(args[++i]);
                else { Console.WriteLine("모르는 옵션: " + a); Console.WriteLine(Usage); return 2; }
            }

            List<Level> levels;
            try
            {
                levels = LevelParser.ParseAll(File.ReadAllText(file, Encoding.UTF8));
            }
            catch (Exception ex) when (ex is LevelFormatException || ex is IOException)
            {
                Console.WriteLine("레벨을 읽지 못했다: " + ex.Message);
                return 2;
            }
            if (levels.Count == 0) { Console.WriteLine("레벨이 없다."); return 2; }
            if (stage < 0 || stage > levels.Count) { Console.WriteLine("스테이지 번호는 1부터 " + levels.Count + "까지다."); return 2; }

            switch (command)
            {
                case "solve": return Solve(levels, stage, solutions, depth, nodes);
                case "check": return Check(levels, nodes);
                case "play": return Play(levels[stage == 0 ? 0 : stage - 1], moves);
                case "prune":
                    int worst = 0;
                    for (int n = 1; n <= levels.Count; n++)
                    {
                        if (stage != 0 && stage != n) continue;
                        worst = Math.Max(worst, Pruner.Run(levels[n - 1], n, slack, remove, nodes));
                        Console.WriteLine();
                    }
                    return worst;
                default: Console.WriteLine(Usage); return 2;
            }
        }

        private static string Header(Level lv, int number)
        {
            return "[" + number + "] " + lv.Title + "  (제한 " + lv.TurnLimit + "턴, 목표 양 " + lv.TargetSheep + "/" + lv.SheepCount + ")";
        }

        private static int Solve(List<Level> levels, int stage, int solutions, int depth, int nodes)
        {
            for (int n = 1; n <= levels.Count; n++)
            {
                if (stage != 0 && stage != n) continue;
                var lv = levels[n - 1];
                var opt = new SolveOptions { MaxSolutions = solutions, MaxDepth = depth, MaxNodes = nodes };
                var watch = Stopwatch.StartNew();
                var r = Solver.Solve(lv, opt);
                watch.Stop();

                Console.WriteLine(Header(lv, n));
                Console.WriteLine(Board.Render(lv, lv.NewState()));
                if (r.OptimalTurns < 0)
                {
                    Console.WriteLine("  해 없음 (" + r.Depth + "턴까지 탐색)");
                }
                else
                {
                    Console.WriteLine("  최적 " + r.OptimalTurns + "턴. 최적 수순 " + r.OptimalSequences + "개, 상태 경로 " + r.OptimalStatePaths + "개");
                    for (int i = 0; i < r.Solutions.Count; i++)
                    {
                        var s = r.Solutions[i];
                        Console.WriteLine("    " + (i + 1) + ". " + lv.MovesText(s.Moves));
                        Console.WriteLine("       이벤트: " + EvText.Describe(s.Events));
                    }
                    if (r.OptimalSequences > r.Solutions.Count) Console.WriteLine("    ... (--solutions 로 더 볼 수 있다)");
                }

                if (r.Intent != Ev.None && r.OptimalTurns > 0)
                {
                    Console.WriteLine("  의도 기믹: " + EvText.Describe(r.Intent));
                    if (r.IntendedTurns < 0) Console.WriteLine("    의도 기믹을 모두 쓰는 해: 없음");
                    else Console.WriteLine("    의도 기믹을 모두 쓰는 최단 해: " + r.IntendedTurns + "턴  " + lv.MovesText(r.Intended.Moves));
                    if (r.BypassTurns < 0) Console.WriteLine("    의도 기믹 없이 깨는 해: 없음 (" + r.Depth + "턴까지 탐색)");
                    else
                    {
                        Console.WriteLine("    의도 기믹 없이 깨는 최단 해: " + r.BypassTurns + "턴  " + lv.MovesText(r.Bypass.Moves));
                        Console.WriteLine("       쓰지 않은 기믹: " + EvText.Describe(r.BypassMissing));
                    }
                }
                else if (r.Intent == Ev.None)
                {
                    Console.WriteLine("  의도 기믹: 지정 안 됨 (@intent)");
                }
                if (lv.OptimalIntent != Ev.None && r.OptimalTurns > 0)
                {
                    Console.WriteLine("  최적해에 나와야 하는 기믹: " + EvText.Describe(lv.OptimalIntent));
                    if (r.OptimalMissing == Ev.None) Console.WriteLine("    최적 길이의 모든 해가 쓴다");
                    else Console.WriteLine("    " + EvText.Describe(r.OptimalMissing) + " 없이도 최적 길이로 깨짐: " + lv.MovesText(r.OptimalBypass.Moves));
                }
                Console.WriteLine("  shift 포인트 " + lv.Moves.Length + "개");
                if (lv.Star3 > 0) Console.WriteLine("  별 기준: 3개 " + lv.Star3 + "턴, 2개 " + lv.Star2 + "턴, 1개 " + lv.TurnLimit + "턴");
                Console.WriteLine("  탐색 상태 " + r.Nodes + "개, " + watch.ElapsedMilliseconds + "ms" + (r.Truncated ? "  ※ 상태 수 한도에 걸려 끝까지 탐색하지 못함" : ""));
                Console.WriteLine();
            }
            return 0;
        }

        /// <summary>스테이지마다 한 줄. 최적 턴이 별 3개 기준과 같은지, 의도 기믹을 피해 가는 해가 없는지 본다.</summary>
        private static int Check(List<Level> levels, int nodes)
        {
            int bad = 0;
            Console.WriteLine("#   최적  수순  경로  의도해  우회해  제한  별3/별2  판정  제목");
            for (int n = 1; n <= levels.Count; n++)
            {
                var lv = levels[n - 1];
                var r = Solver.Solve(lv, new SolveOptions { MaxSolutions = 1, MaxNodes = nodes });
                var problems = new List<string>();
                if (r.OptimalTurns < 0) problems.Add("해 없음");
                if (r.Truncated) problems.Add("탐색 한도");
                if (lv.Intent == Ev.None) problems.Add("@intent 없음");
                else if (r.OptimalTurns > 0)
                {
                    if (r.IntendedTurns < 0) problems.Add("의도해 없음");
                    else if (r.IntendedTurns != r.OptimalTurns) problems.Add("최적해가 의도 기믹을 안 씀");
                    if (r.BypassTurns >= 0) problems.Add("우회해 " + r.BypassTurns + "턴 (" + EvText.Describe(r.BypassMissing) + " 없이)");
                }
                if (r.OptimalMissing != Ev.None) problems.Add("최적해가 " + EvText.Describe(r.OptimalMissing) + "을 안 써도 됨");
                if (lv.Star3 <= 0) problems.Add("@stars 없음");
                else
                {
                    if (lv.Star3 != r.OptimalTurns) problems.Add("별3 기준이 최적과 다름");
                    if (!(lv.Star3 < lv.Star2 && lv.Star2 <= lv.TurnLimit)) problems.Add("별 기준 순서");
                }
                if (Math.Max(0, lv.Rows - 7) + Math.Max(0, lv.Cols - 7) > 4) problems.Add("맵이 7×7보다 행·열 합쳐 4줄 넘게 큼");
                if (r.OptimalTurns > 20) problems.Add("최적 20턴 초과");
                if (problems.Count > 0) bad++;

                Console.WriteLine(
                    n.ToString().PadRight(4) +
                    Num(r.OptimalTurns).PadRight(6) +
                    r.OptimalSequences.ToString().PadRight(6) +
                    r.OptimalStatePaths.ToString().PadRight(6) +
                    Num(r.IntendedTurns).PadRight(8) +
                    Num(r.BypassTurns).PadRight(8) +
                    lv.TurnLimit.ToString().PadRight(6) +
                    (Num(lv.Star3) + "/" + Num(lv.Star2)).PadRight(9) +
                    (problems.Count == 0 ? "OK    " : "확인  ") +
                    lv.Title +
                    (problems.Count == 0 ? "" : "  ← " + string.Join("; ", problems)));
            }
            Console.WriteLine();
            Console.WriteLine(bad == 0 ? "모든 스테이지 통과 (" + levels.Count + "개)" : "확인이 필요한 스테이지 " + bad + "개");
            return bad == 0 ? 0 : 1;
        }

        private static string Num(int v)
        {
            return v < 0 ? "-" : v.ToString();
        }

        private static int Play(Level lv, string moves)
        {
            var g = new Game(lv, true);
            Console.WriteLine(lv.Title + "  (제한 " + lv.TurnLimit + "턴, 목표 양 " + lv.TargetSheep + ")");
            Console.WriteLine(g.Board());
            if (moves == null) return 0;

            foreach (var token in moves.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Move m;
                if (!lv.TryParseMove(token, out m)) { Console.WriteLine("'" + token + "'는 이 레벨에서 둘 수 없는 수다."); return 2; }
                if (g.Outcome != Outcome.Ongoing) { Console.WriteLine("이미 끝난 판이라 '" + token + "'부터는 두지 않았다."); break; }
                g.Do(m);
                Console.WriteLine();
                Console.WriteLine(g.Turn + "턴 " + lv.MoveText(m));
                Console.WriteLine(g.Board());
                foreach (var line in g.Log) Console.WriteLine("  - " + line);
                Console.WriteLine("  이벤트: " + EvText.Describe(g.LastEvents));
            }
            Console.WriteLine();
            Console.WriteLine(g.Outcome == Outcome.Win ? "승리 (" + g.Turn + "턴)" : g.Outcome == Outcome.Lose ? "패배 (" + g.Turn + "턴)" : "진행 중 (" + g.Turn + "턴)");
            Console.WriteLine("전체 이벤트: " + EvText.Describe(g.Events));
            return 0;
        }
    }
}
