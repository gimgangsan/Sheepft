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
            // 도중에 목록이 바뀌어도 되게 오브젝트를 옮겨 담아 두고 도는 작업용 목록. 호출마다 하나만 만든다.
            List<ObjectEntity> buffer = null;

            for (int round = 0; round < MaxRounds; round++)
            {
                bool changed = false;

                // 1. 겹침 상호작용. 같은 칸에서는 지형이 먼저, 그다음 오브젝트끼리.
                // 한 칸의 일이 다른 칸에 영향을 줄 수 있으므로(소가 먹은 바구니가 다른 말의 목표인 경우 등)
                // 바뀌는 것이 없을 때까지 훑은 뒤 자동 반응으로 넘어간다.
                for (int pass = 0; ; pass++)
                {
                    if (pass >= MaxRounds) throw new InvalidOperationException("겹침 상호작용이 끝나지 않았다.");
                    bool passChanged = false;
                    foreach (var tile in board.Tiles)
                    {
                        if (tile.IsBlank) continue;
                        if (tile.Terrain.OnOverlap(board, tile)) passChanged = true;
                        if (board.Result == GameResult.Lost) return;

                        // 겹침 상호작용을 하는 오브젝트(늑대, 말, 소)가 있는 칸만 본다. 나머지는 하는 일이 없다.
                        if (!HasOverlapActor(tile)) continue;
                        if (buffer == null) buffer = new List<ObjectEntity>();
                        buffer.Clear();
                        buffer.AddRange(tile.Objects);
                        for (int i = 0; i < buffer.Count; i++)
                        {
                            var o = buffer[i];
                            if (o.Removed) continue;
                            if (o.OnOverlap(board, tile)) passChanged = true;
                            if (board.Result == GameResult.Lost) return;
                        }
                    }
                    if (!passChanged) break;
                    changed = true;
                }

                // 2. 자동 반응. 종류 순서로 한 바퀴. 같은 종류는 그 차례가 시작될 때의 위치 기준 행 우선.
                // 자동 반응을 하는 것은 늑대(잡기), 말(먹기, 발견), 소(냄새)뿐이므로 그것만 모은다.
                // 자동 반응 중에 자리를 옮기는 것은 말이 먹으며 내려놓는 짐뿐이다. 그래서 차례가 시작될 때의
                // 위치 순서는 한 번 모은 목록과 같고, 말이 먹은 뒤의 소 차례만 다시 모은다(짐이 소일 수 있다).
                if (buffer == null) buffer = new List<ObjectEntity>();
                CollectReactors(board, buffer);
                bool horseAte = false;
                foreach (var kind in AutoOrder)
                {
                    if (kind == AutoReaction.CowSmell && horseAte) CollectReactors(board, buffer);
                    for (int i = 0; i < buffer.Count; i++)
                    {
                        var o = buffer[i];
                        if (o.Removed || !ReactsTo(o, kind)) continue;
                        if (o.TryAutoReact(board, kind))
                        {
                            changed = true;
                            if (kind == AutoReaction.HorseEat) horseAte = true;
                        }
                        if (board.Result == GameResult.Lost) return;
                    }
                }

                if (!changed) return;
                board.Events.Add(new StepEndEvent());
            }
            throw new InvalidOperationException("정리 절차가 " + MaxRounds + "바퀴 안에 끝나지 않았다.");
        }

        /// <summary>OnOverlap을 쓰는 오브젝트가 칸에 있는가. 늑대(합류), 말(태우기, 목표 상실), 소(먹기, 목표 상실).</summary>
        private static bool HasOverlapActor(Tile tile)
        {
            var objects = tile.Objects;
            for (int i = 0; i < objects.Count; i++)
            {
                var o = objects[i];
                if (o is Wolf || o is Horse || o is Cow) return true;
            }
            return false;
        }

        /// <summary>자동 반응을 하는 오브젝트(늑대, 말, 소)를 지금 위치 순서(행 우선, 같은 칸은 Id 순)로 모은다.</summary>
        private static void CollectReactors(Board board, List<ObjectEntity> into)
        {
            into.Clear();
            foreach (var o in board.Objects)
                if (o is Wolf || o is Horse || o is Cow) into.Add(o);
        }

        /// <summary>그 종류의 자동 반응을 하는 오브젝트인가. 나머지는 TryAutoReact가 늘 false다.</summary>
        private static bool ReactsTo(ObjectEntity o, AutoReaction kind)
        {
            switch (kind)
            {
                case AutoReaction.WolfCatch: return o is Wolf;
                case AutoReaction.HorseEat:
                case AutoReaction.HorseSpot: return o is Horse;
                case AutoReaction.CowSmell: return o is Cow;
                default: return true;
            }
        }
    }
}
