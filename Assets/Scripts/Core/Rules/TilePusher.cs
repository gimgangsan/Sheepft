namespace Sheepft.Core
{
    /// <summary>
    /// 칸 하나의 오브젝트를 한 방향으로 1칸 민다. 염소의 밀치기와 소의 밀기가 같이 쓴다.
    /// 순환하지 않고, 다음 칸이 빈칸·격자 바깥·장애물이면 제자리에 남는다. 구현 계획 2절.
    /// </summary>
    public static class TilePusher
    {
        /// <summary>from 칸의 오브젝트가 dir로 밀려 갈 칸. 밀릴 자리가 없으면 null.</summary>
        public static Pos? Destination(Board board, Pos from, Dir dir)
        {
            var dest = from.Step(dir);
            if (board.BlocksAnimal(dest)) return null;
            return dest;
        }

        /// <summary>
        /// from 칸의 오브젝트를 모두 dir로 1칸 민다. 도착 칸의 오브젝트와는 그냥 겹친다(연쇄로 밀지 않는다).
        /// 움직인 것이 있으면 true.
        /// </summary>
        public static bool Push(Board board, Pos from, Dir dir, MoveCause cause)
        {
            var tile = board.At(from);
            if (tile == null || tile.Objects.Count == 0) return false;
            var dest = Destination(board, from, dir);
            if (dest == null) return false;
            foreach (var o in tile.Objects.ToArray()) board.Move(o, dest.Value, cause);
            return true;
        }
    }
}
