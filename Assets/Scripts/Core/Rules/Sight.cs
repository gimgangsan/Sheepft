using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>
    /// 동물의 시야. 말의 발견과 염소의 대상 찾기가 쓴다.
    /// 빈칸·격자 바깥·장애물에서 멈추고 순환하지 않는다. 오브젝트는 시야를 가리지 않는다.
    /// </summary>
    public static class Sight
    {
        /// <summary>from에서 dir 방향으로 보이는 칸들. 가까운 칸부터다. from 자신은 넣지 않는다.</summary>
        public static IEnumerable<Pos> Ray(Board board, Pos from, Dir dir)
        {
            for (var p = from.Step(dir); !board.BlocksAnimal(p); p = p.Step(dir))
                yield return p;
        }
    }
}
