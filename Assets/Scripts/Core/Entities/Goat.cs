using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>염소. 턴마다 상하좌우 1·2칸 거리의 오브젝트를 바깥으로 1칸씩 밀쳐낸다. README 6절.</summary>
    public sealed class Goat : Animal
    {
        private static readonly Dir[] Dirs = { Dir.Up, Dir.Right, Dir.Down, Dir.Left };

        /// <summary>
        /// 종료 후 반응: 공격. 시작할 때 4방향의 대상을 한꺼번에 정한 뒤,
        /// 상 > 우 > 하 > 좌 순으로, 각 방향은 먼 칸부터 민다.
        /// 장애물과 빈칸 너머는 보지 못하고, 밀릴 칸이 막혀 있으면 그 칸의 오브젝트는 제자리에 남는다.
        /// 염소와 같은 칸의 오브젝트는 밀지 않는다.
        /// </summary>
        public override void AfterShift(Board b)
        {
            var pushes = new List<KeyValuePair<ObjectEntity[], Pos>>();
            var attacked = new List<Dir>();
            foreach (var dir in Dirs)
            {
                int before = pushes.Count;
                Pos? near = null, far = null;
                foreach (var p in Sight.Ray(b, Pos, dir))
                {
                    if (near == null) { near = p; continue; }
                    far = p;
                    break;
                }
                // 1칸 거리가 막혀 있으면 그 방향은 공격하지 않는다.
                // 2칸 거리가 막혀 있으면 1칸 거리의 오브젝트가 밀릴 자리가 없다.
                if (near == null || far == null) continue;

                var beyond = TilePusher.Destination(b, far.Value, dir);
                if (beyond != null) Add(pushes, b, far.Value, beyond.Value);
                Add(pushes, b, near.Value, far.Value);
                if (pushes.Count > before) attacked.Add(dir);
            }

            if (pushes.Count > 0) b.Events.Add(new AttackedEvent(Id, attacked.ToArray()));
            foreach (var push in pushes)
                foreach (var o in push.Key)
                    b.Move(o, push.Value, MoveCause.Push);
        }

        private static void Add(List<KeyValuePair<ObjectEntity[], Pos>> pushes, Board b, Pos from, Pos to)
        {
            var objects = b.At(from).Objects;
            if (objects.Count > 0) pushes.Add(new KeyValuePair<ObjectEntity[], Pos>(objects.ToArray(), to));
        }
    }
}
