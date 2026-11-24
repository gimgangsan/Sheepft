using System;
using System.Collections.Generic;
using Sheepft.Core;

namespace Sheepft.Tools
{
    /// <summary>
    /// 맵의 진행 상태를 바이트 배열로 줄이고 되살린다. solver가 상태 수백만 개를 보관하는 형식이자 사전의 키다.
    /// 같은 바이트면 같은 상태다.
    ///
    /// 담는 것: 실제 칸마다 지형 종류, 오브젝트마다 종류·Id·위치와 모든 상태 값.
    /// 담지 않는 것: 빈칸 배치와 규칙(레벨마다 같음), 턴 수, AppliedThisTurn(턴 시작마다 비움), 이벤트.
    /// 오브젝트는 Board.Objects 순서(칸 행 우선, 같은 칸은 Id 순)로 적으므로 같은 상태는 늘 같은 바이트가 된다.
    /// </summary>
    public static class BoardCodec
    {
        private const byte KindSheep = 0, KindWolf = 1, KindGoat = 2, KindHorse = 3, KindCow = 4, KindBasket = 5, KindBucket = 6, KindSpicebush = 7;

        private const byte FlagWet = 1, FlagBurned = 2, FlagFilled = 4, FlagFull = 8, FlagCarried = 16, FlagHasDir = 32;

        /// <summary>양의 근육질. 양동이의 FlagFilled와 같은 비트지만 종류가 달라 겹치지 않는다(8비트를 이미 다 쓴다).</summary>
        private const byte FlagBuff = FlagFilled;

        [ThreadStatic] private static byte[] buffer;

        public static byte[] Encode(Board board)
        {
            if (buffer == null) buffer = new byte[256];
            int n = 0;

            for (int r = 0; r < board.Height; r++)
            {
                for (int c = 0; c < board.Width; c++)
                {
                    var t = board.At(new Pos(r, c));
                    if (t.IsBlank) continue;
                    Put(ref n, (byte)t.Terrain.Type);
                }
            }

            for (int r = 0; r < board.Height; r++)
            {
                for (int c = 0; c < board.Width; c++)
                {
                    var objects = board.At(new Pos(r, c)).Objects;
                    for (int i = 0; i < objects.Count; i++)
                    {
                        var o = objects[i];
                        Put(ref n, KindOf(o));
                        Put(ref n, Small(o.Id, "Id"));
                        Put(ref n, (byte)r);
                        Put(ref n, (byte)c);

                        byte flags = 0;
                        if (o.Wet) flags |= FlagWet;
                        var animal = o as Animal;
                        if (animal != null)
                        {
                            if (animal.Burned) flags |= FlagBurned;
                            if (animal.LastWalkDir != null) flags |= (byte)(FlagHasDir | ((int)animal.LastWalkDir.Value << 6));
                        }
                        var bucket = o as Bucket;
                        if (bucket != null && bucket.Filled) flags |= FlagFilled;
                        var sheep = o as Sheep;
                        if (sheep != null && sheep.Buff) flags |= FlagBuff;
                        var seeker = o as BasketSeeker;
                        if (seeker != null && seeker.Full) flags |= FlagFull;
                        var basket = o as FruitBasket;
                        if (basket != null && basket.Carried) flags |= FlagCarried;
                        Put(ref n, flags);

                        var wolf = o as Wolf;
                        if (wolf != null)
                        {
                            Put(ref n, Small(wolf.Count, "늑대 마리 수"));
                            Put(ref n, Small(wolf.StunLeft, "기절 기간"));
                            Put(ref n, Small(wolf.MealLeft, "식사 기간"));
                        }
                        if (seeker != null) Put(ref n, Small(seeker.TargetId + 1, "목표 Id"));
                        var horse = o as Horse;
                        if (horse != null) Put(ref n, Small(horse.RiderId + 1, "탄 것 Id"));
                    }
                }
            }

            var result = new byte[n];
            Buffer.BlockCopy(buffer, 0, result, 0, n);
            return result;
        }

        /// <summary>
        /// work를 data의 상태로 되돌린다. work는 data를 만든 레벨과 같은 맵(빈칸 배치)이어야 한다.
        /// 턴 수는 0, 결과는 진행 중으로 돌리고 이벤트와 기믹 기록을 비운다.
        /// </summary>
        public static void Load(Board work, byte[] data)
        {
            int n = 0;
            for (int r = 0; r < work.Height; r++)
            {
                for (int c = 0; c < work.Width; c++)
                {
                    var t = work.At(new Pos(r, c));
                    t.Objects.Clear();
                    if (t.IsBlank) continue;
                    var type = (TerrainType)data[n++];
                    if (t.Terrain.Type != type) t.Terrain = TerrainEntity.Create(type);
                }
            }

            while (n < data.Length)
            {
                byte kind = data[n++];
                int id = data[n++];
                var pos = new Pos(data[n++], data[n++]);
                byte flags = data[n++];

                ObjectEntity o;
                switch (kind)
                {
                    case KindSheep: o = new Sheep { Buff = (flags & FlagBuff) != 0 }; break;
                    case KindSpicebush: o = new Spicebush(); break;
                    case KindWolf:
                        o = new Wolf { Count = data[n++], StunLeft = data[n++], MealLeft = data[n++] };
                        break;
                    case KindGoat: o = new Goat(); break;
                    case KindHorse: o = new Horse(); break;
                    case KindCow: o = new Cow(); break;
                    case KindBasket: o = new FruitBasket { Carried = (flags & FlagCarried) != 0 }; break;
                    case KindBucket: o = new Bucket { Filled = (flags & FlagFilled) != 0 }; break;
                    default: throw new InvalidOperationException("알 수 없는 오브젝트 종류 " + kind);
                }
                o.Id = id;
                o.Pos = pos;
                o.Wet = (flags & FlagWet) != 0;
                var animal = o as Animal;
                if (animal != null)
                {
                    animal.Burned = (flags & FlagBurned) != 0;
                    animal.LastWalkDir = (flags & FlagHasDir) != 0 ? (Dir)(flags >> 6) : (Dir?)null;
                }
                var seeker = o as BasketSeeker;
                if (seeker != null)
                {
                    seeker.Full = (flags & FlagFull) != 0;
                    seeker.TargetId = data[n++] - 1;
                }
                var horse = o as Horse;
                if (horse != null) horse.RiderId = data[n++] - 1;

                work.At(pos).Objects.Add(o); // 적은 순서가 이미 칸 안의 Id 순서다.
            }

            work.TurnsUsed = 0;
            work.Result = GameResult.InProgress;
            work.TurnEnding = false;
            work.KnockoutAt = null;
            work.Events.Clear();
            work.Mechanics = Mechanic.None;
        }

        private static byte KindOf(ObjectEntity o)
        {
            switch (o)
            {
                case Sheep _: return KindSheep;
                case Wolf _: return KindWolf;
                case Goat _: return KindGoat;
                case Horse _: return KindHorse;
                case Cow _: return KindCow;
                case FruitBasket _: return KindBasket;
                case Bucket _: return KindBucket;
                case Spicebush _: return KindSpicebush;
                default: throw new ArgumentException("인코딩할 수 없는 오브젝트: " + o.GetType().Name);
            }
        }

        private static byte Small(int value, string what)
        {
            if (value < 0 || value > 255) throw new InvalidOperationException(what + " " + value + "는 0–255를 벗어나 인코딩할 수 없다.");
            return (byte)value;
        }

        private static void Put(ref int n, byte b)
        {
            if (n == buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
            buffer[n++] = b;
        }
    }

    /// <summary>바이트 배열을 내용으로 비교한다. 상태 사전의 키 비교에 쓴다.</summary>
    public sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new ByteArrayComparer();

        public bool Equals(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public int GetHashCode(byte[] a)
        {
            // FNV-1a
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < a.Length; i++) h = (h ^ a[i]) * 16777619;
                return (int)h;
            }
        }
    }
}
