using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>칸의 종류. 두 빈칸은 규칙상 같고 화면에서만 다르다. README 1절.</summary>
    public enum TileKind
    {
        /// <summary>일반 칸.</summary>
        Normal,
        /// <summary>외곽 빈칸 `-`. 맵 바깥.</summary>
        OuterBlank,
        /// <summary>중간 빈칸 `x`. 맵 안의 구멍.</summary>
        InnerBlank
    }

    /// <summary>맵의 칸 하나. 지형 하나와 오브젝트 여럿을 담는다.</summary>
    public sealed class Tile
    {
        public Pos Pos;
        public TileKind Kind;

        /// <summary>빈칸이면 null.</summary>
        public TerrainEntity Terrain;

        /// <summary>빈칸이면 항상 비어 있다. Id 순으로 정렬해 둔다.</summary>
        public readonly List<ObjectEntity> Objects = new List<ObjectEntity>();

        public bool IsBlank => Kind != TileKind.Normal;

        public Tile(Pos pos, TileKind kind, TerrainEntity terrain)
        {
            Pos = pos;
            Kind = kind;
            Terrain = terrain;
        }
    }
}
