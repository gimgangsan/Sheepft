using System;

namespace Sheepft.Core
{
    /// <summary>칸의 좌표 (행, 열). 왼쪽 위가 (0, 0)이다. README 10절.</summary>
    public readonly struct Pos : IEquatable<Pos>
    {
        public readonly int Row;
        public readonly int Col;

        public Pos(int row, int col)
        {
            Row = row;
            Col = col;
        }

        /// <summary>그 방향으로 한 칸 옆. 순환하지 않는다.</summary>
        public Pos Step(Dir dir)
        {
            switch (dir)
            {
                case Dir.Up: return new Pos(Row - 1, Col);
                case Dir.Right: return new Pos(Row, Col + 1);
                case Dir.Down: return new Pos(Row + 1, Col);
                default: return new Pos(Row, Col - 1);
            }
        }

        public bool Equals(Pos other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object obj) => obj is Pos other && Equals(other);
        public override int GetHashCode() => Row * 397 ^ Col;
        public static bool operator ==(Pos a, Pos b) => a.Equals(b);
        public static bool operator !=(Pos a, Pos b) => !a.Equals(b);
        public override string ToString() => "(" + Row + ", " + Col + ")";
    }
}
