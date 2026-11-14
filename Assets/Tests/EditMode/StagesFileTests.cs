using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sheepft.Core;
using UnityEngine;

namespace Sheepft.Tests
{
    /// <summary>실제 레벨 파일 Assets/Levels/stages.txt를 게임 파서로 읽는다.</summary>
    public class StagesFileTests
    {
        private const int StageCount = 30;

        private static string ReadStagesText()
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "Levels", "stages.txt"));
        }

        [Test]
        public void StagesFile_HasThirtyLevels()
        {
            Assert.AreEqual(StageCount, LevelParser.ParseAll(ReadStagesText()).Count);
        }

        [Test]
        public void StagesFile_EveryLevelHasSolutionDescriptionAndStars()
        {
            var boards = LevelParser.ParseAll(ReadStagesText());
            for (int i = 0; i < boards.Count; i++)
            {
                var rules = boards[i].Rules;
                string where = (i + 1) + "번 '" + rules.Name + "': ";
                Assert.IsNotEmpty(rules.Name, where + "이름");
                Assert.Greater(rules.Solution.Count, 0, where + "풀이 줄");
                Assert.IsNotEmpty(rules.Description, where + "설명");
                Assert.Greater(rules.Star3Turns, 0, where + "별 3개 기준");
                Assert.Greater(rules.Star2Turns, 0, where + "별 2개 기준");
                Assert.Greater(rules.ShiftPoints.Count, 0, where + "shift 포인트");
                Assert.Greater(rules.TurnLimit, 0, where + "제한 턴");
                Assert.Greater(rules.TargetSheep, 0, where + "목표 양");
            }
        }

        [Test]
        public void StagesFile_GridsSurviveRoundTrip()
        {
            string text = ReadStagesText();
            var boards = LevelParser.ParseAll(text);
            var grids = OriginalGrids(text);
            Assert.AreEqual(boards.Count, grids.Count);
            for (int i = 0; i < boards.Count; i++)
            {
                string written = LevelWriter.Write(boards[i]);
                // 파일의 열 간격은 손으로 맞춘 것이라 writer와 다르다. 공백을 하나로 줄여 비교한다.
                Assert.AreEqual(grids[i], CollapseSpaces(written), (i + 1) + "번 '" + boards[i].Rules.Name + "'");
            }
        }

        /// <summary>파서를 거치지 않고 레벨마다 맵 줄만 모은다. 공백은 하나로 줄인다.</summary>
        private static List<string> OriginalGrids(string text)
        {
            var grids = new List<string>();
            List<string> rows = null;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("@")) continue;
                if (line.StartsWith("#") && line.Contains("풀이")) continue;
                if (line.StartsWith("#") && line.Contains("shift"))
                {
                    if (rows != null) grids.Add(string.Join("\n", rows));
                    rows = new List<string>();
                    continue;
                }
                rows.Add(CollapseSpaces(line));
            }
            if (rows != null) grids.Add(string.Join("\n", rows));
            return grids;
        }

        private static string CollapseSpaces(string s)
        {
            return Regex.Replace(s, "[ \t]+", " ");
        }
    }
}
