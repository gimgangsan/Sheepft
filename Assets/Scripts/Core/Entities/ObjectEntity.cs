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
        public HashSet<TerrainType> AppliedThisTurn = new HashSet<TerrainType>();

        public virtual bool IsAnimal => false;

        /// <summary>나무 재질 물건이면 들불에 탄다.</summary>
        public virtual bool IsWooden => false;

        /// <summary>
        /// 지형 상호작용을 받지 않는가. 배부른 말·소가 그렇다. README 4절.
        /// </summary>
        public virtual bool IgnoresInteractions => false;

        /// <summary>undo용 복사. Id와 모든 값이 같다.</summary>
        public virtual ObjectEntity Clone()
        {
            var copy = (ObjectEntity)MemberwiseClone();
            copy.AppliedThisTurn = new HashSet<TerrainType>(AppliedThisTurn);
            return copy;
        }
    }

    public abstract class Animal : ObjectEntity
    {
        public bool Burned;

        /// <summary>마지막으로 스스로 걸은 방향. 늑대·말·소만 쓴다.</summary>
        public Dir? LastWalkDir;

        public override bool IsAnimal => true;

        /// <summary>
        /// 행동 불가. 따로 저장하지 않는 공통 판정이다. 해당하면 종료 후 반응을 하지 않는다. README 4절.
        /// 화상과 탑승은 여기서, 기절·식사는 늑대가 더한다.
        /// </summary>
        public virtual bool IsIncapacitated(Board b) => Burned || IsRiding(b);

        /// <summary>
        /// 탑승. 발견 상태인 말의 등에 타고 있다(Horse.RiderId). 같은 칸에 있기만 한 다른 동물은 탑승이 아니다.
        /// </summary>
        public bool IsRiding(Board b)
        {
            if (this is Horse) return false;
            foreach (var o in b.At(Pos).Objects)
            {
                var horse = o as Horse;
                if (horse != null && horse.IsCarrying && horse.RiderId == Id) return true;
            }
            return false;
        }
    }
}
