using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Sheepft.Core;

namespace Sheepft.Tools
{
    /// <summary>칸 하나의 오브젝트 묶음. 표기의 `S2`, `W(w)`, `B` 하나에 해당한다.</summary>
    public struct CellObject
    {
        public char Letter;   // S W G H A B U M
        public int Count;     // 1 이상. 늑대는 무리의 마리 수
        public char State;    // '\0' 없음, 'w' 젖음, 'b' 화상

        public override string ToString()
        {
            return Letter + (Count > 1 ? Count.ToString() : "") + (State != '\0' ? "(" + State + ")" : "");
        }
    }

    /// <summary>
    /// 칸 하나의 표기(`.S2W(w)`)를 읽고 쓴다. 첫 글자는 지형 기호나 빈칸(`-`, `x`), 그 뒤는 오브젝트다.
    /// 표기가 틀려도 예외를 던지지 않는다. 틀린 것은 스테이지를 게임 파서로 읽을 때 드러난다.
    /// </summary>
    public sealed class CellData
    {
        private static readonly Regex ObjectItem = new Regex(@"\G([A-Z])(\d+)?(?:\(([a-z])\))?", RegexOptions.CultureInvariant);

        public char Terrain = '.';
        public readonly List<CellObject> Objects = new List<CellObject>();

        public bool IsBlank => Terrain == Notation.OuterBlank || Terrain == Notation.InnerBlank;
        public bool IsObstacle => Terrain == '#' || Terrain == 'f' || Terrain == 't';

        public static CellData Parse(string token)
        {
            var cell = new CellData();
            if (string.IsNullOrEmpty(token)) return cell;
            cell.Terrain = token[0];
            int i = 1;
            while (i < token.Length)
            {
                var m = ObjectItem.Match(token, i);
                if (!m.Success || m.Length == 0) break;
                cell.Objects.Add(new CellObject
                {
                    Letter = m.Groups[1].Value[0],
                    Count = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1,
                    State = m.Groups[3].Success ? m.Groups[3].Value[0] : '\0',
                });
                i += m.Length;
            }
            return cell;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Terrain);
            foreach (var o in Objects) sb.Append(o);
            return sb.ToString();
        }

        public int SheepCount()
        {
            int n = 0;
            foreach (var o in Objects) if (o.Letter == 'S') n += o.Count;
            return n;
        }
    }

    /// <summary>
    /// 스테이지 에디터가 고치는 스테이지 하나. 칸은 표기 문자열로, 헤더는 값으로 갖는다.
    /// Unity가 직렬화할 수 있는 값만 쓴다(창을 다시 열거나 Play·컴파일로 도메인이 다시 읽혀도 편집 중인 내용이 남는다).
    ///
    /// 고치는 동안에는 틀린 상태도 허용한다. ToText()로 README 10절 표기를 만들고 게임 파서(TryParse)로 읽어 보아
    /// 오류를 알린다. 저장·검증·Play는 모두 이 텍스트를 거치므로, 에디터가 받아들이는 것과 게임이 읽는 것이 같다.
    /// </summary>
    [Serializable]
    public sealed class StageDraft
    {
        public const int AutoTarget = -1;

        public List<string> comments = new List<string>();
        public string name = "";
        public int turnLimit = 10;
        /// <summary>목표 양 수. AutoTarget이면 맵의 양 수.</summary>
        public int target = AutoTarget;
        /// <summary>풀이 줄. `R1:R, C2:U`처럼 사람이 적은 그대로.</summary>
        public string solution = "";
        public string description = "";
        public int star3;
        public int star2;
        /// <summary>`@intent` 기믹 이름들. 공백으로 나눈다.</summary>
        public string intent = "";
        public string optimal = "";
        /// <summary>shift 포인트. `R0:R`처럼 하나씩.</summary>
        public List<string> points = new List<string>();

        public int height;
        public int width;
        /// <summary>칸 표기. 행 우선, 길이 height × width.</summary>
        public List<string> cells = new List<string>();

        // ── 만들기 ──

        /// <summary>새 스테이지. 3×3 땅에 양 하나와 풀 하나, 모든 줄 양방향.</summary>
        public static StageDraft CreateNew(string stageName)
        {
            var d = new StageDraft { name = stageName, height = 3, width = 3, turnLimit = 10 };
            for (int i = 0; i < 9; i++) d.cells.Add(".");
            d.cells[2] = "g";
            d.cells[6] = ".S";
            d.OpenAllLines();
            return d;
        }

        /// <summary>읽어 들인 레벨(레벨을 연 직후의 Board)에서 만든다.</summary>
        public static StageDraft FromBoard(Board board)
        {
            var r = board.Rules;
            var d = new StageDraft
            {
                name = r.Name,
                turnLimit = r.TurnLimit,
                target = r.TargetSheep,
                solution = string.Join(", ", r.Solution),
                description = r.Description,
                star3 = r.Star3Turns,
                star2 = r.Star2Turns,
                intent = Names(r.Intent),
                optimal = Names(r.OptimalIntent),
                height = board.Height,
                width = board.Width,
            };
            d.comments.AddRange(r.Comments);
            foreach (var p in r.ShiftPoints) d.points.Add(p.ToString());
            for (int row = 0; row < board.Height; row++)
                for (int col = 0; col < board.Width; col++)
                    d.cells.Add(LevelWriter.WriteCell(board.At(new Pos(row, col))));
            return d;
        }

        public StageDraft Clone()
        {
            var d = (StageDraft)MemberwiseClone();
            d.comments = new List<string>(comments);
            d.points = new List<string>(points);
            d.cells = new List<string>(cells);
            return d;
        }

        // ── 텍스트와 파싱 ──

        /// <summary>README 10절 표기. 저장할 때는 이 텍스트를 게임 파서로 읽은 뒤 LevelWriter로 정돈해 쓴다.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            foreach (var c in comments) sb.Append(c).Append('\n');
            sb.Append("# ").Append(name).Append("  shift: ").Append(LevelWriter.ShiftSpec(ShiftPoints()))
              .Append("  제한 ").Append(turnLimit).Append("턴");
            if (target != AutoTarget) sb.Append("  목표 양 ").Append(target);
            sb.Append('\n');
            if (!string.IsNullOrWhiteSpace(solution)) sb.Append("# 풀이: ").Append(solution.Trim()).Append('\n');
            if (!string.IsNullOrWhiteSpace(description)) sb.Append("@desc ").Append(description.Trim()).Append('\n');
            if (star3 != 0 || star2 != 0) sb.Append("@stars ").Append(star3).Append(' ').Append(star2).Append('\n');
            if (!string.IsNullOrWhiteSpace(intent)) sb.Append("@intent ").Append(intent.Trim()).Append('\n');
            if (!string.IsNullOrWhiteSpace(optimal)) sb.Append("@optimal ").Append(optimal.Trim()).Append('\n');
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    if (c > 0) sb.Append("  ");
                    sb.Append(cells[r * width + c]);
                }
                if (r < height - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 게임 파서로 읽어 본다. 성공하면 board, 실패하면 error에 이유가 들어간다.
        /// 이름처럼 파서가 헤더 모양 때문에 엉뚱한 말을 하는 경우는 먼저 걸러 알기 쉽게 알린다.
        /// </summary>
        public bool TryParse(out Board board, out string error)
        {
            board = null;
            error = NameProblem();
            if (error != null) return false;
            try
            {
                board = LevelParser.Parse(ToText());
                return true;
            }
            catch (LevelParseException e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>이름이 헤더를 깨뜨리는가. 문제가 없으면 null.</summary>
        public string NameProblem()
        {
            if (string.IsNullOrWhiteSpace(name)) return "이름이 비어 있다.";
            if (name.Contains("shift")) return "이름에 'shift'를 쓸 수 없다(헤더 표시와 헷갈린다).";
            if (name.TrimStart().StartsWith("풀이")) return "이름을 '풀이'로 시작할 수 없다(풀이 줄과 헷갈린다).";
            if (name.Contains("\n") || name.Contains("#")) return "이름에 줄바꿈이나 '#'을 쓸 수 없다.";
            return null;
        }

        // ── 칸 ──

        public CellData Cell(int r, int c) => CellData.Parse(cells[r * width + c]);

        public void SetCell(int r, int c, CellData cell) => cells[r * width + c] = cell.ToString();

        public bool InGrid(int r, int c) => r >= 0 && r < height && c >= 0 && c < width;

        /// <summary>
        /// 지형을 칠한다. 빈칸(`-`, `x`)이나 장애물(돌, 울타리, 나무)을 칠하면 그 칸의 오브젝트는 지운다(그 위에는 놓을 수 없다).
        /// 바뀌었으면 true.
        /// </summary>
        public bool PaintTerrain(int r, int c, char symbol)
        {
            var cell = Cell(r, c);
            string before = cell.ToString();
            cell.Terrain = symbol;
            if (cell.IsBlank || cell.IsObstacle) cell.Objects.Clear();
            SetCell(r, c, cell);
            return cell.ToString() != before;
        }

        /// <summary>
        /// 오브젝트를 하나 더한다. 칸의 마지막 묶음과 종류·상태가 같으면 수를 하나 늘린다(늑대는 무리 수).
        /// 빈칸이나 장애물 칸이면 false.
        /// </summary>
        public bool AddObject(int r, int c, char letter, char state)
        {
            var cell = Cell(r, c);
            if (cell.IsBlank || cell.IsObstacle) return false;
            int last = cell.Objects.Count - 1;
            if (last >= 0 && cell.Objects[last].Letter == letter && cell.Objects[last].State == state)
            {
                var o = cell.Objects[last];
                o.Count++;
                cell.Objects[last] = o;
            }
            else
            {
                cell.Objects.Add(new CellObject { Letter = letter, Count = 1, State = state });
            }
            SetCell(r, c, cell);
            return true;
        }

        /// <summary>칸의 마지막 오브젝트를 하나 뺀다. 없으면 false.</summary>
        public bool RemoveObject(int r, int c)
        {
            var cell = Cell(r, c);
            int last = cell.Objects.Count - 1;
            if (last < 0) return false;
            var o = cell.Objects[last];
            if (o.Count > 1) { o.Count--; cell.Objects[last] = o; }
            else cell.Objects.RemoveAt(last);
            SetCell(r, c, cell);
            return true;
        }

        public int SheepCount()
        {
            int n = 0;
            for (int i = 0; i < cells.Count; i++) n += CellData.Parse(cells[i]).SheepCount();
            return n;
        }

        // ── shift 포인트 ──

        public List<ShiftPoint> ShiftPoints()
        {
            var list = new List<ShiftPoint>();
            foreach (var text in points)
            {
                ShiftPoint p;
                if (ShiftPoint.TryParse(text, out p)) list.Add(p);
            }
            return list;
        }

        public bool HasPoint(ShiftPoint p) => points.Contains(p.ToString());

        public void TogglePoint(ShiftPoint p)
        {
            string text = p.ToString();
            if (!points.Remove(text)) points.Add(text);
        }

        /// <summary>실제 칸이 둘 이상인 모든 행(R, L)과 열(U, D)을 연다. 나머지 포인트는 지운다.</summary>
        public void OpenAllLines()
        {
            points.Clear();
            for (int r = 0; r < height; r++)
                if (RealCount(Axis.Row, r) >= 2) { points.Add("R" + r + ":R"); points.Add("R" + r + ":L"); }
            for (int c = 0; c < width; c++)
                if (RealCount(Axis.Col, c) >= 2) { points.Add("C" + c + ":U"); points.Add("C" + c + ":D"); }
        }

        /// <summary>그 줄의 실제 칸(빈칸이 아닌 칸) 수.</summary>
        public int RealCount(Axis axis, int index)
        {
            int n = 0;
            int len = axis == Axis.Row ? width : height;
            for (int k = 0; k < len; k++)
            {
                var cell = axis == Axis.Row ? Cell(index, k) : Cell(k, index);
                if (!cell.IsBlank) n++;
            }
            return n;
        }

        /// <summary>헤더 표기(`R0:RL, C2:U`)로 shift 포인트를 바꾼다. 읽지 못한 항목이 있으면 false이고 바꾸지 않는다.</summary>
        public bool SetShiftSpec(string spec)
        {
            var next = new List<string>();
            foreach (var raw in spec.Split(','))
            {
                string item = raw.Trim();
                if (item.Length == 0) continue;
                int colon = item.IndexOf(':');
                if (colon < 2) return false;
                string line = item.Substring(0, colon);
                foreach (char dir in item.Substring(colon + 1))
                {
                    ShiftPoint p;
                    if (!ShiftPoint.TryParse(line + ":" + dir, out p)) return false;
                    if (!next.Contains(p.ToString())) next.Add(p.ToString());
                }
            }
            points = next;
            return true;
        }

        // ── 크기 ──

        /// <summary>
        /// at번 자리에 땅으로 된 행을 끼운다(0이면 맨 위, height면 맨 아래). 새 행은 양방향으로 열고,
        /// 그 아래 행의 shift 포인트와 풀이 줄의 행 번호를 하나씩 밀어 준다.
        /// </summary>
        public void InsertRow(int at)
        {
            var row = new List<string>();
            for (int c = 0; c < width; c++) row.Add(".");
            cells.InsertRange(at * width, row);
            height++;
            Renumber(Axis.Row, at, +1);
            points.Add("R" + at + ":R");
            points.Add("R" + at + ":L");
        }

        public void InsertCol(int at)
        {
            for (int r = height - 1; r >= 0; r--) cells.Insert(r * width + at, ".");
            width++;
            Renumber(Axis.Col, at, +1);
            points.Add("C" + at + ":U");
            points.Add("C" + at + ":D");
        }

        /// <summary>at번 행을 지운다. 그 행의 shift 포인트와 그 행을 미는 풀이 수는 지우고, 아래 번호를 당긴다.</summary>
        public void RemoveRow(int at)
        {
            if (height <= 1) return;
            cells.RemoveRange(at * width, width);
            height--;
            Renumber(Axis.Row, at, -1);
        }

        public void RemoveCol(int at)
        {
            if (width <= 1) return;
            for (int r = height - 1; r >= 0; r--) cells.RemoveAt(r * width + at);
            width--;
            Renumber(Axis.Col, at, -1);
        }

        /// <summary>
        /// 행이나 열을 끼우거나(+1) 지운(-1) 뒤 번호를 맞춘다. 지운 줄을 가리키던 포인트와 풀이 수는 없앤다.
        /// </summary>
        private void Renumber(Axis axis, int at, int delta)
        {
            var next = new List<string>();
            foreach (var p in ShiftPoints())
            {
                var moved = Shift(p, axis, at, delta);
                if (moved != null) next.Add(moved.Value.ToString());
            }
            points = next;

            if (string.IsNullOrWhiteSpace(solution)) return;
            var moves = new List<string>();
            foreach (var raw in solution.Split(','))
            {
                string item = raw.Trim();
                if (item.Length == 0) continue;
                ShiftPoint p;
                if (!ShiftPoint.TryParse(item, out p)) { moves.Add(item); continue; } // 읽지 못한 것은 그대로 둔다
                var moved = Shift(p, axis, at, delta);
                if (moved != null) moves.Add(moved.Value.ToString());
            }
            solution = string.Join(", ", moves);
        }

        private static ShiftPoint? Shift(ShiftPoint p, Axis axis, int at, int delta)
        {
            if (p.Axis != axis || p.Index < at) return p;
            if (delta < 0 && p.Index == at) return null;
            return new ShiftPoint(p.Axis, p.Index + delta, p.Dir);
        }

        // ── 기믹 ──

        public Mechanic IntentMechanics => ParseNames(intent);
        public Mechanic OptimalMechanics => ParseNames(optimal);

        public static Mechanic ParseNames(string text)
        {
            Mechanic m;
            string unknown;
            return MechanicText.TryParse(text ?? "", out m, out unknown) ? m : Mechanic.None;
        }

        public static string Names(Mechanic mechanics)
        {
            var names = new List<string>();
            foreach (var one in MechanicText.Split(mechanics)) names.Add(one.ToString());
            return string.Join(" ", names);
        }
    }
}
