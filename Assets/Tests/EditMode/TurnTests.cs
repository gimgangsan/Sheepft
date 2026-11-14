using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>승리, 패배, 승리 불가, undo용 복사. README 2절(판정 시점), 8절.</summary>
    public class TurnTests
    {
        private static readonly ShiftPoint R0Right = new ShiftPoint(Axis.Row, 0, Dir.Right);
        private static readonly ShiftPoint R0Left = new ShiftPoint(Axis.Row, 0, Dir.Left);
        private static readonly ShiftPoint R1Right = new ShiftPoint(Axis.Row, 1, Dir.Right);

        private static Board Level(string header, string grid)
        {
            return LevelParser.Parse("# t  " + header + "\n" + grid);
        }

        // ── 승리 ──

        [Test]
        public void Win_WhenSheepReachesGrass()
        {
            var board = Level("shift: R0:RL  제한 3턴", ".S g .");
            Assert.AreEqual(GameResult.InProgress, board.Result);
            Assert.IsTrue(TurnRunner.PlayTurn(board, R0Right));
            Assert.AreEqual(GameResult.Won, board.Result);
            Assert.AreEqual(1, board.TurnsUsed);
        }

        [Test]
        public void AfterGameEnds_MovesAreRejected()
        {
            var board = Level("shift: R0:RL  제한 3턴", ".S g .");
            TurnRunner.PlayTurn(board, R0Right);
            Assert.IsFalse(TurnRunner.PlayTurn(board, R0Right));
            Assert.AreEqual(1, board.TurnsUsed);
            TestLevel.AssertMap(". gS .", board);
        }

        [Test]
        public void Win_WetOrBurnedSheepDoNotCount()
        {
            var wet = Level("shift: R0:RL  제한 3턴", ".S(w) g .");
            TurnRunner.PlayTurn(wet, R0Right);
            Assert.AreEqual(GameResult.InProgress, wet.Result);
            Assert.AreEqual(0, wet.SheepOnGrass());

            var burned = Level("shift: R0:RL  제한 3턴", ".S(b) g .");
            TurnRunner.PlayTurn(burned, R0Right);
            Assert.AreEqual(GameResult.InProgress, burned.Result);
        }

        [Test]
        public void Win_NeedsTargetNumberOfSheep()
        {
            var board = Level("shift: R0:R, R1:R  제한 5턴", ".S g\n.S g");
            Assert.AreEqual(2, board.Rules.TargetSheep);
            TurnRunner.PlayTurn(board, R0Right);
            Assert.AreEqual(GameResult.InProgress, board.Result);
            TurnRunner.PlayTurn(board, R1Right);
            Assert.AreEqual(GameResult.Won, board.Result);
        }

        [Test]
        public void Win_TargetCanBeLessThanAllSheep()
        {
            var board = Level("shift: R0:R, R1:R  제한 5턴  목표 양 1", ".S g\n.S g");
            TurnRunner.PlayTurn(board, R0Right);
            Assert.AreEqual(GameResult.Won, board.Result);
        }

        [Test]
        public void Win_MergedFlockCountsEverySheep()
        {
            var board = Level("shift: R0:R  제한 5턴", ".S2 g");
            TurnRunner.PlayTurn(board, R0Right);
            Assert.AreEqual(2, board.SheepOnGrass());
            Assert.AreEqual(GameResult.Won, board.Result);
        }

        [Test]
        public void Win_IsJudgedAfterFirstShift_NotAtLevelStart()
        {
            var board = Level("shift: R1:R  제한 5턴", "gS .\n.  .");
            Assert.AreEqual(GameResult.InProgress, board.Result);
            TurnRunner.PlayTurn(board, R1Right);
            Assert.AreEqual(GameResult.Won, board.Result);
        }

        [Test]
        public void Win_NotJudgedWhenTargetIsZero()
        {
            var board = Level("shift: R0:R  제한 5턴", ".B g");
            Assert.AreEqual(0, board.Rules.TargetSheep);
            TurnRunner.PlayTurn(board, R0Right);
            Assert.AreEqual(GameResult.InProgress, board.Result);
        }

        // ── 패배 ──

        [Test]
        public void Lose_WhenTurnLimitIsUsedUp()
        {
            var board = Level("shift: R0:RL  제한 2턴", ".S . . . g");
            TurnRunner.PlayTurn(board, R0Right);
            Assert.AreEqual(GameResult.InProgress, board.Result);
            TurnRunner.PlayTurn(board, R0Right);
            Assert.AreEqual(GameResult.Lost, board.Result);
            Assert.AreEqual(2, board.TurnsUsed);
            Assert.IsFalse(TurnRunner.PlayTurn(board, R0Right));
        }

        [Test]
        public void Win_OnTheLastTurn_IsAWin()
        {
            var board = Level("shift: R0:RL  제한 2턴", ".S . g");
            TurnRunner.PlayTurn(board, R0Right);
            TurnRunner.PlayTurn(board, R0Right);
            Assert.AreEqual(GameResult.Won, board.Result);
        }

        [Test]
        public void Lose_WhenSheepLeftAreFewerThanTarget()
        {
            // 양을 없애는 것(늑대)은 아직 없으므로 Board.Remove로 직접 확인한다.
            var board = Level("shift: R0:R  제한 5턴  목표 양 2", ".S .S .S g");
            var sheep = board.Objects.ToArray();
            board.Remove(sheep[0], RemoveCause.Eaten);
            Assert.AreEqual(GameResult.InProgress, board.Result);
            board.Remove(sheep[1], RemoveCause.Eaten);
            Assert.AreEqual(GameResult.Lost, board.Result);
            Assert.AreEqual(1, board.SheepCount());
        }

        // ── 승리 불가 ──

        [Test]
        public void CannotWin_WhenNoGrassIsLeft()
        {
            Assert.IsFalse(TestLevel.Make(".S g").CannotWin);
            Assert.IsTrue(TestLevel.Make(".S . ^").CannotWin);

            var board = TestLevel.Make(".S g");
            board.SetTerrain(new Pos(0, 1), TerrainType.Fire);
            Assert.IsTrue(board.CannotWin);
            Assert.AreEqual(GameResult.InProgress, board.Result);
        }

        // ── undo용 복사 ──

        [Test]
        public void Clone_IsIndependentDeepCopyWithSameIds()
        {
            var board = TestLevel.Make(".S ~ .U ^\n.  . .W2 g");
            TestLevel.Play(board, "R0:R");
            var copy = board.Clone();

            TestLevel.AssertMap(LevelWriter.Write(board), copy);
            Assert.AreEqual(board.TurnsUsed, copy.TurnsUsed);
            Assert.AreEqual(board.Result, copy.Result);
            Assert.AreSame(board.Rules, copy.Rules);
            CollectionAssert.AreEqual(
                board.Objects.Select(o => o.Id).ToArray(),
                copy.Objects.Select(o => o.Id).ToArray());
            Assert.AreEqual(2, copy.Objects.OfType<Wolf>().Single().Count);
            Assert.AreNotSame(board.Objects.First(), copy.Objects.First());
            Assert.AreEqual(0, copy.Events.Count);

            // 원본을 더 진행해도 복사본은 그대로다.
            string before = LevelWriter.Write(copy);
            TestLevel.Play(board, "R0:R");
            Assert.AreEqual(before, LevelWriter.Write(copy));
            Assert.AreEqual(1, copy.TurnsUsed);

            // 복사본도 따로 진행할 수 있다.
            TestLevel.Play(copy, "R0:R");
            TestLevel.AssertMap(LevelWriter.Write(board), copy);
        }

        [Test]
        public void Clone_CopiesAppliedThisTurnRecord()
        {
            var board = TestLevel.Make(".S ~ .");
            TestLevel.Play(board, "R0:R");
            var copy = board.Clone();
            var original = board.Objects.Single();
            var copied = copy.Objects.Single();
            Assert.IsTrue(copied.AppliedThisTurn.Contains(TerrainType.Water));
            Assert.AreNotSame(original.AppliedThisTurn, copied.AppliedThisTurn);
        }

        // ── 실제 스테이지 ──

        /// <summary>
        /// stages.txt에서 지금까지 만든 규칙(shift, 물, 들불 상호작용, 양동이)만으로 풀리는 스테이지.
        /// 들불이 있어도 주변 8칸에 나무 재질이 없어 확산이 일어나지 않는 것만 골랐다.
        /// 풀이 줄의 마지막 수에서 정확히 클리어되는지 본다.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(10)]
        [TestCase(11)]
        public void Stage_SolutionLineClearsTheLevel(int stage)
        {
            var board = LevelParser.ParseAll(StagesFileTests.ReadStagesText())[stage - 1];
            var solution = board.Rules.Solution;
            for (int i = 0; i < solution.Count; i++)
            {
                Assert.AreEqual(GameResult.InProgress, board.Result, (i + 1) + "번째 수 전에 끝났다.");
                Assert.IsTrue(TurnRunner.PlayTurn(board, solution[i]), solution[i] + "가 거부되었다.");
            }
            Assert.AreEqual(GameResult.Won, board.Result, LevelWriter.Write(board));
            Assert.AreEqual(solution.Count, board.TurnsUsed);
        }
    }
}
