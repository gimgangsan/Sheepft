using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>칸 위에 놓여 움직이는 것. 동물과 물건. README 6절.</summary>
    public abstract class ObjectEntity : TileEntity
    {
        public Pos Pos;
        public bool Wet;

        /// <summary>먹힘, 탐, 합류로 사라졌다.</summary>
        public bool Removed;

        /// <summary>이번 턴에 이미 적용받은 지형 종류. 지형 상호작용은 턴당 1회다. README 2절.</summary>
        public readonly HashSet<TerrainType> AppliedThisTurn = new HashSet<TerrainType>();

        public virtual bool IsAnimal => false;

        /// <summary>나무 재질 물건이면 들불에 탄다.</summary>
        public virtual bool IsWooden => false;
    }

    public abstract class Animal : ObjectEntity
    {
        public bool Burned;

        /// <summary>마지막으로 스스로 걸은 방향. 늑대·말·소만 쓴다.</summary>
        public Dir? LastWalkDir;

        public override bool IsAnimal => true;
    }
}
