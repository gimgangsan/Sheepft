using NUnit.Framework;
using Sheepft.Core;
using Sheepft.Tools;

namespace Sheepft.Tests
{
    /// <summary>solver의 상태 인코딩. 되살린 맵이 원본과 같고, 이어서 두어도 같은 결과가 나와야 한다.</summary>
    public class BoardCodecTests
    {
        private const string Busy =
            "# t  shift: R0:R, R3:R  제한 9턴  목표 양 1\n" +
            ".W  .W  #    .S(w)  ~B\n" +
            ".H  .   .A   .      ^\n" +
            ".M  .   .A   .G(b)  .U\n" +
            ".   .   .S2  .      g";

        [Test]
        public void LoadedBoardMatchesOriginalAndPlaysTheSame()
        {
            var level = LevelParser.Parse(Busy);
            var a = level.Clone();
            TestLevel.Play(a, "R0:R, R3:R"); // 합류(기절), 발견, 냄새, 걷기까지 일어난 상태
            var data = BoardCodec.Encode(a);

            var b = level.Clone();
            BoardCodec.Load(b, data);
            Assert.AreEqual(LevelWriter.Write(a), LevelWriter.Write(b));
            CollectionAssert.AreEqual(data, BoardCodec.Encode(b));

            // 되살린 맵에서 이어 두어도 결과가 같다. 제한 턴이 걸리지 않게 턴 수를 맞춘다.
            b.TurnsUsed = a.TurnsUsed;
            TestLevel.Play(a, "R3:R");
            TestLevel.Play(b, "R3:R");
            Assert.AreEqual(a.Result, b.Result);
            Assert.AreEqual(a.Mechanics, b.Mechanics);
            CollectionAssert.AreEqual(BoardCodec.Encode(a), BoardCodec.Encode(b));
        }

        [Test]
        public void KeepsHiddenValues()
        {
            // 표기법에 나오지 않는 값(기절·식사 기간, 목표, 탄 것, 마지막 걸은 방향, 옮겨진 바구니)도 되살린다.
            var level = LevelParser.Parse(Busy);
            var a = level.Clone();
            TestLevel.Play(a, "R0:R, R3:R, R3:R");
            var b = level.Clone();
            BoardCodec.Load(b, BoardCodec.Encode(a));

            foreach (var o in a.Objects)
            {
                var copy = b.Find(o.Id);
                Assert.IsNotNull(copy, "#" + o.Id);
                Assert.AreEqual(o.GetType(), copy.GetType());
                Assert.AreEqual(o.Pos, copy.Pos);
                var wolf = o as Wolf;
                if (wolf != null)
                {
                    var w2 = (Wolf)copy;
                    Assert.AreEqual(wolf.Count, w2.Count);
                    Assert.AreEqual(wolf.StunLeft, w2.StunLeft);
                    Assert.AreEqual(wolf.MealLeft, w2.MealLeft);
                }
                var animal = o as Animal;
                if (animal != null) Assert.AreEqual(animal.LastWalkDir, ((Animal)copy).LastWalkDir, "#" + o.Id + " 방향");
                var seeker = o as BasketSeeker;
                if (seeker != null)
                {
                    Assert.AreEqual(seeker.TargetId, ((BasketSeeker)copy).TargetId);
                    Assert.AreEqual(seeker.Full, ((BasketSeeker)copy).Full);
                }
                var horse = o as Horse;
                if (horse != null) Assert.AreEqual(horse.RiderId, ((Horse)copy).RiderId);
            }
        }

        [Test]
        public void TerrainChangesAreKept()
        {
            // 들불 확산으로 풀이 탄 상태
            var level = TestLevel.Make("^ g .S\n. . .", target: 0);
            var a = level.Clone();
            TestLevel.Play(a, "R1:R");
            var b = level.Clone();
            BoardCodec.Load(b, BoardCodec.Encode(a));
            Assert.AreEqual(LevelWriter.Write(a), LevelWriter.Write(b));
            Assert.AreEqual(TerrainType.Fire, b.At(new Pos(0, 1)).Terrain.Type);
        }

        [Test]
        public void DifferentStatesGetDifferentKeys()
        {
            var a = TestLevel.Make(".S . .", target: 0);
            var b = a.Clone();
            TestLevel.Play(b, "R0:R");
            CollectionAssert.AreNotEqual(BoardCodec.Encode(a), BoardCodec.Encode(b));
            Assert.IsFalse(ByteArrayComparer.Instance.Equals(BoardCodec.Encode(a), BoardCodec.Encode(b)));
            Assert.IsTrue(ByteArrayComparer.Instance.Equals(BoardCodec.Encode(a), BoardCodec.Encode(a.Clone())));
        }
    }
}
