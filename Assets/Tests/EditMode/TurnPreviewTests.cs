using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>
    /// 도착 칸 미리보기: 수를 두기 전에 스스로 걷는 동물이 턴 끝에 어디 있을지 본다.
    /// 미리본 칸은 실제로 그 수를 두었을 때의 칸과 같아야 하고, 원래 판은 바뀌지 않아야 한다.
    /// </summary>
    public class TurnPreviewTests
    {
        private static ShiftPoint Move(string text)
        {
            ShiftPoint point;
            Assert.IsTrue(ShiftPoint.TryParse(text, out point));
            return point;
        }

        /// <summary>미리본 칸이 실제로 두었을 때 그 동물의 칸과 같은지 본다.</summary>
        private static void AssertMatchesRealTurn(Board board, string move)
        {
            var arrivals = TurnPreview.WalkerArrivals(board, Move(move));
            TestLevel.Play(board, move);
            foreach (var a in arrivals)
            {
                var o = board.Find(a.Id);
                if (o != null) Assert.AreEqual(o.Pos, a.Pos, "#" + a.Id);
            }
        }

        [Test]
        public void Wolf_ShowsTheTileItWalksTo_AndLeavesTheBoardUnchanged()
        {
            var board = TestLevel.Make(
                ".W . . .S\n" +
                ".  . . .", target: 0);
            var wolf = board.Objects.OfType<Wolf>().Single();

            var arrivals = TurnPreview.WalkerArrivals(board, Move("R1:R"));

            Assert.AreEqual(1, arrivals.Count);
            Assert.AreEqual(wolf.Id, arrivals[0].Id);
            Assert.AreEqual(new Pos(0, 1), arrivals[0].Pos);
            // 원래 판은 그대로다.
            Assert.AreEqual(new Pos(0, 0), wolf.Pos);
            Assert.AreEqual(0, board.TurnsUsed);
            AssertMatchesRealTurn(board, "R1:R");
        }

        [Test]
        public void ShiftedWolf_ShowsTheTileAfterShiftAndWalk()
        {
            // 늑대가 있는 줄을 밀면 밀린 자리에서 걷는다. 미리보기는 그 결과를 보인다.
            var board = TestLevel.Make(
                ".W . . . .S\n" +
                ".  . . . .", target: 0);
            var arrivals = TurnPreview.WalkerArrivals(board, Move("C0:D"));
            Assert.AreEqual(1, arrivals.Count);
            Assert.AreNotEqual(new Pos(0, 1), arrivals[0].Pos);
            AssertMatchesRealTurn(board, "C0:D");
        }

        [Test]
        public void WolfPushedByGoat_ShowsFinalTile()
        {
            // 염소가 먼저 늑대를 밀쳐 내고, 늑대는 밀린 자리에서 양 쪽으로 걷는다.
            var board = TestLevel.Make(
                ".G .W . . .S\n" +
                ".  .  . . .", target: 0);
            var arrivals = TurnPreview.WalkerArrivals(board, Move("R1:R"));
            Assert.AreEqual(1, arrivals.Count);
            Assert.AreEqual(new Pos(0, 3), arrivals[0].Pos);
            AssertMatchesRealTurn(board, "R1:R");
        }

        [Test]
        public void HorseWithoutBasket_IsNotShown()
        {
            var board = TestLevel.Make(
                ".H . .\n" +
                ".  . .", target: 0);
            Assert.AreEqual(0, TurnPreview.WalkerArrivals(board, Move("R1:R")).Count);
        }

        [Test]
        public void WolfWithNoPath_IsNotShown()
        {
            // 양이 물 위에 있으면 경로가 없어 대기한다.
            var board = TestLevel.Make(
                ".W . ~S\n" +
                ".  . .", target: 0);
            Assert.AreEqual(0, TurnPreview.WalkerArrivals(board, Move("R1:R")).Count);
        }

        [Test]
        public void HorseWalkingToBasket_IsShown()
        {
            var board = TestLevel.Make(
                ".H . . .A\n" +
                ".  . . .", target: 0);
            var arrivals = TurnPreview.WalkerArrivals(board, Move("R1:R"));
            Assert.AreEqual(1, arrivals.Count);
            Assert.AreEqual(new Pos(0, 1), arrivals[0].Pos);
            AssertMatchesRealTurn(board, "R1:R");
        }
    }
}
