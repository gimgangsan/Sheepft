using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    public class LevelWriterTests
    {
        private const string Header = "# t  shift: R0:R  제한 3턴\n";

        private static string RoundTrip(string grid)
        {
            return LevelWriter.Write(LevelParser.Parse(Header + grid));
        }

        [Test]
        public void RoundTrip_BlanksAndMixedSheep()
        {
            const string grid =
                "-  .S      .  x  x  .       -\n" +
                "-  .S2S(w)  .  x  x  ~S(w)  -";
            // writer는 열마다 가장 긴 칸에 맞추므로, 기대 문자열도 그 간격으로 적는다.
            const string expected =
                "-  .S       .  x  x  .      -\n" +
                "-  .S2S(w)  .  x  x  ~S(w)  -";
            Assert.AreEqual(expected, RoundTrip(grid));
            Assert.AreEqual(expected, RoundTrip(expected));
        }

        [Test]
        public void RoundTrip_ReadmeShiftExampleRow()
        {
            Assert.AreEqual("-  .S  .  x  x  .  -", RoundTrip("- .S . x x . -"));
        }

        [Test]
        public void RoundTrip_AllTerrains()
        {
            const string grid =
                ".  #  ~  ^\n" +
                "g  f  t  _";
            Assert.AreEqual(grid, RoundTrip(grid));
        }

        [Test]
        public void RoundTrip_AllObjectsAndStates()
        {
            const string grid =
                ".S3    .W2     .G     .H\n" +
                ".M     .A      .B     .U\n" +
                ".S(b)  .W3(w)  .B(w)  gWA\n" +
                ".HW    .HA     ~S(w)  ^W(b)";
            Assert.AreEqual(grid, RoundTrip(grid));
        }

        [Test]
        public void RoundTrip_KeepsObjectOrderWithinTile()
        {
            Assert.AreEqual(".HW  .WH  .SS(w)S  .WW", RoundTrip(".HW .WH .SS(w)S .WW"));
        }

        [Test]
        public void Write_GroupsNeighbouringSameObjects()
        {
            Assert.AreEqual(".S3  .G2  .S2S2(w)", RoundTrip(".SSS .GG .SSS(w)S(w)"));
        }

        [Test]
        public void Write_SkipsRemovedObjects()
        {
            var board = LevelParser.Parse(Header + ".S2A .");
            board.At(new Pos(0, 0)).Objects[1].Removed = true;
            Assert.AreEqual(".SA  .", LevelWriter.Write(board));
        }

        [Test]
        public void Write_HasNoTrailingSpacesAndUsesLfBetweenRows()
        {
            string text = RoundTrip(".S2 .\r\n.   .\r\n");
            Assert.AreEqual(".S2  .\n.    .", text);
        }
    }
}
