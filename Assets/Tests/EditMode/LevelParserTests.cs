using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    public class LevelParserTests
    {
        // README 10절의 예시 레벨.
        private const string Example =
            "# 예시  shift: R1:RL, C2:U  제한 4턴  목표 양 2\n" +
            ".   .   g   .\n" +
            ".S  .   .S  #\n" +
            ".   .   .   .\n" +
            ".   .   .   .\n";

        private static Board WithHeader(string header, string grid = ". . .\n. .S .\n. . .")
        {
            return LevelParser.Parse(header + "\n" + grid);
        }

        private static void AssertError(string text, string expectedPart)
        {
            var e = Assert.Throws<LevelParseException>(() => LevelParser.ParseAll(text));
            StringAssert.Contains(expectedPart, e.Message);
        }

        // ── 헤더 ──

        [Test]
        public void Header_ReadsNameTurnLimitAndTarget()
        {
            var rules = LevelParser.Parse(Example).Rules;
            Assert.AreEqual("예시", rules.Name);
            Assert.AreEqual(4, rules.TurnLimit);
            Assert.AreEqual(2, rules.TargetSheep);
        }

        [Test]
        public void Header_NameMayContainSpaces()
        {
            var rules = WithHeader("# 젖은 털은 타지 않는다  shift: R0:R  제한 9턴").Rules;
            Assert.AreEqual("젖은 털은 타지 않는다", rules.Name);
        }

        [Test]
        public void ShiftPoints_BothDirectionsMakeTwoPoints()
        {
            var points = LevelParser.Parse(Example).Rules.ShiftPoints;
            CollectionAssert.AreEqual(
                new[]
                {
                    new ShiftPoint(Axis.Row, 1, Dir.Right),
                    new ShiftPoint(Axis.Row, 1, Dir.Left),
                    new ShiftPoint(Axis.Col, 2, Dir.Up),
                },
                points);
        }

        [Test]
        public void ShiftPoints_ColumnBothDirections()
        {
            var points = WithHeader("# t  shift: C2:UD, C0:D  제한 3턴").Rules.ShiftPoints;
            Assert.AreEqual("C2:U, C2:D, C0:D", string.Join(", ", points));
        }

        [Test]
        public void ShiftPoints_RowWithVerticalDirection_IsError()
        {
            AssertError("# t  shift: R1:U  제한 3턴\n. .\n. .", "행(R)에는 R·L만");
            AssertError("# t  shift: C1:R  제한 3턴\n. .\n. .", "행(R)에는 R·L만");
            AssertError("# t  shift: R1:RD  제한 3턴\n. .\n. .", "행(R)에는 R·L만");
        }

        [Test]
        public void ShiftPoints_Duplicate_IsError()
        {
            AssertError("# t  shift: R1:R, R1:RL  제한 3턴\n. .\n. .", "두 번");
            AssertError("# t  shift: R1:RR  제한 3턴\n. .\n. .", "두 번");
        }

        [Test]
        public void ShiftPoints_SameLineInTwoItems_IsAllowedWhenPointsDiffer()
        {
            var points = WithHeader("# t  shift: R1:R, R1:L  제한 3턴").Rules.ShiftPoints;
            Assert.AreEqual(2, points.Count);
        }

        [Test]
        public void ShiftPoints_IndexOutsideGrid_IsError()
        {
            AssertError("# t  shift: R2:R  제한 3턴\n. .\n. .", "맵에 없는 줄");
            AssertError("# t  shift: C2:U  제한 3턴\n. .\n. .", "맵에 없는 줄");
        }

        [Test]
        public void ShiftPoints_LineWithoutRealTiles_IsError()
        {
            AssertError("# t  shift: R1:R  제한 3턴\n. .\nx -\n. .", "실제 칸이 하나도 없는 줄");
            AssertError("# t  shift: C0:D  제한 3턴\n- .\nx .", "실제 칸이 하나도 없는 줄");
        }

        [Test]
        public void ShiftPoints_Malformed_IsError()
        {
            AssertError("# t  shift: R1  제한 3턴\n. .\n. .", "읽지 못했다");
            AssertError("# t  shift: R1:R,  제한 3턴\n. .\n. .", "읽지 못했다");
            AssertError("# t  shift: X1:R  제한 3턴\n. .\n. .", "읽지 못했다");
        }

        [Test]
        public void TurnLimit_Missing_IsError()
        {
            AssertError("# t  shift: R0:R\n. .", "제한 N턴");
            AssertError("# t  shift: R0:R  제한 0턴\n. .", "1 이상");
        }

        [Test]
        public void Header_UnknownTrailingText_IsError()
        {
            AssertError("# t  shift: R0:R  제한 3턴  보너스 2\n. .", "읽지 못한 부분");
        }

        [Test]
        public void TargetSheep_DefaultsToSheepCount()
        {
            var board = WithHeader("# t  shift: R0:R  제한 3턴", ".S2 .S\n.W  .S(w)");
            Assert.AreEqual(4, board.Rules.TargetSheep);
        }

        [Test]
        public void TargetSheep_MoreThanSheep_IsError()
        {
            AssertError("# t  shift: R0:R  제한 3턴  목표 양 3\n.S .S", "목표 양 수(3)");
        }

        // ── 풀이 줄 ──

        [Test]
        public void Solution_IsOptional()
        {
            Assert.AreEqual(0, LevelParser.Parse(Example).Rules.Solution.Count);
        }

        [Test]
        public void Solution_ReadsMovesInOrder()
        {
            var board = LevelParser.Parse(
                "# 예시  shift: R1:RL, C2:U  제한 4턴\n" +
                "# 풀이: R1:R, C2:U, R1:L\n" +
                ". . .\n. .S .\n. . g");
            Assert.AreEqual("R1:R, C2:U, R1:L", string.Join(", ", board.Rules.Solution));
        }

        [Test]
        public void Solution_MoveWithTwoDirections_IsError()
        {
            AssertError("# t  shift: R1:RL  제한 4턴\n# 풀이: R1:RL\n. .\n. .", "방향은 하나만");
        }

        [Test]
        public void Solution_MoveNotInShiftPoints_IsError()
        {
            AssertError("# t  shift: R1:R, C0:U  제한 4턴\n# 풀이: R1:L\n. .\n. .", "shift 포인트가 아니다");
            AssertError("# t  shift: R1:R, C0:U  제한 4턴\n# 풀이: R0:R\n. .\n. .", "shift 포인트가 아니다");
            AssertError("# t  shift: R1:R, C0:U  제한 4턴\n# 풀이: C0:D\n. .\n. .", "shift 포인트가 아니다");
        }

        [Test]
        public void Solution_WrongAxisDirection_IsError()
        {
            AssertError("# t  shift: R1:R  제한 4턴\n# 풀이: R1:U\n. .\n. .", "행(R)에는 R·L만");
        }

        [Test]
        public void Solution_AfterGrid_IsError()
        {
            AssertError("# t  shift: R1:R  제한 4턴\n. .\n# 풀이: R1:R\n. .", "맵보다 앞");
        }

        // ── @ 줄 ──

        [Test]
        public void Meta_DescAndStarsGoIntoRules()
        {
            var rules = LevelParser.Parse(
                "# t  shift: R0:R  제한 7턴\n" +
                "# 풀이: R0:R\n" +
                "@desc 줄을 밀면 양이 움직입니다.\n" +
                "@intent SheepWrap\n" +
                "@optimal Wait\n" +
                "@stars 3 4\n" +
                ".S g").Rules;
            Assert.AreEqual("줄을 밀면 양이 움직입니다.", rules.Description);
            Assert.AreEqual(3, rules.Star3Turns);
            Assert.AreEqual(4, rules.Star2Turns);
        }

        [Test]
        public void Meta_MultipleDescLinesAreJoined()
        {
            var rules = LevelParser.Parse(
                "# t  shift: R0:R  제한 7턴\n@desc 첫 문장.\n@desc 둘째 문장.\n.S g").Rules;
            Assert.AreEqual("첫 문장. 둘째 문장.", rules.Description);
        }

        [Test]
        public void Meta_MissingLinesLeaveDefaults()
        {
            var rules = LevelParser.Parse(Example).Rules;
            Assert.AreEqual("", rules.Description);
            Assert.AreEqual(0, rules.Star3Turns);
            Assert.AreEqual(0, rules.Star2Turns);
        }

        [Test]
        public void Meta_UnknownKey_IsError()
        {
            AssertError("# t  shift: R0:R  제한 7턴\n@hint 힌트\n.S g", "모르는 표시 '@hint'");
        }

        [Test]
        public void Meta_BadStars_IsError()
        {
            AssertError("# t  shift: R0:R  제한 7턴\n@stars 3\n.S g", "@stars");
            AssertError("# t  shift: R0:R  제한 7턴\n@stars a b\n.S g", "@stars");
        }

        // ── 줄 나누기 ──

        [Test]
        public void CommentsAndEmptyLinesAreSkipped()
        {
            var board = LevelParser.Parse(
                "// 주석\r\n\r\n" +
                "# t  shift: R0:R  제한 7턴\r\n" +
                "// 헤더 아래 주석\r\n" +
                ".S .\r\n" +
                "\r\n" +
                "// 맵 사이 주석\r\n" +
                ".  g\r\n");
            Assert.AreEqual(2, board.Height);
            Assert.AreEqual(2, board.Width);
        }

        [Test]
        public void MultipleLevels_AreSplitByHeaders()
        {
            var boards = LevelParser.ParseAll(
                "# 하나  shift: R0:R  제한 3턴\n" +
                "# 풀이: R0:R\n" +
                ".S g\n" +
                "\n" +
                "# 둘  shift: C0:D  제한 5턴\n" +
                "#  .\n" +
                ".S g\n" +
                "#  #\n");
            Assert.AreEqual(2, boards.Count);
            Assert.AreEqual("하나", boards[0].Rules.Name);
            Assert.AreEqual(1, boards[0].Height);
            Assert.AreEqual(1, boards[0].Rules.Solution.Count);
            Assert.AreEqual("둘", boards[1].Rules.Name);
            Assert.AreEqual(3, boards[1].Height);
            Assert.AreEqual(0, boards[1].Rules.Solution.Count);
        }

        [Test]
        public void RowStartingWithStone_IsNotAHeader()
        {
            var board = WithHeader("# t  shift: R0:R  제한 3턴", "# . #\n# # #");
            Assert.AreEqual(TerrainType.Stone, board.At(new Pos(0, 0)).Terrain.Type);
            Assert.AreEqual(TerrainType.Stone, board.At(new Pos(1, 1)).Terrain.Type);
        }

        [Test]
        public void LineBeforeHeader_IsError()
        {
            AssertError(". . .\n# t  shift: R0:R  제한 3턴\n. .", "헤더보다 먼저");
        }

        [Test]
        public void LevelWithoutGrid_IsError()
        {
            AssertError("# t  shift: R0:R  제한 3턴\n@desc 설명\n", "맵 줄이 없다");
        }

        [Test]
        public void Parse_RequiresExactlyOneLevel()
        {
            Assert.Throws<LevelParseException>(() => LevelParser.Parse("// 비어 있음\n"));
            Assert.Throws<LevelParseException>(() => LevelParser.Parse(Example + Example));
        }

        [Test]
        public void ErrorMessage_HasLineNumber()
        {
            var e = Assert.Throws<LevelParseException>(() => LevelParser.ParseAll(
                "// 주석\n# t  shift: R0:R  제한 3턴\n. .\n. . .\n"));
            Assert.AreEqual(4, e.Line);
            StringAssert.StartsWith("4번째 줄", e.Message);
        }

        // ── 격자 ──

        [Test]
        public void Grid_RowsMustHaveSameWidth()
        {
            AssertError("# t  shift: R0:R  제한 3턴\n. . .\n. .", "칸 수");
        }

        [Test]
        public void Grid_TerrainSymbols()
        {
            var board = WithHeader("# t  shift: R0:R  제한 3턴", ". # ~ ^\ng f t _");
            var types = board.Tiles.Select(t => t.Terrain.Type).ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    TerrainType.Ground, TerrainType.Stone, TerrainType.Water, TerrainType.Fire,
                    TerrainType.Grass, TerrainType.Fence, TerrainType.Tree, TerrainType.Ash,
                },
                types);
        }

        [Test]
        public void Grid_TerrainProperties()
        {
            var board = WithHeader("# t  shift: R0:R  제한 3턴", ". # ~ ^\ng f t _");
            string obstacles = string.Concat(board.Tiles.Where(t => t.Terrain.IsObstacle).Select(LevelWriter.WriteCell));
            string wooden = string.Concat(board.Tiles.Where(t => t.Terrain.IsWooden).Select(LevelWriter.WriteCell));
            Assert.AreEqual("#ft", obstacles);
            Assert.AreEqual("gft", wooden);
        }

        [Test]
        public void Grid_BlankKindsAreDistinguished()
        {
            var board = WithHeader("# t  shift: R0:R  제한 3턴", "- . x");
            var outer = board.At(new Pos(0, 0));
            var inner = board.At(new Pos(0, 2));
            Assert.AreEqual(TileKind.OuterBlank, outer.Kind);
            Assert.AreEqual(TileKind.InnerBlank, inner.Kind);
            Assert.AreEqual(TileKind.Normal, board.At(new Pos(0, 1)).Kind);
            Assert.IsTrue(outer.IsBlank);
            Assert.IsTrue(inner.IsBlank);
            Assert.IsNull(outer.Terrain);
            Assert.IsNull(inner.Terrain);
            Assert.AreEqual(0, outer.Objects.Count);
        }

        [Test]
        public void Grid_ObjectOnBlank_IsError()
        {
            AssertError("# t  shift: R0:R  제한 3턴\n. xS .", "빈칸에는 오브젝트를 둘 수 없다");
            AssertError("# t  shift: R0:R  제한 3턴\n. -S .", "빈칸에는 오브젝트를 둘 수 없다");
            AssertError("# t  shift: R0:R  제한 3턴\n. xx .", "빈칸에는 오브젝트를 둘 수 없다");
        }

        [Test]
        public void Grid_ObjectOnObstacle_IsError()
        {
            AssertError("# t  shift: R0:R  제한 3턴\n. #S .", "장애물 칸");
        }

        [Test]
        public void Grid_UnknownSymbols_AreErrors()
        {
            AssertError("# t  shift: R0:R  제한 3턴\n. ? .", "모르는 지형 기호");
            AssertError("# t  shift: R0:R  제한 3턴\n. .Z .", "모르는 오브젝트 기호");
            AssertError("# t  shift: R0:R  제한 3턴\n. .S(z) .", "모르는 상태");
            AssertError("# t  shift: R0:R  제한 3턴\n. .S0 .", "개수는 1 이상");
            AssertError("# t  shift: R0:R  제한 3턴\n. .s .", "읽지 못했다");
            AssertError("# t  shift: R0:R  제한 3턴\n. .A(b) .", "화상은 동물에게만");
            AssertError("# t  shift: R0:R  제한 3턴\n. .U(w) .", "물 찬 양동이");
        }

        [Test]
        public void Grid_AllObjectSymbols()
        {
            var board = WithHeader("# t  shift: R0:R  제한 3턴", ".S .W .G .H\n.M .A .B .U");
            var objects = board.Objects.ToArray();
            Assert.AreEqual(8, objects.Length);
            Assert.IsInstanceOf<Sheep>(objects[0]);
            Assert.IsInstanceOf<Wolf>(objects[1]);
            Assert.IsInstanceOf<Goat>(objects[2]);
            Assert.IsInstanceOf<Horse>(objects[3]);
            Assert.IsInstanceOf<Cow>(objects[4]);
            Assert.IsInstanceOf<FruitBasket>(objects[5]);
            Assert.IsInstanceOf<Bucket>(objects[6]);
            Assert.IsInstanceOf<Bucket>(objects[7]);
            Assert.IsFalse(((Bucket)objects[6]).Filled);
            Assert.IsTrue(((Bucket)objects[7]).Filled);
            Assert.AreEqual(5, objects.Count(o => o.IsAnimal));
            Assert.AreEqual(3, objects.Count(o => o.IsWooden));
        }

        [Test]
        public void Grid_SheepCountMakesOneEntityEach()
        {
            var board = WithHeader("# t  shift: R0:R  제한 3턴", ". .S2S(w) .");
            var objects = board.At(new Pos(0, 1)).Objects;
            Assert.AreEqual(3, objects.Count);
            Assert.IsTrue(objects.All(o => o is Sheep));
            Assert.IsFalse(objects[0].Wet);
            Assert.IsFalse(objects[1].Wet);
            Assert.IsTrue(objects[2].Wet);
        }

        [Test]
        public void Grid_WolfCountIsPackSize()
        {
            var board = WithHeader("# t  shift: R0:R  제한 3턴", ". .W2 .W");
            var pack = (Wolf)board.At(new Pos(0, 1)).Objects.Single();
            var single = (Wolf)board.At(new Pos(0, 2)).Objects.Single();
            Assert.AreEqual(2, pack.Count);
            Assert.AreEqual(1, single.Count);
        }

        [Test]
        public void Grid_BurnedAndWetStates()
        {
            var board = WithHeader("# t  shift: R0:R  제한 3턴", ".S(b) .W(w) .B(w)");
            var sheep = (Sheep)board.At(new Pos(0, 0)).Objects[0];
            var wolf = (Wolf)board.At(new Pos(0, 1)).Objects[0];
            var bucket = (Bucket)board.At(new Pos(0, 2)).Objects[0];
            Assert.IsTrue(sheep.Burned);
            Assert.IsFalse(sheep.Wet);
            Assert.IsTrue(wolf.Wet);
            Assert.IsFalse(wolf.Burned);
            Assert.IsTrue(bucket.Wet);
        }

        [Test]
        public void Grid_ObjectsGetIdsInReadingOrderAndKnowTheirPos()
        {
            var board = WithHeader("# t  shift: R0:R  제한 3턴", ".  gWA .\n.S2 .   .HW");
            var objects = board.Objects.ToArray();
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, objects.Select(o => o.Id).ToArray());
            Assert.IsInstanceOf<Wolf>(objects[0]);
            Assert.IsInstanceOf<FruitBasket>(objects[1]);
            Assert.IsInstanceOf<Horse>(objects[4]);
            Assert.IsInstanceOf<Wolf>(objects[5]);
            Assert.AreEqual(new Pos(0, 1), objects[0].Pos);
            Assert.AreEqual(new Pos(1, 0), objects[3].Pos);
            Assert.AreEqual(new Pos(1, 2), objects[5].Pos);
        }
    }
}
