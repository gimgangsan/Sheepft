using System.Text;

namespace Sheepft.Core
{
    /// <summary>
    /// 맵을 README 10절 표기로 쓴다. LevelParser의 반대다. 헤더는 쓰지 않는다.
    /// 테스트가 "수를 둔 뒤의 맵"을 문자열로 비교하는 데 쓴다.
    /// </summary>
    public static class LevelWriter
    {
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
