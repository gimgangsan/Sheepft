using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>
    /// 검증용 기믹 표시(Board.Mechanics). 스테이지 검증기가 `@intent`를 판정할 때 쓴다.
    /// stages.txt가 쓰는 기믹을 하나씩 확인한다. 모두 목표 양 0(승리·패배 판정 없음)으로 만든다.
    /// </summary>
    public class MechanicTests
    {
        /// <summary>맵에 수를 차례로 두고, 모든 턴에 켜진 기믹을 합쳐 돌려준다.</summary>
        private static Mechanic Played(string grid, string moves)
        {
            var board = TestLevel.Make(grid, target: 0);
            var all = Mechanic.None;
            foreach (var raw in moves.Split(','))
            {
                TestLevel.Play(board, raw);
                all |= board.Mechanics;
            }
            return all;
        }

        private static void AssertHas(Mechanic expected, Mechanic actual)
        {
            Assert.AreEqual(expected, actual & expected,
                "빠진 기믹: " + MechanicText.Describe(expected & ~actual) + " / 켜진 기믹: " + MechanicText.Describe(actual));
        }

        private static void AssertLacks(Mechanic unexpected, Mechanic actual)
        {
            Assert.AreEqual(Mechanic.None, actual & unexpected, "켜지면 안 되는 기믹: " + MechanicText.Describe(actual & unexpected));
        }

        // ── shift ──

        [Test]
        public void Shift_MovesSheep()
        {
            var m = Played(".S . .", "R0:R");
            AssertHas(Mechanic.ShiftMove, m);
            AssertLacks(Mechanic.Wait | Mechanic.Wrap | Mechanic.VoidSkip, m);
        }

        [Test]
        public void Shift_SheepWrapsAtLineEnd()
        {
            AssertHas(Mechanic.ShiftMove | Mechanic.Wrap | Mechanic.SheepWrap, Played(". . .S", "R0:R"));
        }

        [Test]
        public void Shift_WolfWrapsAtLineEnd()
        {
            var m = Played(". . .W", "R0:R");
            AssertHas(Mechanic.Wrap | Mechanic.WolfWrap, m);
            AssertLacks(Mechanic.SheepWrap, m);
        }

        [Test]
        public void Shift_SkipsOverVoid()
        {
            var m = Played(".S x .", "R0:R");
            AssertHas(Mechanic.VoidSkip, m);
            AssertLacks(Mechanic.Wrap, m);
        }

        [Test]
        public void Shift_SheepCatchesUpWithBlockedSheep()
        {
            // 돌 앞의 양은 제자리, 뒤따르던 양이 같은 칸에 들어온다.
            AssertHas(Mechanic.Blocked | Mechanic.SheepMerge, Played(".S .S #", "R0:R"));
        }

        [Test]
        public void Shift_SheepMovingTogetherDoNotMerge()
        {
            AssertLacks(Mechanic.SheepMerge, Played(".S .S . .", "R0:R"));
        }

        [Test]
        public void Shift_EmptyLineIsWait()
        {
            var m = Played(". .\n.S .", "R0:R");
            AssertHas(Mechanic.Wait, m);
            AssertLacks(Mechanic.ShiftMove, m);
        }

        [Test]
        public void Mechanics_AreClearedEachTurn()
        {
            var board = TestLevel.Make(".S . .\n. . .", target: 0);
            TestLevel.Play(board, "R0:R");
            AssertHas(Mechanic.ShiftMove, board.Mechanics);
            TestLevel.Play(board, "R1:R");
            AssertLacks(Mechanic.ShiftMove, board.Mechanics);
            AssertHas(Mechanic.Wait, board.Mechanics);
        }

        // ── 물과 들불 ──

        [Test]
        public void Water_WetsSheep()
        {
            AssertHas(Mechanic.SheepWet, Played(".S ~", "R0:R"));
        }

        [Test]
        public void Water_CuresBurnedSheep()
        {
            AssertHas(Mechanic.SheepCured | Mechanic.SheepWet, Played(".S(b) ~", "R0:R"));
        }

        [Test]
        public void Fire_DriesWetSheep()
        {
            var m = Played(".S(w) ^", "R0:R");
            AssertHas(Mechanic.SheepDried, m);
            AssertLacks(Mechanic.SheepBurned, m);
        }

        [Test]
        public void Fire_BurnsDrySheep()
        {
            AssertHas(Mechanic.SheepBurned, Played(".S ^", "R0:R"));
        }

        [Test]
        public void Fire_BurnsWolf()
        {
            AssertHas(Mechanic.WolfBurned, Played(".W ^", "R0:R"));
        }

        [Test]
        public void Fire_BurnsBasket()
        {
            AssertHas(Mechanic.ItemBurned, Played(".A ^", "R0:R"));
        }

        [Test]
        public void FireSpread_BurnsGrassAndFence()
        {
            var m = Played("^ g\nf .\n. .", "R2:R");
            AssertHas(Mechanic.FireSpread | Mechanic.GrassBurned | Mechanic.ObstacleBurned, m);
        }

        [Test]
        public void EnteringBurntFence_IsMarked()
        {
            // 1턴: 울타리가 확산으로 탄다. 2턴: 양이 처음에 울타리였던 칸에 들어간다.
            var m = Played("^ f .S\n. . .", "R1:R, R0:L");
            AssertHas(Mechanic.EnterBurnt | Mechanic.SheepEnterBurnt, m);
        }

        // ── 양동이 ──

        [Test]
        public void Bucket_FillsInWater()
        {
            AssertHas(Mechanic.BucketFilled, Played(".B ~", "R0:R"));
        }

        [Test]
        public void Bucket_ExtinguishesFire()
        {
            AssertHas(Mechanic.Extinguish, Played(".U ^", "R0:R"));
        }

        // ── 늑대 ──

        [Test]
        public void Wolf_WalksTowardSheep()
        {
            AssertHas(Mechanic.WolfWalk, Played(".W . .S\n. . .", "R1:R"));
        }

        [Test]
        public void Wolf_WithNoPath()
        {
            var m = Played(".W # .S\n# # #", "R1:R");
            AssertHas(Mechanic.WolfNoPath, m);
            AssertLacks(Mechanic.WolfWalk, m);
        }

        [Test]
        public void Wolf_EatsSheep()
        {
            var m = Played(".S .W #", "R0:R");
            AssertHas(Mechanic.WolfEat, m);
            AssertLacks(Mechanic.PackCatch, m);
        }

        [Test]
        public void Wolf_PackCatchesNeighbour()
        {
            AssertHas(Mechanic.WolfEat | Mechanic.PackCatch, Played(".S . .W2 #", "R0:R"));
        }

        [Test]
        public void Wolf_MergesAndStunnedPackSparesSheep()
        {
            var m = Played(".W .W #\n. . .S", "R0:R");
            AssertHas(Mechanic.WolfMerge | Mechanic.StunSpared, m);
            AssertLacks(Mechanic.WolfEat, m);
        }

        [Test]
        public void Wolf_EatingPackSparesLeftoverSheep()
        {
            // 2마리 무리가 3마리 중 2마리를 먹는다. 남은 1마리는 식사 중이라 잡지 않는다.
            AssertHas(Mechanic.WolfEat | Mechanic.MealSpared, Played(".S3 .W2 #", "R0:R"));
        }

        // ── 염소 ──

        [TestCase(".G .S . .\n. . . .", Mechanic.GoatPushSheep)]
        [TestCase(".G .W . .\n. . . .", Mechanic.GoatPushWolf)]
        [TestCase(".G .A . .\n. . . .", Mechanic.GoatPushItem)]
        public void Goat_PushesNeighbour(string grid, Mechanic expected)
        {
            AssertHas(Mechanic.GoatPush | expected, Played(grid, "R1:R"));
        }

        [Test]
        public void Goat_CannotSeePastStone()
        {
            var m = Played(".G # .S\n. . .", "R1:R");
            AssertHas(Mechanic.GoatSightBlocked, m);
            AssertLacks(Mechanic.GoatPush, m);
        }

        // ── 말 ──

        [Test]
        public void Horse_CarriesSheepAndDropsItOnBasketCell()
        {
            var m = Played(".HS . . gA\n. . . .", "R1:R, R1:R");
            AssertHas(Mechanic.HorseDiscover | Mechanic.HorseWalk | Mechanic.HorseCarry | Mechanic.HorseCarrySheep
                      | Mechanic.HorseEat | Mechanic.HorseDrop | Mechanic.HorseDropSheep, m);
        }

        [Test]
        public void Horse_CarriesWolfThatCannotAct()
        {
            AssertHas(Mechanic.HorseCarryWolf | Mechanic.RideHeld, Played(".HW . . .A\n. . . .", "R1:R"));
        }

        [Test]
        public void Horse_WithUnreachableBasketHoldsItsRider()
        {
            // 말 감옥: 바구니가 물 위라 갈 길이 없다. 탄 늑대는 걷지 못한다.
            var m = Played(".HW . ~A\n. . .\n.S . .\n. . .", "R3:R");
            AssertHas(Mechanic.HorseNoPath | Mechanic.RideHeld, m);
            AssertLacks(Mechanic.WolfWalk, m);
        }

        [Test]
        public void Horse_WithoutTargetDoesNotHoldItsRider()
        {
            var m = Played(".HW . .\n. . .\n.S . .\n. . .", "R3:R");
            AssertLacks(Mechanic.RideHeld, m);
            AssertHas(Mechanic.WolfWalk, m);
        }

        [Test]
        public void Horse_IsLuredByBasketAnotherHorseDropped()
        {
            AssertHas(Mechanic.HorseLure, Played(".HA . .A .\n. . . .\n. . .H .\n. . . .", "R3:R"));
        }

        // ── 소 ──

        [Test]
        public void Cow_PushesSheep()
        {
            AssertHas(Mechanic.CowSmell | Mechanic.CowWalk | Mechanic.CowPush | Mechanic.CowPushSheep,
                Played(".M .S . .A\n. . . .", "R1:R"));
        }

        [Test]
        public void Cow_PushesWolf()
        {
            AssertHas(Mechanic.CowPushWolf, Played(".M .W ~ .A\n. . . .\n. . . .S\n. . . .", "R3:R"));
        }

        [Test]
        public void Cow_IsLuredByBasketAHorseCarried()
        {
            // 말이 싣고 온 바구니가 소의 냄새 범위(3칸)에 들어온다.
            AssertHas(Mechanic.CowSmell | Mechanic.CowLure, Played(".HA . . .A\n. . . .\n. . . .\n. .M . .", "R2:R"));
        }

        [Test]
        public void Cow_EatsBasket()
        {
            AssertHas(Mechanic.CowEat, Played(".M .A #", "R0:R"));
        }

        // ── 레벨 파일 ──

        [Test]
        public void Parser_ReadsIntentAndOptimal()
        {
            var board = LevelParser.Parse(
                "# t  shift: R0:R  제한 3턴\n" +
                "@intent SheepWrap SheepMerge\n" +
                "@intent Wait\n" +
                "@optimal VoidSkip\n" +
                ".S .");
            Assert.AreEqual(Mechanic.SheepWrap | Mechanic.SheepMerge | Mechanic.Wait, board.Rules.Intent);
            Assert.AreEqual(Mechanic.VoidSkip, board.Rules.OptimalIntent);
        }

        [TestCase("NoSuchThing")]
        [TestCase("sheepwrap")]
        [TestCase("3")]
        public void Parser_RejectsUnknownMechanic(string name)
        {
            Assert.Throws<LevelParseException>(() => LevelParser.Parse(
                "# t  shift: R0:R  제한 3턴\n@intent " + name + "\n.S ."));
        }
    }
}
