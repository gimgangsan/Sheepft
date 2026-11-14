using System;
using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>
    /// 정리 절차. README 2절, 구현 계획 4절.
    /// shift, 동물 한 마리의 행동, 들불 확산, 기절·식사 해제가 끝날 때마다 돌린다.
    /// </summary>
    public static class Settler
    {
        private const int MaxRounds = 1000;

        private static readonly AutoReaction[] AutoOrder =
        {
            AutoReaction.WolfCatch,
            AutoReaction.HorseEat,
            AutoReaction.HorseSpot,
            AutoReaction.CowSmell,
        };

        /// <summary>
        /// 바뀌는 것이 없을 때까지 겹침 상호작용과 자동 반응을 반복한다.
        /// 패배가 성립하면 바로 끝낸다.
        /// </summary>
        public static void Settle(Board board)
        {
            for (int round = 0; round < MaxRounds; round++)
            {
                bool changed = false;

                // 1. 겹침 상호작용. 같은 칸에서는 지형이 먼저, 그다음 오브젝트끼리.
                foreach (var tile in board.Tiles)
                {
                    if (tile.IsBlank) continue;
                    if (tile.Terrain.OnOverlap(board, tile)) changed = true;
                    if (board.Result == GameResult.Lost) return;

                    if (tile.Objects.Count == 0) continue;
                    foreach (var o in tile.Objects.ToArray())
                    {
                        if (o.Removed) continue;
                        if (o.OnOverlap(board, tile)) changed = true;
                        if (board.Result == GameResult.Lost) return;
                    }
                }

                // 2. 자동 반응. 종류 순서로 한 바퀴. 같은 종류는 그 차례가 시작될 때의 위치 기준 행 우선.
                foreach (var kind in AutoOrder)
                {
                    foreach (var o in new List<ObjectEntity>(board.Objects))
                    {
                        if (o.Removed) continue;
                        if (o.TryAutoReact(board, kind)) changed = true;
                        if (board.Result == GameResult.Lost) return;
                    }
                }

                if (!changed) return;
                board.Events.Add(new StepEndEvent());
            }
            throw new InvalidOperationException("정리 절차가 " + MaxRounds + "바퀴 안에 끝나지 않았다.");
        }
    }
}
