using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>스스로 걸은 동물 하나와 턴이 끝났을 때 그 동물이 있을 칸.</summary>
    public readonly struct WalkerArrival
    {
        public readonly int Id;
        public readonly Pos Pos;

        public WalkerArrival(int id, Pos pos)
        {
            Id = id;
            Pos = pos;
        }

        public override string ToString() => "#" + Id + " " + Pos;
    }

    /// <summary>
    /// 수를 두기 전에 그 턴의 결과를 미리 본다. 판을 복사해 한 턴을 끝까지 돌리므로
    /// shift, 염소의 밀치기, 말의 운반, 소의 밀기까지 실제와 똑같다. 원래 판은 바뀌지 않는다.
    /// </summary>
    public static class TurnPreview
    {
        /// <summary>
        /// 지금 판에 move를 두면 이번 턴에 스스로 걸은 동물(늑대, 말, 소)이 턴이 끝났을 때 있을 칸. Id 순서다.
        /// 걷지 않은 동물(목표가 없는 말, 경로가 없는 동물, 기절·식사·화상·탑승·배부름)은 넣지 않는다.
        /// 걸은 뒤 사라졌으면(합류) 마지막으로 옮겨진 칸이다. 거부되는 수이면 빈 목록이다.
        /// </summary>
        public static List<WalkerArrival> WalkerArrivals(Board board, ShiftPoint move)
        {
            var result = new List<WalkerArrival>();
            var sim = board.Clone();
            if (!TurnRunner.PlayTurn(sim, move)) return result;

            // 걸은 동물과 각각 마지막으로 옮겨진 칸.
            var walked = new SortedSet<int>();
            var lastPos = new Dictionary<int, Pos>();
            foreach (var e in sim.Events)
            {
                var moved = e as MovedEvent;
                if (moved == null) continue;
                lastPos[moved.Id] = moved.To;
                if (moved.Cause == MoveCause.Walk) walked.Add(moved.Id);
            }

            foreach (int id in walked)
            {
                var o = sim.Find(id);
                result.Add(new WalkerArrival(id, o != null ? o.Pos : lastPos[id]));
            }
            return result;
        }
    }
}
