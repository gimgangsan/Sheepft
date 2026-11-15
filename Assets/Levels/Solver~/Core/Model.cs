using System;
using System.Collections.Generic;
using System.Text;

namespace Sheepft.Core
{
    public enum Terrain : byte { Void, Ground, Stone, Fence, Tree, Water, Fire, Grass, Ash }

    public enum Kind : byte { Sheep, Wolf, Goat, Horse, Cow, Basket, Bucket }

    /// <summary>동점 처리 순서(상 > 우 > 하 > 좌)와 같은 순서로 번호를 매긴다.</summary>
    public enum Dir : sbyte { None = -1, Up = 0, Right = 1, Down = 2, Left = 3 }

    public enum Outcome { Ongoing, Win, Lose }

    public static class Rules
    {
        public static readonly int[] DR = { -1, 0, 1, 0 };
        public static readonly int[] DC = { 0, 1, 0, -1 };

        /// <summary>돌, 울타리, 나무. shift, 밀치기, 걷기, 시야를 모두 막는다.</summary>
        public static bool IsObstacle(Terrain t)
        {
            return t == Terrain.Stone || t == Terrain.Fence || t == Terrain.Tree;
        }

        /// <summary>확산으로 들불이 되는 지형.</summary>
        public static bool IsWood(Terrain t)
        {
            return t == Terrain.Fence || t == Terrain.Tree || t == Terrain.Grass;
        }

        /// <summary>스스로 걷는 동물이 들어갈 수 있는 지형. 물과 들불은 피한다.</summary>
        public static bool IsWalkable(Terrain t)
        {
            return t == Terrain.Ground || t == Terrain.Grass || t == Terrain.Ash;
        }

        public static bool IsAnimal(Kind k)
        {
            return k <= Kind.Cow;
        }
    }

    public sealed class Entity
    {
        public int Id;
        public Kind Kind;
        public int Pos;

        public bool Wet;
        public bool Burned;

        /// <summary>늑대 무리의 마리 수. 잡는 범위는 Count - 1.</summary>
        public int Count = 1;
        /// <summary>늑대의 기절, 식사 남은 기간. 0이면 해당 없음.</summary>
        public int Stun;
        public int Meal;

        /// <summary>늑대, 말, 소가 마지막으로 스스로 걸은 방향.</summary>
        public Dir LastDir = Dir.None;

        /// <summary>양동이: 물 찬 양동이.</summary>
        public bool Full;
        /// <summary>말, 소: 배부름.</summary>
        public bool Sated;
        /// <summary>말, 소: 목표 바구니의 Id. 없으면 -1.</summary>
        public int Target = -1;
        /// <summary>바구니: 말이 운반하거나 내려놓은 적이 있음. 바구니 유인 판정에만 쓴다.</summary>
        public bool Carried;
        /// <summary>말: 등에 태운 오브젝트의 Id. 없으면 -1. 한 번에 하나만 태운다.</summary>
        public int Rider = -1;

        /// <summary>이번 턴 지형 적용 기록. 턴 시작 때 초기화한다.</summary>
        public bool WaterDone;
        public bool FireDone;

        public Entity Clone()
        {
            return (Entity)MemberwiseClone();
        }
    }

    /// <summary>한 행 또는 한 열의 실제 칸을 순서대로 이은 것. 빈칸은 들어가지 않는다.</summary>
    public sealed class ShiftLine
    {
        public bool IsRow;
        public int Index;
        /// <summary>행은 왼쪽에서 오른쪽, 열은 위에서 아래 순서.</summary>
        public int[] Cells;
        /// <summary>정방향(행은 오른쪽, 열은 아래)으로 밀 수 있음.</summary>
        public bool Forward;
        /// <summary>역방향(행은 왼쪽, 열은 위)으로 밀 수 있음.</summary>
        public bool Backward;
        /// <summary>칸 번호 → 줄 안에서의 순서. 줄에 없으면 -1.</summary>
        public int[] IndexOfCell;
    }

    public readonly struct Move : IEquatable<Move>
    {
        public readonly int Line;
        /// <summary>true면 행은 오른쪽, 열은 아래.</summary>
        public readonly bool Forward;

        public Move(int line, bool forward)
        {
            Line = line;
            Forward = forward;
        }

        public bool Equals(Move other) { return Line == other.Line && Forward == other.Forward; }
        public override bool Equals(object obj) { return obj is Move m && Equals(m); }
        public override int GetHashCode() { return Line * 2 + (Forward ? 1 : 0); }
    }

    public sealed class Level
    {
        public string Title = "";
        public string Description = "";
        /// <summary>헤더 다음의 "# 풀이:" 줄. 없으면 null.</summary>
        public string SolutionText;
        /// <summary>외곽 빈칸(-)으로 적힌 칸. 규칙은 중간 빈칸(x)과 같고 화면에서만 다르다.</summary>
        public HashSet<int> OuterVoid = new HashSet<int>();
        public int Rows;
        public int Cols;
        /// <summary>처음 지형. 게임 중 지형은 GameState.T에 있다.</summary>
        public Terrain[] Terrain;
        public List<Entity> Entities = new List<Entity>();
        public List<ShiftLine> Lines = new List<ShiftLine>();
        public Move[] Moves;
        public int TurnLimit;
        public int TargetSheep;

        /// <summary>solver용 표시. 제한 턴 안의 모든 해에 나와야 하는 이벤트.</summary>
        public Ev Intent = Ev.None;
        /// <summary>solver용 표시. 최적 길이의 모든 해에 나와야 하는 이벤트. 더 긴 해에는 없어도 된다.</summary>
        public Ev OptimalIntent = Ev.None;
        /// <summary>별 3개, 2개 기준 턴. 없으면 -1.</summary>
        public int Star3 = -1;
        public int Star2 = -1;

        /// <summary>칸*4 + 방향 → 이웃 칸. 맵 밖이나 빈칸이면 -1.</summary>
        public int[] Nb;

        /// <summary>처음 배치에 그 종류가 있는가. 오브젝트는 새로 생기지 않으므로 없는 종류의 처리는 건너뛴다.</summary>
        public bool[] HasKind = new bool[7];
        /// <summary>처음 지형에 들불이 있는가. 없으면 지형은 끝까지 바뀌지 않는다.</summary>
        public bool HasFire;

        /// <summary>shift 가능한 줄과 방향만 바꾼 사본. 지형과 오브젝트는 함께 쓴다.</summary>
        public Level WithShift(string shiftSpec)
        {
            var copy = (Level)MemberwiseClone();
            copy.Lines = new List<ShiftLine>();
            LevelParser.BuildLines(copy, shiftSpec, "");
            return copy;
        }

        /// <summary>실제 칸이 둘 이상인 모든 행과 열을 양방향으로 연 shift 표기.</summary>
        public string AllShiftSpec()
        {
            var sb = new StringBuilder();
            for (int r = 0; r < Rows; r++)
            {
                int n = 0;
                for (int c = 0; c < Cols; c++) if (Terrain[Pos(r, c)] != Core.Terrain.Void) n++;
                if (n < 2) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append("R").Append(r).Append(" : RL");
            }
            for (int c = 0; c < Cols; c++)
            {
                int n = 0;
                for (int r = 0; r < Rows; r++) if (Terrain[Pos(r, c)] != Core.Terrain.Void) n++;
                if (n < 2) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append("C").Append(c).Append(" : UD");
            }
            return sb.ToString();
        }

        /// <summary>지금의 shift 가능한 줄과 방향을 헤더 표기로 적는다.</summary>
        public string ShiftSpec()
        {
            var sb = new StringBuilder();
            foreach (var line in Lines)
            {
                if (!line.Forward && !line.Backward) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(line.IsRow ? "R" : "C").Append(line.Index).Append(":");
                if (line.IsRow) sb.Append(line.Forward ? "R" : "").Append(line.Backward ? "L" : "");
                else sb.Append(line.Backward ? "U" : "").Append(line.Forward ? "D" : "");
            }
            return sb.ToString();
        }

        public int CellCount { get { return Rows * Cols; } }
        public int Pos(int r, int c) { return r * Cols + c; }
        public int RowOf(int pos) { return pos / Cols; }
        public int ColOf(int pos) { return pos % Cols; }

        public bool InMap(int r, int c)
        {
            return r >= 0 && r < Rows && c >= 0 && c < Cols;
        }

        public GameState NewState()
        {
            var s = new GameState();
            // 들불이 없는 레벨은 지형이 바뀌지 않으므로 모든 상태가 처음 지형을 함께 쓴다.
            s.TerrainShared = !HasFire;
            s.T = HasFire ? (Terrain[])Terrain.Clone() : Terrain;
            s.E = new List<Entity>(Entities.Count);
            foreach (var e in Entities) s.E.Add(e.Clone());
            // 처음부터 말과 같은 칸에 있는 것은 레벨을 열 때 바로 태운다(게임의 Board.SeatInitialRiders와 같다).
            Riders.Update(s);
            return s;
        }

        public int SheepCount
        {
            get
            {
                int n = 0;
                foreach (var e in Entities) if (e.Kind == Kind.Sheep) n++;
                return n;
            }
        }

        /// <summary>README 10절 표기. 예: R2:R, C1:U</summary>
        public string MoveText(Move m)
        {
            var line = Lines[m.Line];
            if (line.IsRow) return "R" + line.Index + (m.Forward ? ":R" : ":L");
            return "C" + line.Index + (m.Forward ? ":D" : ":U");
        }

        public string MovesText(IEnumerable<Move> moves)
        {
            var sb = new StringBuilder();
            foreach (var m in moves)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(MoveText(m));
            }
            return sb.ToString();
        }

        /// <summary>R2→, R2>, C1↑, C1^, C1↓, C1v, R2←, R2&lt; 를 받는다. 이 레벨에서 허용되지 않는 수면 false.</summary>
        public bool TryParseMove(string text, out Move move)
        {
            move = default(Move);
            if (string.IsNullOrEmpty(text) || text.Length < 3) return false;
            // README 10절 표기 R2:R, C1:U 를 옛 표기 R2>, C1^ 로 바꿔 읽는다.
            int colon = text.IndexOf(':');
            if (colon > 0 && colon == text.Length - 2)
            {
                char d = char.ToUpperInvariant(text[colon + 1]);
                char old = d == 'R' ? '>' : d == 'L' ? '<' : d == 'U' ? '^' : d == 'D' ? 'v' : '?';
                text = text.Substring(0, colon) + old;
            }
            char k = char.ToUpperInvariant(text[0]);
            if (k != 'R' && k != 'C') return false;
            int i = 1, idx = 0;
            while (i < text.Length && char.IsDigit(text[i])) { idx = idx * 10 + (text[i] - '0'); i++; }
            if (i == 1 || i != text.Length - 1) return false;
            char a = text[i];
            bool isRow = k == 'R';
            bool forward;
            if (isRow)
            {
                if (a == '→' || a == '>') forward = true;
                else if (a == '←' || a == '<') forward = false;
                else return false;
            }
            else
            {
                if (a == '↓' || a == 'v' || a == 'V') forward = true;
                else if (a == '↑' || a == '^') forward = false;
                else return false;
            }
            for (int li = 0; li < Lines.Count; li++)
            {
                var line = Lines[li];
                if (line.IsRow != isRow || line.Index != idx) continue;
                if (forward ? !line.Forward : !line.Backward) return false;
                move = new Move(li, forward);
                return true;
            }
            return false;
        }
    }

    /// <summary>상태를 가리키는 128비트 값. 서로 다른 상태가 같은 값이 될 확률은 무시할 수 있다.</summary>
    public readonly struct StateKey : IEquatable<StateKey>
    {
        public readonly ulong A;
        public readonly ulong B;

        public StateKey(ulong a, ulong b) { A = a; B = b; }

        public bool Equals(StateKey other) { return A == other.A && B == other.B; }
        public override bool Equals(object obj) { return obj is StateKey k && Equals(k); }
        public override int GetHashCode() { return (int)(A ^ (A >> 32)); }
    }

    /// <summary>탐색에서 상태를 작게 보관하는 형태. 오브젝트 하나가 ulong 하나다.</summary>
    public sealed class PackedState
    {
        public ulong[] E;
        /// <summary>지형이 바뀌지 않는 레벨이면 null.</summary>
        public Terrain[] T;
    }

    public sealed class GameState
    {
        public Terrain[] T;
        public List<Entity> E;
        /// <summary>T가 레벨의 처음 지형 배열 그대로다(들불이 없는 레벨). 복사하지 않는다.</summary>
        public bool TerrainShared;

        // Load가 다시 쓰는 오브젝트 보관함
        private readonly List<Entity> pool = new List<Entity>();

        [ThreadStatic] private static ulong[] sortBuffer;

        public GameState Clone()
        {
            var s = new GameState();
            s.TerrainShared = TerrainShared;
            s.T = TerrainShared ? T : (Terrain[])T.Clone();
            s.E = new List<Entity>(E.Count);
            for (int i = 0; i < E.Count; i++) s.E.Add(E[i].Clone());
            return s;
        }

        public Entity Find(int id)
        {
            for (int i = 0; i < E.Count; i++) if (E[i].Id == id) return E[i];
            return null;
        }

        // 오브젝트 하나를 ulong 하나로. 이번 턴 지형 적용 기록은 턴 시작 때 지워지므로 넣지 않는다.
        // riderField는 10비트. Pack은 말의 Rider + 1, Key는 등에 태운 말 1 / 등에 탄 것 2 / 그 밖 0.
        private static ulong Encode(Entity e, int idField, int targetField, int riderField)
        {
            ulong v = (ulong)e.Kind;
            v = (v << 10) | (uint)e.Pos;
            v = (v << 1) | (e.Wet ? 1u : 0u);
            v = (v << 1) | (e.Burned ? 1u : 0u);
            v = (v << 1) | (e.Full ? 1u : 0u);
            v = (v << 1) | (e.Sated ? 1u : 0u);
            v = (v << 1) | (e.Carried ? 1u : 0u);
            v = (v << 6) | (uint)(e.Count & 63);
            v = (v << 2) | (uint)(e.Stun & 3);
            v = (v << 2) | (uint)(e.Meal & 3);
            v = (v << 3) | (uint)((int)e.LastDir + 1);
            v = (v << 11) | (uint)targetField;
            v = (v << 9) | (uint)idField;
            v = (v << 10) | (uint)riderField;
            return v;
        }

        /// <summary>
        /// 턴과 턴 사이의 상태를 나타내는 키. 같은 칸의 같은 상태 오브젝트는 서로 바꿔도 같은 키가 된다.
        /// (Id는 넣지 않고, 목표 바구니는 그 바구니의 위치로 적는다. 말의 등에 탄 것은 표시만 한다.
        /// 한 칸에 등에 태운 말이 둘 이상이면 누가 누구를 태웠는지는 구분하지 않는다.)
        /// </summary>
        public StateKey Key()
        {
            int n = E.Count;
            ulong[] buf = sortBuffer;
            if (buf == null || buf.Length < n) buf = sortBuffer = new ulong[Math.Max(64, n)];
            for (int i = 0; i < n; i++)
            {
                var e = E[i];
                int target = 0;
                if (e.Target >= 0)
                {
                    var t = Find(e.Target);
                    if (t != null) target = t.Pos + 1;
                }
                int rider = e.Rider >= 0 ? 1 : 0;
                for (int k = 0; k < n && rider == 0; k++)
                    if (E[k].Rider == e.Id) rider = 2;
                ulong v = Encode(e, 0, target, rider);
                // 삽입 정렬 (오브젝트 수가 적다)
                int j = i - 1;
                while (j >= 0 && buf[j] > v) { buf[j + 1] = buf[j]; j--; }
                buf[j + 1] = v;
            }

            ulong a = 0x243F6A8885A308D3UL, b = 0x13198A2E03707344UL;
            if (!TerrainShared)
            {
                for (int i = 0; i < T.Length; i++) { a = Mix1(a, (ulong)T[i] + 1); b = Mix2(b, (ulong)T[i] + 1); }
            }
            for (int i = 0; i < n; i++) { a = Mix1(a, buf[i]); b = Mix2(b, buf[i]); }
            return new StateKey(Final(a ^ (ulong)n), Final(b + (ulong)n));
        }

        private static ulong Mix1(ulong h, ulong v)
        {
            h ^= v;
            h *= 0x9E3779B97F4A7C15UL;
            return (h << 31) | (h >> 33);
        }

        private static ulong Mix2(ulong h, ulong v)
        {
            h += v * 0xC2B2AE3D27D4EB4FUL;
            h ^= h >> 29;
            return h * 0x165667B19E3779F9UL;
        }

        private static ulong Final(ulong h)
        {
            h ^= h >> 33;
            h *= 0xFF51AFD7ED558CCDUL;
            h ^= h >> 33;
            h *= 0xC4CEB9FE1A85EC53UL;
            return h ^ (h >> 33);
        }

        public PackedState Pack()
        {
            var p = new PackedState();
            p.E = new ulong[E.Count];
            for (int i = 0; i < E.Count; i++) p.E[i] = Encode(E[i], E[i].Id, E[i].Target + 1, E[i].Rider + 1);
            p.T = TerrainShared ? null : (Terrain[])T.Clone();
            return p;
        }

        /// <summary>보관해 둔 상태를 이 객체에 풀어 놓는다. 오브젝트 객체를 다시 써서 새로 만들지 않는다.</summary>
        public void Load(PackedState p)
        {
            if (!TerrainShared) Array.Copy(p.T, T, T.Length);
            E.Clear();
            for (int i = 0; i < p.E.Length; i++)
            {
                if (pool.Count <= i) pool.Add(new Entity());
                var e = pool[i];
                ulong v = p.E[i];
                e.Rider = (int)(v & 1023) - 1; v >>= 10;
                e.Id = (int)(v & 511); v >>= 9;
                e.Target = (int)(v & 2047) - 1; v >>= 11;
                e.LastDir = (Dir)((int)(v & 7) - 1); v >>= 3;
                e.Meal = (int)(v & 3); v >>= 2;
                e.Stun = (int)(v & 3); v >>= 2;
                e.Count = (int)(v & 63); v >>= 6;
                e.Carried = (v & 1) != 0; v >>= 1;
                e.Sated = (v & 1) != 0; v >>= 1;
                e.Full = (v & 1) != 0; v >>= 1;
                e.Burned = (v & 1) != 0; v >>= 1;
                e.Wet = (v & 1) != 0; v >>= 1;
                e.Pos = (int)(v & 1023); v >>= 10;
                e.Kind = (Kind)(int)(v & 7);
                e.WaterDone = false;
                e.FireDone = false;
                E.Add(e);
            }
        }
    }

    /// <summary>
    /// 말의 등 (README 6절). 말은 한 번에 하나만 태우고, 먼저 탄 것이 칸을 떠나거나 사라질 때까지 유지한다.
    /// 게임 코드의 Horse.UpdateRiders와 같은 규칙이다.
    /// </summary>
    public static class Riders
    {
        /// <summary>말 h가 등에 태운 것. 없거나 이미 칸을 떠났으면 null.</summary>
        public static Entity Of(GameState s, Entity h)
        {
            if (h.Rider < 0) return null;
            var r = s.Find(h.Rider);
            return r != null && r.Pos == h.Pos ? r : null;
        }

        /// <summary>
        /// 내린 것을 지우고, 등이 빈 말(배부른 말 제외)에 태운다. 말 Id 순으로,
        /// 같은 칸에서 아직 아무 말에도 타지 않은 오브젝트(말 제외) 가운데 Id가 가장 작은 것. 바뀌었으면 true.
        /// </summary>
        public static bool Update(GameState s)
        {
            var E = s.E;
            bool ch = false;
            for (int i = 0; i < E.Count; i++)
            {
                var h = E[i];
                if (h.Kind != Kind.Horse || h.Rider < 0) continue;
                if (h.Sated || Of(s, h) == null) { h.Rider = -1; ch = true; }
            }

            for (var h = NextHorse(E, -1); h != null; h = NextHorse(E, h.Id))
            {
                if (h.Sated || h.Rider >= 0) continue;
                Entity best = null;
                for (int i = 0; i < E.Count; i++)
                {
                    var x = E[i];
                    if (x.Pos != h.Pos || x.Kind == Kind.Horse || IsRidden(E, x.Id)) continue;
                    if (best == null || x.Id < best.Id) best = x;
                }
                if (best == null) continue;
                h.Rider = best.Id;
                ch = true;
            }
            return ch;
        }

        private static Entity NextHorse(List<Entity> E, int afterId)
        {
            Entity best = null;
            for (int i = 0; i < E.Count; i++)
            {
                var e = E[i];
                if (e.Kind != Kind.Horse || e.Id <= afterId) continue;
                if (best == null || e.Id < best.Id) best = e;
            }
            return best;
        }

        private static bool IsRidden(List<Entity> E, int id)
        {
            for (int i = 0; i < E.Count; i++)
                if (E[i].Kind == Kind.Horse && E[i].Rider == id) return true;
            return false;
        }
    }
}
