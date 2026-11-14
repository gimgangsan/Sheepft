using System;
using System.Collections.Generic;

namespace Sheepft.Core
{
    public sealed class Solution
    {
        public Move[] Moves;
        /// <summary>이 수순에서 일어난 모든 이벤트.</summary>
        public Ev Events;
    }

    public sealed class SolveOptions
    {
        /// <summary>탐색할 최대 턴 수. 0 이하면 레벨의 제한 턴.</summary>
        public int MaxDepth = 0;
        /// <summary>수순을 적어 줄 최적해의 최대 개수.</summary>
        public int MaxSolutions = 20;
        /// <summary>탐색 한 번에 보관할 상태 수의 한도. 넘으면 Truncated가 켜진다.</summary>
        public int MaxNodes = 6000000;
        /// <summary>의도 기믹. null이면 레벨의 @intent를 쓴다.</summary>
        public Ev? Intent = null;
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

        public Ev Intent;
        /// <summary>의도 기믹을 모두 쓰는 해의 최소 턴 수. 없으면 -1.</summary>
        public int IntendedTurns = -1;
        public Solution Intended;
        /// <summary>의도 기믹 중 하나라도 쓰지 않고 제한 턴 안에 깨는 해의 최소 턴 수. 없으면 -1.</summary>
        public int BypassTurns = -1;
        public Solution Bypass;
        /// <summary>그 해가 쓰지 않은 의도 기믹.</summary>
        public Ev BypassMissing;

        /// <summary>@optimal 이벤트 중, 그것 없이도 최적 길이로 깨지는 것. 없으면 None.</summary>
        public Ev OptimalMissing;
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
    /// 너비 우선 탐색. 수는 레벨이 허용하는 줄과 방향이고, 아무것도 움직이지 않는 shift(대기)도 수로 센다.
    ///
    /// 1. 최적 턴 수, 최적 길이 해의 개수와 수순.
    /// 2. 의도 기믹 하나하나에 대해, 그 이벤트가 일어나는 수를 금지하고 다시 탐색한다.
    ///    그래도 제한 턴 안에 깨지면 그 기믹 없이 깨는 해(우회해)가 있는 것이다.
    /// </summary>
    public static class Solver
    {
        public static SolveReport Solve(Level lv, SolveOptions opt = null)
        {
            if (opt == null) opt = new SolveOptions();
            var report = new SolveReport();
            report.Depth = opt.MaxDepth > 0 ? opt.MaxDepth : lv.TurnLimit;
            report.Intent = opt.Intent ?? lv.Intent;
            if (lv.TargetSheep <= 0 || !Simulator.HasGrass(lv.NewState())) return report;

            // 1. 최적해
            var best = Search(lv, report.Depth, Ev.None, true, opt.MaxSolutions, opt.MaxNodes);
            report.Nodes = best.Nodes;
            report.Truncated = best.Truncated;
            report.OptimalTurns = best.Turns;
            report.OptimalSequences = best.Sequences;
            report.OptimalStatePaths = best.StatePaths;
            report.Solutions = best.Solutions;
            if (best.Turns < 0) return report;

            // @optimal: 그 이벤트를 금지하고도 최적 길이로 깨지는지 본다.
            foreach (Ev one in EvText.Split(lv.OptimalIntent))
            {
                var r = Search(lv, best.Turns, one, false, 1, opt.MaxNodes);
                if (r.Truncated) report.Truncated = true;
                if (r.Turns < 0) continue;
                report.OptimalMissing |= one;
                if (report.OptimalBypass == null) report.OptimalBypass = r.Solutions[0];
            }
            if (report.Intent == Ev.None) return report;

            // 2. 의도 기믹을 하나씩 금지하고 탐색
            foreach (Ev one in EvText.Split(report.Intent))
            {
                var r = Search(lv, report.Depth, one, false, 1, opt.MaxNodes);
                if (r.Nodes > report.Nodes) report.Nodes = r.Nodes;
                if (r.Truncated) report.Truncated = true;
                if (r.Turns < 0) continue;
                if (report.BypassTurns < 0 || r.Turns < report.BypassTurns)
                {
                    report.BypassTurns = r.Turns;
                    report.Bypass = r.Solutions[0];
                    report.BypassMissing = report.Intent & ~r.Solutions[0].Events;
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
                FindIntended(lv, opt, report);
            }
            return report;
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

        private struct Open
        {
            public int Node;
            public PackedState State;
        }

        private static long Add(long a, long b)
        {
            long s = a + b;
            return s < 0 ? long.MaxValue : s; // 넘치면 최댓값에 묶어 둔다.
        }

        /// <param name="forbid">이 이벤트가 하나라도 일어나는 수는 두지 않는다.</param>
        /// <param name="count">true면 승리가 처음 나온 턴을 끝까지 훑어 해의 개수를 세고 수순을 모은다. false면 첫 해에서 멈춘다.</param>
        private static SearchResult Search(Level lv, int maxDepth, Ev forbid, bool count, int maxSolutions, int maxNodes)
        {
            var result = new SearchResult();
            var index = new Dictionary<StateKey, int>();
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

            var work = lv.NewState();          // 풀어 놓고 한 수 두어 보는 작업용 상태
            index[work.Key()] = 0;
            depthOf.Add(0);
            parent.Add(-1);
            parentMove.Add(-1);
            if (count) { sequences.Add(1); statePaths.Add(1); head.Add(-1); }

            var frontier = new List<Open> { new Open { Node = 0, State = work.Pack() } };
            var winParents = new List<int>();
            var winMoves = new List<int>();
            var seenChildren = new List<int>();
            var seenWins = new List<StateKey>();

            for (int depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
            {
                var next = new List<Open>();
                long winStatePaths = 0;

                for (int f = 0; f < frontier.Count; f++)
                {
                    var open = frontier[f];
                    seenChildren.Clear();
                    seenWins.Clear();
                    for (int mi = 0; mi < lv.Moves.Length; mi++)
                    {
                        work.Load(open.State);
                        var r = Simulator.Step(lv, work, lv.Moves[mi]);
                        if (r.Outcome == Outcome.Lose) continue;
                        if ((r.Events & forbid) != 0) continue;

                        if (r.Outcome == Outcome.Win)
                        {
                            if (!count)
                            {
                                result.Turns = depth + 1;
                                result.Solutions.Add(Replay(lv, PathTo(lv, parent, parentMove, open.Node, mi)));
                                result.Nodes = depthOf.Count;
                                return result;
                            }
                            winParents.Add(open.Node);
                            winMoves.Add(mi);
                            var wk = work.Key();
                            if (!seenWins.Contains(wk)) { seenWins.Add(wk); winStatePaths = Add(winStatePaths, statePaths[open.Node]); }
                            continue;
                        }
                        if (!work.TerrainShared && !Simulator.HasGrass(work)) continue; // 승리 불가 상태

                        var key = work.Key();
                        int id;
                        if (index.TryGetValue(key, out id))
                        {
                            if (!count || depthOf[id] != depth + 1) continue; // 더 일찍 닿은 상태
                        }
                        else
                        {
                            if (depthOf.Count >= maxNodes) { result.Truncated = true; continue; }
                            id = depthOf.Count;
                            index[key] = id;
                            depthOf.Add(depth + 1);
                            parent.Add(open.Node);
                            parentMove.Add(mi);
                            if (count) { sequences.Add(0); statePaths.Add(0); head.Add(-1); }
                            next.Add(new Open { Node = id, State = work.Pack() });
                        }
                        if (!count) continue;

                        edgeParent.Add(open.Node);
                        edgeMove.Add(mi);
                        edgeNext.Add(head[id]);
                        head[id] = edgeParent.Count - 1;
                        sequences[id] = Add(sequences[id], sequences[open.Node]);
                        if (!seenChildren.Contains(id))
                        {
                            seenChildren.Add(id);
                            statePaths[id] = Add(statePaths[id], statePaths[open.Node]);
                        }
                    }
                    frontier[f] = default(Open); // 다 쓴 상태는 놓아 준다.
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
                        Walk(lv, depthOf, head, edgeParent, edgeMove, edgeNext, winParents[i], suffix, maxSolutions, result.Solutions);
                    }
                    break;
                }
                frontier = next;
            }
            result.Nodes = depthOf.Count;
            return result;
        }

        private static void Walk(Level lv, List<int> depthOf, List<int> head, List<int> edgeParent, List<int> edgeMove, List<int> edgeNext,
            int node, List<int> suffix, int max, List<Solution> output)
        {
            if (output.Count >= max) return;
            if (depthOf[node] == 0)
            {
                var moves = new Move[suffix.Count];
                for (int i = 0; i < moves.Length; i++) moves[i] = lv.Moves[suffix[suffix.Count - 1 - i]];
                output.Add(Replay(lv, moves));
                return;
            }
            // 간선은 넣은 순서의 역순으로 이어져 있다. 먼저 찾은 것부터 보이도록 모아서 뒤집는다.
            var edges = new List<int>();
            for (int e = head[node]; e >= 0; e = edgeNext[e]) edges.Add(e);
            for (int k = edges.Count - 1; k >= 0; k--)
            {
                int e = edges[k];
                suffix.Add(edgeMove[e]);
                Walk(lv, depthOf, head, edgeParent, edgeMove, edgeNext, edgeParent[e], suffix, max, output);
                suffix.RemoveAt(suffix.Count - 1);
                if (output.Count >= max) return;
            }
        }

        private static Move[] PathTo(Level lv, List<int> parent, List<int> parentMove, int node, int lastMove)
        {
            var rev = new List<int> { lastMove };
            for (int n = node; parent[n] >= 0; n = parent[n]) rev.Add(parentMove[n]);
            var moves = new Move[rev.Count];
            for (int i = 0; i < moves.Length; i++) moves[i] = lv.Moves[rev[rev.Count - 1 - i]];
            return moves;
        }

        /// <summary>수순을 처음부터 다시 두어 이벤트를 모은다. 승리로 끝나지 않으면 solver의 오류다.</summary>
        public static Solution Replay(Level lv, Move[] moves)
        {
            var s = lv.NewState();
            Ev events = Ev.None;
            Outcome last = Outcome.Ongoing;
            for (int i = 0; i < moves.Length; i++)
            {
                if (last != Outcome.Ongoing) throw new InvalidOperationException("해가 중간에 끝났다: " + lv.MovesText(moves));
                var r = Simulator.Step(lv, s, moves[i]);
                events |= r.Events;
                last = r.Outcome;
            }
            if (last != Outcome.Win) throw new InvalidOperationException("해가 승리로 끝나지 않았다: " + lv.MovesText(moves));
            return new Solution { Moves = moves, Events = events };
        }

        // ------------------------------------------------------------------
        // 의도 기믹을 모두 쓰는 가장 짧은 해 (최적 길이의 우회해가 있을 때만 필요)
        // ------------------------------------------------------------------

        private struct MaskedKey : IEquatable<MaskedKey>
        {
            public StateKey Key;
            public Ev Mask;
            public bool Equals(MaskedKey o) { return Key.Equals(o.Key) && Mask == o.Mask; }
            public override bool Equals(object obj) { return obj is MaskedKey k && Equals(k); }
            public override int GetHashCode() { return Key.GetHashCode() ^ ((ulong)Mask).GetHashCode(); }
        }

        private struct MaskedOpen
        {
            public int Node;
            public PackedState State;
            public Ev Mask;
        }

        /// <summary>
        /// 같은 상태라도 지금까지 쓴 의도 기믹이 다르면 다른 노드로 본다.
        /// 이벤트는 한 번 일어나면 지워지지 않으므로, (상태, 쓴 기믹)마다 가장 먼저 닿은 턴만 남기면 된다.
        /// </summary>
        private static void FindIntended(Level lv, SolveOptions opt, SolveReport report)
        {
            Ev intent = report.Intent;
            foreach (var s in report.Solutions)
            {
                if ((s.Events & intent) != intent) continue;
                report.IntendedTurns = report.OptimalTurns;
                report.Intended = s;
                return;
            }

            var index = new HashSet<MaskedKey>();
            var parent = new List<int> { -1 };
            var parentMove = new List<int> { -1 };
            var work = lv.NewState();
            index.Add(new MaskedKey { Key = work.Key(), Mask = Ev.None });
            var frontier = new List<MaskedOpen> { new MaskedOpen { Node = 0, State = work.Pack(), Mask = Ev.None } };

            for (int depth = 0; depth < report.Depth && frontier.Count > 0; depth++)
            {
                var next = new List<MaskedOpen>();
                foreach (var open in frontier)
                {
                    for (int mi = 0; mi < lv.Moves.Length; mi++)
                    {
                        work.Load(open.State);
                        var r = Simulator.Step(lv, work, lv.Moves[mi]);
                        if (r.Outcome == Outcome.Lose) continue;
                        Ev mask = open.Mask | (r.Events & intent);
                        if (r.Outcome == Outcome.Win)
                        {
                            if (mask != intent) continue;
                            report.IntendedTurns = depth + 1;
                            report.Intended = Replay(lv, PathTo(lv, parent, parentMove, open.Node, mi));
                            return;
                        }
                        if (!work.TerrainShared && !Simulator.HasGrass(work)) continue;
                        if (!index.Add(new MaskedKey { Key = work.Key(), Mask = mask })) continue;
                        if (parent.Count >= opt.MaxNodes) { report.Truncated = true; continue; }
                        int id = parent.Count;
                        parent.Add(open.Node);
                        parentMove.Add(mi);
                        next.Add(new MaskedOpen { Node = id, State = work.Pack(), Mask = mask });
                    }
                }
                frontier = next;
            }
        }
    }
}
