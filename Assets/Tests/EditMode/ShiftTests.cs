using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>플레이어의 shift. README 3절.</summary>
    public class ShiftTests
    {
        [Test]
        public void Shift_SkipsBlanksAndWraps_ReadmeExample()
        {
            // 실제 칸은 1·2·5열이다. 오른쪽으로 밀면 1열 → 2열 → 5열 → 1열.
            const string level = "- .S . x x . -";
            TestLevel.AssertAfter(level, "R0:R", "- . .S x x . -");
            TestLevel.AssertAfter(level, "R0:R, R0:R", "- . . x x .S -");
            TestLevel.AssertAfter(level, "R0:R, R0:R, R0:R", "- .S . x x . -");
        }

        [Test]
        public void Shift_Left_WrapsToLastRealTile()
        {
            TestLevel.AssertAfter("- .S . x x . -", "R0:L", "- . . x x .S -");
        }

        [Test]
        public void Shift_Column_SkipsBlankInTheMiddle()
        {
            // 1열의 실제 칸은 0·2·3행이다.
            const string level =
                ". .S\n" +
                ". -\n" +
                ". .\n" +
                ". .";
            TestLevel.AssertAfter(level, "C1:D",
                ". .\n" +
                ". -\n" +
                ". .S\n" +
                ". .");
            TestLevel.AssertAfter(level, "C1:D, C1:U",
                ". .S\n" +
                ". -\n" +
                ". .\n" +
                ". .");
            TestLevel.AssertAfter(level, "C1:U",
                ". .\n" +
                ". -\n" +
                ". .\n" +
                ". .S");
        }

        [Test]
        public void Shift_BlockedByObstacle_StaysInPlace()
        {
            TestLevel.AssertAfter(".S # .", "R0:R", ".S # .");
            TestLevel.AssertAfter(".S f .", "R0:R", ".S f .");
            TestLevel.AssertAfter(".S t .", "R0:R", ".S t .");
        }

        [Test]
        public void Shift_ObstacleBeyondBlank_StaysInPlace()
        {
            TestLevel.AssertAfter(".S x # .", "R0:R", ".S x # .");
        }

        [Test]
        public void Shift_WrapDestinationIsObstacle_StaysInPlace()
        {
            TestLevel.AssertAfter("# . .S", "R0:R", "# . .S");
            TestLevel.AssertAfter(".S . #", "R0:L", ".S . #");
        }

        [Test]
        public void Shift_ObjectsDoNotBlockEachOther()
        {
            // 돌 앞에서 멈춘 양 위로 뒤 양이 들어와 겹친다.
            TestLevel.AssertAfter(".S .S #", "R0:R", ". .S2 #");
            TestLevel.AssertAfter(".S .S #", "R0:R, R0:R", ". .S2 #");
        }

        [Test]
        public void Shift_AllObjectsMoveAtOnce()
        {
            TestLevel.AssertAfter(".S .S . .", "R0:R", ". .S .S .");
            TestLevel.AssertAfter(".S .B .A", "R0:R", ".A .S .B");
        }

        [Test]
        public void Shift_ObjectsOnSameTileMoveTogether()
        {
            TestLevel.AssertAfter(".S2A . .", "R0:R", ". .S2A .");
        }

        [Test]
        public void Shift_OnlyTheChosenLineMoves()
        {
            TestLevel.AssertAfter(
                ".S . .\n" +
                ".S . .",
                "R1:R",
                ".S . .\n" +
                ". .S .");
            TestLevel.AssertAfter(
                ".S .S\n" +
                ".  .",
                "C0:D",
                ".  .S\n" +
                ".S .");
        }

        [Test]
        public void Shift_LineWithOneRealTile_MovesNothingButUsesTurn()
        {
            var board = TestLevel.AssertAfter("x .S x", "R0:R", "x .S x");
            Assert.AreEqual(1, board.TurnsUsed);
        }

        [Test]
        public void Shift_EmptyLine_IsAWaitThatUsesTurn()
        {
            var board = TestLevel.AssertAfter(".S .\n.  .", "R1:R, R1:L", ".S .\n.  .");
            Assert.AreEqual(2, board.TurnsUsed);
        }

        [Test]
        public void Shift_MoveNotInShiftPoints_IsRejectedWithoutUsingTurn()
        {
            var board = LevelParser.Parse("# t  shift: R0:R, C1:U  제한 5턴\n.S . .\n.  . .");
            Assert.IsFalse(TurnRunner.PlayTurn(board, new ShiftPoint(Axis.Row, 0, Dir.Left)));
            Assert.IsFalse(TurnRunner.PlayTurn(board, new ShiftPoint(Axis.Row, 1, Dir.Right)));
            Assert.IsFalse(TurnRunner.PlayTurn(board, new ShiftPoint(Axis.Col, 1, Dir.Down)));
            Assert.IsFalse(TurnRunner.PlayTurn(board, new ShiftPoint(Axis.Col, 9, Dir.Up)));
            Assert.AreEqual(0, board.TurnsUsed);
            Assert.AreEqual(0, board.Events.Count);
            TestLevel.AssertMap(".S . .\n.  . .", board);

            Assert.IsTrue(TurnRunner.PlayTurn(board, new ShiftPoint(Axis.Row, 0, Dir.Right)));
            Assert.AreEqual(1, board.TurnsUsed);
        }

        [Test]
        public void Shift_KeepsObjectPosInSyncWithTile()
        {
            var board = TestLevel.Make(".S . .");
            TestLevel.Play(board, "R0:R");
            var sheep = board.Objects.Single();
            Assert.AreEqual(new Pos(0, 1), sheep.Pos);
            Assert.AreSame(sheep, board.At(new Pos(0, 1)).Objects[0]);
        }

        [Test]
        public void Shift_OverlappedObjectsStayInIdOrder()
        {
            // 양동이(Id 1)가 멈춘 칸으로 양(Id 0)이 들어와도 칸 안의 순서는 Id 순서다.
            var board = TestLevel.AssertAfter(".S .B #", "R0:R", ". .SB #");
            CollectionAssert.AreEqual(new[] { 0, 1 }, board.At(new Pos(0, 1)).Objects.Select(o => o.Id).ToArray());
        }

        [Test]
        public void Shift_RecordsMovedEvents()
        {
            var board = TestLevel.Make(". . .S");
            TestLevel.Play(board, "R0:R");
            var moved = board.Events.OfType<MovedEvent>().Single();
            Assert.AreEqual(0, moved.Id);
            Assert.AreEqual(new Pos(0, 2), moved.From);
            Assert.AreEqual(new Pos(0, 0), moved.To);
            Assert.AreEqual(MoveCause.Shift, moved.Cause);
            Assert.IsTrue(board.Events.OfType<StepEndEvent>().Any());
        }

        [Test]
        public void Events_AreClearedAtTheStartOfEachTurn()
        {
            var board = TestLevel.Make(".S . .\n.  . .");
            TestLevel.Play(board, "R0:R");
            Assert.AreEqual(1, board.Events.OfType<MovedEvent>().Count());
            TestLevel.Play(board, "R1:R");
            Assert.AreEqual(0, board.Events.OfType<MovedEvent>().Count());
        }

        [Test]
        public void ShiftPoint_TryParse()
        {
            ShiftPoint p;
            Assert.IsTrue(ShiftPoint.TryParse(" R12:L ", out p));
            Assert.AreEqual(new ShiftPoint(Axis.Row, 12, Dir.Left), p);
            Assert.IsTrue(ShiftPoint.TryParse("C0:D", out p));
            Assert.AreEqual(new ShiftPoint(Axis.Col, 0, Dir.Down), p);
            Assert.IsFalse(ShiftPoint.TryParse("R1:U", out p));
            Assert.IsFalse(ShiftPoint.TryParse("R1:RL", out p));
            Assert.IsFalse(ShiftPoint.TryParse("R:R", out p));
            Assert.IsFalse(ShiftPoint.TryParse("X1:R", out p));
            Assert.IsFalse(ShiftPoint.TryParse("R1R", out p));
            Assert.IsFalse(ShiftPoint.TryParse("", out p));
            Assert.IsFalse(ShiftPoint.TryParse(null, out p));
        }
    }
}
