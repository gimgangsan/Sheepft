using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sheepft.Core;
using Sheepft.Tools;
using UnityEngine;

namespace Sheepft.Tests
{
    /// <summary>스테이지 에디터의 편집 모델(StageDraft, StageFile).</summary>
    public class StageDraftTests
    {
        private static StageDraft Draft(string text) => StageDraft.FromBoard(LevelParser.Parse(text));

        private static Board Parsed(StageDraft d)
        {
            Board board;
            string error;
            Assert.IsTrue(d.TryParse(out board, out error), error);
            return board;
        }

        // ── 파일 ──

        [Test]
        public void StagesFile_LoadThenSave_IsTheSameAsWriteFile()
        {
            string text = StagesFileTests.ReadStagesText();
            List<int> dropped;
            var file = StageFile.Load(text, out dropped);
            Assert.Greater(file.stages.Count, 0);
            Assert.IsEmpty(dropped);
            var errors = new List<string>();
            string saved = file.ToFileText(errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));

            var parsed = LevelParser.ParseFile(text);
            Assert.AreEqual(LevelWriter.WriteFile(parsed.Comments, parsed.Levels), saved);
        }

        [Test]
        public void File_SurvivesJsonSnapshot()
        {
            List<int> dropped;
            var file = StageFile.Load(StagesFileTests.ReadStagesText(), out dropped);
            var copy = JsonUtility.FromJson<StageFile>(JsonUtility.ToJson(file));
            var e1 = new List<string>();
            var e2 = new List<string>();
            Assert.AreEqual(file.ToFileText(e1), copy.ToFileText(e2));
        }

        [Test]
        public void File_AddDuplicateDeleteMove()
        {
            var file = new StageFile();
            file.stages.Add(StageDraft.CreateNew("A"));
            file.stages.Add(StageDraft.CreateNew("B"));

            Assert.AreEqual(1, file.AddNew(0));
            Assert.AreEqual(new[] { "A", "New Stage", "B" }, file.stages.Select(s => s.name).ToArray());
            Assert.AreEqual(3, file.Duplicate(2));
            Assert.AreEqual("B Copy", file.stages[3].name);
            Assert.AreEqual(2, file.AddNew(1));
            Assert.AreEqual("New Stage 2", file.stages[2].name);

            Assert.AreEqual(0, file.Move(3, 0));
            Assert.AreEqual(new[] { "B", "A", "New Stage", "New Stage 2", "B Copy" }, file.stages.Select(s => s.name).ToArray());
            Assert.AreEqual(4, file.Move(0, 99));
            file.Delete(1);
            Assert.AreEqual(new[] { "A", "New Stage 2", "B Copy", "B" }, file.stages.Select(s => s.name).ToArray());

            var errors = new List<string>();
            Assert.IsNotNull(file.ToFileText(errors), string.Join("\n", errors));
        }

        [Test]
        public void File_RefusesToSaveDuplicateNamesOrBrokenStages()
        {
            var file = new StageFile();
            file.stages.Add(StageDraft.CreateNew("A"));
            file.stages.Add(StageDraft.CreateNew("A"));
            var errors = new List<string>();
            Assert.IsNull(file.ToFileText(errors));
            StringAssert.Contains("겹친다", errors[0]);

            file.stages[1].name = "B";
            file.stages[1].solution = "R9:R";
            errors.Clear();
            Assert.IsNull(file.ToFileText(errors));
            StringAssert.StartsWith("2. B:", errors[0]);
        }

        // ── 스테이지 ──

        [Test]
        public void NewStage_ParsesWithAllLinesOpen()
        {
            var board = Parsed(StageDraft.CreateNew("Fresh"));
            Assert.AreEqual(12, board.Rules.ShiftPoints.Count);
            Assert.AreEqual(1, board.Rules.TargetSheep);
        }

        [Test]
        public void Draft_RoundTripsEveryField()
        {
            const string text =
                "# 예시  shift: R1:RL, C2:U  제한 4턴  목표 양 2\n" +
                "# 풀이: R1:R, C2:U\n" +
                "@desc 설명\n" +
                "@stars 3 4\n" +
                "@intent SheepMerge\n" +
                "@optimal Wait\n" +
                ".   .   g   .\n" +
                ".S  .   .S  #";
            var a = LevelParser.Parse(text);
            var b = Parsed(Draft(text));
            Assert.AreEqual(LevelWriter.WriteLevel(a), LevelWriter.WriteLevel(b));
        }

        [Test]
        public void PaintTerrain_ObstacleOrBlankClearsObjects()
        {
            var d = Draft("# t  shift: R0:RL  제한 9턴  목표 양 0\n.S2  g  .");
            Assert.IsTrue(d.PaintTerrain(0, 0, '~'));
            Assert.AreEqual("~S2", d.cells[0]);
            Assert.IsTrue(d.PaintTerrain(0, 0, '#'));
            Assert.AreEqual("#", d.cells[0]);
            Assert.IsFalse(d.PaintTerrain(0, 0, '#'));
            d.PaintTerrain(0, 2, 'x');
            Assert.AreEqual("x", d.cells[2]);
        }

        [Test]
        public void AddObject_StacksSameKindAndState()
        {
            var d = StageDraft.CreateNew("t");
            d.AddObject(1, 1, 'S', '\0');
            d.AddObject(1, 1, 'S', '\0');
            d.AddObject(1, 1, 'S', 'w');
            d.AddObject(1, 1, 'W', '\0');
            d.AddObject(1, 1, 'W', '\0');
            Assert.AreEqual(".S2S(w)W2", d.cells[4]);
            Assert.IsTrue(d.RemoveObject(1, 1));
            Assert.AreEqual(".S2S(w)W", d.cells[4]);
            d.PaintTerrain(0, 0, 't');
            Assert.IsFalse(d.AddObject(0, 0, 'S', '\0'));

            var board = Parsed(d);
            Assert.AreEqual(4, board.SheepCount());
            Assert.AreEqual(4, board.Rules.TargetSheep); // 목표 양을 비우면 양 수
        }

        [Test]
        public void TogglePoint_AndShiftSpec()
        {
            var d = StageDraft.CreateNew("t");
            var r0Left = new ShiftPoint(Axis.Row, 0, Dir.Left);
            Assert.IsTrue(d.HasPoint(r0Left));
            d.TogglePoint(r0Left);
            Assert.IsFalse(d.HasPoint(r0Left));
            d.TogglePoint(r0Left);
            Assert.IsTrue(d.HasPoint(r0Left));

            Assert.IsTrue(d.SetShiftSpec("R0:R, C2:UD"));
            CollectionAssert.AreEquivalent(new[] { "R0:R", "C2:U", "C2:D" }, d.points);
            Assert.IsFalse(d.SetShiftSpec("R0:U"));
            CollectionAssert.AreEquivalent(new[] { "R0:R", "C2:U", "C2:D" }, d.points);
        }

        [Test]
        public void InsertRow_RenumbersPointsAndSolution()
        {
            var d = Draft(
                "# t  shift: R0:R, R1:L, C0:D  제한 9턴  목표 양 1\n" +
                "# 풀이: R1:L, C0:D\n" +
                ".   g\n" +
                ".S  .");
            d.InsertRow(1);
            Assert.AreEqual(3, d.height);
            Assert.AreEqual(new[] { ".", "g", ".", ".", ".S", "." }, d.cells.ToArray());
            CollectionAssert.AreEquivalent(new[] { "R0:R", "R2:L", "C0:D", "R1:R", "R1:L" }, d.points);
            Assert.AreEqual("R2:L, C0:D", d.solution);
            Parsed(d);
        }

        [Test]
        public void RemoveCol_DropsItsPointsAndMoves()
        {
            var d = Draft(
                "# t  shift: R0:R, C0:D, C1:U, C2:D  제한 9턴  목표 양 1\n" +
                "# 풀이: C1:U, C2:D\n" +
                ".   .  g\n" +
                ".S  .  .");
            d.RemoveCol(1);
            Assert.AreEqual(2, d.width);
            Assert.AreEqual(new[] { ".", "g", ".S", "." }, d.cells.ToArray());
            CollectionAssert.AreEquivalent(new[] { "R0:R", "C0:D", "C1:D" }, d.points);
            Assert.AreEqual("C1:D", d.solution);
            Parsed(d);
        }

        [Test]
        public void InsertCol_AtEdges()
        {
            var d = StageDraft.CreateNew("t");
            d.InsertCol(0);
            d.InsertCol(d.width);
            Assert.AreEqual(5, d.width);
            Assert.AreEqual(".", d.cells[0]);
            Assert.AreEqual("g", d.cells[3]);
            Parsed(d);
        }

        [Test]
        public void TryParse_ExplainsBadNames()
        {
            var d = StageDraft.CreateNew("my shift stage");
            Board board;
            string error;
            Assert.IsFalse(d.TryParse(out board, out error));
            StringAssert.Contains("shift", error);
            d.name = "";
            Assert.IsFalse(d.TryParse(out board, out error));
        }

        [Test]
        public void CellData_ParsesAndWritesTokens()
        {
            foreach (var token in new[] { ".", "-", "x", "~S(w)", ".S2S(w)W2", "^W(b)", ".HA", ".U", ".B(w)" })
                Assert.AreEqual(token, CellData.Parse(token).ToString());
        }
    }
}
