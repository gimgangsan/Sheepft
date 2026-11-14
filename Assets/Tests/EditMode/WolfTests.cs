using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>
    /// 늑대: 양 잡기, 합류, 걷기, 기절·식사 기간. README 2·4·6절, 구현 계획 2절 "늑대 합류" 표.
    /// 양이 먹혀도 게임이 끝나지 않게 대부분 목표 양 0으로 만든다(승리·패배 판정 없음).
    /// 아무것도 없는 줄을 밀어 "대기"하는 수를 자주 쓴다.
    /// </summary>
    public class WolfTests
    {
        private static Wolf[] Wolves(Board board)
        {
            return board.Objects.OfType<Wolf>().OrderBy(w => w.Id).ToArray();
        }

        private static Wolf TheWolf(Board board)
        {
            return board.Objects.OfType<Wolf>().Single();
        }

        // ── 양 잡기 ──

        [Test]
        public void Catch_SingleWolfEatsSheepOnItsTile()
        {
            var board = TestLevel.AssertAfter(".S .W #", "R0:R", ". .W #", target: 0);
            Assert.IsTrue(board.Events.OfType<RemovedEvent>().Any(e => e.Cause == RemoveCause.Eaten));
            Assert.IsTrue(board.Events.OfType<StatusChangedEvent>().Any(e => e.Status == StatusKind.Eating && e.On));
            Assert.IsTrue(TheWolf(board).IsEating);
        }

        [Test]
        public void Catch_SingleWolfDoesNotReachNeighbourTiles()
        {
            // 1마리의 범위는 같은 칸뿐이다. 물 위의 양에게는 걸어가지도 못한다.
            TestLevel.AssertAfter(
                ".W ~S\n" +
                ".  .",
                "R1:R",
                ".W ~S(w)\n" +
                ".  .", target: 0);
        }

        [Test]
        public void Catch_PackOfTwoLeavesOneOfThreeSheep_AndEatsItWhenMealEnds()
        {
            // README: 양 3마리가 겹친 칸을 2마리 무리가 잡으면 1마리가 남는다.
            var board = TestLevel.AssertAfter(".S3 .W2 #", "R0:R", ". .SW2 #", target: 0);
            Assert.AreEqual(1, TheWolf(board).MealLeft);

            // 식사 중에는 잡지 않는다. 남은 양은 식사가 끝날 때 범위 안에 있으면 그때 먹힌다.
            TestLevel.Play(board, "R0:R");
            TestLevel.AssertMap(". .W2 #", board);
            // 턴 끝의 해제 뒤에 걸린 식사는 다음 턴 끝까지만 간다.
            Assert.AreEqual(1, TheWolf(board).MealLeft);
        }

        [Test]
        public void Catch_PackRangeIsSquare_IncludesDiagonal()
        {
            TestLevel.AssertAfter(
                ".S .   .\n" +
                ".  .W2 .\n" +
                ".  .   .",
                "R2:R",
                ".  .   .\n" +
                ".  .W2 .\n" +
                ".  .   .", target: 0);
        }

        [Test]
        public void Catch_RangeIgnoresObstacles()
        {
            TestLevel.AssertAfter(
                ".W2 # .S\n" +
                "#   # .\n" +
                ".   . .",
                "R2:R",
                ".W2 # .S\n" +
                "#   # .\n" +
                ".   . .", target: 0);

            TestLevel.AssertAfter(
                ".W3 # .S\n" +
                "#   # .\n" +
                ".   . .",
                "R2:R",
                ".W3 # .\n" +
                "#   # .\n" +
                ".   . .", target: 0);
        }

        [Test]
        public void Catch_RangeDoesNotWrapAroundTheMap()
        {
            // 순환한다면 (0,3)은 (0,0)의 바로 옆이다.
            TestLevel.AssertAfter(
                ".W2 # . .S\n" +
                "#   # . .\n" +
                ".   . . .",
                "R2:R",
                ".W2 # . .S\n" +
                "#   # . .\n" +
                ".   . . .", target: 0);
        }

        [Test]
        public void Catch_SameDistance_RowMajorOrder_UpToPackSize()
        {
            TestLevel.AssertAfter(
                ".  .   .S\n" +
                ".S .W2 .\n" +
                ".  .S  .\n" +
                ".  .   .",
                "R3:R",
                ".  .   .\n" +
                ".  .W2 .\n" +
                ".  .S  .\n" +
                ".  .   .", target: 0);
        }

        [Test]
        public void Catch_NearerTilesFirst()
        {
            TestLevel.AssertAfter(
                ".S .  .S  .  .\n" +
                ".  .S .   .  .\n" +
                ".  .  .W3 .S .\n" +
                ".  .  .   .S .\n" +
                ".  .  .   .  .\n" +
                ".  .  .   .  .",
                "R5:R",
                ".S .  .S  .  .\n" +
                ".  .  .   .  .\n" +
                ".  .  .W3 .  .\n" +
                ".  .  .   .  .\n" +
                ".  .  .   .  .\n" +
                ".  .  .   .  .", target: 0);
        }

        [Test]
        public void Catch_WetOrBurnedSheepFirstWithinATile()
        {
            TestLevel.AssertAfter(".SS(w)S(b) .W2 #", "R0:R", ". .SW2 #", target: 0);
        }

        [Test]
        public void Catch_BurnedWolfDoesNotCatch_UntilCured()
        {
            var board = TestLevel.AssertAfter(
                ".S .W(b) #\n" +
                ".  ~     .",
                "R0:R",
                ".  .SW(b) #\n" +
                ".  ~      .", target: 0);

            // 같은 칸에 양이 있는 채로 화상이 풀리면 그 순간 잡는다.
            TestLevel.Play(board, "C1:D");
            TestLevel.AssertMap(
                ".  .     #\n" +
                ".  ~W(w) .", board);
            Assert.IsTrue(TheWolf(board).IsEating);
        }

        [Test]
        public void Catch_EatingBelowTarget_LosesAtOnce()
        {
            var board = TestLevel.Make(".S .W #");
            TestLevel.Play(board, "R0:R");
            Assert.AreEqual(GameResult.Lost, board.Result);
            Assert.AreEqual(1, board.TurnsUsed);
        }

        [Test]
        public void Catch_OverlappingAWolfOnGrass_IsHandledBeforeWinning()
        {
            var board = TestLevel.Make(".S gW #");
            TestLevel.Play(board, "R0:R");
            Assert.AreEqual(GameResult.Lost, board.Result);
        }

        [Test]
        public void Catch_EnteringPackRangeOnGrass_IsHandledBeforeWinning()
        {
            var board = TestLevel.Make(
                ".S g\n" +
                ".  .W2");
            TestLevel.Play(board, "R0:R");
            Assert.AreEqual(GameResult.Lost, board.Result);
        }

        [Test]
        public void Win_WhenSheepReachesGrassJustBeforeWolfWalksIn()
        {
            // README: 늑대가 걸어오기 직전에 양이 풀에 도착하면 클리어다.
            var board = TestLevel.Make(
                ".S g\n" +
                ".  .W");
            TestLevel.Play(board, "R0:R");
            Assert.AreEqual(GameResult.Won, board.Result);
            TestLevel.AssertMap(
                ".  gS\n" +
                ".  .W", board);
        }

        // ── 걷기 ──

        [Test]
        public void Walk_OneStepTowardSheep()
        {
            var board = TestLevel.AssertAfter(
                ".W . . .S\n" +
                ".  . . .",
                "R1:R",
                ".  .W . .S\n" +
                ".  .  . .", target: 0);
            Assert.AreEqual(Dir.Right, TheWolf(board).LastWalkDir);
            var moved = board.Events.OfType<MovedEvent>().Single();
            Assert.AreEqual(MoveCause.Walk, moved.Cause);
        }

        [Test]
        public void Walk_WalksOntoSheepAndEatsIt()
        {
            var board = TestLevel.AssertAfter(
                ".W .S\n" +
                ".  .",
                "R1:R",
                ".  .W\n" +
                ".  .", target: 0);
            Assert.IsTrue(TheWolf(board).IsEating);
        }

        [Test]
        public void Walk_ShortestPath_AroundStoneWaterAndFire()
        {
            const string moves = "R4:R, R4:R, R4:R, R4:R";
            TestLevel.AssertAfter(
                ".W #  .S\n" +
                ".  ~  .\n" +
                ".  ^  .\n" +
                ".  .  .\n" +
                ".  .  .",
                moves,
                ".  #  .S\n" +
                ".  ~  .\n" +
                ".  ^  .\n" +
                ".  .W .\n" +
                ".  .  .", target: 0);
        }

        [Test]
        public void Walk_GoesToNearestSheepByPathDistance()
        {
            // (0,0)의 양은 직선으로는 같은 3칸이지만 걸어서는 7칸이다. (3,3)의 양 쪽으로 간다.
            TestLevel.AssertAfter(
                ".S #  .  .\n" +
                ".  #  .W .\n" +
                ".  #  .  .\n" +
                ".  .  .  .S\n" +
                ".  .  .  .",
                "R4:R",
                ".S #  .  .\n" +
                ".  #  .  .W\n" +
                ".  #  .  .\n" +
                ".  .  .  .S\n" +
                ".  .  .  .", target: 0);
        }

        [Test]
        public void Walk_NoPath_Waits()
        {
            TestLevel.AssertAfter(
                ".W # .S\n" +
                "#  . .\n" +
                ".  . .",
                "R2:R",
                ".W # .S\n" +
                "#  . .\n" +
                ".  . .", target: 0);
        }

        [Test]
        public void Walk_SheepOnWaterOrFire_IsNotReachable()
        {
            TestLevel.AssertAfter(
                ".W . ~S\n" +
                ".  . .",
                "R1:R",
                ".W . ~S(w)\n" +
                ".  . .", target: 0);

            TestLevel.AssertAfter(
                ".W . ^S(b)\n" +
                ".  . .",
                "R1:R",
                ".W . ^S(b)\n" +
                ".  . .", target: 0);
        }

        [Test]
        public void Walk_DoesNotWrapAroundTheMap()
        {
            // 순환한다면 (0,0)에서 왼쪽으로 한 칸이면 양에게 닿는다.
            TestLevel.AssertAfter(
                ".W # . .S\n" +
                ".  # . .",
                "R1:R",
                ".W # . .S\n" +
                ".  # . .", target: 0);
        }

        [Test]
        public void Walk_BlankTilesAreObstacles()
        {
            TestLevel.AssertAfter(
                ".W x .S\n" +
                ".  - .",
                "R1:R",
                ".W x .S\n" +
                ".  - .", target: 0);
        }

        [Test]
        public void Walk_Tie_UpThenRightThenDownThenLeft()
        {
            // 위와 오른쪽이 같으면 위.
            TestLevel.AssertAfter(
                ".  .  .S\n" +
                ".  .  .\n" +
                ".W .  .\n" +
                ".  .  .",
                "R3:R",
                ".  .  .S\n" +
                ".W .  .\n" +
                ".  .  .\n" +
                ".  .  .", target: 0);

            // 오른쪽과 아래가 같으면 오른쪽.
            TestLevel.AssertAfter(
                ".W .  .\n" +
                ".  .  .\n" +
                ".  .  .S\n" +
                ".  .  .",
                "R3:R",
                ".  .W .\n" +
                ".  .  .\n" +
                ".  .  .S\n" +
                ".  .  .", target: 0);

            // 아래와 왼쪽이 같으면 아래.
            TestLevel.AssertAfter(
                ".  .  .W\n" +
                ".  .  .\n" +
                ".S .  .\n" +
                ".  .  .",
                "R3:R",
                ".  .  .\n" +
                ".  .  .W\n" +
                ".S .  .\n" +
                ".  .  .", target: 0);
        }

        [Test]
        public void Walk_Tie_PrefersLastWalkedDirection()
        {
            // 첫 걸음은 오른쪽뿐이다. 둘째 걸음은 위와 오른쪽이 같은데, 마지막에 걸은 오른쪽으로 간다.
            var board = TestLevel.AssertAfter(
                ".  .  .  .S\n" +
                "#  .  .  .\n" +
                ".W .  .  .\n" +
                ".  .  .  .",
                "R3:R, R3:R",
                ".  .  .  .S\n" +
                "#  .  .  .\n" +
                ".  .  .W .\n" +
                ".  .  .  .", target: 0);
            Assert.AreEqual(Dir.Right, TheWolf(board).LastWalkDir);
        }

        [Test]
        public void Walk_ShiftDoesNotCountAsLastWalkedDirection()
        {
            // 늑대가 shift로 오른쪽으로 밀린 뒤에도 동점이면 위로 간다.
            var board = TestLevel.AssertAfter(
                ".  .  .  .S\n" +
                ".  .  .  .\n" +
                ".W .  .  .\n" +
                ".  .  .  .",
                "R2:R",
                ".  .  .  .S\n" +
                ".  .W .  .\n" +
                ".  .  .  .\n" +
                ".  .  .  .", target: 0);
            Assert.AreEqual(Dir.Up, TheWolf(board).LastWalkDir);
        }

        [Test]
        public void Walk_LeavesOtherObjectsOnTheTileBehind()
        {
            TestLevel.AssertAfter(
                ".WB . .S\n" +
                ".   . .",
                "R1:R",
                ".B .W .S\n" +
                ".  .  .", target: 0);
        }

        [Test]
        public void Walk_WolfStandingOnWaterCanWalkOut()
        {
            TestLevel.AssertAfter(
                "~W . .S\n" +
                ".  . .",
                "R1:R",
                "~  .W(w) .S\n" +
                ".  .     .", target: 0);
        }

        [Test]
        public void Walk_BurnedWolfDoesNotWalk()
        {
            TestLevel.AssertAfter(
                ".W(b) . .S\n" +
                ".     . .",
                "R1:R",
                ".W(b) . .S\n" +
                ".     . .", target: 0);
        }

        [Test]
        public void Walk_EveryWolfMovesOncePerTurn()
        {
            TestLevel.AssertAfter(
                ".W .  .  .\n" +
                ".  .  .  .S\n" +
                ".W .  .  .\n" +
                ".  .  .  .",
                "R3:R",
                ".  .W .  .\n" +
                ".W .  .  .S\n" +
                ".  .  .  .\n" +
                ".  .  .  .", target: 0);
        }

        // ── 기절·식사 기간 ──

        [Test]
        public void Meal_LastsThroughTheEndOfTheNextTurn()
        {
            var board = TestLevel.Make(
                ".S .W #\n" +
                ".  .  .\n" +
                ".  .S .\n" +
                ".  .  .", target: 0);

            // 1턴: 먹는다. 식사는 이 턴의 남은 시간과 다음 턴 끝까지다.
            TestLevel.Play(board, "R0:R");
            Assert.AreEqual(1, TheWolf(board).MealLeft);
            Assert.IsTrue(TheWolf(board).IsIncapacitated(board));

            // 2턴: 식사 중이라 걷지 않는다. 턴 끝에 풀린다.
            TestLevel.Play(board, "R3:R");
            Assert.AreEqual(new Pos(0, 1), TheWolf(board).Pos);
            Assert.AreEqual(0, TheWolf(board).MealLeft);
            Assert.IsTrue(board.Events.OfType<StatusChangedEvent>().Any(e => e.Status == StatusKind.Eating && !e.On));

            // 3턴: 다시 걷는다.
            TestLevel.Play(board, "R3:R");
            Assert.AreEqual(new Pos(1, 1), TheWolf(board).Pos);
        }

        // ── 합류 ──

        [Test]
        public void Merge_AddsCounts_AndStuns()
        {
            var board = TestLevel.AssertAfter(".W3 .W2 #", "R0:R", ". .W5 #", target: 0);
            var pack = TheWolf(board);
            Assert.AreEqual(0, pack.Id, "Id가 가장 작은 늑대가 흡수한다.");
            Assert.AreEqual(5, pack.Count);
            Assert.IsTrue(pack.IsStunned);
            Assert.AreEqual(1, pack.StunLeft);
            Assert.IsTrue(board.Events.OfType<RemovedEvent>().Any(e => e.Id == 1 && e.Cause == RemoveCause.Merged));
            Assert.IsTrue(board.Events.OfType<CountChangedEvent>().Any(e => e.Id == 0 && e.Count == 5));
            Assert.IsTrue(board.Events.OfType<StatusChangedEvent>().Any(e => e.Status == StatusKind.Stunned && e.On));
        }

        [Test]
        public void Merge_SeveralPacksAtOnce()
        {
            var board = TestLevel.Make(".W .W2 .W .", target: 0);
            var wolves = Wolves(board);
            board.Move(wolves[0], new Pos(0, 1), MoveCause.Shift);
            board.Move(wolves[2], new Pos(0, 1), MoveCause.Shift);
            Settler.Settle(board);

            TestLevel.AssertMap(". .W4 . .", board);
            Assert.AreEqual(0, TheWolf(board).Id);
            Assert.AreEqual(1, board.Events.OfType<CountChangedEvent>().Count(), "한 번에 합친다.");
        }

        [Test]
        public void Merge_WetPlusDry_IsWet()
        {
            TestLevel.AssertAfter(".W(w) .W #", "R0:R", ". .W2(w) #", target: 0);
            TestLevel.AssertAfter(".W .W(w) #", "R0:R", ". .W2(w) #", target: 0);
        }

        [Test]
        public void Merge_BothEating_KeepsTheLongerMeal()
        {
            var board = TestLevel.Make(".W .W . .", target: 0);
            var wolves = Wolves(board);
            wolves[0].MealLeft = 1;
            wolves[1].MealLeft = 2;
            board.Move(wolves[1], new Pos(0, 0), MoveCause.Shift);
            Settler.Settle(board);

            var pack = TheWolf(board);
            Assert.AreEqual(2, pack.Count);
            Assert.AreEqual(2, pack.MealLeft);
            Assert.AreEqual(2, pack.StunLeft);
        }

        [Test]
        public void Merge_EatingPlusNotEating_KeepsEating()
        {
            var board = TestLevel.Make(".W .W . .", target: 0);
            var wolves = Wolves(board);
            wolves[1].MealLeft = 1;
            board.Move(wolves[1], new Pos(0, 0), MoveCause.Shift);
            Settler.Settle(board);

            var pack = TheWolf(board);
            Assert.AreEqual(0, pack.Id);
            Assert.AreEqual(1, pack.MealLeft);
            Assert.IsTrue(pack.IsEating);
            Assert.IsTrue(board.Events.OfType<StatusChangedEvent>().Any(e => e.Id == 0 && e.Status == StatusKind.Eating && e.On));
        }

        [Test]
        public void Merge_StunnedPack_DoesNotCatch_ThenCatchesWithNewRangeWhenStunEnds()
        {
            // 합류 직후 기절하고, 기절 중에는 범위 안의 양을 잡지 않는다.
            var board = TestLevel.AssertAfter(
                ".W .W #\n" +
                ".  .S .",
                "R0:R",
                ".  .W2 #\n" +
                ".  .S  .", target: 0);
            Assert.AreEqual(1, TheWolf(board).StunLeft);

            // 다음 턴 동안에도 잡지 않다가, 기절이 풀리는 턴 끝 정리에서 새 범위(n − 1)로 잡는다.
            TestLevel.Play(board, "R0:R");
            TestLevel.AssertMap(
                ".  .W2 #\n" +
                ".  .   .", board);
            var pack = TheWolf(board);
            Assert.AreEqual(0, pack.StunLeft);
            Assert.AreEqual(1, pack.MealLeft);

            // 기절 해제(StepEnd)가 양이 먹히는 것보다 먼저 기록된다.
            var events = board.Events;
            int released = events.FindIndex(e => e is StatusChangedEvent s && s.Status == StatusKind.Stunned && !s.On);
            int eaten = events.FindIndex(e => e is RemovedEvent r && r.Cause == RemoveCause.Eaten);
            Assert.GreaterOrEqual(released, 0);
            Assert.Greater(eaten, released);
        }

        [Test]
        public void Merge_ByWalking_StunnedPackDoesNotWalk_AndForgetsLastDirection()
        {
            var board = TestLevel.Make(
                ".W .  .W\n" +
                "#  .  #\n" +
                ".  .S .\n" +
                ".  .  .", target: 0);

            // 1턴: 두 늑대가 (0,1)로 걸어 들어가 합류한다.
            TestLevel.Play(board, "R3:R");
            TestLevel.AssertMap(
                ".  .W2 .\n" +
                "#  .   #\n" +
                ".  .S  .\n" +
                ".  .   .", board);
            var pack = TheWolf(board);
            Assert.AreEqual(1, pack.StunLeft);
            Assert.IsNull(pack.LastWalkDir);
            Assert.IsTrue(pack.IsIncapacitated(board));

            // 2턴: 기절 중이라 걷지 않는다. 턴 끝에 풀리지만 양은 범위 밖이다.
            TestLevel.Play(board, "R3:R");
            TestLevel.AssertMap(
                ".  .W2 .\n" +
                "#  .   #\n" +
                ".  .S  .\n" +
                ".  .   .", board);
            Assert.AreEqual(0, pack.StunLeft);

            // 3턴: 한 칸 걸어 내려가고, 양이 범위에 들어와 먹힌다.
            TestLevel.Play(board, "R3:R");
            TestLevel.AssertMap(
                ".  .   .\n" +
                "#  .W2 #\n" +
                ".  .   .\n" +
                ".  .   .", board);
            Assert.AreEqual(Dir.Down, pack.LastWalkDir);
        }

        [Test]
        public void Merge_BurnedWolfDoesNotMerge_UntilCured()
        {
            var board = TestLevel.AssertAfter(
                ".W .W(b) #\n" +
                ".  ~     .",
                "R0:R",
                ".  .WW(b) #\n" +
                ".  ~      .", target: 0);
            Assert.AreEqual(0, Wolves(board)[0].StunLeft);

            // 같은 칸에서 화상이 풀리는 순간 합류한다.
            TestLevel.Play(board, "C1:D");
            TestLevel.AssertMap(
                ".  .       #\n" +
                ".  ~W2(w)  .", board);
            Assert.IsTrue(TheWolf(board).IsStunned);
        }

        [Test]
        public void Merge_UnionsTerrainAppliedThisTurn()
        {
            var board = TestLevel.Make(".W .W . .", target: 0);
            var wolves = Wolves(board);
            wolves[1].AppliedThisTurn.Add(TerrainType.Water);
            board.Move(wolves[1], new Pos(0, 0), MoveCause.Shift);
            Settler.Settle(board);
            Assert.IsTrue(TheWolf(board).AppliedThisTurn.Contains(TerrainType.Water));
        }

        // ── undo ──

        [Test]
        public void Undo_RestoresPackSizeStunAndMeal()
        {
            var session = new GameSession(TestLevel.Make(".S .W .W #", target: 0));
            ShiftPoint move;
            Assert.IsTrue(ShiftPoint.TryParse("R0:R", out move));

            // 1턴: 양은 (0,1)로, 앞 늑대는 뒤 늑대 칸으로 가 합류한다. 그 뒤 기절한 무리는 양을 잡지 않는다.
            Assert.IsTrue(session.Play(move));
            TestLevel.AssertMap(". .S .W2 #", session.Board);
            var copy = session.Board.Clone();
            Assert.AreEqual(1, TheWolf(copy).StunLeft);

            Assert.IsTrue(session.Undo());
            TestLevel.AssertMap(".S .W .W #", session.Board);
            Assert.IsTrue(Wolves(session.Board).All(w => w.Count == 1 && w.StunLeft == 0 && w.MealLeft == 0));
        }
    }
}
