using System;

namespace Sheepft.Core
{
    /// <summary>
    /// shift 포인트 하나 = 행이나 열 하나 + 방향 하나. README 3절.
    /// 플레이어의 수와 풀이 수순도 같은 타입을 쓴다.
    /// </summary>
    public readonly struct ShiftPoint : IEquatable<ShiftPoint>
    {
        /// <summary>Row면 Dir은 Right/Left, Col이면 Up/Down만 쓴다.</summary>
        public readonly Axis Axis;
        public readonly int Index;
        public readonly Dir Dir;

        public ShiftPoint(Axis axis, int index, Dir dir)
        {
            bool horizontal = dir == Dir.Right || dir == Dir.Left;
            if ((axis == Axis.Row) != horizontal)
                throw new ArgumentException("행은 Right/Left, 열은 Up/Down만 쓴다.", nameof(dir));
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            Axis = axis;
            Index = index;
            Dir = dir;
        }

        public bool Equals(ShiftPoint other) => Axis == other.Axis && Index == other.Index && Dir == other.Dir;
        public override bool Equals(object obj) => obj is ShiftPoint other && Equals(other);
        public override int GetHashCode() => ((int)Axis * 31 + Index) * 31 + (int)Dir;
        public static bool operator ==(ShiftPoint a, ShiftPoint b) => a.Equals(b);
        public static bool operator !=(ShiftPoint a, ShiftPoint b) => !a.Equals(b);

        /// <summary>
        /// README 10절 표기의 수 하나를 읽는다. 예: `R1:R`, `C2:U`. 방향은 하나만 적는다.
        /// 형식이 틀리거나 행·열과 방향이 맞지 않으면 false.
        /// </summary>
        public static bool TryParse(string text, out ShiftPoint point)
        {
            point = default(ShiftPoint);
            if (text == null) return false;
            text = text.Trim();
            int colon = text.IndexOf(':');
            if (colon < 2 || colon != text.Length - 2) return false;
            if (text[0] != 'R' && text[0] != 'C') return false;

            int index = 0;
            for (int i = 1; i < colon; i++)
            {
                if (text[i] < '0' || text[i] > '9' || index > 100000) return false;
                index = index * 10 + (text[i] - '0');
            }

            Dir dir;
            if (!Notation.TryDir(text[colon + 1], out dir)) return false;
            var axis = text[0] == 'R' ? Axis.Row : Axis.Col;
            bool horizontal = dir == Dir.Right || dir == Dir.Left;
            if ((axis == Axis.Row) != horizontal) return false;

            point = new ShiftPoint(axis, index, dir);
            return true;
        }

        /// <summary>README 10절 표기. 예: `R1:R`, `C2:U`</summary>
        public override string ToString() =>
            (Axis == Axis.Row ? "R" : "C") + Index + ":" + Notation.DirChar(Dir);
    }
}
