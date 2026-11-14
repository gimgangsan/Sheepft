using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>들불 확산. README 7절. 아무것도 없는 줄을 밀어 턴만 넘긴다.</summary>
    public class FireSpreadTests
    {
        [Test]
        public void Spread_BurnsWoodenTerrainInEightNeighbours_OneStepPerTurn()
        {
            var board = TestLevel.AssertAfter(
                "^ . g\n" +
                ". g .\n" +
                ". . .",
                "R2:R",
                "^ . g\n" +
                ". ^ .\n" +
                ". . .", target: 0);
            Assert.IsFalse(board.CannotWin);
            Assert.IsTrue(board.Events.OfType<TerrainChangedEvent>().Any(e => e.To == TerrainType.Fire));

            // 새로 생긴 들불은 다음 턴부터 번진다.
            TestLevel.Play(board, "R2:R");
            TestLevel.AssertMap(
                "^ . ^\n" +
                ". ^ .\n" +
                ". . .", board);
            Assert.IsTrue(board.CannotWin);
            Assert.AreEqual(GameResult.InProgress, board.Result);
        }

        [Test]
        public void Spread_BurnsFenceAndTree()
        {
            var board = TestLevel.AssertAfter(
                "^ f t\n" +
                ". . .",
                "R1:R",
                "^ ^ t\n" +
                ". . .", target: 0);
            TestLevel.Play(board, "R1:R");
            TestLevel.AssertMap(
                "^ ^ ^\n" +
                ". . .", board);
        }

        [Test]
        public void Spread_DoesNotReachTwoTilesAway_OrWrap()
        {
            TestLevel.AssertAfter(
                "^ . g g\n" +
                ". . g g\n" +
                "g g g g",
                "R1:R",
                "^ . g g\n" +
                ". . g g\n" +
                "g g g g", target: 0);
        }

        [Test]
        public void Spread_DestroysDryWoodenItems_AndIgnitesTheirTile()
        {
            var board = TestLevel.AssertAfter(
                "^ .A    .\n" +
                ". .B(w) .U\n" +
                ". .     .",
                "R2:R, R2:R",
                "^ ^     .\n" +
                ". .B(w) .U\n" +
                ". .     .", target: 0);

            TestLevel.AssertAfter(
                "^ .B\n" +
                ". .",
                "R1:R",
                "^ ^\n" +
                ". .", target: 0);
        }

        [Test]
        public void Spread_WaterTileDoesNotIgnite_ButDryItemOnItIsDestroyed()
        {
            // 정리 절차 없이 확산만 돌린다. 물 위의 마른 바구니는 한 턴에 물을 두 번 만난 경우에 생긴다.
            var board = TestLevel.Make("^ ~A", target: 0);
            Assert.IsTrue(FireSpread.Spread(board));
            TestLevel.AssertMap("^ ~", board);
        }

        [Test]
        public void Spread_AnimalOnIgnitedTile_GetsFireInteraction()
        {
            // 확산은 동물을 직접 해치지 않지만, 서 있는 칸이 들불이 되면 그 순간 들불 상호작용을 받는다.
            TestLevel.AssertAfter(
                "^ gS\n" +
                ". .",
                "R1:R",
                "^ ^S(b)\n" +
                ". .", target: 0);

            TestLevel.AssertAfter(
                "^ gS(w)\n" +
                ". .",
                "R1:R",
                "^ ^S\n" +
                ". .", target: 0);
        }

        [Test]
        public void Spread_AnimalsNextToFire_AreNotHarmed()
        {
            TestLevel.AssertAfter(
                "^ .S\n" +
                ". .",
                "R1:R",
                "^ .S\n" +
                ". .", target: 0);
        }

        [Test]
        public void Spread_FilledBucketOnBurningGrass_PutsItOutButLeavesAsh()
        {
            // README 설계 메모: 물 찬 양동이는 풀을 지키지 못한다.
            TestLevel.AssertAfter(
                "^ gU\n" +
                ". .",
                "R1:R",
                "^ _B(w)\n" +
                ". .", target: 0);
        }

        [Test]
        public void Spread_NothingToBurn_ChangesNothing()
        {
            var board = TestLevel.Make("^ . .\n. # ~", target: 0);
            Assert.IsFalse(FireSpread.Spread(board));
            Assert.AreEqual(0, board.Events.Count);
        }
    }
}
