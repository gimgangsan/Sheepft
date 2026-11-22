using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>
    /// 예전 Solver~/Tests의 규칙 테스트 가운데 게임 테스트에 같은 경우가 없던 것을 게임 규칙으로 옮겼다.
    /// 나머지(81개 중 68개)는 ShiftTests, TerrainTests, FireSpreadTests, WolfTests, GoatTests, HorseCowTests,
    /// TurnTests, LevelParserTests에 같은 경우가 이미 있다.
    /// </summary>
    public class LegacyRuleTests
    {
        private static Board Level(string text) => LevelParser.Parse(text);

        private static string Cell(Board board, int row, int col) => LevelWriter.WriteCell(board.At(new Pos(row, col)));

        /// <summary>수를 차례로 두고, 모든 턴에 켜진 기믹을 합쳐 돌려준다.</summary>
        private static Mechanic Play(Board board, string moves)
        {
            var all = Mechanic.None;
            foreach (var move in moves.Split(','))
            {
                TestLevel.Play(board, move);
                all |= board.Mechanics;
            }
            return all;
        }

        [Test]
        public void BurntFence_CanBePassedByShift()
        {
            var board = Level(
                "# t  shift: R0:R, R2:R  제한 9턴  목표 양 1\n" +
                ".S(w)  f  .\n" +
                "^      .  .\n" +
                ".      .  .");
            Play(board, "R0:R"); // 울타리에 막힌다. 턴 끝에 울타리가 탄다.
            TestLevel.AssertMap(
                ".S(w)  ^  .\n" +
                "^      .  .\n" +
                ".      .  .", board);
            var m = Play(board, "R0:R");
            TestLevel.AssertMap(
                ".  ^S  .\n" +
                "^  .   .\n" +
                ".  .   .", board);
            Assert.AreNotEqual(Mechanic.None, m & Mechanic.SheepEnterBurnt);
        }

        [Test]
        public void Bucket_WetEmptyFillsAgain()
        {
            var board = Level("# t  shift: R0:R  제한 9턴\n.B(w)  ~  .");
            Play(board, "R0:R");
            TestLevel.AssertMap(".  ~U  .", board);
        }

        [Test]
        public void Wolf_SeveralNearestSheep_EveryCloserTileIsACandidate()
        {
            // README 6절: 양을 하나로 고르지 않는다. 왼쪽·오른쪽·아래가 후보이고 상 > 우 > 하 > 좌로 오른쪽.
            var board = Level(
                "# t  shift: R3:R  제한 9턴  목표 양 1\n" +
                ".   .  .   .\n" +
                ".S  .  .W  .\n" +
                ".   .  .   .S\n" +
                ".   .  .   .");
            Play(board, "R3:R");
            Assert.AreEqual(".W", Cell(board, 1, 3));
            Play(board, "R3:R");
            Assert.AreEqual(GameResult.InProgress, board.Result); // 한 마리를 먹어도 목표 1마리는 남는다.
            Assert.AreEqual(".W", Cell(board, 2, 3));
        }

        [Test]
        public void Wolf_SheepCanWinNextToAnEatingWolf()
        {
            var board = Level("# t  shift: R0:R  제한 9턴  목표 양 1\n.S  .S  gW  #");
            Play(board, "R0:R");
            Assert.AreEqual(GameResult.InProgress, board.Result);
            Play(board, "R0:R");
            Assert.AreEqual(GameResult.Won, board.Result);
        }

        [Test]
        public void Wolf_MergingComesBeforeCatchingInTheSameTile()
        {
            var board = Level(
                "# t  shift: R0:R, R2:R  제한 9턴  목표 양 1\n" +
                ".SW  .W  #\n" +
                ".    .   .\n" +
                ".    .   .");
            Play(board, "R0:R");
            Assert.AreEqual(GameResult.InProgress, board.Result);
            Assert.AreEqual(".SW2", Cell(board, 0, 1));
            Play(board, "R2:R");
            Assert.AreEqual(GameResult.Lost, board.Result); // 기절이 풀리는 순간 먹힌다.
        }

        [Test]
        public void Wolf_CatchRangeIgnoresBlanks()
        {
            var board = Level(
                "# t  shift: R1:R  제한 9턴  목표 양 1\n" +
                ".W2  x\n" +
                "x    .S");
            Play(board, "R1:R");
            Assert.AreEqual(GameResult.Lost, board.Result);
        }

        [Test]
        public void Wolf_WalksBeforeTheFireSpreads()
        {
            // 늑대가 풀로 걸어 들어간 뒤 그 풀이 탄다. 확산이 먼저였다면 늑대는 들불을 피했을 것이다.
            var board = Level(
                "# t  shift: R2:R  제한 9턴  목표 양 1\n" +
                "^   .  .\n" +
                ".W  g  .S\n" +
                ".   .  .");
            var m = Play(board, "R2:R");
            Assert.AreEqual("^W(b)", Cell(board, 1, 1));
            Assert.AreNotEqual(Mechanic.None, m & Mechanic.WolfBurned);
        }

        [Test]
        public void Cow_SmellsAcrossABlankButCannotWalkAcrossIt()
        {
            var board = Level(
                "# t  shift: R1:R  제한 9턴\n" +
                ".M  x  .A\n" +
                "x   x  .");
            var m = Play(board, "R1:R");
            Assert.AreEqual(Mechanic.CowSmell | Mechanic.CowNoPath, m & (Mechanic.CowSmell | Mechanic.CowNoPath));
            Assert.AreEqual(".M", Cell(board, 0, 0));
        }

        [Test]
        public void Cow_EatsABasketStuckAgainstABlank()
        {
            var board = Level(
                "# t  shift: R1:R  제한 9턴\n" +
                ".M  .A  x  .\n" +
                ".   .   .  .");
            Play(board, "R1:R");
            TestLevel.AssertMap(
                ".  .M  x  .\n" +
                ".  .   .  .", board);
        }

        [Test]
        public void Cow_WaitsWhenItsBasketIsOnWater()
        {
            var board = Level(
                "# t  shift: R1:R  제한 9턴\n" +
                ".M  .  ~A\n" +
                ".   .  .");
            var m = Play(board, "R1:R");
            TestLevel.AssertMap(
                ".M  .  ~A(w)\n" +
                ".   .  .", board);
            Assert.AreNotEqual(Mechanic.None, m & Mechanic.CowNoPath);
        }

        [Test]
        public void Cow_AvoidsWaterItselfButPushesAWolfIntoIt()
        {
            var board = Level(
                "# t  shift: R3:R  제한 9턴  목표 양 1\n" +
                ".M  .W  ~  .A\n" +
                ".   .   .  .\n" +
                ".   .   .  .S\n" +
                ".   .   .  .");
            var m = Play(board, "R3:R");
            // 소는 물을 피해 오른쪽으로 한 칸 가며 늑대를 물로 민다. 젖은 늑대는 물에서 걸어 나온다.
            TestLevel.AssertMap(
                ".  .M  ~  .W(w)A\n" +
                ".  .   .  .\n" +
                ".  .   .  .S\n" +
                ".  .   .  .", board);
            Assert.AreNotEqual(Mechanic.None, m & Mechanic.CowPushWolf);
        }

        [Test]
        public void Settle_PackCatchesBeforeAHorseEatsAndDropsItsSheep()
        {
            // 말이 (1,1)에 들어서면 무리(2,2)의 범위 안이다. 늑대 잡기가 말 먹기보다 먼저라서 양이 먹힌다.
            var board = Level(
                "# t  shift: R3:R  제한 9턴  목표 양 1\n" +
                "#    .A  .    .\n" +
                ".HS  .   ~    .A\n" +
                ".    ~   .W2  ~\n" +
                "x    x   x    .");
            var m = Play(board, "R3:R");
            Assert.AreEqual(GameResult.Lost, board.Result);
            Assert.AreNotEqual(Mechanic.None, m & Mechanic.WolfEat);
            Assert.AreEqual(Mechanic.None, m & Mechanic.HorseEat);
        }

        [Test]
        public void Settle_NothingRunsBeforeTheFirstShift()
        {
            // 처음부터 겹쳐 있어도 첫 shift 뒤의 정리에서 처리된다.
            var board = Level(
                "# t  shift: R1:R  제한 9턴  목표 양 1\n" +
                ".SW  .\n" +
                ".    .");
            Assert.AreEqual(".SW", Cell(board, 0, 0));
            Play(board, "R1:R");
            Assert.AreEqual(GameResult.Lost, board.Result);
        }
    }
}
