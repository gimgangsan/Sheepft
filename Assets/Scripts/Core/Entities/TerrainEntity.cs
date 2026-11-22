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

        /// <summary>같은 칸의 오브젝트에 효과를 주는 지형인가. 물과 들불만 그렇다.</summary>
        protected virtual bool AffectsObjects => false;

        /// <summary>이 지형이 오브젝트 하나에 주는 효과. 상태를 바꿨으면 true. 턴당 1회 기록은 OnOverlap이 맡는다.</summary>
        protected virtual bool ApplyTo(Board b, Tile t, ObjectEntity o) => false;

        /// <summary>
        /// 지형과 오브젝트의 겹침 상호작용. README 2절.
        /// 이번 턴에 이 지형 종류를 아직 적용받지 않은 오브젝트에만 적용하고 기록한다.
        /// 물 찬 양동이를 먼저, 나머지는 Id 순서로 처리한다. 도중에 지형이 바뀌면 멈춘다.
        /// </summary>
        public override bool OnOverlap(Board b, Tile t)
        {
            if (!AffectsObjects || t.Objects.Count == 0) return false;

            bool changed = false;
            var objects = t.Objects.ToArray();
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var o in objects)
                {
                    var bucket = o as Bucket;
                    bool filled = bucket != null && bucket.Filled;
                    if (filled != (pass == 0)) continue;
                    if (o.Removed || o.IgnoresInteractions || o.AppliedThisTurn.Contains(Type)) continue;
                    if (t.Terrain != this) return changed;

                    o.AppliedThisTurn.Add(Type);
                    if (ApplyTo(b, t, o)) changed = true;
                }
            }
            return changed;
        }

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

    /// <summary>물. 젖음, 화상 해제, 빈 양동이 채우기. README 5·6절.</summary>
    public sealed class Water : TerrainEntity
    {
        public override TerrainType Type => TerrainType.Water;
        protected override bool AffectsObjects => true;

        protected override bool ApplyTo(Board b, Tile t, ObjectEntity o)
        {
            var bucket = o as Bucket;
            if (bucket != null)
            {
                // 빈 양동이는 젖었든 말랐든 물 찬 양동이가 된다. 물 찬 양동이에는 젖음 상태가 없다.
                if (bucket.Filled) return false;
                b.SetFilled(bucket, true);
                b.SetWet(bucket, false);
                b.Mark(Mechanic.BucketFilled);
                return true;
            }

            bool changed = b.SetWet(o, true);
            if (changed && o is Sheep) b.Mark(Mechanic.SheepWet);
            var animal = o as Animal;
            if (animal != null && b.SetBurned(animal, false))
            {
                changed = true;
                if (o is Sheep) b.Mark(Mechanic.SheepCured);
                else if (o is Wolf) b.Mark(Mechanic.WolfCured);
            }
            return changed;
        }
    }

    /// <summary>들불. README 7절의 겹침 상호작용. 확산은 턴 단계가 맡는다.</summary>
    public sealed class Fire : TerrainEntity
    {
        public override TerrainType Type => TerrainType.Fire;
        protected override bool AffectsObjects => true;

        protected override bool ApplyTo(Board b, Tile t, ObjectEntity o)
        {
            var bucket = o as Bucket;
            if (bucket != null && bucket.Filled)
            {
                // 불을 끈다. 그 칸은 재가 되고 양동이는 젖은 빈 양동이가 된다.
                b.SetTerrain(t.Pos, TerrainType.Ash);
                b.SetFilled(bucket, false);
                b.SetWet(bucket, true);
                b.Mark(Mechanic.Extinguish);
                return true;
            }

            if (o.Wet)
            {
                if (o is Sheep) b.Mark(Mechanic.SheepDried);
                return b.SetWet(o, false);
            }

            var animal = o as Animal;
            if (animal != null)
            {
                if (!b.SetBurned(animal, true)) return false;
                if (o is Sheep) b.Mark(Mechanic.SheepBurned);
                else if (o is Wolf) b.Mark(Mechanic.WolfBurned);
                else b.Mark(Mechanic.AnimalBurned);
                return true;
            }

            b.Remove(o, RemoveCause.Burned);
            b.Mark(Mechanic.ItemBurned);
            return true;
        }
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
