using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Sheepft.Core
{
    public sealed class LevelFormatException : Exception
    {
        public LevelFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// README 10절의 ASCII 레벨 표기를 읽는다. 한 파일에 레벨 여러 개를 이어 적을 수 있다.
    ///
    /// 헤더: "# 제목  shift: R1:RL, C2:UD  제한 4턴  목표 양 2"
    /// 그 아래에 맵 줄이 온다. 칸은 공백으로 구분한다.
    ///
    /// solver용 추가 줄(게임 규칙과는 무관하며 생략할 수 있다):
    ///   @desc 플레이어용 설명
    ///   @intent Wrap SheepMerge     제한 턴 안의 모든 해에 나와야 하는 이벤트 (Ev 이름)
    ///   @optimal Wait               최적 길이의 모든 해에 나와야 하는 이벤트
    ///   @stars 5 7                  별 3개 기준 턴, 별 2개 기준 턴
    ///   // 로 시작하는 줄은 주석
    /// </summary>
    public static class LevelParser
    {
        private static readonly Regex Header = new Regex(
            @"^#\s*(?<title>.*?)\s*shift\s*:\s*(?<shift>.*?)\s*제한\s*(?<limit>\d+)\s*턴(?:\s*목표\s*양\s*(?<target>\d+))?\s*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex ShiftItem = new Regex(
            @"^([RC])\s*(\d+)\s*:\s*([RLUD]+)$", RegexOptions.CultureInvariant);

        private static readonly Regex SolutionLine = new Regex(
            @"^#\s*풀이\s*:(.*)$", RegexOptions.CultureInvariant);

        private sealed class Draft
        {
            public string Title;
            public string Shift;
            public int Limit;
            public int Target = -1;
            public List<string[]> Rows = new List<string[]>();
            public List<string> Meta = new List<string>();
            public int HeaderLine;
            public string Solution;
        }

        public static Level ParseOne(string text)
        {
            var all = ParseAll(text);
            if (all.Count == 0) throw new LevelFormatException("레벨 헤더를 찾지 못했다. 헤더는 '# 제목  shift: ...  제한 N턴' 형식이다.");
            return all[0];
        }

        public static List<Level> ParseAll(string text)
        {
            var result = new List<Level>();
            Draft cur = null;
            var lines = text.Replace("\r", "").Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;

                // 돌(#)로 시작하는 맵 줄과 헤더는 "shift"가 있는지로 구분한다.
                if (line[0] == '#' && line.Contains("shift"))
                {
                    var m = Header.Match(line);
                    if (!m.Success)
                        throw new LevelFormatException((n + 1) + "번째 줄: 헤더를 읽지 못했다. 예: # 제목  shift: R1:RL, C2:UD  제한 4턴  목표 양 2");
                    if (cur != null) result.Add(Build(cur));
                    cur = new Draft();
                    cur.Title = m.Groups["title"].Value.Trim();
                    cur.Shift = m.Groups["shift"].Value.Trim();
                    cur.Limit = int.Parse(m.Groups["limit"].Value);
                    if (m.Groups["target"].Success) cur.Target = int.Parse(m.Groups["target"].Value);
                    cur.HeaderLine = n + 1;
                    continue;
                }

                if (cur == null) throw new LevelFormatException((n + 1) + "번째 줄: 레벨 헤더보다 먼저 나온 줄이다.");

                if (line[0] == '@') { cur.Meta.Add(line); continue; }

                // 풀이 줄(선택): "# 풀이: R1:R, C2:U". 돌로 시작하는 맵 줄과 구분한다.
                var sol = SolutionLine.Match(line);
                if (sol.Success) { cur.Solution = sol.Groups[1].Value.Trim(); continue; }

                cur.Rows.Add(line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            }
            if (cur != null) result.Add(Build(cur));
            return result;
        }

        private static Level Build(Draft d)
        {
            string where = "레벨 '" + d.Title + "' (" + d.HeaderLine + "번째 줄): ";
            if (d.Rows.Count == 0) throw new LevelFormatException(where + "맵 줄이 없다.");

            var lv = new Level();
            lv.Title = d.Title;
            lv.TurnLimit = d.Limit;
            lv.SolutionText = d.Solution;
            lv.Rows = d.Rows.Count;
            foreach (var row in d.Rows) if (row.Length > lv.Cols) lv.Cols = row.Length;
            lv.Terrain = new Terrain[lv.Rows * lv.Cols]; // 짧은 줄의 오른쪽은 빈칸(Void)으로 남는다.

            int nextId = 0;
            for (int r = 0; r < lv.Rows; r++)
            {
                var row = d.Rows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    ParseCell(lv, row[c], lv.Pos(r, c), ref nextId, where + "(" + r + "," + c + ") '" + row[c] + "': ");
                }
            }

            if (lv.CellCount > 1024) throw new LevelFormatException(where + "맵이 너무 크다 (칸 1024개까지).");
            if (lv.Entities.Count > 500) throw new LevelFormatException(where + "오브젝트가 너무 많다 (500개까지).");
            foreach (var e in lv.Entities)
            {
                lv.HasKind[(int)e.Kind] = true;
                if (e.Count > 63) throw new LevelFormatException(where + "늑대 무리는 63마리까지다.");
            }
            foreach (var t in lv.Terrain) if (t == Terrain.Fire) lv.HasFire = true;

            // 이웃 표. 맵 밖과 빈칸은 -1.
            lv.Nb = new int[lv.CellCount * 4];
            for (int p = 0; p < lv.CellCount; p++)
            {
                for (int dir = 0; dir < 4; dir++)
                {
                    int r = lv.RowOf(p) + Rules.DR[dir], c = lv.ColOf(p) + Rules.DC[dir];
                    int q = -1;
                    if (lv.InMap(r, c) && lv.Terrain[lv.Pos(r, c)] != Terrain.Void) q = lv.Pos(r, c);
                    lv.Nb[p * 4 + dir] = q;
                }
            }

            BuildLines(lv, d.Shift, where);

            int sheep = lv.SheepCount;
            lv.TargetSheep = d.Target < 0 ? sheep : d.Target;
            if (lv.TargetSheep > sheep) throw new LevelFormatException(where + "목표 양 수(" + lv.TargetSheep + ")가 레벨의 양 수(" + sheep + ")보다 많다.");

            foreach (var meta in d.Meta) ParseMeta(lv, meta, where);
            return lv;
        }

        private static void ParseCell(Level lv, string token, int pos, ref int nextId, string where)
        {
            Terrain t;
            switch (token[0])
            {
                case '.': t = Terrain.Ground; break;
                case '#': t = Terrain.Stone; break;
                case '~': t = Terrain.Water; break;
                case '^': t = Terrain.Fire; break;
                case 'g': t = Terrain.Grass; break;
                case 'f': t = Terrain.Fence; break;
                case 't': t = Terrain.Tree; break;
                case '_': t = Terrain.Ash; break;
                case 'x': t = Terrain.Void; break;
                case '-': t = Terrain.Void; lv.OuterVoid.Add(pos); break;
                default: throw new LevelFormatException(where + "모르는 지형 기호 '" + token[0] + "'.");
            }
            lv.Terrain[pos] = t;
            if (token.Length == 1) return;
            if (t == Terrain.Void) throw new LevelFormatException(where + "빈칸에는 오브젝트를 둘 수 없다.");
            if (Rules.IsObstacle(t)) throw new LevelFormatException(where + "장애물 칸에는 오브젝트를 둘 수 없다.");

            int i = 1;
            while (i < token.Length)
            {
                char ch = token[i++];
                Kind kind;
                bool full = false;
                switch (ch)
                {
                    case 'S': kind = Kind.Sheep; break;
                    case 'W': kind = Kind.Wolf; break;
                    case 'G': kind = Kind.Goat; break;
                    case 'H': kind = Kind.Horse; break;
                    case 'M': kind = Kind.Cow; break;
                    case 'A': kind = Kind.Basket; break;
                    case 'B': kind = Kind.Bucket; break;
                    case 'U': kind = Kind.Bucket; full = true; break;
                    default: throw new LevelFormatException(where + "모르는 오브젝트 기호 '" + ch + "'.");
                }
                int count = 0;
                bool hasCount = false;
                while (i < token.Length && char.IsDigit(token[i])) { count = count * 10 + (token[i] - '0'); i++; hasCount = true; }
                if (!hasCount) count = 1;
                if (count < 1) throw new LevelFormatException(where + "개수는 1 이상이어야 한다.");

                bool wet = false, burned = false;
                if (i < token.Length && token[i] == '(')
                {
                    int close = token.IndexOf(')', i);
                    if (close < 0) throw new LevelFormatException(where + "괄호가 닫히지 않았다.");
                    for (int k = i + 1; k < close; k++)
                    {
                        if (token[k] == 'w') wet = true;
                        else if (token[k] == 'b') burned = true;
                        else throw new LevelFormatException(where + "모르는 상태 '" + token[k] + "'. (w) 젖음, (b) 화상만 쓴다.");
                    }
                    i = close + 1;
                }
                if (burned && !Rules.IsAnimal(kind)) throw new LevelFormatException(where + "화상은 동물에게만 붙는다.");
                if (wet && full) throw new LevelFormatException(where + "물 찬 양동이에는 젖음 상태가 없다.");

                int entities = kind == Kind.Wolf ? 1 : count;
                for (int k = 0; k < entities; k++)
                {
                    var e = new Entity();
                    e.Id = nextId++;
                    e.Kind = kind;
                    e.Pos = pos;
                    e.Wet = wet;
                    e.Burned = burned;
                    e.Full = full;
                    if (kind == Kind.Wolf) e.Count = count;
                    lv.Entities.Add(e);
                }
            }
        }

        public static void BuildLines(Level lv, string shift, string where)
        {
            var moves = new List<Move>();
            foreach (var raw in shift.Split(','))
            {
                string item = raw.Trim();
                if (item.Length == 0) continue;
                var m = ShiftItem.Match(item);
                if (!m.Success) throw new LevelFormatException(where + "shift 표기 '" + item + "'를 읽지 못했다. 예: R1:RL, C2:U");
                bool isRow = m.Groups[1].Value == "R";
                int index = int.Parse(m.Groups[2].Value);
                string dirs = m.Groups[3].Value;

                if (index >= (isRow ? lv.Rows : lv.Cols)) throw new LevelFormatException(where + "'" + item + "': 맵에 없는 줄이다.");
                foreach (var ln in lv.Lines)
                    if (ln.IsRow == isRow && ln.Index == index) throw new LevelFormatException(where + "'" + item + "': 같은 줄을 두 번 적었다.");

                var line = new ShiftLine();
                line.IsRow = isRow;
                line.Index = index;
                foreach (char ch in dirs)
                {
                    if (isRow && ch == 'R') line.Forward = true;
                    else if (isRow && ch == 'L') line.Backward = true;
                    else if (!isRow && ch == 'D') line.Forward = true;
                    else if (!isRow && ch == 'U') line.Backward = true;
                    else throw new LevelFormatException(where + "'" + item + "': 행은 R, L, RL, 열은 U, D, UD만 쓴다.");
                }

                var cells = new List<int>();
                int len = isRow ? lv.Cols : lv.Rows;
                for (int k = 0; k < len; k++)
                {
                    int p = isRow ? lv.Pos(index, k) : lv.Pos(k, index);
                    if (lv.Terrain[p] != Terrain.Void) cells.Add(p);
                }
                if (cells.Count == 0) throw new LevelFormatException(where + "'" + item + "': 실제 칸이 하나도 없는 줄이다.");
                line.Cells = cells.ToArray();
                line.IndexOfCell = new int[lv.CellCount];
                for (int k = 0; k < line.IndexOfCell.Length; k++) line.IndexOfCell[k] = -1;
                for (int k = 0; k < line.Cells.Length; k++) line.IndexOfCell[line.Cells[k]] = k;

                int li = lv.Lines.Count;
                lv.Lines.Add(line);
                if (line.Forward) moves.Add(new Move(li, true));
                if (line.Backward) moves.Add(new Move(li, false));
            }
            lv.Moves = moves.ToArray();
        }

        private static void ParseMeta(Level lv, string line, string where)
        {
            int sp = line.IndexOfAny(new[] { ' ', '\t' });
            string name = sp < 0 ? line : line.Substring(0, sp);
            string arg = sp < 0 ? "" : line.Substring(sp + 1).Trim();
            switch (name)
            {
                case "@desc":
                    lv.Description = lv.Description.Length == 0 ? arg : lv.Description + " " + arg;
                    break;
                case "@intent":
                    Ev ev;
                    if (!EvText.TryParse(arg, out ev)) throw new LevelFormatException(where + "@intent '" + arg + "': 모르는 이벤트 이름이 있다.");
                    lv.Intent |= ev;
                    break;
                case "@optimal":
                    Ev oe;
                    if (!EvText.TryParse(arg, out oe)) throw new LevelFormatException(where + "@optimal '" + arg + "': 모르는 이벤트 이름이 있다.");
                    lv.OptimalIntent |= oe;
                    break;
                case "@stars":
                    var parts = arg.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    int a, b;
                    if (parts.Length != 2 || !int.TryParse(parts[0], out a) || !int.TryParse(parts[1], out b))
                        throw new LevelFormatException(where + "@stars 는 '별3 기준 턴, 별2 기준 턴' 두 숫자를 적는다.");
                    lv.Star3 = a;
                    lv.Star2 = b;
                    break;
                default:
                    throw new LevelFormatException(where + "모르는 표시 '" + name + "'.");
            }
        }
    }

    /// <summary>상태를 README 10절 표기로 되돌려 적는다. 테스트와 명령줄 출력에 쓴다.</summary>
    public static class Board
    {
        public static char TerrainChar(Terrain t)
        {
            switch (t)
            {
                case Terrain.Ground: return '.';
                case Terrain.Stone: return '#';
                case Terrain.Water: return '~';
                case Terrain.Fire: return '^';
                case Terrain.Grass: return 'g';
                case Terrain.Fence: return 'f';
                case Terrain.Tree: return 't';
                case Terrain.Ash: return '_';
                default: return 'x';
            }
        }

        public static string CellText(Level lv, GameState s, int pos)
        {
            var sb = new StringBuilder();
            sb.Append(TerrainChar(s.T[pos]));
            // 순서: 양, 늑대, 염소, 말, 소, 바구니, 빈 양동이, 물 찬 양동이
            AppendGroup(sb, s, pos, Kind.Sheep, 'S', false);
            foreach (var e in s.E)
            {
                if (e.Pos != pos || e.Kind != Kind.Wolf) continue;
                sb.Append('W');
                if (e.Count > 1) sb.Append(e.Count);
                AppendState(sb, e.Wet, e.Burned);
            }
            AppendGroup(sb, s, pos, Kind.Goat, 'G', false);
            AppendGroup(sb, s, pos, Kind.Horse, 'H', false);
            AppendGroup(sb, s, pos, Kind.Cow, 'M', false);
            AppendGroup(sb, s, pos, Kind.Basket, 'A', false);
            AppendGroup(sb, s, pos, Kind.Bucket, 'B', false);
            AppendGroup(sb, s, pos, Kind.Bucket, 'U', true);
            return sb.ToString();
        }

        private static void AppendGroup(StringBuilder sb, GameState s, int pos, Kind kind, char letter, bool full)
        {
            // 상태 조합별로 묶는다: 보통, 젖음, 화상
            for (int state = 0; state < 3; state++)
            {
                bool wet = state == 1, burned = state == 2;
                int n = 0;
                foreach (var e in s.E)
                {
                    if (e.Pos != pos || e.Kind != kind || e.Full != full) continue;
                    if (e.Wet == wet && e.Burned == burned) n++;
                }
                if (n == 0) continue;
                sb.Append(letter);
                if (n > 1) sb.Append(n);
                AppendState(sb, wet, burned);
            }
        }

        private static void AppendState(StringBuilder sb, bool wet, bool burned)
        {
            if (wet) sb.Append("(w)");
            else if (burned) sb.Append("(b)");
        }

        public static string Render(Level lv, GameState s)
        {
            var cells = new string[lv.CellCount];
            int width = 1;
            for (int p = 0; p < cells.Length; p++)
            {
                cells[p] = CellText(lv, s, p);
                if (cells[p].Length > width) width = cells[p].Length;
            }
            var sb = new StringBuilder();
            for (int r = 0; r < lv.Rows; r++)
            {
                if (r > 0) sb.Append('\n');
                for (int c = 0; c < lv.Cols; c++)
                {
                    string cell = cells[lv.Pos(r, c)];
                    sb.Append(c == lv.Cols - 1 ? cell : cell.PadRight(width + 2));
                }
            }
            return sb.ToString();
        }
    }
}
