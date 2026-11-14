using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>
    /// 스스로 걷는 동물(늑대, 말, 소)의 공통 걷기 규칙. README 6절, 구현 계획 2절.
    /// 걸을 수 있는 칸(IsWalkable)만 지나가고 순환하지 않는다.
    /// </summary>
    public static class Pathfinder
    {
        private const int Unreached = int.MaxValue;

        private static readonly Dir[] Dirs = { Dir.Up, Dir.Right, Dir.Down, Dir.Left };

        /// <summary>
        /// 목표 칸들 중 경로 거리로 가장 가까운 곳으로 가는 첫 걸음의 방향.
        /// 물이나 들불 위의 목표는 갈 수 없는 것으로 본다.
        /// 동점은 마지막으로 스스로 걸은 방향, 그다음 상 > 우 > 하 > 좌 순이다.
        /// 경로가 없거나 이미 목표 칸에 있으면 null이다.
        /// </summary>
        public static Dir? FirstStep(Board board, Pos from, IEnumerable<Pos> targets, Dir? lastWalkDir)
        {
            var dist = DistancesFrom(board, targets);
            if (dist == null) return null;
            if (dist[from.Row, from.Col] == 0) return null;

            // 서 있는 칸은 물이나 들불이어도 걸어 나올 수 있으므로 이웃 칸의 거리만 본다.
            int best = Unreached;
            foreach (var d in Dirs)
                best = System.Math.Min(best, DistanceAt(board, dist, from.Step(d)));
            if (best == Unreached) return null;

            if (lastWalkDir != null && DistanceAt(board, dist, from.Step(lastWalkDir.Value)) == best)
                return lastWalkDir;
            foreach (var d in Dirs)
                if (DistanceAt(board, dist, from.Step(d)) == best) return d;
            return null;
        }

        /// <summary>
        /// 목표 칸들에서 거꾸로 퍼져 나가며 잰 걷기 거리표. 닿지 않는 칸은 Unreached다.
        /// 갈 수 있는 목표가 하나도 없으면 null.
        /// </summary>
        private static int[,] DistancesFrom(Board board, IEnumerable<Pos> targets)
        {
            var dist = new int[board.Height, board.Width];
            for (int r = 0; r < board.Height; r++)
                for (int c = 0; c < board.Width; c++)
                    dist[r, c] = Unreached;

            var queue = new Queue<Pos>();
            foreach (var target in targets)
            {
                if (!board.IsWalkable(target) || dist[target.Row, target.Col] == 0) continue;
                dist[target.Row, target.Col] = 0;
                queue.Enqueue(target);
            }
            if (queue.Count == 0) return null;

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in Dirs)
                {
                    var q = p.Step(d);
                    if (!board.IsWalkable(q) || dist[q.Row, q.Col] != Unreached) continue;
                    dist[q.Row, q.Col] = dist[p.Row, p.Col] + 1;
                    queue.Enqueue(q);
                }
            }
            return dist;
        }

        private static int DistanceAt(Board board, int[,] dist, Pos p)
        {
            return board.At(p) == null ? Unreached : dist[p.Row, p.Col];
        }
    }
}
