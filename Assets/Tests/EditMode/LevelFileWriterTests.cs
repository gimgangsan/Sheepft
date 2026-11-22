using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>
    /// 파일 전체 쓰기(LevelWriter.WriteLevel, WriteFile)와 주석 지키기(LevelParser.ParseFile).
    /// 스테이지 에디터가 저장한 파일이 원래 파일과 같은 내용이어야 한다.
    /// </summary>
    public class LevelFileWriterTests
    {
        [Test]
        public void StagesFile_SurvivesWriteFile()
        {
            var original = LevelParser.ParseFile(StagesFileTests.ReadStagesText());
            Assert.IsEmpty(original.DroppedCommentLines, "붙일 곳 없는 주석이 있다");
            string written = LevelWriter.WriteFile(original.Comments, original.Levels);
            var again = LevelParser.ParseFile(written);

            CollectionAssert.AreEqual(original.Comments, again.Comments);
            Assert.AreEqual(original.Levels.Count, again.Levels.Count);
            for (int i = 0; i < original.Levels.Count; i++)
                AssertSameLevel(original.Levels[i], again.Levels[i], (i + 1) + "번");
        }

        [Test]
        public void WriteFile_IsStableWhenWrittenTwice()
        {
            var original = LevelParser.ParseFile(StagesFileTests.ReadStagesText());
            string once = LevelWriter.WriteFile(original.Comments, original.Levels);
            var parsed = LevelParser.ParseFile(once);
            string twice = LevelWriter.WriteFile(parsed.Comments, parsed.Levels);
            Assert.AreEqual(once, twice);
        }

        [Test]
        public void WriteLevel_WritesEveryPart()
        {
            var board = LevelParser.Parse(
                "// 묶음 1\n" +
                "# 예시 이름  shift: C2:UD, R1:L, R1:R  제한 4턴  목표 양 2\n" +
                "# 풀이: R1:R, C2:U\n" +
                "@desc 첫 줄\n" +
                "@desc 둘째 줄\n" +
                "@stars 2 3\n" +
                "@intent SheepMerge Wait\n" +
                "@optimal VoidSkip\n" +
                ".   .   g   .\n" +
                ".S  .   .S  #");
            // 첫 헤더 위의 주석은 파일 주석이므로 Parse(스테이지 하나)에서는 스테이지에 붙지 않는다.
            Assert.AreEqual(
                "# 예시 이름  shift: R1:RL, C2:UD  제한 4턴  목표 양 2\n" +
                "# 풀이: R1:R, C2:U\n" +
                "@desc 첫 줄 둘째 줄\n" +
                "@stars 2 3\n" +
                "@intent Wait SheepMerge\n" +
                "@optimal VoidSkip\n" +
                ".   .  g   .\n" +
                ".S  .  .S  #",
                LevelWriter.WriteLevel(board));
        }

        [Test]
        public void WriteLevel_OmitsOptionalLinesThatAreMissing()
        {
            var board = LevelParser.Parse("# t  shift: R0:R  제한 3턴\n.S  g");
            Assert.AreEqual("# t  shift: R0:R  제한 3턴  목표 양 1\n.S  g", LevelWriter.WriteLevel(board));
        }

        [Test]
        public void ParseFile_KeepsFileCommentsAndCommentsAboveHeaders()
        {
            var file = LevelParser.ParseFile(
                "// 파일 설명\n" +
                "// 둘째 줄\n" +
                "\n" +
                "# 하나  shift: R0:R  제한 3턴\n" +
                ".S  g\n" +
                "\n" +
                "// 묶음 2\n" +
                "\n" +
                "// 늑대 시작\n" +
                "# 둘  shift: R0:R  제한 3턴\n" +
                ".S  g");
            CollectionAssert.AreEqual(new[] { "// 파일 설명", "// 둘째 줄" }, file.Comments);
            Assert.IsEmpty(file.Levels[0].Rules.Comments);
            CollectionAssert.AreEqual(new[] { "// 묶음 2", "// 늑대 시작" }, file.Levels[1].Rules.Comments);
            Assert.IsEmpty(file.DroppedCommentLines);

            string written = LevelWriter.WriteFile(file.Comments, file.Levels);
            Assert.AreEqual(
                "// 파일 설명\n" +
                "// 둘째 줄\n" +
                "\n" +
                "# 하나  shift: R0:R  제한 3턴  목표 양 1\n" +
                ".S  g\n" +
                "\n" +
                "// 묶음 2\n" +
                "// 늑대 시작\n" +
                "# 둘  shift: R0:R  제한 3턴  목표 양 1\n" +
                ".S  g\n",
                written);
        }

        [Test]
        public void ParseFile_ReportsCommentsItCannotKeep()
        {
            var file = LevelParser.ParseFile(
                "# 하나  shift: R0:R  제한 3턴\n" +
                "// 헤더와 맵 사이\n" +
                ".S  g\n" +
                "// 맵 중간\n" +
                ".   .\n" +
                "// 파일 끝");
            CollectionAssert.AreEqual(new[] { 2, 4, 6 }, file.DroppedCommentLines);
            Assert.AreEqual(2, file.Levels[0].Height);
        }

        [Test]
        public void ShiftSpec_SortsRowsThenColumns()
        {
            var points = new[]
            {
                new ShiftPoint(Axis.Col, 3, Dir.Down),
                new ShiftPoint(Axis.Row, 2, Dir.Left),
                new ShiftPoint(Axis.Col, 0, Dir.Up),
                new ShiftPoint(Axis.Row, 0, Dir.Left),
                new ShiftPoint(Axis.Row, 0, Dir.Right),
                new ShiftPoint(Axis.Col, 3, Dir.Up),
            };
            Assert.AreEqual("R0:RL, R2:L, C0:U, C3:UD", LevelWriter.ShiftSpec(points));
        }

        private static void AssertSameLevel(Board a, Board b, string where)
        {
            var ra = a.Rules;
            var rb = b.Rules;
            Assert.AreEqual(ra.Name, rb.Name, where + " 이름");
            CollectionAssert.AreEquivalent(ra.ShiftPoints.ToList(), rb.ShiftPoints.ToList(), where + " shift");
            Assert.AreEqual(ra.TurnLimit, rb.TurnLimit, where + " 제한");
            Assert.AreEqual(ra.TargetSheep, rb.TargetSheep, where + " 목표");
            CollectionAssert.AreEqual(ra.Solution.ToList(), rb.Solution.ToList(), where + " 풀이");
            Assert.AreEqual(ra.Description, rb.Description, where + " 설명");
            Assert.AreEqual(ra.Star3Turns, rb.Star3Turns, where + " 별3");
            Assert.AreEqual(ra.Star2Turns, rb.Star2Turns, where + " 별2");
            Assert.AreEqual(ra.Intent, rb.Intent, where + " intent");
            Assert.AreEqual(ra.OptimalIntent, rb.OptimalIntent, where + " optimal");
            CollectionAssert.AreEqual(ra.Comments.ToList(), rb.Comments.ToList(), where + " 주석");
            Assert.AreEqual(LevelWriter.Write(a), LevelWriter.Write(b), where + " 맵");
            CollectionAssert.AreEqual(Ids(a), Ids(b), where + " 오브젝트 Id");
        }

        private static List<string> Ids(Board board)
        {
            return board.Objects.Select(o => o.Id + ":" + o.GetType().Name + "@" + o.Pos).ToList();
        }
    }
}
