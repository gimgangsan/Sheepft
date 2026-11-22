using System.Collections.Generic;
using System.Text;

namespace Sheepft.Core
{
    /// <summary>
    /// README 10절 표기로 쓴다. LevelParser의 반대다.
    /// Write는 맵만 쓴다(테스트가 "수를 둔 뒤의 맵"을 문자열로 비교하는 데 쓴다).
    /// WriteLevel, WriteFile은 헤더·풀이 줄·`@` 줄·주석까지 쓴다(스테이지 에디터가 파일을 저장하는 데 쓴다).
    /// </summary>
    public static class LevelWriter
    {
        /// <summary>
        /// 파일 전체. 파일 주석, 빈 줄, 스테이지들(사이에 빈 줄 하나)을 쓰고 줄 끝은 `\n`, 마지막에 줄바꿈 하나.
        /// </summary>
        public static string WriteFile(IEnumerable<string> fileComments, IEnumerable<Board> levels)
        {
            var sb = new StringBuilder();
            if (fileComments != null)
            {
                foreach (var c in fileComments) sb.Append(c).Append('\n');
                if (sb.Length > 0) sb.Append('\n');
            }
            bool first = true;
            foreach (var level in levels)
            {
                if (!first) sb.Append('\n');
                first = false;
                sb.Append(WriteLevel(level)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 스테이지 하나: 주석, 헤더, 풀이 줄, `@desc`, `@stars`, `@intent`, `@optimal`, 맵. 줄 사이는 `\n`, 끝에는 줄바꿈이 없다.
        /// 맵은 지금 상태를 쓰므로 레벨을 연 직후의 Board를 넘긴다. 헤더의 `목표 양`은 늘 적는다.
        /// </summary>
        public static string WriteLevel(Board board)
        {
            var rules = board.Rules;
            var sb = new StringBuilder();
            foreach (var c in rules.Comments) sb.Append(c).Append('\n');
            sb.Append("# ").Append(rules.Name)
              .Append("  shift: ").Append(ShiftSpec(rules.ShiftPoints))
              .Append("  제한 ").Append(rules.TurnLimit).Append("턴")
              .Append("  목표 양 ").Append(rules.TargetSheep).Append('\n');
            if (rules.Solution.Count > 0) sb.Append("# 풀이: ").Append(string.Join(", ", rules.Solution)).Append('\n');
            if (!string.IsNullOrEmpty(rules.Description)) sb.Append("@desc ").Append(rules.Description).Append('\n');
            if (rules.Star3Turns > 0 || rules.Star2Turns > 0) sb.Append("@stars ").Append(rules.Star3Turns).Append(' ').Append(rules.Star2Turns).Append('\n');
            if (rules.Intent != Mechanic.None) sb.Append("@intent ").Append(MechanicNames(rules.Intent)).Append('\n');
            if (rules.OptimalIntent != Mechanic.None) sb.Append("@optimal ").Append(MechanicNames(rules.OptimalIntent)).Append('\n');
            sb.Append(Write(board));
            return sb.ToString();
        }

        /// <summary>
        /// 헤더의 shift 표기. 행 먼저 번호 순, 그다음 열. 한 줄의 두 방향은 이어 쓴다(행은 `RL`, 열은 `UD` 순서).
        /// 예: `R0:RL, R2:R, C1:UD`
        /// </summary>
        public static string ShiftSpec(IEnumerable<ShiftPoint> points)
        {
            var set = new HashSet<ShiftPoint>(points);
            int maxIndex = -1;
            foreach (var p in set) if (p.Index > maxIndex) maxIndex = p.Index;
            var sb = new StringBuilder();
            for (int r = 0; r <= maxIndex; r++)
                AppendLine(sb, "R" + r,
                    set.Contains(new ShiftPoint(Axis.Row, r, Dir.Right)) ? "R" : "",
                    set.Contains(new ShiftPoint(Axis.Row, r, Dir.Left)) ? "L" : "");
            for (int c = 0; c <= maxIndex; c++)
                AppendLine(sb, "C" + c,
                    set.Contains(new ShiftPoint(Axis.Col, c, Dir.Up)) ? "U" : "",
                    set.Contains(new ShiftPoint(Axis.Col, c, Dir.Down)) ? "D" : "");
            return sb.ToString();
        }

        private static void AppendLine(StringBuilder sb, string line, string a, string b)
        {
            if (a.Length + b.Length == 0) return;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(line).Append(':').Append(a).Append(b);
        }

        /// <summary>기믹 이름을 공백으로 잇는다. `@intent` 줄 표기다.</summary>
        private static string MechanicNames(Mechanic mechanics)
        {
            var names = new List<string>();
            foreach (var one in MechanicText.Split(mechanics)) names.Add(one.ToString());
            return string.Join(" ", names);
        }

        /// <summary>
        /// 행마다 한 줄, 줄 사이는 `\n`. 열마다 가장 긴 칸에 맞춰 너비를 정하고 공백 두 칸으로 띄운다.
        /// 줄 끝에는 공백이 없다.
        /// </summary>
        public static string Write(Board board)
        {
            var cells = new string[board.Height, board.Width];
            var widths = new int[board.Width];
            for (int r = 0; r < board.Height; r++)
            {
                for (int c = 0; c < board.Width; c++)
                {
                    cells[r, c] = WriteCell(board.At(new Pos(r, c)));
                    if (cells[r, c].Length > widths[c]) widths[c] = cells[r, c].Length;
                }
            }

            var sb = new StringBuilder();
            for (int r = 0; r < board.Height; r++)
            {
                if (r > 0) sb.Append('\n');
                for (int c = 0; c < board.Width; c++)
                {
                    bool last = c == board.Width - 1;
                    sb.Append(last ? cells[r, c] : cells[r, c].PadRight(widths[c] + 2));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 칸 하나. 지형 기호 뒤에 오브젝트를 Id 순서로 붙인다.
        /// 이웃한 같은 종류·같은 상태 오브젝트는 숫자로 묶는다 (`S2S(w)`). 늑대의 숫자는 무리의 마리 수다.
        /// </summary>
        public static string WriteCell(Tile tile)
        {
            if (tile.Kind == TileKind.OuterBlank) return Notation.OuterBlank.ToString();
            if (tile.Kind == TileKind.InnerBlank) return Notation.InnerBlank.ToString();

            var sb = new StringBuilder();
            sb.Append(Notation.TerrainChar(tile.Terrain.Type));

            var objects = tile.Objects;
            int i = 0;
            while (i < objects.Count)
            {
                var o = objects[i];
                if (o.Removed) { i++; continue; }

                char letter = Notation.ObjectChar(o);
                string state = StateText(o);
                int count = 1;
                i++;

                var wolf = o as Wolf;
                if (wolf != null)
                {
                    count = wolf.Count;
                }
                else
                {
                    while (i < objects.Count)
                    {
                        var next = objects[i];
                        if (next.Removed) { i++; continue; }
                        if (Notation.ObjectChar(next) != letter || StateText(next) != state) break;
                        count++;
                        i++;
                    }
                }

                sb.Append(letter);
                if (count > 1) sb.Append(count);
                sb.Append(state);
            }
            return sb.ToString();
        }

        private static string StateText(ObjectEntity o)
        {
            // 표기법은 상태를 하나만 적는다. 젖음과 화상은 물과 들불이 서로 풀어 주므로 함께 있지 않다.
            var animal = o as Animal;
            if (animal != null && animal.Burned) return "(b)";
            if (o.Wet) return "(w)";
            return "";
        }
    }
}
