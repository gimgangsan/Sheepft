using System.Linq;
using NUnit.Framework;
using Sheepft.Core;
using Sheepft.Tools;

namespace Sheepft.Tests
{
    /// <summary>스테이지 검증용 solver. 예전 Solver~/Tests/SolverTests.cs를 옮겼다.</summary>
    public class SolverTests
    {
        private const string ReadmeExample =
            "# 예시  shift: R1:RL, C2:UD  제한 4턴  목표 양 2\n" +
            ".   .   g   .\n" +
            ".S  .   .S  #\n" +
            ".   .   .   .\n" +
            ".   .   .   .";

        private const string ReadmeVoidExample =
            "# 빈칸 예시  shift: R0:R, C4:UD  제한 3턴  목표 양 1\n" +
            ".   .S  x   x   .\n" +
            ".   .   .   .   .\n" +
            "x   x   x   .   g";

        [Test]
        public void SolvesReadmeExampleInThreeTurns()
        {
            var r = Solver.Solve(LevelParser.Parse(ReadmeExample));
            Assert.AreEqual(3, r.OptimalTurns);
            CollectionAssert.Contains(r.Solutions.Select(s => s.MovesText).ToList(), "R1:R, R1:R, C2:U");
            Assert.IsTrue(r.Solutions.All(s => (s.Mechanics & Mechanic.SheepMerge) != 0));
        }

        [Test]
        public void SolvesReadmeVoidExampleExactlyAsDescribed()
        {
            var r = Solver.Solve(LevelParser.Parse(ReadmeVoidExample));
            Assert.AreEqual(2, r.OptimalTurns);
            Assert.AreEqual(1, r.OptimalSequences);
            Assert.AreEqual("R0:R, C4:U", r.Solutions[0].MovesText);
            Assert.AreNotEqual(Mechanic.None, r.Solutions[0].Mechanics & Mechanic.VoidSkip);
            Assert.AreNotEqual(Mechanic.None, r.Solutions[0].Mechanics & Mechanic.SheepWrap);
        }

        [Test]
        public void ReportsShortestSolutionThatSkipsIntendedMechanic()
        {
            var r = Solver.Solve(LevelParser.Parse(
                "# t  shift: R0:RL  제한 5턴  목표 양 1\n" +
                "@intent SheepWrap\n" +
                ".S  .  .  g"));
            Assert.AreEqual(1, r.OptimalTurns);   // R0:L로 순환
            Assert.AreEqual(1, r.IntendedTurns);
            Assert.AreEqual(3, r.BypassTurns);    // R0:R 세 번이면 순환 없이도 깨진다
            Assert.AreEqual("R0:R, R0:R, R0:R", r.Bypass.MovesText);
            Assert.AreEqual(Mechanic.SheepWrap, r.BypassMissing);
        }

        [Test]
        public void FindsNoBypassWhenTurnLimitRulesItOut()
        {
            var r = Solver.Solve(LevelParser.Parse(
                "# t  shift: R0:RL  제한 2턴  목표 양 1\n" +
                "@intent SheepWrap\n" +
                ".S  .  .  g"));
            Assert.AreEqual(1, r.OptimalTurns);
            Assert.AreEqual(-1, r.BypassTurns);
        }

        [Test]
        public void CountsMoveSequencesAndStatePathsSeparately()
        {
            // 빈 줄 R1은 두 방향 모두 같은 "대기"라서 수순으로는 둘이어도 상태 경로는 하나다.
            var r = Solver.Solve(LevelParser.Parse(
                "# t  shift: R0:R, R1:RL  제한 4턴  목표 양 1\n" +
                ".S  .  .  g\n" +
                ".   .  .  ."));
            Assert.AreEqual(3, r.OptimalTurns);
            Assert.AreEqual(1, r.OptimalSequences);
            Assert.AreEqual(1, r.OptimalStatePaths);

            var wait = Solver.Solve(LevelParser.Parse(
                "# t  shift: R1:RL  제한 5턴  목표 양 1\n" +
                ".G  .S  .  g\n" +
                ".   .   .  ."));
            Assert.AreEqual(2, wait.OptimalTurns);
            Assert.AreEqual(4, wait.OptimalSequences); // R1:R/R1:L 두 번
            Assert.AreEqual(1, wait.OptimalStatePaths);
        }

        [Test]
        public void WaitingMovesCountAsMoves()
        {
            // 밀 수 있는 줄은 빈 줄뿐이다. 두 턴을 기다리면 염소가 양을 풀까지 밀어 준다.
            var r = Solver.Solve(LevelParser.Parse(
                "# t  shift: R1:R  제한 5턴  목표 양 1\n" +
                ".G  .S  .  g\n" +
                ".   .   .  ."));
            Assert.AreEqual(2, r.OptimalTurns);
            Assert.AreEqual("R1:R, R1:R", r.Solutions[0].MovesText);
            Assert.AreNotEqual(Mechanic.None, r.Solutions[0].Mechanics & Mechanic.Wait);
            Assert.AreNotEqual(Mechanic.None, r.Solutions[0].Mechanics & Mechanic.GoatPushSheep);
        }

        [Test]
        public void ReportsUnsolvableLevels()
        {
            var r = Solver.Solve(LevelParser.Parse(
                "# t  shift: R0:R  제한 5턴  목표 양 1\n" +
                ".S  #  g"));
            Assert.AreEqual(-1, r.OptimalTurns);
            Assert.IsEmpty(r.Solutions);
        }

        [Test]
        public void EveryReportedSolutionWinsWithGameRules()
        {
            var level = LevelParser.Parse(
                "# t  shift: R0:RL  제한 6턴  목표 양 1\n" +
                ".S  .W  #  .  g");
            var r = Solver.Solve(level);
            Assert.Greater(r.OptimalTurns, 0);
            foreach (var s in r.Solutions)
            {
                var board = level.Clone();
                foreach (var m in s.Moves) TurnRunner.PlayTurn(board, m);
                Assert.AreEqual(GameResult.Won, board.Result, s.MovesText);
            }
        }

        [Test]
        public void OptimalIntentFailsWhenAnOptimalSolutionSkipsIt()
        {
            // 최적 1턴(R0:L, 순환)과 R0:R 세 번. 최적 길이에서는 늘 순환하므로 @optimal SheepWrap은 지켜진다.
            var ok = Solver.Solve(LevelParser.Parse(
                "# t  shift: R0:RL  제한 5턴  목표 양 1\n" +
                "@optimal SheepWrap\n" +
                ".S  .  .  g"));
            Assert.AreEqual(Mechanic.None, ok.OptimalMissing);

            var bad = Solver.Solve(LevelParser.Parse(
                "# t  shift: R0:RL  제한 5턴  목표 양 1\n" +
                "@optimal Wait\n" +
                ".S  .  .  g"));
            Assert.AreEqual(Mechanic.Wait, bad.OptimalMissing);
        }

        [Test]
        public void SolverIgnoresTurnLimitWhenSearchingDeeper()
        {
            // prune은 제한 턴보다 깊이 찾아본다. 헤더의 제한(1턴)에 막히지 않고 3턴 해를 찾아야 한다.
            var r = Solver.Solve(LevelParser.Parse(
                "# t  shift: R0:R  제한 1턴  목표 양 1\n" +
                ".S  .  .  g"), new SolveOptions { MaxDepth = 5 });
            Assert.AreEqual(3, r.OptimalTurns);
        }

        [Test]
        public void ChecksSolutionLineWithGameRules()
        {
            var good = LevelParser.Parse(
                "# t  shift: R0:RL  제한 2턴  목표 양 1\n" +
                "# 풀이: R0:L\n" +
                "@intent SheepWrap\n" +
                "@stars 1 2\n" +
                ".S  .  .  g");
            var check = StageChecker.Check(good, 1);
            Assert.IsTrue(check.Ok, string.Join("; ", check.Problems));

            var bad = LevelParser.Parse(
                "# t  shift: R0:RL  제한 2턴  목표 양 1\n" +
                "# 풀이: R0:R\n" +
                "@intent SheepWrap\n" +
                "@stars 1 2\n" +
                ".S  .  .  g");
            Assert.IsTrue(StageChecker.Check(bad, 1).Problems.Any(p => p.StartsWith("풀이 줄")));
        }

        [Test]
        public void CheckerAllowsBypassSlowerThanOptimal()
        {
            // 의도는 순환. 최적은 R0:L 1턴(순환), 순환 없이 R0:R 세 번은 3턴이라 최적보다 느리다.
            var level = LevelParser.Parse(
                "# t  shift: R0:RL  제한 5턴  목표 양 1\n" +
                "@intent SheepWrap\n" +
                "@stars 1 2\n" +
                ".S  .  .  g");
            var check = StageChecker.Check(level, 1);
            Assert.AreEqual(3, check.Report.BypassTurns);
            Assert.IsTrue(check.Ok, string.Join("; ", check.Problems));
        }

        [Test]
        public void CheckerRejectsBypassAsFastAsOptimal()
        {
            // 의도는 순환. R0:R 두 번(순환)과 R0:L 두 번(순환 없음)이 둘 다 2턴이다.
            var level = LevelParser.Parse(
                "# t  shift: R0:RL  제한 3턴  목표 양 1\n" +
                "@intent SheepWrap\n" +
                "@stars 2 3\n" +
                "g  .  .S  .");
            var check = StageChecker.Check(level, 1);
            Assert.AreEqual(2, check.Report.BypassTurns);
            Assert.IsTrue(check.Problems.Any(p => p.StartsWith("우회해")), string.Join("; ", check.Problems));
        }

        [Test]
        public void PrunerRemovesPointThatBypassesIntent()
        {
            // 의도는 순환. R0:L 두 번이면 순환 없이 최적과 같은 2턴에 깨지므로 R0:L을 빼야 한다.
            var level = LevelParser.Parse(
                "# t  shift: R0:R  제한 3턴  목표 양 1\n" +
                "@intent SheepWrap\n" +
                "@stars 2 3\n" +
                "g  .  .S  .");
            var result = Pruner.Run(level);
            Assert.IsTrue(result.Found, result.Text);
            StringAssert.DoesNotContain("R0:RL", result.Spec);
            StringAssert.DoesNotContain("R0:L", result.Spec);
        }
    }
}
