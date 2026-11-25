using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>턴 표시 `남은 턴/기준`의 오른쪽 수. 아직 받을 수 있는 가장 높은 별의 기준을 남은 턴으로 보여 준다.</summary>
    public class StarTargetTests
    {
        // 제한 10턴, 별 3개는 3턴 이내, 별 2개는 6턴 이내 → 남은 턴 기준 7, 4, 0.
        private static LevelRules Rules(int star3, int star2) =>
            new LevelRules { TurnLimit = 10, Star3Turns = star3, Star2Turns = star2 };

        [TestCase(10, 7)]
        [TestCase(8, 7)]
        [TestCase(7, 4)] // 7에서 한 수 더 두면 별 3개는 못 받는다
        [TestCase(5, 4)]
        [TestCase(4, 0)]
        [TestCase(1, 0)]
        [TestCase(0, 0)]
        public void ShowsNextReachableStarLimit(int turnsLeft, int expected)
        {
            Assert.AreEqual(expected, Rules(3, 6).StarTargetTurnsLeft(turnsLeft));
        }

        [Test]
        public void NoStarsLine_ShowsZero()
        {
            Assert.AreEqual(0, Rules(0, 0).StarTargetTurnsLeft(10));
        }

        [Test]
        public void OnlyStar3_SkipsToZeroAfterIt()
        {
            Assert.AreEqual(7, Rules(3, 0).StarTargetTurnsLeft(10));
            Assert.AreEqual(0, Rules(3, 0).StarTargetTurnsLeft(7));
        }
    }
}
