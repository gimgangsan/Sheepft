using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>말과 소, 과일 바구니, 탑승. README 4·6절. 아무것도 없는 줄을 밀어 턴만 넘긴다.</summary>
    public class HorseCowTests
    {
        private static Horse TheHorse(Board board) => board.Objects.OfType<Horse>().Single();
        private static Cow TheCow(Board board) => board.Objects.OfType<Cow>().Single();

        // ── 말: 발견, 이동, 먹기 ──

        [Test]
        public void Horse_SpotsBasketInItsRow_WalksToIt_AndEatsWhenAdjacent()
        {
            var board = TestLevel.AssertAfter(
                ".H . . . .A\n" +
                ".  . . . .",
                "R1:R, R1:R",
                ".  . .H . .A\n" +
                ".  . .  . .", target: 0);
            Assert.IsTrue(TheHorse(board).HasTarget);
            Assert.AreEqual(Dir.Right, TheHorse(board).LastWalkDir);

            // 바구니 옆 칸에 도착하는 순간 먹는다.
            TestLevel.Play(board, "R1:R");
            TestLevel.AssertMap(
                ".  . . .H .\n" +
                ".  . . .  .", board);
            Assert.IsTrue(TheHorse(board).Full);
            Assert.IsFalse(TheHorse(board).HasTarget);
            Assert.IsTrue(board.Events.OfType<StatusChangedEvent>().Any(e => e.Status == StatusKind.Full && e.On));
        }

        [Test]
        public void Horse_CarriesObjectsOnItsTile_AndDropsThemWhereTheBasketWas()
        {
            var board = TestLevel.AssertAfter(
                ".HSB . . . .A\n" +
                ".    . . . .",
                "R1:R",
                ".  .HSB . . .A\n" +
                ".  .    . . .", target: 0);
            Assert.IsTrue(board.Events.OfType<MovedEvent>().Any(e => e.Cause == MoveCause.Carry));
            Assert.IsTrue(board.Events.OfType<TargetChangedEvent>().Any(e => e.Id == 0 && e.TargetId == 3));

            TestLevel.Play(board, "R1:R, R1:R");
            TestLevel.AssertMap(
                ".  . . .H .SB\n" +
                ".  . . .  .", board);
            Assert.IsTrue(board.Events.OfType<MovedEvent>().Any(e => e.Cause == MoveCause.HorseEat));
        }

        [Test]
        public void Horse_DoesNotCarryOtherHorses()
        {
            // 두 말이 같은 바구니를 발견하고 각자 걷는다. 서로 싣지 않는다.
            var board = TestLevel.AssertAfter(
                ".H2 . . . .A\n" +
                ".   . . . .",
                "R1:R",
                ".  .H2 . . .A\n" +
                ".  .   . . .", target: 0);
            Assert.IsTrue(board.Events.OfType<MovedEvent>().All(e => e.Cause == MoveCause.Walk));
        }

        [Test]
        public void Horse_SightIsBlockedByObstacles_AndDoesNotSeeBasketOnItsOwnTile()
        {
            var blocked = TestLevel.AssertAfter(
                ".H # .A\n" +
                ".  . .",
                "R1:R",
                ".H # .A\n" +
                ".  . .", target: 0);
            Assert.IsFalse(TheHorse(blocked).HasTarget);

            var same = TestLevel.AssertAfter(
                ".HA . .\n" +
                ".   . .",
                "R1:R",
                ".HA . .\n" +
                ".   . .", target: 0);
            Assert.IsFalse(TheHorse(same).HasTarget);
            Assert.IsFalse(TheHorse(same).Full);
        }

        [Test]
        public void Horse_SightDoesNotWrap()
        {
            var board = TestLevel.AssertAfter(
                ".A # .H\n" +
                "#  # .",
                "R1:R",
                ".A # .H\n" +
                "#  # .", target: 0);
            Assert.IsFalse(TheHorse(board).HasTarget);
        }

        [Test]
        public void Horse_SpotsNearest_TieIsUpRightDownLeft()
        {
            // 위·오른쪽·왼쪽 모두 2칸이다. 위의 바구니를 발견해 한 칸 올라가고, 옆 칸이 되어 먹는다.
            TestLevel.AssertAfter(
                ".  .  .A .  .\n" +
                ".  .  .  .  .\n" +
                ".A .  .H .  .A\n" +
                ".  .  .  .  .\n" +
                ".  .  .  .  .",
                "R4:R",
                ".  .  .  .  .\n" +
                ".  .  .H .  .\n" +
                ".A .  .  .  .A\n" +
                ".  .  .  .  .\n" +
                ".  .  .  .  .", target: 0);
        }

        [Test]
        public void Horse_EatsOnlyOneAdjacentBasket_UpFirst()
        {
            var board = TestLevel.AssertAfter(
                ".  .A .\n" +
                ".A .H .A\n" +
                ".  .  .\n" +
                ".  .  .",
                "R3:R, R3:R",
                ".  .  .\n" +
                ".A .H .A\n" +
                ".  .  .\n" +
                ".  .  .", target: 0);
            Assert.IsTrue(TheHorse(board).Full);
        }

        [Test]
        public void Horse_KeepsTargetWhenAnotherBasketIsCloser_AndWaitsWhenTargetIsUnreachable()
        {
            // 목표가 물 위에 있으면 경로가 없다. 목표는 바꾸지 않고 대기한다.
            var board = TestLevel.AssertAfter(
                ".H . . ~A\n" +
                ".  . . .",
                "R1:R",
                ".H . . ~A(w)\n" +
                ".  . . .", target: 0);
            Assert.IsTrue(TheHorse(board).HasTarget);
        }

        [Test]
        public void Horse_LosesTarget_WhenBasketEntersItsTile_AndCanSpotAnother()
        {
            var board = TestLevel.Make(".H . . .A .A", target: 0);
            var horse = TheHorse(board);
            var baskets = board.Objects.OfType<FruitBasket>().ToArray();
            board.SetTarget(horse, baskets[1].Id);
            board.Move(baskets[1], horse.Pos, MoveCause.Shift);
            Settler.Settle(board);

            // 같은 칸에 들어온 바구니는 먹지 않고, 바로 다른 바구니를 발견한다.
            TestLevel.AssertMap(".HA . . .A .", board);
            Assert.IsFalse(horse.Full);
            Assert.AreEqual(baskets[0].Id, horse.TargetId);
        }

        [Test]
        public void Horse_LosesTarget_WhenBasketIsGone()
        {
            var board = TestLevel.Make(".H . . .A", target: 0);
            var horse = TheHorse(board);
            var basket = board.Objects.OfType<FruitBasket>().Single();
            board.SetTarget(horse, basket.Id);
            board.Remove(basket, RemoveCause.Burned);
            Settler.Settle(board);
            Assert.IsFalse(horse.HasTarget);
        }

        [Test]
        public void Horse_Burned_StillSpotsAndEats_ButDoesNotWalk()
        {
            var walk = TestLevel.AssertAfter(
                ".H(b) . . .A\n" +
                ".     . . .",
                "R1:R",
                ".H(b) . . .A\n" +
                ".     . . .", target: 0);
            Assert.IsTrue(TheHorse(walk).HasTarget);

            var eat = TestLevel.AssertAfter(
                ".H(b) .A .\n" +
                ".     .  .",
                "R1:R",
                ".H(b) .  .\n" +
                ".     .  .", target: 0);
            Assert.IsTrue(TheHorse(eat).Full);
        }

        [Test]
        public void Horse_Full_DoesNothingAndIgnoresTerrain()
        {
            var board = TestLevel.Make(
                "~H . .A\n" +
                ".  . .", target: 0);
            board.SetFull(TheHorse(board), true);
            TestLevel.Play(board, "R1:R");
            TestLevel.AssertMap(
                "~H . .A\n" +
                ".  . .", board);
            Assert.IsFalse(TheHorse(board).HasTarget);
        }

        [Test]
        public void Horse_EatenBasketDropOff_CanLureAnotherHorse()
        {
            // 싣고 온 바구니는 먹은 바구니 자리에 내려놓는다. 배부른 말은 다시 먹지 않는다.
            var board = TestLevel.AssertAfter(
                ".HA .A . .\n" +
                ".   .  . .",
                "R1:R",
                ".H  .A . .\n" +
                ".   .  . .", target: 0);
            Assert.IsTrue(TheHorse(board).Full);
            Assert.AreEqual(1, board.Objects.OfType<FruitBasket>().Count());
        }

        // ── 탑승 ──

        [Test]
        public void Riding_WolfOnSpottingHorse_DoesNotWalk_ButIsCarried()
        {
            var board = TestLevel.AssertAfter(
                ".HW . . . . .A\n" +
                ".S  . . . . .\n" +
                ".   . . . . .",
                "R2:R",
                ".  .HW . . . .A\n" +
                ".S .   . . . .\n" +
                ".  .   . . . .", target: 0);
            var wolf = board.Objects.OfType<Wolf>().Single();
            Assert.IsTrue(wolf.IsRiding(board));
            Assert.IsTrue(wolf.IsIncapacitated(board));
            Assert.IsFalse(TheHorse(board).IsRiding(board));
        }

        [Test]
        public void Riding_HorseWithoutTarget_DoesNotHoldTheWolf()
        {
            TestLevel.AssertAfter(
                ".HW . .\n" +
                ".S  . .\n" +
                ".   . .",
                "R2:R",
                ".H  . .\n" +
                ".W  . .\n" +
                ".   . .", target: 0);
        }

        [Test]
        public void Riding_WolfStillCatchesSheep()
        {
            // 말이 늑대를 싣고 양이 있는 칸으로 들어가면 그 순간 잡는다.
            TestLevel.AssertAfter(
                ".HW .S . . .A\n" +
                ".   .  . . .",
                "R1:R",
                ".  .HW . . .A\n" +
                ".  .   . . .", target: 0);
        }

        [Test]
        public void Riding_GoatDoesNotAttack()
        {
            // 탑승한 염소는 공격하지 않는다. 그 뒤 말이 염소를 싣고 양의 칸으로 간다.
            TestLevel.AssertAfter(
                ".HG .S . . . .A\n" +
                ".   .  . . . .",
                "R1:R",
                ".  .HGS . . . .A\n" +
                ".  .    . . . .", target: 0);
        }

        // ── 소: 냄새, 밀기, 먹기 ──

        [Test]
        public void Cow_SmellsWithinThreeTiles_IgnoringObstacles()
        {
            var far = TestLevel.AssertAfter(
                ".M # . . .A\n" +
                ".  . . . .",
                "R1:R",
                ".M # . . .A\n" +
                ".  . . . .", target: 0);
            Assert.IsFalse(TheCow(far).HasTarget);

            // 돌 너머 2칸의 바구니는 냄새로 찾고, 돌을 돌아 걸어간다.
            var near = TestLevel.AssertAfter(
                ".M # .A .\n" +
                ".  . .  .",
                "R1:R",
                ".  # .A .\n" +
                ".M . .  .", target: 0);
            Assert.IsTrue(TheCow(near).HasTarget);
            Assert.AreEqual(Dir.Down, TheCow(near).LastWalkDir);
        }

        [Test]
        public void Cow_PushesTheTileAhead_AndHerdsTheBasketToTheEdge()
        {
            var board = TestLevel.AssertAfter(
                ".M .S .A . .\n" +
                ".  .  .  . .",
                "R1:R",
                ".  .M .SA . .\n" +
                ".  .  .   . .", target: 0);
            Assert.IsTrue(board.Events.OfType<MovedEvent>().Any(e => e.Cause == MoveCause.CowPush));

            // 바구니가 맵 끝에 닿아 더 밀리지 않으면 소가 들어가 겹치며 먹는다.
            TestLevel.Play(board, "R1:R, R1:R, R1:R");
            TestLevel.AssertMap(
                ".  .  .  . .MS\n" +
                ".  .  .  . .", board);
            Assert.IsTrue(TheCow(board).Full);
        }

        [Test]
        public void Cow_PushDoesNotChain()
        {
            // 밀려난 양은 다음 칸의 양과 그냥 겹친다.
            TestLevel.AssertAfter(
                ".M .S .S .A .\n" +
                ".  .  .  .  .",
                "R1:R",
                ".  .M .S2 .A .\n" +
                ".  .  .   .  .", target: 0);
        }

        [Test]
        public void Cow_PushesObjectsIntoFire_ButWalksAroundItItself()
        {
            // 오른쪽과 아래가 같은 거리라 오른쪽으로 간다. 앞 칸의 빈 양동이는 들불로 밀려 탄다.
            // 바구니는 들불 옆이라 젖은 것으로 둔다(마르면 확산에 탄다).
            TestLevel.AssertAfter(
                ".M .B ^ .A(w)\n" +
                ".  .  . .",
                "R1:R",
                ".  .M ^ .A(w)\n" +
                ".  .  . .", target: 0);
        }

        [Test]
        public void Cow_EatsBasketShiftedOntoItsTile()
        {
            var board = TestLevel.AssertAfter(
                ".A .M #\n" +
                ".  .  .",
                "R0:R",
                ".  .M #\n" +
                ".  .  .", target: 0);
            Assert.IsTrue(TheCow(board).Full);
        }

        [Test]
        public void Cow_SmellTie_IsRowMajor()
        {
            // 위와 왼쪽의 바구니가 같은 거리다. (0,0)에 가까운 위쪽을 목표로 삼아 올라가 먹는다.
            TestLevel.AssertAfter(
                ".  .A .\n" +
                ".A .M .\n" +
                ".  .  .",
                "R2:R",
                ".  .M .\n" +
                ".A .  .\n" +
                ".  .  .", target: 0);
        }

        [Test]
        public void Cow_Burned_SmellsButDoesNotWalk()
        {
            var board = TestLevel.AssertAfter(
                ".M(b) . .A\n" +
                ".     . .",
                "R1:R",
                ".M(b) . .A\n" +
                ".     . .", target: 0);
            Assert.IsTrue(TheCow(board).HasTarget);
        }

        [Test]
        public void Cow_OnBasketTile_EatsBeforeTheHorseNextToIt()
        {
            // 겹침 상호작용이 자동 반응보다 먼저다.
            var board = TestLevel.AssertAfter(
                ".A .M # .\n" +
                ".  .H . .",
                "R0:R",
                ".  .M # .\n" +
                ".  .H . .", target: 0);
            Assert.IsTrue(TheCow(board).Full);
            Assert.IsFalse(TheHorse(board).Full);
        }

        // ── undo ──

        [Test]
        public void Clone_KeepsTargetAndFull()
        {
            var board = TestLevel.Make(
                ".H . . . .A\n" +
                ".  . . . .", target: 0);
            TestLevel.Play(board, "R1:R");
            var copy = board.Clone();
            Assert.AreEqual(TheHorse(board).TargetId, TheHorse(copy).TargetId);
            Assert.IsNotNull(TheHorse(copy).Target(copy));

            TestLevel.Play(copy, "R1:R, R1:R");
            Assert.IsTrue(TheHorse(copy).Full);
            Assert.IsFalse(TheHorse(board).Full);
        }
    }
}
