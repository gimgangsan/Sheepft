using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>
    /// 들불 확산. 종료 후 반응의 마지막 단계다. README 7절.
    /// 칸 하나의 행동이 아니라, 확산이 시작될 때 있던 들불이 한꺼번에 번진다.
    /// </summary>
    public static class FireSpread
    {
        /// <summary>
        /// 들불마다 주변 8칸(순환 없음)의 나무 재질 지형을 들불로 바꾸고,
        /// 마른 나무 재질 물건을 파괴하며 그 칸도 들불로 바꾼다. 물 칸은 들불이 되지 않는다.
        /// 바뀐 것이 있으면 true.
        /// </summary>
        public static bool Spread(Board board)
        {
            var fires = new List<Pos>();
            foreach (var tile in board.Tiles)
                if (!tile.IsBlank && tile.Terrain.Type == TerrainType.Fire) fires.Add(tile.Pos);
            if (fires.Count == 0) return false;

            // 불붙을 칸을 모아 두었다가 한꺼번에 바꾼다. 새로 생긴 들불은 다음 턴부터 번진다.
            var ignite = new List<Pos>();
            bool changed = false;
            foreach (var fire in fires)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        var tile = board.At(new Pos(fire.Row + dr, fire.Col + dc));
                        if (tile == null || tile.IsBlank) continue;

                        bool burn = tile.Terrain.IsWooden;
                        foreach (var o in tile.Objects.ToArray())
                        {
                            if (!BurnsInSpread(o)) continue;
                            board.Remove(o, RemoveCause.Burned);
                            burn = true;
                            changed = true;
                        }

                        var type = tile.Terrain.Type;
                        if (burn && type != TerrainType.Fire && type != TerrainType.Water && !ignite.Contains(tile.Pos))
                            ignite.Add(tile.Pos);
                    }
                }
            }

            foreach (var p in ignite)
            {
                board.SetTerrain(p, TerrainType.Fire);
                changed = true;
            }
            return changed;
        }

        /// <summary>마른 나무 재질 물건(과일 바구니, 빈 양동이)만 확산으로 탄다. 동물은 직접 해치지 않는다.</summary>
        private static bool BurnsInSpread(ObjectEntity o)
        {
            if (o.IsAnimal || !o.IsWooden || o.Wet) return false;
            var bucket = o as Bucket;
            return bucket == null || !bucket.Filled;
        }
    }
}
