using System;

namespace Sheepft.Core
{
    public enum TerrainType
    {
        Ground,
        Stone,
        Fence,
        Tree,
        Water,
        Fire,
        Grass,
        Ash
    }

    /// <summary>
    /// 지형. 칸마다 정확히 하나이고 움직이지 않는다. README 5절.
    /// 지형이 바뀌면 인스턴스를 새로 만들어 교체한다. 빈칸은 지형이 아니다.
    /// </summary>
    public abstract class TerrainEntity : TileEntity
    {
        public abstract TerrainType Type { get; }

        /// <summary>장애물이면 shift, 밀치기, 걷기, 시야를 모두 막는다.</summary>
        public virtual bool IsObstacle => false;

        /// <summary>나무 재질이면 확산으로 들불이 된다.</summary>
        public virtual bool IsWooden => false;

        /// <summary>이 지형이 오브젝트 하나에 주는 효과. 턴당 1회 기록은 부르는 쪽이 맡는다.</summary>
        public virtual void ApplyTo(Board b, Tile t, ObjectEntity o) { }

        public static TerrainEntity Create(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Ground: return new Ground();
                case TerrainType.Stone: return new Stone();
                case TerrainType.Fence: return new Fence();
                case TerrainType.Tree: return new Tree();
                case TerrainType.Water: return new Water();
                case TerrainType.Fire: return new Fire();
                case TerrainType.Grass: return new Grass();
                case TerrainType.Ash: return new Ash();
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }

    public sealed class Ground : TerrainEntity
    {
        public override TerrainType Type => TerrainType.Ground;
    }

    public sealed class Stone : TerrainEntity
    {
        public override TerrainType Type => TerrainType.Stone;
        public override bool IsObstacle => true;
    }

    public sealed class Fence : TerrainEntity
    {
        public override TerrainType Type => TerrainType.Fence;
        public override bool IsObstacle => true;
        public override bool IsWooden => true;
    }

    public sealed class Tree : TerrainEntity
    {
        public override TerrainType Type => TerrainType.Tree;
        public override bool IsObstacle => true;
        public override bool IsWooden => true;
    }

    public sealed class Water : TerrainEntity
    {
        public override TerrainType Type => TerrainType.Water;
    }

    public sealed class Fire : TerrainEntity
    {
        public override TerrainType Type => TerrainType.Fire;
    }

    public sealed class Grass : TerrainEntity
    {
        public override TerrainType Type => TerrainType.Grass;
        public override bool IsWooden => true;
    }

    public sealed class Ash : TerrainEntity
    {
        public override TerrainType Type => TerrainType.Ash;
    }
}
