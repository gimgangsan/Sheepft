using System.Linq;
using NUnit.Framework;
using Sheepft.Core;
using Sheepft.Tools;

namespace Sheepft.Tests
{
    /// <summary>
    /// 엔딩 스테이지의 생강꽃(`P`)과 근육질 양. README 6절 "생강꽃", 8절.
    /// 양이 생강꽃과 겹치면 근육질 양이 되고, 근육질 양은 먹히지 않으며 같은 칸의 늑대를 쓰러뜨린다.
    /// 늑대를 쓰러뜨리면 승리다.
    /// </summary>
    public class SpicebushTests
    {
        private static Sheep[] AllSheep(Board board)
        {
            return board.Objects.OfType<Sheep>().OrderBy(s => s.Id).ToArray();
        }

        // ── 변신 ──

        [Test]
        public void SheepEatsSpicebushAndBecomesBuff()
        {
            // 생강꽃은 돌에 막혀 남고 양이 들어와 겹친다.
            var board = TestLevel.AssertAfter(".S .P #", "R0:R", ". .S #");
            var sheep = AllSheep(board).Single();
            Assert.IsTrue(sheep.Buff);
            Assert.IsFalse(board.Objects.OfType<Spicebush>().Any());
            Assert.IsTrue(board.Events.OfType<StatusChangedEvent>().Any(e => e.Id == sheep.Id && e.Status == StatusKind.Buff && e.On));
            Assert.IsTrue(board.Events.OfType<RemovedEvent>().Any(e => e.Cause == RemoveCause.Eaten));
            Assert.AreNotEqual(Mechanic.None, board.Mechanics & Mechanic.SheepBuff);
            Assert.AreEqual(GameResult.InProgress, board.Result);
        }

        [Test]
        public void OneSpicebushFeedsOneSheep()
        {
            // 양 무리가 겹쳐도 Id가 가장 작은 한 마리만 먹는다.
            var board = TestLevel.AssertAfter(".S2 .P #", "R0:R", ". .S2 #", target: 0);
            var sheep = AllSheep(board);
            Assert.IsTrue(sheep[0].Buff);
            Assert.IsFalse(sheep[1].Buff);
        }

        [Test]
        public void SpicebushBurnsLikeWoodenItem()
        {
            // 나무 재질 물건이라 확산으로 탄다. 생강꽃도 풀도 없으면 승리할 길이 없다.
            var board = TestLevel.Make(
                "^ .P\n" +
                ".  . ", target: 0);
            Assert.IsFalse(board.CannotWin);
            TestLevel.Play(board, "C0:D"); // 아무것도 움직이지 않는 수
            TestLevel.AssertMap(
                "^ ^\n" +
                ". .", board);
            Assert.IsTrue(board.CannotWin);
        }

        // ── 늑대 쓰러뜨리기 ──

        [Test]
        public void BuffSheepKnocksOutWolfOnSameTile_AndWins()
        {
            // 같은 칸에서 양이 생강꽃을 먹고, 그 칸의 늑대를 쓰러뜨린다. 늑대는 양을 먹지 못한다.
            var board = TestLevel.AssertAfter(".SP .W #", "R0:R", ". .S #");
            Assert.IsTrue(AllSheep(board).Single().Buff);
            Assert.IsFalse(board.Objects.OfType<Wolf>().Any());
            Assert.IsTrue(board.Events.OfType<RemovedEvent>().Any(e => e.Cause == RemoveCause.KnockedOut));
            Assert.IsFalse(board.Events.OfType<RemovedEvent>().Any(e => e.Cause == RemoveCause.Eaten && e.Id == 0));
            Assert.AreEqual(new Pos(0, 1), board.KnockoutAt);
            Assert.AreNotEqual(Mechanic.None, board.Mechanics & Mechanic.WolfKnockout);
            Assert.AreEqual(GameResult.Won, board.Result);
        }

        [Test]
        public void WolfWalkingIntoBuffSheepIsKnockedOut()
        {
            // 1턴: 양이 근육질이 되고 늑대가 다가온다. 2턴: 늑대가 양의 칸으로 걸어 들어와 쓰러진다.
            var board = TestLevel.AssertAfter(
                ".S .P #\n" +
                ".  .  .W",
                "R0:R",
                ". .S  #\n" +
                ". .W  .");
            Assert.AreEqual(GameResult.InProgress, board.Result);

            TestLevel.Play(board, "C2:D"); // 아무것도 움직이지 않는 수
            TestLevel.AssertMap(
                ". .S #\n" +
                ". .  .", board);
            Assert.AreEqual(new Pos(0, 1), board.KnockoutAt);
            Assert.AreEqual(GameResult.Won, board.Result);
        }

        [Test]
        public void PackDoesNotEatBuffSheepInRange()
        {
            // 2마리 무리는 주변 8칸을 잡지만 근육질 양은 먹지 않는다. 먹혔으면 목표 1마리에 못 미쳐 패배다.
            var board = TestLevel.Make(
                ".S .P #\n" +
                ".  .  .W2");
            TestLevel.Play(board, "R0:R");
            Assert.AreEqual(1, board.SheepCount());
            Assert.AreEqual(GameResult.InProgress, board.Result);
        }

        [Test]
        public void PlainSheepIsStillEaten()
        {
            // 생강꽃을 먹기 전의 양은 보통 양이다.
            var board = TestLevel.Make(".S .W #");
            TestLevel.Play(board, "R0:R");
            Assert.AreEqual(GameResult.Lost, board.Result);
            Assert.IsNull(board.KnockoutAt);
        }

        [Test]
        public void UndoRestoresPlainSheepAndSpicebush()
        {
            var session = new GameSession(TestLevel.Make(".SP .W #"));
            Assert.IsTrue(session.Play(new ShiftPoint(Axis.Row, 0, Dir.Right)));
            Assert.AreEqual(GameResult.Won, session.Board.Result);
            Assert.IsTrue(session.Undo());
            Assert.IsFalse(AllSheep(session.Board).Single().Buff);
            Assert.IsTrue(session.Board.Objects.OfType<Spicebush>().Any());
            Assert.IsNull(session.Board.KnockoutAt);
            Assert.AreEqual(GameResult.InProgress, session.Board.Result);
        }

        // ── 표기와 solver ──

        [Test]
        public void NotationRoundTrip()
        {
            var board = TestLevel.Make(".SP .P2 .");
            Assert.AreEqual(3, board.Objects.OfType<Spicebush>().Count());
            TestLevel.AssertMap(".SP .P2 .", board);
        }

        [Test]
        public void MechanicNamesParse()
        {
            Mechanic m;
            string unknown;
            Assert.IsTrue(MechanicText.TryParse("SheepBuff WolfKnockout", out m, out unknown), unknown);
            Assert.AreEqual(Mechanic.SheepBuff | Mechanic.WolfKnockout, m);
        }

        [Test]
        public void CodecKeepsSpicebushAndBuff()
        {
            var level = TestLevel.Make(
                ".S .P #  .P\n" +
                ".  .  .W .");
            var a = level.Clone();
            TestLevel.Play(a, "R0:R"); // 양이 근육질이 됨. 3열의 생강꽃은 순환해 0열로 간다.
            var b = level.Clone();
            BoardCodec.Load(b, BoardCodec.Encode(a));
            Assert.AreEqual(LevelWriter.Write(a), LevelWriter.Write(b));
            Assert.IsTrue(AllSheep(b).Single().Buff);
            Assert.AreEqual(1, b.Objects.OfType<Spicebush>().Count());
            CollectionAssert.AreEqual(BoardCodec.Encode(a), BoardCodec.Encode(b));
        }

        [Test]
        public void SolverFindsKnockoutWin()
        {
            var level = LevelParser.Parse(
                "# t  shift: R0:RL  제한 3턴\n" +
                ".S .P #  .W");
            // 1턴: 양이 생강꽃 칸에 들어가 근육질이 되고, 순환해 바로 옆에 온 늑대가 걸어 들어와 쓰러진다.
            var report = Solver.Solve(level);
            Assert.AreEqual(1, report.OptimalTurns, "해를 찾지 못했거나 길이가 다르다");
        }
    }
}
