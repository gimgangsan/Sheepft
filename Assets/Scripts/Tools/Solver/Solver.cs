using System;
using System.Collections.Generic;
using System.Threading;
using Sheepft.Core;

namespace Sheepft.Tools
{
    public sealed class Solution
    {
        public ShiftPoint[] Moves;
        /// <summary>이 수순에서 쓰인 모든 기믹.</summary>
        public Mechanic Mechanics;

        public string MovesText => Solver.MovesText(Moves);
    }

    public sealed class SolveOptions
    {
        /// <summary>탐색할 최대 턴 수. 0 이하면 레벨의 제한 턴.</summary>
        public int MaxDepth = 0;
        /// <summary>수순을 적어 줄 최적해의 최대 개수.</summary>
        public int MaxSolutions = 20;
        /// <summary>탐색 한 번에 보관할 상태 수의 한도. 넘으면 Truncated가 켜진다.</summary>
        public int MaxNodes = 4000000;
        /// <summary>의도 기믹. null이면 레벨의 @intent를 쓴다.</summary>
        public Mechanic? Intent = null;
        /// <summary>취소되면 OperationCanceledException을 던진다.</summary>
        public CancellationToken Cancel;
    }

    public sealed class SolveReport
    {
        /// <summary>최적 턴 수. 제한 턴 안에 해가 없으면 -1.</summary>
        public int OptimalTurns = -1;
        /// <summary>최적 길이 해의 개수 (수순 기준).</summary>
        public long OptimalSequences;
        /// <summary>최적 길이 해의 개수 (거쳐 가는 상태 기준). 결과가 같은 수는 하나로 센다.</summary>
        public long OptimalStatePaths;
        public List<Solution> Solutions = new List<Solution>();

        public Mechanic Intent;
        /// <summary>의도 기믹을 모두 쓰는 해의 최소 턴 수. 없으면 -1.</summary>
        public int IntendedTurns = -1;
        public Solution Intended;
        /// <summary>의도 기믹 중 하나라도 쓰지 않고 제한 턴 안에 깨는 해의 최소 턴 수. 없으면 -1.</summary>
        public int BypassTurns = -1;
        public Solution Bypass;
        /// <summary>그 해가 쓰지 않은 의도 기믹.</summary>
        public Mechanic BypassMissing;

        /// <summary>@optimal 기믹 중, 그것 없이도 최적 길이로 깨지는 것. 없으면 None.</summary>
        public Mechanic OptimalMissing;
        /// <summary>그런 최적 길이 해의 예.</summary>
        public Solution OptimalBypass;

        /// <summary>탐색한 최대 턴 수.</summary>
        public int Depth;
        /// <summary>가장 큰 탐색에서 보관한 상태 수.</summary>
        public long Nodes;
        /// <summary>상태 수 한도에 걸려 탐색을 끝까지 하지 못했다. 이때 "없음"은 믿을 수 없다.</summary>
        public bool Truncated;
    }

    /// <summary>
    /// 너비 우선 탐색. 한 턴 진행은 게임이 쓰는 TurnRunner.PlayTurn 그대로다.
    /// 수는 레벨의 shift 포인트이고, 아무것도 움직이지 않는 shift(대기)도 수로 센다.
    ///
    /// 1. 최적 턴 수, 최적 길이 해의 개수와 수순.
    /// 2. 의도 기믹 하나하나에 대해, 그 기믹이 쓰이는 수를 금지하고 다시 탐색한다.
    ///    그래도 제한 턴 안에 깨지면 그 기믹 없이 깨는 해(우회해)가 있는 것이다.
    ///
    /// Core에는 바뀌는 static 상태가 없으므로 백그라운드 스레드에서 돌려도 된다.
    /// </summary>
    public static class Solver
    {
        public static SolveReport Solve(Board level, SolveOptions opt = null)
        {
            if (opt == null) opt = new SolveOptions();
            var report = new SolveReport();
            report.Depth = opt.MaxDepth > 0 ? opt.MaxDepth : level.Rules.TurnLimit;
            report.Intent = opt.Intent ?? level.Rules.Intent;
            if (level.Rules.TargetSheep <= 0 || level.CannotWin) return report;

            var moves = new ShiftPoint[level.Rules.ShiftPoints.Count];
            for (int i = 0; i < moves.Length; i++) moves[i] = level.Rules.ShiftPoints[i];
            var start = SearchBoard(level);

            // 1. 최적해
            var best = Search(start, moves, report.Depth, Mechanic.None, true, opt.MaxSolutions, opt);
            report.Nodes = best.Nodes;
            report.Truncated = best.Truncated;
            report.OptimalTurns = best.Turns;
            report.OptimalSequences = best.Sequences;
            report.OptimalStatePaths = best.StatePaths;
            report.Solutions = best.Solutions;
            if (best.Turns < 0) return report;

            // @optimal: 그 기믹을 금지하고도 최적 길이로 깨지는지 본다.
            foreach (var one in MechanicText.Split(level.Rules.OptimalIntent))
            {
                var r = Search(start, moves, best.Turns, one, false, 1, opt);
                if (r.Truncated) report.Truncated = true;
                if (r.Turns < 0) continue;
                report.OptimalMissing |= one;
                if (report.OptimalBypass == null) report.OptimalBypass = r.Solutions[0];
            }
            if (report.Intent == Mechanic.None) return report;

            // 2. 의도 기믹을 하나씩 금지하고 탐색
            foreach (var one in MechanicText.Split(report.Intent))
            {
                var r = Search(start, moves, report.Depth, one, false, 1, opt);
                if (r.Nodes > report.Nodes) report.Nodes = r.Nodes;
                if (r.Truncated) report.Truncated = true;
                if (r.Turns < 0) continue;
                if (report.BypassTurns < 0 || r.Turns < report.BypassTurns)
                {
                    report.BypassTurns = r.Turns;
                    report.Bypass = r.Solutions[0];
                    report.BypassMissing = report.Intent & ~r.Solutions[0].Mechanics;
                }
            }

            // 우회해가 없거나 최적보다 길면, 최적 길이의 해는 모두 의도 기믹을 전부 쓴다.
            if (report.BypassTurns < 0 || report.BypassTurns > report.OptimalTurns)
            {
                report.IntendedTurns = report.OptimalTurns;
                report.Intended = report.Solutions[0];
            }
            else
            {
                FindIntended(start, moves, opt, report);
            }
            return report;
        }

        /// <summary>수순을 `R1:R, C2:U`처럼 적는다.</summary>
        public static string MovesText(IEnumerable<ShiftPoint> moves)
        {
            // 풀이 줄(`# 풀이: R1:R, C2:U`)과 같은 모양이라 그대로 복사해 붙일 수 있다.
            return moves == null ? "" : string.Join(", ", moves);
        }

        /// <summary>
        /// 탐색용 맵. 레벨을 복사하고 제한 턴을 없앤다(턴 수는 탐색 깊이로 센다).
        /// 제한 턴보다 깊이 찾아보는 경우(prune)에도 TurnRunner가 패배로 끝내지 않게 하기 위해서다.
        /// </summary>
        internal static Board SearchBoard(Board level)
        {
            var board = level.Clone();
            board.Rules = WithRules(level.Rules, level.Rules.ShiftPoints, int.MaxValue);
            board.TurnsUsed = 0;
            board.Result = GameResult.InProgress;
            return board;
        }

        /// <summary>규칙을 복사하면서 shift 포인트와 제한 턴만 바꾼다.</summary>
        internal static LevelRules WithRules(LevelRules src, IReadOnlyList<ShiftPoint> points, int turnLimit)
        {
            return new LevelRules
            {
                Name = src.Name,
                ShiftPoints = points,
                TurnLimit = turnLimit,
                TargetSheep = src.TargetSheep,
                Solution = src.Solution,
                Description = src.Description,
                Star3Turns = src.Star3Turns,
                Star2Turns = src.Star2Turns,
                Intent = src.Intent,
                OptimalIntent = src.OptimalIntent,
                Comments = src.Comments,
            };
        }

        // ------------------------------------------------------------------
        // 너비 우선 탐색
        // ------------------------------------------------------------------

        private sealed class SearchResult
        {
            public int Turns = -1;
            public long Sequences;
            public long StatePaths;
            public List<Solution> Solutions = new List<Solution>();
            public long Nodes;
            public bool Truncated;
        }

        private static long Add(long a, long b)
        {
            long s = a + b;
            return s < 0 ? long.MaxValue : s; // 넘치면 최댓값에 묶어 둔다.
        }

        /// <param name="start">SearchBoard로 만든 처음 상태. 고치지 않는다.</param>
        /// <param name="forbid">이 기믹이 하나라도 쓰이는 수는 두지 않는다.</param>
        /// <param name="count">true면 승리가 처음 나온 턴을 끝까지 훑어 해의 개수를 세고 수순을 모은다. false면 첫 해에서 멈춘다.</param>
        private static SearchResult Search(Board start, ShiftPoint[] moves, int maxDepth, Mechanic forbid, bool count, int maxSolutions, SolveOptions opt)
        {
            var result = new SearchResult();
            var index = new Dictionary<byte[], int>(ByteArrayComparer.Instance);
            var depthOf = new List<int>();
            var parent = new List<int>();      // 처음 닿은 경로
            var parentMove = new List<int>();
            // 개수 세기용 (count일 때만)
            var sequences = new List<long>();
            var statePaths = new List<long>();
            var head = new List<int>();        // 노드로 들어오는 간선의 연결 목록
            var edgeParent = new List<int>();
            var edgeMove = new List<int>();
            var edgeNext = new List<int>();

            var expander = new Expander(start, moves);
            var root = BoardCodec.Encode(start);
            index[root] = 0;
            depthOf.Add(0);
            parent.Add(-1);
            parentMove.Add(-1);
            if (count) { sequences.Add(1); statePaths.Add(1); head.Add(-1); }

            var frontier = new List<KeyValuePair<int, byte[]>> { new KeyValuePair<int, byte[]>(0, root) };
            var winParents = new List<int>();
            var winMoves = new List<int>();
            var seenChildren = new List<int>();
            var seenWins = new HashSet<byte[]>(ByteArrayComparer.Instance);

            for (int depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
            {
                var next = new List<KeyValuePair<int, byte[]>>();
                long winStatePaths = 0;

                for (int f = 0; f < frontier.Count; f++)
                {
                    opt.Cancel.ThrowIfCancellationRequested();
                    int node = frontier[f].Key;
                    seenChildren.Clear();
                    seenWins.Clear();
                    expander.SetParent(frontier[f].Value);
                    for (int mi = 0; mi < moves.Length; mi++)
                    {
                        var step = expander.Play(mi);
                        if (step.Result == GameResult.Lost) continue;
                        if ((step.Mechanics & forbid) != 0) continue;

                        if (step.Result == GameResult.Won)
                        {
                            if (!count)
                            {
                                result.Turns = depth + 1;
                                result.Solutions.Add(Replay(start, PathTo(moves, parent, parentMove, node, mi)));
                                result.Nodes = depthOf.Count;
                                return result;
                            }
                            winParents.Add(node);
                            winMoves.Add(mi);
                            if (seenWins.Add(step.Key)) winStatePaths = Add(winStatePaths, statePaths[node]);
                            continue;
                        }
                        if (step.CannotWin) continue; // 풀이 모두 탔다. 승리 불가 상태.

                        var key = step.Key;
                        int id;
                        if (index.TryGetValue(key, out id))
                        {
                            if (!count || depthOf[id] != depth + 1) continue; // 더 일찍 닿은 상태
                        }
                        else
                        {
                            if (depthOf.Count >= opt.MaxNodes) { result.Truncated = true; continue; }
                            id = depthOf.Count;
                            index[key] = id;
                            depthOf.Add(depth + 1);
                            parent.Add(node);
                            parentMove.Add(mi);
                            if (count) { sequences.Add(0); statePaths.Add(0); head.Add(-1); }
                            next.Add(new KeyValuePair<int, byte[]>(id, key));
                        }
                        if (!count) continue;

                        edgeParent.Add(node);
                        edgeMove.Add(mi);
                        edgeNext.Add(head[id]);
                        head[id] = edgeParent.Count - 1;
                        sequences[id] = Add(sequences[id], sequences[node]);
                        if (!seenChildren.Contains(id))
                        {
                            seenChildren.Add(id);
                            statePaths[id] = Add(statePaths[id], statePaths[node]);
                        }
                    }
                    frontier[f] = default(KeyValuePair<int, byte[]>); // 다 쓴 상태는 놓아 준다.
                }

                if (winParents.Count > 0)
                {
                    result.Turns = depth + 1;
                    result.StatePaths = winStatePaths;
                    for (int i = 0; i < winParents.Count; i++) result.Sequences = Add(result.Sequences, sequences[winParents[i]]);

                    // 수순 모으기: 승리한 수에서 거꾸로 올라가며 모든 경로를 적는다.
                    var suffix = new List<int>();
                    for (int i = 0; i < winParents.Count && result.Solutions.Count < maxSolutions; i++)
                    {
                        suffix.Clear();
                        suffix.Add(winMoves[i]);
                        Walk(start, moves, depthOf, head, edgeParent, edgeMove, edgeNext, winParents[i], suffix, maxSolutions, result.Solutions);
                    }
                    break;
                }
                frontier = next;
            }
            result.Nodes = depthOf.Count;
            return result;
        }

        /// <summary>한 수를 둔 결과. 패배가 아니면 Key에 결과 상태가 있다.</summary>
        private struct Step
        {
            public GameResult Result;
            public Mechanic Mechanics;
            public bool CannotWin;
            public byte[] Key;
        }

        /// <summary>
        /// 부모 상태 하나에서 모든 수를 두어 본다.
        /// 아무것도 움직이지 않는 수(빈 줄, 모든 오브젝트가 장애물에 막힌 줄)는 shift가 하는 일이 없으므로
        /// 그 뒤의 턴 진행도 모두 같다. 그래서 그런 수는 부모마다 한 번만 실제로 두고 결과를 나눠 쓴다.
        /// 다른 점은 막힌 줄이 켜는 Blocked 기믹뿐이고, Blocked는 shift에서만 켜지므로 그것만 붙였다 뗀다.
        /// </summary>
        private sealed class Expander
        {
            private readonly Board work;
            private readonly ShiftPoint[] moves;
            private byte[] parent;
            private bool fresh;           // work가 지금 부모 상태 그대로인가
            private bool haveWait;
            private Step wait;            // Blocked를 뺀 대기 결과

            public Expander(Board start, ShiftPoint[] moves)
            {
                work = start.Clone();
                work.Rules = start.Rules;
                this.moves = moves;
            }

            public void SetParent(byte[] state)
            {
                parent = state;
                BoardCodec.Load(work, state);
                fresh = true;
                haveWait = false;
            }

            public Step Play(int mi)
            {
                if (!fresh) BoardCodec.Load(work, parent);
                bool blocked;
                bool isWait = IsWait(moves[mi], out blocked);
                if (isWait && haveWait)
                {
                    var reused = wait;
                    if (blocked) reused.Mechanics |= Mechanic.Blocked;
                    fresh = true; // 다시 두지 않았으므로 work는 부모 상태 그대로다.
                    return reused;
                }

                TurnRunner.PlayTurn(work, moves[mi]);
                fresh = false;
                var step = new Step { Result = work.Result, Mechanics = work.Mechanics };
                if (step.Result != GameResult.Lost)
                {
                    step.Key = BoardCodec.Encode(work);
                    step.CannotWin = step.Result == GameResult.InProgress && work.CannotWin;
                }
                if (isWait)
                {
                    wait = step;
                    wait.Mechanics &= ~Mechanic.Blocked;
                    haveWait = true;
                }
                return step;
            }

            /// <summary>지금 work(부모 상태)에서 그 shift가 아무것도 움직이지 않는가. LineShifter와 같은 판정이다.</summary>
            private bool IsWait(ShiftPoint point, out bool blocked)
            {
                blocked = false;
                var line = work.Line(point.Axis, point.Index);
                int n = line.Count;
                if (n <= 1) return true;
                int step = point.Dir == Dir.Right || point.Dir == Dir.Down ? 1 : -1;
                for (int i = 0; i < n; i++)
                {
                    if (line[i].Objects.Count == 0) continue;
                    if (!work.BlocksShift(line[(i + step + n) % n])) return false;
                    blocked = true;
                }
                return true;
            }
        }

        private static void Walk(Board start, ShiftPoint[] moves, List<int> depthOf, List<int> head, List<int> edgeParent, List<int> edgeMove, List<int> edgeNext,
            int node, List<int> suffix, int max, List<Solution> output)
        {
            if (output.Count >= max) return;
            if (depthOf[node] == 0)
            {
                var path = new ShiftPoint[suffix.Count];
                for (int i = 0; i < path.Length; i++) path[i] = moves[suffix[suffix.Count - 1 - i]];
                output.Add(Replay(start, path));
                return;
            }
            // 간선은 넣은 순서의 역순으로 이어져 있다. 먼저 찾은 것부터 보이도록 모아서 뒤집는다.
            var edges = new List<int>();
            for (int e = head[node]; e >= 0; e = edgeNext[e]) edges.Add(e);
            for (int k = edges.Count - 1; k >= 0; k--)
            {
                int e = edges[k];
                suffix.Add(edgeMove[e]);
                Walk(start, moves, depthOf, head, edgeParent, edgeMove, edgeNext, edgeParent[e], suffix, max, output);
                suffix.RemoveAt(suffix.Count - 1);
                if (output.Count >= max) return;
            }
        }

        private static ShiftPoint[] PathTo(ShiftPoint[] moves, List<int> parent, List<int> parentMove, int node, int lastMove)
        {
            var rev = new List<int> { lastMove };
            for (int n = node; parent[n] >= 0; n = parent[n]) rev.Add(parentMove[n]);
            var path = new ShiftPoint[rev.Count];
            for (int i = 0; i < path.Length; i++) path[i] = moves[rev[rev.Count - 1 - i]];
            return path;
        }

        /// <summary>수순을 처음부터 다시 두어 기믹을 모은다. 승리로 끝나지 않으면 solver의 오류다.</summary>
        public static Solution Replay(Board start, ShiftPoint[] path)
        {
            var board = SearchBoard(start);
            var mechanics = Mechanic.None;
            for (int i = 0; i < path.Length; i++)
            {
                if (board.Result != GameResult.InProgress) throw new InvalidOperationException("해가 중간에 끝났다: " + MovesText(path));
                if (!TurnRunner.PlayTurn(board, path[i])) throw new InvalidOperationException("둘 수 없는 수가 있다: " + MovesText(path));
                mechanics |= board.Mechanics;
            }
            if (board.Result != GameResult.Won) throw new InvalidOperationException("해가 승리로 끝나지 않았다: " + MovesText(path));
            return new Solution { Moves = path, Mechanics = mechanics };
        }

        // ------------------------------------------------------------------
        // 의도 기믹을 모두 쓰는 가장 짧은 해 (최적 길이의 우회해가 있을 때만 필요)
        // ------------------------------------------------------------------

        private struct MaskedKey : IEquatable<MaskedKey>
        {
            public byte[] Key;
            public Mechanic Mask;

            public bool Equals(MaskedKey o) { return Mask == o.Mask && ByteArrayComparer.Instance.Equals(Key, o.Key); }
            public override bool Equals(object obj) { return obj is MaskedKey k && Equals(k); }
            public override int GetHashCode() { return ByteArrayComparer.Instance.GetHashCode(Key) ^ ((ulong)Mask).GetHashCode(); }
        }

        private struct MaskedOpen
        {
            public int Node;
            public byte[] State;
            public Mechanic Mask;
        }

        /// <summary>
        /// 같은 상태라도 지금까지 쓴 의도 기믹이 다르면 다른 노드로 본다.
        /// 기믹은 한 번 쓰면 지워지지 않으므로, (상태, 쓴 기믹)마다 가장 먼저 닿은 턴만 남기면 된다.
        /// </summary>
        private static void FindIntended(Board start, ShiftPoint[] moves, SolveOptions opt, SolveReport report)
        {
            var intent = report.Intent;
            foreach (var s in report.Solutions)
            {
                if ((s.Mechanics & intent) != intent) continue;
                report.IntendedTurns = report.OptimalTurns;
                report.Intended = s;
                return;
            }

            var index = new HashSet<MaskedKey>();
            var parent = new List<int> { -1 };
            var parentMove = new List<int> { -1 };
            var work = start.Clone();
            work.Rules = start.Rules;
            var root = BoardCodec.Encode(start);
            index.Add(new MaskedKey { Key = root, Mask = Mechanic.None });
            var frontier = new List<MaskedOpen> { new MaskedOpen { Node = 0, State = root, Mask = Mechanic.None } };

            for (int depth = 0; depth < report.Depth && frontier.Count > 0; depth++)
            {
                var next = new List<MaskedOpen>();
                foreach (var open in frontier)
                {
                    opt.Cancel.ThrowIfCancellationRequested();
                    for (int mi = 0; mi < moves.Length; mi++)
                    {
                        BoardCodec.Load(work, open.State);
                        TurnRunner.PlayTurn(work, moves[mi]);
                        if (work.Result == GameResult.Lost) continue;
                        var mask = open.Mask | (work.Mechanics & intent);
                        if (work.Result == GameResult.Won)
                        {
                            if (mask != intent) continue;
                            report.IntendedTurns = depth + 1;
                            report.Intended = Replay(start, PathTo(moves, parent, parentMove, open.Node, mi));
                            return;
                        }
                        if (work.CannotWin) continue;
                        var key = BoardCodec.Encode(work);
                        if (!index.Add(new MaskedKey { Key = key, Mask = mask })) continue;
                        if (parent.Count >= opt.MaxNodes) { report.Truncated = true; continue; }
                        int id = parent.Count;
                        parent.Add(open.Node);
                        parentMove.Add(mi);
                        next.Add(new MaskedOpen { Node = id, State = key, Mask = mask });
                    }
                }
                frontier = next;
            }
        }
    }
}
