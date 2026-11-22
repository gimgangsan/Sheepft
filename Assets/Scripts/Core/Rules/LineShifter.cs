using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>플레이어의 shift. README 3절, 구현 계획 3절.</summary>
    public static class LineShifter
    {
        /// <summary>
        /// 줄 하나를 1칸 shift한다. 줄은 빈칸을 뺀 실제 칸을 이은 것이고 끝에서 순환한다.
        /// 도착 칸이 장애물 지형이면 그 칸의 오브젝트는 제자리에 남는다.
        /// 오브젝트끼리는 막지 않는다. 움직인 것이 있으면 true. 아무것도 움직이지 않으면 Wait를 켠다.
        /// </summary>
        public static bool Shift(Board board, ShiftPoint point)
        {
            var line = board.Line(point.Axis, point.Index);
            int n = line.Count;
            if (n <= 1)
            {
                board.Mark(Mechanic.Wait);
                return false;
            }

            int step = point.Dir == Dir.Right || point.Dir == Dir.Down ? 1 : -1;
            var sheepBefore = board.SheepPositions();

            // 도착지를 모두 정한 뒤 한꺼번에 옮긴다. 결과는 자기 칸과 다음 칸의 지형만으로 정해진다.
            var moves = new List<KeyValuePair<ObjectEntity, Pos>>();
            for (int i = 0; i < n; i++)
            {
                if (line[i].Objects.Count == 0) continue;
                int next = i + step;
                bool wrap = next < 0 || next >= n;
                var dest = line[(next + n) % n];
                if (board.BlocksShift(dest))
                {
                    board.Mark(Mechanic.Blocked);
                    continue;
                }

                board.Mark(Mechanic.ShiftMove);
                if (wrap) board.Mark(Mechanic.Wrap);
                else if (!IsNeighbor(line[i].Pos, dest.Pos)) board.Mark(Mechanic.VoidSkip);
                foreach (var o in line[i].Objects)
                {
                    if (wrap && o is Sheep) board.Mark(Mechanic.SheepWrap);
                    else if (wrap && o is Wolf) board.Mark(Mechanic.WolfWrap);
                    moves.Add(new KeyValuePair<ObjectEntity, Pos>(o, dest.Pos));
                }
            }

            foreach (var move in moves)
                board.Move(move.Key, move.Value, MoveCause.Shift);

            if (moves.Count == 0)
            {
                board.Mark(Mechanic.Wait);
                return false;
            }
            board.MarkSheepMerge(sheepBefore);
            return true;
        }

        private static bool IsNeighbor(Pos a, Pos b)
        {
            return System.Math.Abs(a.Row - b.Row) + System.Math.Abs(a.Col - b.Col) == 1;
        }
    }
}
