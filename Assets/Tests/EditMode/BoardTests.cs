using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    public class BoardTests
    {
        private const string Header = "# t  shift: R0:R  제한 3턴\n";

        private static Board Make(string grid)
        {
            return LevelParser.Parse(Header + grid);
        }

        private static string Cols(Board b, int row)
        {
            return string.Join(",", b.Line(Axis.Row, row).Select(t => t.Pos.Col));
        }

        private static string Rows(Board b, int col)
        {
            return string.Join(",", b.Line(Axis.Col, col).Select(t => t.Pos.Row));
        }

        [Test]
        public void Line_Row_SkipsBlanks()
        {
            // README 3절의 예: 실제 칸은 1·2·5열 세 칸이다.
            var board = Make("- .S . x x . -");
            Assert.AreEqual("1,2,5", Cols(board, 0));
            Assert.AreSame(board.At(new Pos(0, 1)), board.Line(Axis.Row, 0)[0]);
        }

        [Test]
        public void Line_Col_SkipsBlanks()
        {
            // ㄷ자 맵: 1열과 2열은 가운데가 빈칸이다.
            var board = Make(
                ". . .\n" +
                ". - x\n" +
                ". . .");
            Assert.AreEqual("0,1,2", Rows(board, 0));
            Assert.AreEqual("0,2", Rows(board, 1));
            Assert.AreEqual("0,2", Rows(board, 2));
            Assert.AreEqual("0", Cols(board, 1));
        }

        [Test]
        public void Line_IncludesObstacleTiles()
        {
            var board = Make(". # x f t .");
            Assert.AreEqual("0,1,3,4,5", Cols(board, 0));
        }

        [Test]
        public void Line_AllBlankOrOutside_IsEmpty()
        {
            var board = Make(". .\nx -");
            Assert.AreEqual(0, board.Line(Axis.Row, 1).Count);
            Assert.AreEqual(0, board.Line(Axis.Row, 2).Count);
            Assert.AreEqual(0, board.Line(Axis.Row, -1).Count);
            Assert.AreEqual(0, board.Line(Axis.Col, 2).Count);
        }

        [Test]
        public void At_OutsideGrid_IsNull()
        {
            var board = Make(". .\n. .");
            Assert.AreEqual(2, board.Height);
            Assert.AreEqual(2, board.Width);
            Assert.IsNotNull(board.At(new Pos(1, 1)));
            Assert.IsNull(board.At(new Pos(-1, 0)));
            Assert.IsNull(board.At(new Pos(0, -1)));
            Assert.IsNull(board.At(new Pos(2, 0)));
            Assert.IsNull(board.At(new Pos(0, 2)));
        }

        [Test]
        public void IsBlank_TrueForBothBlankKindsAndOutside()
        {
            var board = Make("- x . #");
            Assert.IsTrue(board.IsBlank(new Pos(0, 0)));
            Assert.IsTrue(board.IsBlank(new Pos(0, 1)));
            Assert.IsFalse(board.IsBlank(new Pos(0, 2)));
            Assert.IsFalse(board.IsBlank(new Pos(0, 3)));
            Assert.IsTrue(board.IsBlank(new Pos(0, 4)));
            Assert.IsTrue(board.IsBlank(new Pos(1, 0)));
        }

        [Test]
        public void BlocksShift_OnlyObstacleTerrain()
        {
            var board = Make(". # f t ~ ^ g _ x -");
            string blocked = string.Concat(board.Tiles.Where(board.BlocksShift).Select(LevelWriter.WriteCell));
            Assert.AreEqual("#ft", blocked);
            Assert.IsFalse(board.BlocksShift(null));
        }

        [Test]
        public void BlocksAnimal_ObstaclesBlanksAndOutside()
        {
            var board = Make(". # f t ~ ^ g _ x -");
            string blocked = string.Concat(board.Tiles.Where(t => board.BlocksAnimal(t.Pos)).Select(LevelWriter.WriteCell));
            Assert.AreEqual("#ftx-", blocked);
            Assert.IsTrue(board.BlocksAnimal(new Pos(0, 10)));
            Assert.IsTrue(board.BlocksAnimal(new Pos(-1, 0)));
        }

        [Test]
        public void Pos_StepDoesNotWrap()
        {
            var p = new Pos(0, 0);
            Assert.AreEqual(new Pos(-1, 0), p.Step(Dir.Up));
            Assert.AreEqual(new Pos(0, 1), p.Step(Dir.Right));
            Assert.AreEqual(new Pos(1, 0), p.Step(Dir.Down));
            Assert.AreEqual(new Pos(0, -1), p.Step(Dir.Left));
        }

        [Test]
        public void ShiftPoint_ToStringUsesReadmeNotation()
        {
            Assert.AreEqual("R1:R", new ShiftPoint(Axis.Row, 1, Dir.Right).ToString());
            Assert.AreEqual("C2:U", new ShiftPoint(Axis.Col, 2, Dir.Up).ToString());
            Assert.Throws<System.ArgumentException>(() => new ShiftPoint(Axis.Row, 1, Dir.Up));
        }
    }
}
