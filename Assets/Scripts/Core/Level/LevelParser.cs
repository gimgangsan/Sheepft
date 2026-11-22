using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sheepft.Core
{
    /// <summary>레벨 파일이 표기법에 맞지 않을 때 던진다. 메시지에 줄 번호가 들어 있다.</summary>
    public sealed class LevelParseException : Exception
    {
        /// <summary>문제가 있는 줄. 1부터 센다.</summary>
        public readonly int Line;

        public LevelParseException(int line, string message) : base(line + "번째 줄: " + message)
        {
            Line = line;
        }
    }

    /// <summary>
    /// README 10절 표기법의 레벨 파일을 읽는다. 한 파일에 레벨을 여러 개 이어 적을 수 있다.
    ///
    ///   # 이름  shift: R1:RL, C2:U  제한 4턴  목표 양 2     헤더. `목표 양`은 생략할 수 있다
    ///   # 풀이: R1:R, C2:U                                  선택
    ///   @desc 문장                                          선택. 여러 줄이면 공백으로 이어 붙인다
    ///   @stars 3 4                                          선택. 별 3개, 별 2개 기준 턴
    ///   @intent ..., @optimal ...                           선택. 스테이지 검증용 기믹 이름(Mechanic)
    ///   .   .   g   .                                       맵. 칸은 공백으로 나눈다
    ///
    /// `//`로 시작하는 줄과 빈 줄은 건너뛴다.
    /// </summary>
    public static class LevelParser
    {
        private static readonly Regex HeaderLine = new Regex(
            @"^#\s*(?<name>.*?)\s*shift\s*:(?<rest>.*)$", RegexOptions.CultureInvariant);
        private static readonly Regex SolutionLine = new Regex(
            @"^#\s*풀이\s*:(?<moves>.*)$", RegexOptions.CultureInvariant);
        private static readonly Regex TurnLimitPart = new Regex(
            @"제한\s*(\d+)\s*턴", RegexOptions.CultureInvariant);
        private static readonly Regex TargetPart = new Regex(
            @"목표\s*양\s*(\d+)", RegexOptions.CultureInvariant);
        private static readonly Regex ShiftItem = new Regex(
            @"^([RC])(\d+):([RLUD]+)$", RegexOptions.CultureInvariant);
        private static readonly Regex ObjectItem = new Regex(
            @"\G([A-Z])(\d+)?(?:\(([a-z])\))?", RegexOptions.CultureInvariant);

        private static readonly char[] Blanks = { ' ', '\t' };

        private sealed class Draft
        {
            public int HeaderLine;
            public string Name;
            public string ShiftText;
            public int TurnLimit;
            public int Target = -1;
            public string SolutionText;
            public int SolutionLine;
            public readonly List<string> Desc = new List<string>();
            public bool HasStars;
            public int Star3, Star2;
            public Mechanic Intent, OptimalIntent;
            public readonly List<string[]> Rows = new List<string[]>();
            public readonly List<int> RowLines = new List<int>();
        }

        /// <summary>레벨이 정확히 하나 들어 있는 문자열을 읽는다.</summary>
        public static Board Parse(string text)
        {
            var all = ParseAll(text);
            if (all.Count != 1)
                throw new LevelParseException(1, "레벨이 정확히 하나여야 하는데 " + all.Count + "개다. 헤더는 '# 이름  shift: ...  제한 N턴' 형식이다.");
            return all[0];
        }

        /// <summary>파일 하나의 모든 레벨을 적힌 순서대로 읽는다.</summary>
        public static List<Board> ParseAll(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

            var result = new List<Board>();
            Draft cur = null;
            var lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int no = i + 1;
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;

                // 헤더, 풀이 줄, 돌로 시작하는 맵 줄이 모두 `#`으로 시작한다.
                // `풀이`와 `shift`라는 말로 구분한다.
                if (line[0] == '#')
                {
                    var sol = SolutionLine.Match(line);
                    if (sol.Success)
                    {
                        if (cur == null) throw new LevelParseException(no, "풀이 줄이 헤더보다 먼저 나왔다.");
                        if (cur.Rows.Count > 0) throw new LevelParseException(no, "풀이 줄은 맵보다 앞에 적는다.");
                        if (cur.SolutionText != null) throw new LevelParseException(no, "풀이 줄이 두 번 나왔다.");
                        cur.SolutionText = sol.Groups["moves"].Value;
                        cur.SolutionLine = no;
                        continue;
                    }
                    if (line.Contains("shift"))
                    {
                        if (cur != null) result.Add(Build(cur));
                        cur = ParseHeader(line, no);
                        continue;
                    }
                }

                if (cur == null) throw new LevelParseException(no, "레벨 헤더보다 먼저 나온 줄이다.");

                if (line[0] == '@')
                {
                    if (cur.Rows.Count > 0) throw new LevelParseException(no, "`@` 줄은 맵보다 앞에 적는다.");
                    ParseMeta(cur, line, no);
                    continue;
                }

                cur.Rows.Add(line.Split(Blanks, StringSplitOptions.RemoveEmptyEntries));
                cur.RowLines.Add(no);
            }
            if (cur != null) result.Add(Build(cur));
            return result;
        }

        private static Draft ParseHeader(string line, int no)
        {
            var m = HeaderLine.Match(line);
            if (!m.Success)
                throw new LevelParseException(no, "헤더를 읽지 못했다. 예: # 이름  shift: R1:RL, C2:U  제한 4턴  목표 양 2");

            var d = new Draft();
            d.HeaderLine = no;
            d.Name = m.Groups["name"].Value.Trim();
            string rest = m.Groups["rest"].Value;

            var limit = TurnLimitPart.Match(rest);
            if (!limit.Success) throw new LevelParseException(no, "헤더에 '제한 N턴'이 없다.");
            if (!int.TryParse(limit.Groups[1].Value, out d.TurnLimit) || d.TurnLimit < 1)
                throw new LevelParseException(no, "제한 턴은 1 이상이어야 한다.");

            var target = TargetPart.Match(rest);
            if (target.Success && !int.TryParse(target.Groups[1].Value, out d.Target))
                throw new LevelParseException(no, "목표 양 수를 읽지 못했다.");

            // shift 목록은 `제한`이나 `목표` 앞까지다. 그 뒤에는 두 항목 말고 다른 글자가 없어야 한다.
            int cut = limit.Index;
            if (target.Success && target.Index < cut) cut = target.Index;
            d.ShiftText = rest.Substring(0, cut);
            string tail = rest.Substring(cut);
            tail = TargetPart.Replace(TurnLimitPart.Replace(tail, "", 1), "", 1);
            if (tail.Trim().Length > 0)
                throw new LevelParseException(no, "헤더에서 읽지 못한 부분이 있다: '" + tail.Trim() + "'");
            return d;
        }

        private static void ParseMeta(Draft d, string line, int no)
        {
            int sp = line.IndexOfAny(Blanks);
            string key = sp < 0 ? line : line.Substring(0, sp);
            string arg = sp < 0 ? "" : line.Substring(sp + 1).Trim();
            switch (key)
            {
                case "@desc":
                    d.Desc.Add(arg);
                    break;
                case "@stars":
                    if (d.HasStars) throw new LevelParseException(no, "@stars 줄이 두 번 나왔다.");
                    var parts = arg.Split(Blanks, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2 || !int.TryParse(parts[0], out d.Star3) || !int.TryParse(parts[1], out d.Star2)
                        || d.Star3 < 1 || d.Star2 < 1)
                        throw new LevelParseException(no, "@stars 에는 별 3개 기준 턴과 별 2개 기준 턴, 두 숫자를 적는다. 예: @stars 3 4");
                    d.HasStars = true;
                    break;
                case "@intent":
                case "@optimal":
                    // 스테이지 검증용. 게임 진행에는 쓰지 않는다. 줄이 여럿이면 합친다.
                    Mechanic m;
                    string unknown;
                    if (!MechanicText.TryParse(arg, out m, out unknown))
                        throw new LevelParseException(no, "모르는 기믹 이름 '" + unknown + "'. Mechanic enum의 이름을 그대로 적는다.");
                    if (key == "@intent") d.Intent |= m;
                    else d.OptimalIntent |= m;
                    break;
                default:
                    throw new LevelParseException(no, "모르는 표시 '" + key + "'. @desc, @stars, @intent, @optimal만 쓴다.");
            }
        }

        private static Board Build(Draft d)
        {
            if (d.Rows.Count == 0) throw new LevelParseException(d.HeaderLine, "레벨 '" + d.Name + "'에 맵 줄이 없다.");

            int height = d.Rows.Count;
            int width = d.Rows[0].Length;
            var tiles = new Tile[height, width];
            int nextId = 0;
            int sheep = 0;
            for (int r = 0; r < height; r++)
            {
                var row = d.Rows[r];
                int no = d.RowLines[r];
                if (row.Length != width)
                    throw new LevelParseException(no, "칸 수가 " + row.Length + "개다. 모든 행의 칸 수가 같아야 한다 (첫 행은 " + width + "개).");
                for (int c = 0; c < width; c++)
                {
                    var tile = ParseCell(row[c], new Pos(r, c), no, ref nextId);
                    foreach (var o in tile.Objects)
                        if (o is Sheep) sheep++;
                    tiles[r, c] = tile;
                }
            }

            var rules = new LevelRules();
            rules.Name = d.Name;
            rules.TurnLimit = d.TurnLimit;
            rules.TargetSheep = d.Target < 0 ? sheep : d.Target;
            if (rules.TargetSheep > sheep)
                throw new LevelParseException(d.HeaderLine, "목표 양 수(" + rules.TargetSheep + ")가 레벨의 양 수(" + sheep + ")보다 많다.");
            rules.Description = string.Join(" ", d.Desc);
            rules.Star3Turns = d.Star3;
            rules.Star2Turns = d.Star2;
            rules.Intent = d.Intent;
            rules.OptimalIntent = d.OptimalIntent;

            var board = new Board(tiles, rules);
            board.SeatInitialRiders();
            rules.ShiftPoints = ParseShiftPoints(d, board);
            if (d.SolutionText != null) rules.Solution = ParseSolution(d, rules.ShiftPoints);
            return board;
        }

        private static List<ShiftPoint> ParseShiftPoints(Draft d, Board board)
        {
            int no = d.HeaderLine;
            var points = new List<ShiftPoint>();
            foreach (var raw in d.ShiftText.Split(','))
            {
                string item = raw.Trim();
                var m = ShiftItem.Match(item);
                if (!m.Success)
                    throw new LevelParseException(no, "shift 표기 '" + item + "'를 읽지 못했다. 예: R1:RL, C2:U");

                var axis = m.Groups[1].Value == "R" ? Axis.Row : Axis.Col;
                int index;
                if (!int.TryParse(m.Groups[2].Value, out index) || index >= (axis == Axis.Row ? board.Height : board.Width))
                    throw new LevelParseException(no, "'" + item + "': 맵에 없는 줄이다.");
                if (board.Line(axis, index).Count == 0)
                    throw new LevelParseException(no, "'" + item + "': 실제 칸이 하나도 없는 줄이다.");

                foreach (char ch in m.Groups[3].Value)
                {
                    var point = MakePoint(axis, index, ch, item, no);
                    if (points.Contains(point))
                        throw new LevelParseException(no, "shift 포인트 " + point + "를 두 번 적었다.");
                    points.Add(point);
                }
            }
            return points;
        }

        private static List<ShiftPoint> ParseSolution(Draft d, IReadOnlyList<ShiftPoint> allowed)
        {
            int no = d.SolutionLine;
            var moves = new List<ShiftPoint>();
            foreach (var raw in d.SolutionText.Split(','))
            {
                string item = raw.Trim();
                var m = ShiftItem.Match(item);
                if (!m.Success)
                    throw new LevelParseException(no, "풀이의 수 '" + item + "'를 읽지 못했다. 예: R1:R, C2:U");
                if (m.Groups[3].Value.Length != 1)
                    throw new LevelParseException(no, "풀이의 수 '" + item + "': 방향은 하나만 적는다.");

                var axis = m.Groups[1].Value == "R" ? Axis.Row : Axis.Col;
                int index;
                if (!int.TryParse(m.Groups[2].Value, out index))
                    throw new LevelParseException(no, "풀이의 수 '" + item + "': 맵에 없는 줄이다.");
                var point = MakePoint(axis, index, m.Groups[3].Value[0], item, no);

                bool found = false;
                foreach (var p in allowed)
                    if (p == point) { found = true; break; }
                if (!found)
                    throw new LevelParseException(no, "풀이의 수 '" + item + "'는 이 레벨의 shift 포인트가 아니다.");
                moves.Add(point);
            }
            return moves;
        }

        private static ShiftPoint MakePoint(Axis axis, int index, char dirChar, string item, int no)
        {
            Dir dir;
            Notation.TryDir(dirChar, out dir);
            bool horizontal = dir == Dir.Right || dir == Dir.Left;
            if ((axis == Axis.Row) != horizontal)
                throw new LevelParseException(no, "'" + item + "': 행(R)에는 R·L만, 열(C)에는 U·D만 쓴다.");
            return new ShiftPoint(axis, index, dir);
        }

        private static Tile ParseCell(string token, Pos pos, int no, ref int nextId)
        {
            string where = pos + " '" + token + "': ";

            if (token[0] == Notation.OuterBlank || token[0] == Notation.InnerBlank)
            {
                if (token.Length > 1) throw new LevelParseException(no, where + "빈칸에는 오브젝트를 둘 수 없다.");
                var kind = token[0] == Notation.OuterBlank ? TileKind.OuterBlank : TileKind.InnerBlank;
                return new Tile(pos, kind, null);
            }

            TerrainType type;
            if (!Notation.TryTerrain(token[0], out type))
                throw new LevelParseException(no, where + "모르는 지형 기호 '" + token[0] + "'.");
            var tile = new Tile(pos, TileKind.Normal, TerrainEntity.Create(type));
            if (token.Length == 1) return tile;
            if (tile.Terrain.IsObstacle)
                throw new LevelParseException(no, where + "장애물 칸에는 오브젝트를 둘 수 없다.");

            int i = 1;
            while (i < token.Length)
            {
                var m = ObjectItem.Match(token, i);
                if (!m.Success)
                    throw new LevelParseException(no, where + "'" + token.Substring(i) + "'를 읽지 못했다. 오브젝트는 대문자 + 숫자(선택) + (w)/(b)(선택)다.");
                i += m.Length;

                char letter = m.Groups[1].Value[0];
                int count = 1;
                if (m.Groups[2].Success && (!int.TryParse(m.Groups[2].Value, out count) || count < 1))
                    throw new LevelParseException(no, where + "개수는 1 이상이어야 한다.");

                bool wet = false, burned = false;
                if (m.Groups[3].Success)
                {
                    string state = m.Groups[3].Value;
                    if (state == "w") wet = true;
                    else if (state == "b") burned = true;
                    else throw new LevelParseException(no, where + "모르는 상태 '(" + state + ")'. (w) 젖음, (b) 화상만 쓴다.");
                }

                // 늑대는 무리 하나가 엔티티 하나다. 나머지는 숫자만큼 엔티티를 만든다.
                int entities = letter == 'W' ? 1 : count;
                for (int k = 0; k < entities; k++)
                {
                    var o = CreateObject(letter);
                    if (o == null) throw new LevelParseException(no, where + "모르는 오브젝트 기호 '" + letter + "'.");

                    var animal = o as Animal;
                    if (burned && animal == null) throw new LevelParseException(no, where + "화상은 동물에게만 붙는다.");
                    var bucket = o as Bucket;
                    if (wet && bucket != null && bucket.Filled)
                        throw new LevelParseException(no, where + "물 찬 양동이에는 젖음 상태가 없다.");

                    o.Id = nextId++;
                    o.Pos = pos;
                    o.Wet = wet;
                    if (animal != null) animal.Burned = burned;
                    var wolf = o as Wolf;
                    if (wolf != null) wolf.Count = count;
                    tile.Objects.Add(o);
                }
            }
            return tile;
        }

        private static ObjectEntity CreateObject(char letter)
        {
            switch (letter)
            {
                case 'S': return new Sheep();
                case 'W': return new Wolf();
                case 'G': return new Goat();
                case 'H': return new Horse();
                case 'M': return new Cow();
                case 'A': return new FruitBasket();
                case 'B': return new Bucket();
                case 'U': return new Bucket { Filled = true };
                default: return null;
            }
        }
    }
}
