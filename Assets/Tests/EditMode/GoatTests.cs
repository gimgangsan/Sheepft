using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>염소의 공격(밀치기). README 6절. 아무것도 없는 줄을 밀어 턴만 넘긴다.</summary>
    public class GoatTests
    {
        [Test]
        public void Attack_PushesDistanceOneAndTwo_OutwardInFourDirections()
        {
            var board = TestLevel.AssertAfter(
                ".  .  .  .  .  .  .\n" +
                ".  .  .  .S .  .  .\n" +
                ".  .  .  .B .  .  .\n" +
                ".  .B .  .G .  .S .\n" +
                ".  .  .  .  .  .  .\n" +
                ".  .  .  .U .  .  .\n" +
                ".  .  .  .  .  .  .\n" +
                ".  .  .  .  .  .  .",
                "R7:R",
                ".  .  .  .S .  .  .\n" +
                ".  .  .  .B .  .  .\n" +
                ".  .  .  .  .  .  .\n" +
                ".B .  .  .G .  .  .S\n" +
                ".  .  .  .  .  .  .\n" +
                ".  .  .  .  .  .  .\n" +
                ".  .  .  .U .  .  .\n" +
                ".  .  .  .  .  .  .", target: 0);
            Assert.IsTrue(board.Events.OfType<MovedEvent>().All(e => e.Cause == MoveCause.Push));
            Assert.AreEqual(5, board.Events.OfType<MovedEvent>().Count());
        }

        [Test]
        public void Attack_DoesNotWrap_ObjectsAtTheEdgeStay()
        {
            // 2칸 거리의 양은 격자 끝이라 밀릴 자리가 없다. 1칸 거리의 양도 그 뒤가 격자 밖이다.
            TestLevel.AssertAfter(
                ".S .  .G .S\n" +
                ".  .  .  .",
                "R1:R",
                ".S .  .G .S\n" +
                ".  .  .  .", target: 0);
        }

        [Test]
        public void Attack_BlankTilesAreObstacles()
        {
            // 왼쪽: 2칸 거리 양의 밀릴 칸이 x라 제자리. 오른쪽: 1칸 거리가 x라 그 너머는 공격하지 않는다.
            TestLevel.AssertAfter(
                "x .S . .G x .S .\n" +
                ". .  . .  . .  .",
                "R1:R",
                "x .S . .G x .S .\n" +
                ". .  . .  . .  .", target: 0);

            TestLevel.AssertAfter(
                "- .S . .G - .S .\n" +
                ". .  . .  . .  .",
                "R1:R",
                "- .S . .G - .S .\n" +
                ". .  . .  . .  .", target: 0);
        }

        [Test]
        public void Attack_ObstacleAtDistanceOne_HidesDistanceTwo()
        {
            TestLevel.AssertAfter(
                ". .S # .G\n" +
                ". .  . .",
                "R1:R",
                ". .S # .G\n" +
                ". .  . .", target: 0);
        }

        [Test]
        public void Attack_ObstacleAtDistanceTwo_NearObjectStays()
        {
            TestLevel.AssertAfter(
                "# .S .G .\n" +
                ". .  .  .",
                "R1:R",
                "# .S .G .\n" +
                ". .  .  .", target: 0);
        }

        [Test]
        public void Attack_DoesNotPushObjectsOnItsOwnTile()
        {
            TestLevel.AssertAfter(
                ".GS .S .  .\n" +
                ".   .  .  .",
                "R1:R",
                ".GS .  .S .\n" +
                ".   .  .  .", target: 0);
        }

        [Test]
        public void Attack_WholeTileMovesTogether()
        {
            TestLevel.AssertAfter(
                ".G .S2B .  .\n" +
                ".  .    .  .",
                "R1:R",
                ".G .  .S2B .\n" +
                ".  .  .    .", target: 0);
        }

        [Test]
        public void Attack_PushedIntoWater_GetsWet()
        {
            TestLevel.AssertAfter(
                ".G .S ~ .\n" +
                ".  .  . .",
                "R1:R",
                ".G .  ~S(w) .\n" +
                ".  .  .     .", target: 0);
        }

        [Test]
        public void Attack_BurnedGoatDoesNotAttack()
        {
            TestLevel.AssertAfter(
                ".G(b) .S . .\n" +
                ".     .  . .",
                "R1:R",
                ".G(b) .S . .\n" +
                ".     .  . .", target: 0);
        }

        [Test]
        public void Attack_GoatsPushEachOther_InRowMajorOrder()
        {
            // 앞 염소가 뒤 염소를 한 칸 밀고, 밀려난 염소는 새 자리에서 공격한다(앞 염소는 격자 끝이라 제자리).
            TestLevel.AssertAfter(
                ".G .G . .\n" +
                ".  .  . .",
                "R1:R",
                ".G .  .G .\n" +
                ".  .  .  .", target: 0);
        }

        [Test]
        public void Attack_PushesWolfAway_BeforeItWalks()
        {
            // 염소가 늑대보다 먼저다. 늑대는 한 칸 밀려난 뒤 양 쪽으로 한 칸 걷는다.
            TestLevel.AssertAfter(
                ".G .W .  .  .S\n" +
                ".  .  .  .  .",
                "R1:R",
                ".G .  .  .W .S\n" +
                ".  .  .  .  .", target: 0);
        }
    }
}
