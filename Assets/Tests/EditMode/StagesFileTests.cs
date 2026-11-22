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
        internal static string ReadStagesText()
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "Levels", "stages.txt"));
        }

        /// <summary>스테이지 수는 에디터로 바뀔 수 있으므로 정해 두지 않는다. 하나 이상이고 이름이 겹치지 않으면 된다.</summary>
        [Test]
        public void StagesFile_HasLevelsWithDistinctNames()
        {
            var boards = LevelParser.ParseAll(ReadStagesText());
            Assert.Greater(boards.Count, 0);
            var names = new HashSet<string>();
            foreach (var board in boards) Assert.IsTrue(names.Add(board.Rules.Name), "이름이 겹친다: " + board.Rules.Name);
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

        /// <summary>
        /// 풀이 줄(`# 풀이:`)의 수순을 게임 규칙으로 실제로 두어 본다. 마지막 수에서 승리해야 하고,
        /// 풀이는 최적해이므로 길이가 별 3개 기준과 같아야 한다.
        /// </summary>
        [Test]
        public void StagesFile_EverySolutionWinsWithGameRules()
        {
            var boards = LevelParser.ParseAll(ReadStagesText());
            var failures = new List<string>();
            for (int i = 0; i < boards.Count; i++)
            {
                var board = boards[i].Clone();
                var rules = board.Rules;
                string where = (i + 1) + "번 '" + rules.Name + "': ";
                for (int k = 0; k < rules.Solution.Count; k++)
                {
                    if (board.Result != GameResult.InProgress)
                    {
                        failures.Add(where + (k + 1) + "번째 수 전에 판이 끝났다 (" + board.Result + ")");
                        break;
                    }
                    TurnRunner.PlayTurn(board, rules.Solution[k]);
                }
                if (board.Result != GameResult.Won)
                    failures.Add(where + "풀이 " + string.Join(", ", rules.Solution) + " 로 승리하지 못했다 (" + board.Result + ")");
                else if (rules.Solution.Count != rules.Star3Turns)
                    failures.Add(where + "풀이 길이 " + rules.Solution.Count + "턴이 별 3개 기준 " + rules.Star3Turns + "턴과 다르다");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
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
