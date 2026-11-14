using System;
using System.Collections.Generic;

namespace Sheepft.Core
{
    public struct StepResult
    {
        public Outcome Outcome;
        public Ev Events;
    }

    /// <summary>
    /// README의 규칙대로 한 턴을 진행한다. 상태는 넘겨받은 GameState를 직접 고친다.
    /// 제한 턴은 여기서 세지 않는다(Game과 Solver가 센다).
    /// </summary>
    public static class Simulator
    {
        public static StepResult Step(Level level, GameState state, Move move, List<string> log = null)
        {
            var sim = new Sim(level, state, log);
            var result = new StepResult();
            result.Outcome = sim.Run(move);
            result.Events = sim.Events;
            return result;
        }

        /// <summary>풀 칸 위에 젖지 않고 화상을 입지 않은 양이 목표 수 이상 있는가.</summary>
        public static bool IsWin(Level level, GameState s)
        {
            if (level.TargetSheep <= 0) return false;
            int n = 0;
            for (int i = 0; i < s.E.Count; i++)
            {
                var e = s.E[i];
                if (e.Kind == Kind.Sheep && !e.Wet && !e.Burned && s.T[e.Pos] == Terrain.Grass) n++;
            }
            return n >= level.TargetSheep;
        }

        /// <summary>풀이 하나도 남지 않으면 승리 불가 상태다(README 8절).</summary>
        public static bool HasGrass(GameState s)
        {
            for (int i = 0; i < s.T.Length; i++) if (s.T[i] == Terrain.Grass) return true;
            return false;
        }
    }

    internal sealed class Sim
    {
        private const int Inf = int.MaxValue;

        private readonly Level L;
        private readonly GameState S;
        private readonly List<Entity> E;
        private readonly Terrain[] T;
        private readonly List<string> Log;

        public Ev Events;
        private bool lost;
        /// <summary>이번 턴의 2단계 승리 판정이 끝났는가. 그 뒤에 걸린 기절·식사는 다음 턴 끝까지만 간다.</summary>
        private bool turnEnded;

        // 길 찾기와 위치 기록에 다시 쓰는 버퍼 (탐색에서 수백만 번 불리므로 매번 만들지 않는다)
        [ThreadStatic] private static int[] distBuffer;
        [ThreadStatic] private static int[] queueBuffer;
        [ThreadStatic] private static int[] posBuffer;
        private readonly int[] dist;
        private readonly int[] queue;
        private int queueTail;
        private int[] before;
        private bool beforeHasTwoSheep;

        public Sim(Level level, GameState state, List<string> log)
        {
            L = level;
            S = state;
            E = state.E;
            T = state.T;
            Log = log;

            int n = level.CellCount;
            if (distBuffer == null || distBuffer.Length < n) { distBuffer = new int[n]; queueBuffer = new int[n]; }
            dist = distBuffer;
            queue = queueBuffer;
        }

        /// <summary>기절·식사 기간: 걸린 턴의 남은 시간과 다음 턴 끝까지.</summary>
        private int Timer { get { return turnEnded ? 1 : 2; } }

        // ------------------------------------------------------------------
        // 턴
        // ------------------------------------------------------------------

        public Outcome Run(Move move)
        {
            for (int i = 0; i < E.Count; i++) { E[i].WaterDone = false; E[i].FireDone = false; }

            // 1단계: shift
            Shift(move);
            Cleanup();
            if (lost) return Outcome.Lose;
            if (Simulator.IsWin(L, S)) return Outcome.Win;

            // 2단계: 종료 후 반응. 염소 → 말 → 소 → 늑대 → 들불 확산
            if (!Phase(Kind.Goat)) return Outcome.Lose;
            if (!Phase(Kind.Horse)) return Outcome.Lose;
            if (!Phase(Kind.Cow)) return Outcome.Lose;
            if (!Phase(Kind.Wolf)) return Outcome.Lose;
            if (L.HasFire && Spread())
            {
                Cleanup();
                if (lost) return Outcome.Lose;
            }
            if (Simulator.IsWin(L, S)) return Outcome.Win;

            // 승리 판정 뒤에 기절과 식사가 풀린다. 풀리는 순간에도 정리 절차를 돌린다.
            turnEnded = true;
            if (L.HasKind[(int)Kind.Wolf])
            {
                bool released = false;
                for (int i = 0; i < E.Count; i++)
                {
                    var e = E[i];
                    if (e.Kind != Kind.Wolf) continue;
                    if (e.Stun > 0 && --e.Stun == 0) { released = true; if (Log != null) Log.Add("늑대 " + At(e.Pos) + " 기절 해제"); }
                    if (e.Meal > 0 && --e.Meal == 0) { released = true; if (Log != null) Log.Add("늑대 " + At(e.Pos) + " 식사 끝"); }
                }
                if (released)
                {
                    Cleanup();
                    if (lost) return Outcome.Lose;
                }
            }
            return Outcome.Ongoing;
        }

        /// <summary>한 종류의 종료 후 반응. 차례가 시작될 때의 위치 기준으로 (0,0)부터 행 우선 순서.</summary>
        private bool Phase(Kind kind)
        {
            if (!L.HasKind[(int)kind]) return true;
            int n = 0;
            for (int i = 0; i < E.Count; i++) if (E[i].Kind == kind) n++;
            if (n == 0) return true;

            // 차례가 시작될 때의 순서를 Id로 적어 둔다. 도중에 움직여도 순서는 바뀌지 않는다.
            var ids = new int[n];
            Entity prev = null;
            for (int k = 0; k < n; k++) { prev = NextInOrder(kind, prev); ids[k] = prev.Id; }

            for (int k = 0; k < n; k++)
            {
                var e = S.Find(ids[k]);
                if (e == null) continue; // 차례가 오기 전에 사라졌거나 합쳐졌다.
                bool acted;
                switch (kind)
                {
                    case Kind.Goat: acted = GoatAttack(e); break;
                    case Kind.Horse: acted = HorseMove(e); break;
                    case Kind.Cow: acted = CowMove(e); break;
                    default: acted = WolfMove(e); break;
                }
                if (!acted) continue;
                Cleanup();
                if (lost) return false;
            }
            return true;
        }

        /// <summary>그 종류 중 prev 바로 다음 차례(위치, 그다음 Id 순). prev가 null이면 첫째. 없으면 null.</summary>
        private Entity NextInOrder(Kind kind, Entity prev)
        {
            int pp = prev == null ? -1 : prev.Pos, pi = prev == null ? -1 : prev.Id;
            Entity best = null;
            for (int i = 0; i < E.Count; i++)
            {
                var e = E[i];
                if (e.Kind != kind) continue;
                if (e.Pos < pp || (e.Pos == pp && e.Id <= pi)) continue;
                if (best == null || e.Pos < best.Pos || (e.Pos == best.Pos && e.Id < best.Id)) best = e;
            }
            return best;
        }

        // ------------------------------------------------------------------
        // 1단계: shift (README 3절)
        // ------------------------------------------------------------------

        private void Shift(Move move)
        {
            var line = L.Lines[move.Line];
            var cells = line.Cells;
            int n = cells.Length;
            bool moved = false;
            if (n > 1)
            {
                Snapshot();
                // 각 오브젝트의 결과는 자기 칸과 줄의 다음 칸의 지형만으로 정해지므로 하나씩 옮겨도 된다.
                for (int i = 0; i < E.Count; i++)
                {
                    var e = E[i];
                    int idx = line.IndexOfCell[e.Pos];
                    if (idx < 0) continue;
                    int ni = move.Forward ? idx + 1 : idx - 1;
                    bool wrap = false;
                    if (ni == n) { ni = 0; wrap = true; }
                    else if (ni < 0) { ni = n - 1; wrap = true; }
                    int np = cells[ni];
                    if (Rules.IsObstacle(T[np])) { Events |= Ev.Blocked; continue; }

                    bool skip = !wrap && !Adjacent(e.Pos, np);
                    e.Pos = np;
                    moved = true;
                    Events |= Ev.ShiftMove;
                    if (wrap)
                    {
                        Events |= Ev.Wrap;
                        if (e.Kind == Kind.Sheep) Events |= Ev.SheepWrap;
                        else if (e.Kind == Kind.Wolf) Events |= Ev.WolfWrap;
                    }
                    if (skip) Events |= Ev.VoidSkip;
                    Entered(e);
                }
                CheckSheepMerge();
            }
            if (!moved) Events |= Ev.Wait;
        }

        // ------------------------------------------------------------------
        // 정리 절차 (README 2절)
        // ------------------------------------------------------------------

        private void Cleanup()
        {
            bool changed;
            do
            {
                changed = Overlaps();
                changed |= AutoReactions();
                if (lost) return;
            } while (changed);
        }

        /// <summary>1. 겹침 상호작용. 지형, 늑대 합류, 소의 먹기, 목표 상실.</summary>
        private bool Overlaps()
        {
            bool any = false, ch;
            do
            {
                ch = false;

                // 물 찬 양동이를 가장 먼저 처리한다. 불이 꺼지면 같은 칸의 다른 오브젝트는 들불 상호작용을 받지 않는다.
                if (L.HasFire && L.HasKind[(int)Kind.Bucket])
                {
                    for (int i = 0; i < E.Count; i++)
                    {
                        var e = E[i];
                        if (e.Kind != Kind.Bucket || !e.Full || e.FireDone || T[e.Pos] != Terrain.Fire) continue;
                        T[e.Pos] = Terrain.Ash;
                        e.Full = false;
                        e.Wet = true;
                        e.FireDone = true;
                        Events |= Ev.Extinguish;
                        if (Log != null) Log.Add("양동이 " + At(e.Pos) + " 불을 끔");
                        ch = true;
                    }
                }

                // 지형 상호작용: 오브젝트마다, 지형 종류마다 턴당 1번.
                for (int i = E.Count - 1; i >= 0; i--)
                {
                    var e = E[i];
                    Terrain t = T[e.Pos];
                    if (t != Terrain.Water && t != Terrain.Fire) continue;
                    if (e.Sated) continue; // 배부른 말·소는 모든 상호작용을 하지 않는다.
                    if (t == Terrain.Water)
                    {
                        if (e.WaterDone) continue;
                        e.WaterDone = true;
                        if (e.Kind == Kind.Bucket)
                        {
                            if (!e.Full)
                            {
                                e.Full = true;
                                e.Wet = false;
                                Events |= Ev.BucketFilled;
                                if (Log != null) Log.Add("양동이 " + At(e.Pos) + " 물이 참");
                                ch = true;
                            }
                            continue;
                        }
                        if (!e.Wet)
                        {
                            e.Wet = true;
                            ch = true;
                            if (e.Kind == Kind.Sheep) Events |= Ev.SheepWet;
                            if (Log != null) Log.Add(Name(e) + " " + At(e.Pos) + " 젖음");
                        }
                        if (e.Burned)
                        {
                            e.Burned = false;
                            ch = true;
                            if (e.Kind == Kind.Sheep) Events |= Ev.SheepCured;
                            else if (e.Kind == Kind.Wolf) Events |= Ev.WolfCured;
                            if (Log != null) Log.Add(Name(e) + " " + At(e.Pos) + " 화상 회복");
                        }
                    }
                    else
                    {
                        if (e.FireDone) continue;
                        e.FireDone = true;
                        if (e.Kind == Kind.Bucket && e.Full) continue; // 위에서 처리됨
                        if (e.Wet)
                        {
                            e.Wet = false;
                            ch = true;
                            if (e.Kind == Kind.Sheep) Events |= Ev.SheepDried;
                            if (Log != null) Log.Add(Name(e) + " " + At(e.Pos) + " 불에서 마름");
                        }
                        else if (!Rules.IsAnimal(e.Kind))
                        {
                            if (Log != null) Log.Add(Name(e) + " " + At(e.Pos) + " 불에 탐");
                            E.RemoveAt(i);
                            Events |= Ev.ItemBurned;
                            ch = true;
                        }
                        else if (!e.Burned)
                        {
                            e.Burned = true;
                            ch = true;
                            if (e.Kind == Kind.Sheep) Events |= Ev.SheepBurned;
                            else if (e.Kind == Kind.Wolf) Events |= Ev.WolfBurned;
                            else Events |= Ev.AnimalBurned;
                            if (Log != null) Log.Add(Name(e) + " " + At(e.Pos) + " 화상");
                        }
                    }
                }

                // 늑대 합류: 화상이 아닌 늑대끼리 같은 칸이 되면 하나의 무리가 되고 기절한다.
                if (L.HasKind[(int)Kind.Wolf])
                {
                    for (int i = 0; i < E.Count; i++)
                    {
                        var a = E[i];
                        if (a.Kind != Kind.Wolf || a.Burned) continue;
                        bool merged = false;
                        for (int j = E.Count - 1; j > i; j--)
                        {
                            var b = E[j];
                            if (b.Kind != Kind.Wolf || b.Burned || b.Pos != a.Pos) continue;
                            a.Count += b.Count;
                            a.Wet = a.Wet && b.Wet;
                            if (b.Meal > a.Meal) a.Meal = b.Meal;
                            E.RemoveAt(j);
                            merged = true;
                        }
                        if (!merged) continue;
                        a.Stun = Timer;
                        a.LastDir = Dir.None;
                        Events |= Ev.WolfMerge;
                        if (Log != null) Log.Add("늑대 " + At(a.Pos) + " 합류, " + a.Count + "마리 무리 (기절)");
                        ch = true;
                    }
                }

                // 소의 먹기: 소와 바구니가 같은 칸이 되는 순간.
                if (L.HasKind[(int)Kind.Cow] && L.HasKind[(int)Kind.Basket])
                {
                    for (int i = 0; i < E.Count; i++)
                    {
                        var cow = E[i];
                        if (cow.Kind != Kind.Cow || cow.Sated) continue;
                        Entity basket = null;
                        for (int j = 0; j < E.Count; j++)
                        {
                            var b = E[j];
                            if (b.Kind != Kind.Basket || b.Pos != cow.Pos) continue;
                            if (basket == null || b.Id == cow.Target) basket = b;
                        }
                        if (basket == null) continue;
                        E.Remove(basket);
                        cow.Sated = true;
                        cow.Target = -1;
                        Events |= Ev.CowEat;
                        if (Log != null) Log.Add("소 " + At(cow.Pos) + " 바구니를 먹음");
                        ch = true;
                        i = -1; // 목록이 바뀌었으니 처음부터 다시 본다.
                    }
                }

                // 목표 상실: 목표 바구니가 사라졌거나, 말의 칸에 들어옴.
                if (L.HasKind[(int)Kind.Basket])
                {
                    for (int i = 0; i < E.Count; i++)
                    {
                        var e = E[i];
                        if (e.Target < 0) continue;
                        var b = S.Find(e.Target);
                        if (b == null || (e.Kind == Kind.Horse && b.Pos == e.Pos))
                        {
                            e.Target = -1;
                            ch = true;
                        }
                    }
                }

                any |= ch;
            } while (ch);
            return any;
        }

        /// <summary>2. 자동 반응. 말의 발견 → 말의 먹기 → 소의 냄새 → 늑대의 양 잡기.</summary>
        private bool AutoReactions()
        {
            bool ch = false;
            bool baskets = L.HasKind[(int)Kind.Basket];

            // 자동 반응 도중에는 말, 소, 늑대가 자리를 옮기지 않으므로 순서를 그때그때 찾아도 된다.
            if (baskets && L.HasKind[(int)Kind.Horse])
            {
                for (var h = NextInOrder(Kind.Horse, null); h != null; h = NextInOrder(Kind.Horse, h))
                {
                    if (h.Sated || h.Target >= 0) continue;
                    var b = HorseSees(h);
                    if (b == null) continue;
                    h.Target = b.Id;
                    Events |= Ev.HorseDiscover;
                    if (b.Carried) Events |= Ev.HorseLure;
                    if (Log != null) Log.Add("말 " + At(h.Pos) + " 바구니 " + At(b.Pos) + " 발견");
                    ch = true;
                }
                for (var h = NextInOrder(Kind.Horse, null); h != null; h = NextInOrder(Kind.Horse, h))
                {
                    if (h.Sated) continue;
                    if (HorseEat(h)) ch = true;
                }
            }

            if (baskets && L.HasKind[(int)Kind.Cow])
            {
                for (var c = NextInOrder(Kind.Cow, null); c != null; c = NextInOrder(Kind.Cow, c))
                {
                    if (c.Sated || c.Target >= 0) continue;
                    var b = CowSmells(c);
                    if (b == null) continue;
                    c.Target = b.Id;
                    Events |= Ev.CowSmell;
                    if (b.Carried) Events |= Ev.CowLure;
                    if (Log != null) Log.Add("소 " + At(c.Pos) + " 바구니 " + At(b.Pos) + " 냄새");
                    ch = true;
                }
            }

            if (L.HasKind[(int)Kind.Wolf])
            {
                for (var w = NextInOrder(Kind.Wolf, null); w != null; w = NextInOrder(Kind.Wolf, w))
                {
                    if (w.Burned) continue;
                    if (WolfCatch(w)) ch = true;
                    if (lost) return true;
                }
            }
            return ch;
        }

        // ------------------------------------------------------------------
        // 늑대 (README 6절)
        // ------------------------------------------------------------------

        private bool WolfCatch(Entity w)
        {
            int range = w.Count - 1;
            int wr = L.RowOf(w.Pos), wc = L.ColOf(w.Pos);

            // 범위는 무리를 중심으로 한 정사각형. 장애물과 빈칸은 무시하고 순환하지 않는다.
            int inRange = 0;
            for (int i = 0; i < E.Count; i++)
            {
                var s = E[i];
                if (s.Kind == Kind.Sheep && Ring(wr, wc, s.Pos) <= range) inRange++;
            }
            if (inRange == 0) return false;
            if (w.Stun > 0) { Events |= Ev.StunSpared; return false; }
            if (w.Meal > 0) { Events |= Ev.MealSpared; return false; }

            // 가까운 칸부터, 거리가 같으면 (0,0)부터 행 우선. 한 번에 무리 크기만큼만 먹는다.
            int eat = Math.Min(w.Count, inRange);
            for (int k = 0; k < eat; k++)
            {
                Entity best = null;
                int bestRing = Inf;
                for (int i = 0; i < E.Count; i++)
                {
                    var s = E[i];
                    if (s.Kind != Kind.Sheep) continue;
                    int ring = Ring(wr, wc, s.Pos);
                    if (ring > range) continue;
                    if (best == null || ring < bestRing || (ring == bestRing && (s.Pos < best.Pos || (s.Pos == best.Pos && s.Id < best.Id))))
                    {
                        best = s;
                        bestRing = ring;
                    }
                }
                if (best.Pos != w.Pos) Events |= Ev.PackCatch;
                if (Log != null) Log.Add("늑대 " + At(w.Pos) + " 양 " + At(best.Pos) + " 을 먹음");
                E.Remove(best);
            }
            Events |= Ev.WolfEat;
            w.Meal = Timer;

            int left = 0;
            for (int i = 0; i < E.Count; i++) if (E[i].Kind == Kind.Sheep) left++;
            if (left < L.TargetSheep) lost = true;
            return true;
        }

        private int Ring(int r, int c, int pos)
        {
            return Math.Max(Math.Abs(L.RowOf(pos) - r), Math.Abs(L.ColOf(pos) - c));
        }

        private bool WolfMove(Entity w)
        {
            if (w.Burned || w.Stun > 0 || w.Meal > 0) return false;
            if (Mounted(w)) { Events |= Ev.RideHeld; return false; }

            // 갈 수 있는 양 = 걸어 들어갈 수 있는 지형 위의 양.
            PathReset();
            bool anySheep = false;
            for (int i = 0; i < E.Count; i++)
            {
                var s = E[i];
                if (s.Kind != Kind.Sheep) continue;
                anySheep = true;
                if (s.Pos == w.Pos) return false; // 같은 칸의 양은 걸어갈 대상이 아니다.
                if (Rules.IsWalkable(T[s.Pos])) PathSeed(s.Pos);
            }
            if (!anySheep) return false;

            // 가장 가까운 양이 여럿이면 그중 어느 양에게든 가까워지는 칸이 모두 후보다.
            int dir = PathFirstStep(w.Pos, w.LastDir);
            if (dir < 0) { Events |= Ev.WolfNoPath; return false; }

            int np = L.Nb[w.Pos * 4 + dir];
            if (Log != null) Log.Add("늑대 " + At(w.Pos) + " → " + At(np));
            w.Pos = np; // 같은 칸에 다른 오브젝트가 있어도 혼자만 이동한다.
            w.LastDir = (Dir)dir;
            Events |= Ev.WolfWalk;
            Entered(w);
            return true;
        }

        // ------------------------------------------------------------------
        // 염소 (README 6절)
        // ------------------------------------------------------------------

        private bool GoatAttack(Entity g)
        {
            if (g.Burned) return false;
            if (Mounted(g)) { Events |= Ev.RideHeld; return false; }

            // 공격을 시작할 때 4방향의 대상을 한꺼번에 정한다: 시작할 때의 위치(before)로 대상을 고른다.
            Snapshot();
            int n = E.Count;
            int gr = L.RowOf(g.Pos), gc = L.ColOf(g.Pos);
            bool moved = false;

            // 상 > 우 > 하 > 좌 순으로, 각 방향은 먼 칸부터 민다. 순환하지 않고 빈칸을 건너뛰지도 않는다.
            for (int d = 0; d < 4; d++)
            {
                int c1 = L.Nb[g.Pos * 4 + d];
                if (c1 < 0 || Rules.IsObstacle(T[c1]))
                {
                    // 장애물이나 빈칸 너머는 보지 못한다.
                    int r2 = gr + 2 * Rules.DR[d], c2x = gc + 2 * Rules.DC[d];
                    if (L.InMap(r2, c2x))
                    {
                        int p2 = L.Pos(r2, c2x);
                        for (int i = 0; i < n; i++) if (before[i] == p2) { Events |= Ev.GoatSightBlocked; break; }
                    }
                    continue;
                }
                int c2 = L.Nb[c1 * 4 + d];
                if (c2 < 0 || Rules.IsObstacle(T[c2])) continue; // 1칸 거리의 오브젝트가 밀릴 자리가 없다.

                int dest = L.Nb[c2 * 4 + d];
                if (dest >= 0 && !Rules.IsObstacle(T[dest]))
                {
                    for (int i = 0; i < n; i++) if (before[i] == c2) { GoatPush(E[i], dest); moved = true; }
                }
                for (int i = 0; i < n; i++) if (before[i] == c1) { GoatPush(E[i], c2); moved = true; }
            }
            if (moved) CheckSheepMerge();
            return moved;
        }

        private void GoatPush(Entity e, int dest)
        {
            if (Log != null) Log.Add("염소가 " + Name(e) + " " + At(e.Pos) + " → " + At(dest) + " 밀침");
            e.Pos = dest;
            Events |= Ev.GoatPush;
            if (e.Kind == Kind.Sheep) Events |= Ev.GoatPushSheep;
            else if (e.Kind == Kind.Wolf) Events |= Ev.GoatPushWolf;
            else if (!Rules.IsAnimal(e.Kind)) Events |= Ev.GoatPushItem;
            Entered(e);
        }

        // ------------------------------------------------------------------
        // 말 (README 6절)
        // ------------------------------------------------------------------

        /// <summary>같은 행이나 열에서 보이는 가장 가까운 바구니. 동점이면 상 > 우 > 하 > 좌.</summary>
        private Entity HorseSees(Entity h)
        {
            Entity best = null;
            int bestDist = Inf;
            for (int d = 0; d < 4; d++)
            {
                int p = h.Pos, steps = 0;
                while (true)
                {
                    p = L.Nb[p * 4 + d];
                    if (p < 0 || Rules.IsObstacle(T[p])) break; // 시야는 장애물과 빈칸에 막힌다.
                    steps++;
                    var b = FirstAt(p, Kind.Basket);
                    if (b == null) continue;
                    if (steps < bestDist) { best = b; bestDist = steps; }
                    break;
                }
            }
            return best;
        }

        private bool HorseEat(Entity h)
        {
            Entity basket = null;
            if (h.Target >= 0)
            {
                var t = S.Find(h.Target);
                if (t != null && Adjacent(t.Pos, h.Pos)) basket = t;
            }
            for (int d = 0; d < 4 && basket == null; d++)
            {
                int p = L.Nb[h.Pos * 4 + d];
                if (p >= 0) basket = FirstAt(p, Kind.Basket);
            }
            if (basket == null) return false;

            int dest = basket.Pos;
            E.Remove(basket);
            if (Log != null) Log.Add("말 " + At(h.Pos) + " 바구니 " + At(dest) + " 를 먹음");

            // 말과 같은 칸의 다른 오브젝트(다른 말 제외)를 모두 바구니가 있던 칸으로 옮긴다.
            Snapshot();
            bool dropped = false;
            for (int i = 0; i < E.Count; i++)
            {
                var x = E[i];
                if (x == h || x.Pos != h.Pos || x.Kind == Kind.Horse) continue;
                x.Pos = dest;
                dropped = true;
                if (x.Kind == Kind.Sheep) Events |= Ev.HorseDropSheep;
                if (x.Kind == Kind.Basket) x.Carried = true;
                Entered(x);
            }
            if (dropped) { Events |= Ev.HorseDrop; CheckSheepMerge(); }

            h.Sated = true;
            h.Target = -1;
            Events |= Ev.HorseEat;
            return true;
        }

        private bool HorseMove(Entity h)
        {
            if (h.Sated || h.Target < 0 || h.Burned) return false;
            var b = S.Find(h.Target);
            if (b == null || b.Pos == h.Pos) return false;

            int dir = StepToward(h.Pos, b.Pos, h.LastDir);
            if (dir < 0) { Events |= Ev.HorseNoPath; return false; }
            int np = L.Nb[h.Pos * 4 + dir];
            if (Log != null) Log.Add("말 " + At(h.Pos) + " → " + At(np));

            // 운반: 같은 칸의 다른 오브젝트(다른 말 제외)를 모두 데리고 간다.
            Snapshot();
            for (int i = 0; i < E.Count; i++)
            {
                var x = E[i];
                if (x == h || x.Pos != h.Pos || x.Kind == Kind.Horse) continue;
                x.Pos = np;
                Events |= Ev.HorseCarry;
                if (x.Kind == Kind.Sheep) Events |= Ev.HorseCarrySheep;
                else if (x.Kind == Kind.Wolf) Events |= Ev.HorseCarryWolf;
                else if (x.Kind == Kind.Basket) x.Carried = true;
                Entered(x);
            }
            h.Pos = np;
            h.LastDir = (Dir)dir;
            Events |= Ev.HorseWalk;
            Entered(h);
            CheckSheepMerge();
            return true;
        }

        /// <summary>발견 상태인 말과 같은 칸의 다른 동물은 탑승 상태다. 위치로만 판단한다.</summary>
        private bool Mounted(Entity e)
        {
            if (e.Kind == Kind.Horse || !L.HasKind[(int)Kind.Horse]) return false;
            for (int i = 0; i < E.Count; i++)
            {
                var h = E[i];
                if (h.Kind == Kind.Horse && h.Pos == e.Pos && h.Target >= 0) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 소 (README 6절)
        // ------------------------------------------------------------------

        /// <summary>맨해튼 거리 3칸 안의 가장 가까운 바구니. 동점이면 (0,0)부터 행 우선.</summary>
        private Entity CowSmells(Entity c)
        {
            Entity best = null;
            int bestDist = Inf;
            int cr = L.RowOf(c.Pos), cc = L.ColOf(c.Pos);
            for (int i = 0; i < E.Count; i++)
            {
                var b = E[i];
                if (b.Kind != Kind.Basket) continue;
                int d = Math.Abs(L.RowOf(b.Pos) - cr) + Math.Abs(L.ColOf(b.Pos) - cc);
                if (d > 3) continue;
                if (best == null || d < bestDist || (d == bestDist && (b.Pos < best.Pos || (b.Pos == best.Pos && b.Id < best.Id))))
                {
                    best = b;
                    bestDist = d;
                }
            }
            return best;
        }

        private bool CowMove(Entity c)
        {
            if (c.Sated || c.Target < 0 || c.Burned) return false;
            if (Mounted(c)) { Events |= Ev.RideHeld; return false; }
            var b = S.Find(c.Target);
            if (b == null || b.Pos == c.Pos) return false;

            int dir = StepToward(c.Pos, b.Pos, c.LastDir);
            if (dir < 0) { Events |= Ev.CowNoPath; return false; }
            int np = L.Nb[c.Pos * 4 + dir];
            if (Log != null) Log.Add("소 " + At(c.Pos) + " → " + At(np));

            // 소 앞 한 칸에만 적용되는 길이 1짜리 shift. 맵 끝, 빈칸, 장애물이면 밀리지 않는다. 연쇄로 밀지 않는다.
            int dest = L.Nb[np * 4 + dir];
            if (dest >= 0 && !Rules.IsObstacle(T[dest]))
            {
                Snapshot();
                bool pushed = false;
                for (int i = 0; i < E.Count; i++)
                {
                    var x = E[i];
                    if (x.Pos != np) continue;
                    x.Pos = dest; // 밀려난 자리에 이미 있던 오브젝트와는 그냥 겹친다.
                    pushed = true;
                    Events |= Ev.CowPush;
                    if (x.Kind == Kind.Sheep) Events |= Ev.CowPushSheep;
                    else if (x.Kind == Kind.Wolf) Events |= Ev.CowPushWolf;
                    else if (x.Kind == Kind.Basket) Events |= Ev.CowPushBasket;
                    Entered(x);
                }
                if (pushed) CheckSheepMerge();
            }
            c.Pos = np;
            c.LastDir = (Dir)dir;
            Events |= Ev.CowWalk;
            Entered(c);
            return true;
        }

        // ------------------------------------------------------------------
        // 들불 확산 (README 7절)
        // ------------------------------------------------------------------

        private bool Spread()
        {
            // 확산이 시작될 때 있던 들불만 번진다: 불붙을 칸을 모아 두었다가 한꺼번에 바꾼다.
            List<int> ignite = null;
            bool changed = false;
            for (int f = 0; f < T.Length; f++)
            {
                if (T[f] != Terrain.Fire) continue;
                int fr = L.RowOf(f), fc = L.ColOf(f);
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int r = fr + dr, c = fc + dc;
                        if (!L.InMap(r, c)) continue;
                        int p = L.Pos(r, c);
                        Terrain t = T[p];
                        if (t == Terrain.Void) continue;

                        bool burn = Rules.IsWood(t);
                        // 마른 나무 재질 물건(바구니, 빈 양동이)은 파괴되고 그 칸이 들불이 된다.
                        for (int i = E.Count - 1; i >= 0; i--)
                        {
                            var e = E[i];
                            if (e.Pos != p || e.Wet) continue;
                            if (e.Kind == Kind.Basket || (e.Kind == Kind.Bucket && !e.Full))
                            {
                                if (Log != null) Log.Add(Name(e) + " " + At(p) + " 확산에 탐");
                                E.RemoveAt(i);
                                Events |= Ev.ItemBurned;
                                burn = true;
                                changed = true;
                            }
                        }
                        if (burn && t != Terrain.Fire)
                        {
                            if (ignite == null) ignite = new List<int>();
                            ignite.Add(p);
                        }
                    }
                }
            }
            if (ignite != null)
            {
                for (int k = 0; k < ignite.Count; k++)
                {
                    int p = ignite[k];
                    if (T[p] == Terrain.Fire) continue; // 두 들불이 같은 칸을 태움
                    if (T[p] == Terrain.Grass) Events |= Ev.GrassBurned;
                    else if (T[p] == Terrain.Fence || T[p] == Terrain.Tree) Events |= Ev.ObstacleBurned;
                    if (Log != null) Log.Add("들불이 " + At(p) + " 로 번짐");
                    T[p] = Terrain.Fire;
                    Events |= Ev.FireSpread;
                    changed = true;
                }
            }
            return changed;
        }

        // ------------------------------------------------------------------
        // 걷기 공통 규칙 (README 6절)
        // ------------------------------------------------------------------

        private int StepToward(int from, int target, Dir last)
        {
            if (!Rules.IsWalkable(T[target])) return -1; // 목표가 물이나 들불 위에 있으면 경로가 없다.
            PathReset();
            PathSeed(target);
            return PathFirstStep(from, last);
        }

        private void PathReset()
        {
            int n = L.CellCount;
            for (int p = 0; p < n; p++) dist[p] = Inf;
            queueTail = 0;
        }

        private void PathSeed(int target)
        {
            if (dist[target] == 0) return;
            dist[target] = 0;
            queue[queueTail++] = target;
        }

        /// <summary>
        /// 심어 둔 목표 칸들 중 가장 가까운 곳으로 가는 첫 걸음의 방향. 경로가 없으면 -1.
        /// 동점은 마지막으로 걸은 방향, 그다음 상 > 우 > 하 > 좌.
        /// </summary>
        private int PathFirstStep(int from, Dir last)
        {
            if (queueTail == 0) return -1;
            // 목표에서 거꾸로 퍼져 나간다. 걸을 수 있는 칸만 지난다.
            int head = 0;
            while (head < queueTail)
            {
                int p = queue[head++];
                for (int d = 0; d < 4; d++)
                {
                    int q = L.Nb[p * 4 + d];
                    if (q < 0 || dist[q] != Inf || !Rules.IsWalkable(T[q])) continue;
                    dist[q] = dist[p] + 1;
                    queue[queueTail++] = q;
                }
            }
            // 서 있는 칸은 물이나 들불이어도 걸어 나올 수 있으므로, 이웃 칸의 거리만 본다.
            int best = Inf;
            for (int d = 0; d < 4; d++)
            {
                int q = L.Nb[from * 4 + d];
                if (q >= 0 && dist[q] < best) best = dist[q];
            }
            if (best == Inf) return -1;
            if (last != Dir.None)
            {
                int q = L.Nb[from * 4 + (int)last];
                if (q >= 0 && dist[q] == best) return (int)last;
            }
            for (int d = 0; d < 4; d++)
            {
                int q = L.Nb[from * 4 + d];
                if (q >= 0 && dist[q] == best) return d;
            }
            return -1;
        }

        // ------------------------------------------------------------------
        // 도우미
        // ------------------------------------------------------------------

        private bool Adjacent(int a, int b)
        {
            return Math.Abs(L.RowOf(a) - L.RowOf(b)) + Math.Abs(L.ColOf(a) - L.ColOf(b)) == 1;
        }

        private Entity FirstAt(int pos, Kind kind)
        {
            for (int i = 0; i < E.Count; i++) if (E[i].Pos == pos && E[i].Kind == kind) return E[i];
            return null;
        }

        /// <summary>오브젝트가 새 칸에 들어갔을 때의 기록. 지형 효과는 정리 절차에서 적용한다.</summary>
        private void Entered(Entity e)
        {
            Terrain t0 = L.Terrain[e.Pos];
            if (t0 == Terrain.Fence || t0 == Terrain.Tree)
            {
                Events |= Ev.EnterBurnt;
                if (e.Kind == Kind.Sheep) Events |= Ev.SheepEnterBurnt;
            }
        }

        /// <summary>움직이기 전의 위치를 적어 둔다. 양 합치기 판정과 염소의 대상 고르기에 쓴다.</summary>
        private void Snapshot()
        {
            int n = E.Count;
            if (posBuffer == null || posBuffer.Length < n) posBuffer = new int[Math.Max(64, n)];
            before = posBuffer;
            int sheep = 0;
            for (int i = 0; i < n; i++)
            {
                before[i] = E[i].Pos;
                if (E[i].Kind == Kind.Sheep) sheep++;
            }
            beforeHasTwoSheep = sheep >= 2;
        }

        /// <summary>Snapshot 뒤에 서로 다른 칸에 있던 양이 한 칸에 모였는가.</summary>
        private void CheckSheepMerge()
        {
            if (!beforeHasTwoSheep) return;
            for (int i = 0; i < E.Count; i++)
            {
                if (E[i].Kind != Kind.Sheep) continue;
                for (int j = i + 1; j < E.Count; j++)
                {
                    if (E[j].Kind != Kind.Sheep) continue;
                    if (E[i].Pos == E[j].Pos && before[i] != before[j]) { Events |= Ev.SheepMerge; return; }
                }
            }
        }

        private string At(int pos)
        {
            return "(" + L.RowOf(pos) + "," + L.ColOf(pos) + ")";
        }

        private static string Name(Entity e)
        {
            switch (e.Kind)
            {
                case Kind.Sheep: return "양";
                case Kind.Wolf: return "늑대";
                case Kind.Goat: return "염소";
                case Kind.Horse: return "말";
                case Kind.Cow: return "소";
                case Kind.Basket: return "바구니";
                default: return "양동이";
            }
        }
    }
}
