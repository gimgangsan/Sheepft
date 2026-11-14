using System;

namespace Sheepft.Core
{
    /// <summary>README 10절의 기호표. 파서와 writer가 함께 쓴다.</summary>
    public static class Notation
    {
        public const char OuterBlank = '-';
        public const char InnerBlank = 'x';

        public static bool TryTerrain(char ch, out TerrainType type)
        {
            switch (ch)
            {
                case '.': type = TerrainType.Ground; return true;
                case '#': type = TerrainType.Stone; return true;
                case '~': type = TerrainType.Water; return true;
                case '^': type = TerrainType.Fire; return true;
                case 'g': type = TerrainType.Grass; return true;
                case 'f': type = TerrainType.Fence; return true;
                case 't': type = TerrainType.Tree; return true;
                case '_': type = TerrainType.Ash; return true;
                default: type = TerrainType.Ground; return false;
            }
        }

        public static char TerrainChar(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Ground: return '.';
                case TerrainType.Stone: return '#';
                case TerrainType.Water: return '~';
                case TerrainType.Fire: return '^';
                case TerrainType.Grass: return 'g';
                case TerrainType.Fence: return 'f';
                case TerrainType.Tree: return 't';
                case TerrainType.Ash: return '_';
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public static char ObjectChar(ObjectEntity o)
        {
            switch (o)
            {
                case Sheep _: return 'S';
                case Wolf _: return 'W';
                case Goat _: return 'G';
                case Horse _: return 'H';
                case Cow _: return 'M';
                case FruitBasket _: return 'A';
                case Bucket bucket: return bucket.Filled ? 'U' : 'B';
                default: throw new ArgumentException("표기법에 없는 오브젝트: " + o.GetType().Name, nameof(o));
            }
        }

        public static char DirChar(Dir dir)
        {
            switch (dir)
            {
                case Dir.Up: return 'U';
                case Dir.Right: return 'R';
                case Dir.Down: return 'D';
                default: return 'L';
            }
        }

        public static bool TryDir(char ch, out Dir dir)
        {
            switch (ch)
            {
                case 'U': dir = Dir.Up; return true;
                case 'R': dir = Dir.Right; return true;
                case 'D': dir = Dir.Down; return true;
                case 'L': dir = Dir.Left; return true;
                default: dir = Dir.Up; return false;
            }
        }
    }
}
