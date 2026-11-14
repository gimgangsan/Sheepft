using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>플레이어의 shift. README 3절, 구현 계획 3절.</summary>
    public static class LineShifter
    {
        /// <summary>
        /// 줄 하나를 1칸 shift한다. 줄은 빈칸을 뺀 실제 칸을 이은 것이고 끝에서 순환한다.
        /// 도착 칸이 장애물 지형이면 그 칸의 오브젝트는 제자리에 남는다.
        /// 오브젝트끼리는 막지 않는다. 움직인 것이 있으면 true.
        /// </summary>
        public static bool Shift(Board board, ShiftPoint point)
        {
            var line = board.Line(point.Axis, point.Index);
            int n = line.Count;
            if (n <= 1) return false;

            int step = point.Dir == Dir.Right || point.Dir == Dir.Down ? 1 : -1;

            // 도착지를 모두 정한 뒤 한꺼번에 옮긴다. 결과는 자기 칸과 다음 칸의 지형만으로 정해진다.
            var moves = new List<KeyValuePair<ObjectEntity, Pos>>();
            for (int i = 0; i < n; i++)
            {
                if (line[i].Objects.Count == 0) continue;
                var dest = line[(i + step + n) % n];
                if (board.BlocksShift(dest)) continue;
                foreach (var o in line[i].Objects)
                    moves.Add(new KeyValuePair<ObjectEntity, Pos>(o, dest.Pos));
            }

            foreach (var move in moves)
                board.Move(move.Key, move.Value, MoveCause.Shift);
            return moves.Count > 0;
        }
    }
}
