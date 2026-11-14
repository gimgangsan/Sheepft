using System.Linq;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>물, 들불, 양동이. README 2절(턴당 1회), 5–7절.</summary>
    public class TerrainTests
    {
        // 둘째 행은 아무것도 없는 줄이다. `R1:R`은 턴만 보내는 "대기"로 쓴다.

        // ── 물 ──

        [Test]
        public void Water_MakesEnteringObjectWet()
        {
            TestLevel.AssertAfter(".S ~ .", "R0:R", ". ~S(w) .");
            TestLevel.AssertAfter(".A ~ .", "R0:R", ". ~A(w) .");
        }

        [Test]
        public void Wet_StaysAfterLeavingWater()
        {
            TestLevel.AssertAfter(".S ~ .", "R0:R, R0:R", ". ~ .S(w)");
        }

        [Test]
        public void Water_CuresBurnAndLeavesWet()
        {
            TestLevel.AssertAfter(".S(b) ~ .", "R0:R", ". ~S(w) .");
        }

        [Test]
        public void Water_AppliesToObjectThatStays_AtFirstSettleOfTheTurn()
        {
            // 레벨 시작 때는 정리 절차를 돌리지 않는다. 첫 shift 뒤에 처음 적용된다.
            var board = TestLevel.Make("~S .\n.  .");
            TestLevel.AssertMap("~S .\n.  .", board);
            TestLevel.Play(board, "R1:R");
            TestLevel.AssertMap("~S(w) .\n.  .", board);
        }

        [Test]
        public void Water_ObjectShiftedOffBeforeFirstSettle_StaysDry()
        {
            TestLevel.AssertAfter("~S . .", "R0:R", "~ .S .");
        }

        // ── 들불 ──

        [Test]
        public void Fire_BurnsDryAnimal()
        {
            TestLevel.AssertAfter(".S ^ .", "R0:R", ". ^S(b) .");
        }

        [Test]
        public void Fire_DriesWetAnimalWithoutHarm()
        {
            TestLevel.AssertAfter(".S(w) ^ .", "R0:R", ". ^S .");
            TestLevel.AssertAfter(".S ~ ^ .", "R0:R, R0:R", ". ~ ^S .");
        }

        [Test]
        public void Fire_DriedAnimalThatStays_IsBurnedNextTurn()
        {
            // README 2절: 젖은 채 들불에 들어가 마른 동물이 다음 턴에도 그 칸에 있으면 화상을 입는다.
            const string level = ".S(w) ^ .\n.     . .";
            TestLevel.AssertAfter(level, "R0:R", ". ^S .\n. . .");
            TestLevel.AssertAfter(level, "R0:R, R1:R", ". ^S(b) .\n. . .");
        }

        [Test]
        public void Fire_WetAnimalStartingOnFire_DriesThenBurns()
        {
            const string level = "^S(w) .\n.     .";
            TestLevel.AssertAfter(level, "R1:R", "^S .\n. .");
            TestLevel.AssertAfter(level, "R1:R, R1:R", "^S(b) .\n. .");
        }

        [Test]
        public void Burn_ThenWater_ThenFire_EndsDryAndUnburned()
        {
            // 화상 → 물에서 회복(젖음) → 불에서 마름.
            TestLevel.AssertAfter(".S ^ ~ ^ .", "R0:R", ". ^S(b) ~ ^ .");
            TestLevel.AssertAfter(".S ^ ~ ^ .", "R0:R, R0:R", ". ^ ~S(w) ^ .");
            TestLevel.AssertAfter(".S ^ ~ ^ .", "R0:R, R0:R, R0:R", ". ^ ~ ^S .");
        }

        [Test]
        public void BurnedAnimal_StillMovesByShift()
        {
            TestLevel.AssertAfter(".S(b) . .", "R0:R", ". .S(b) .");
        }

        [Test]
        public void Fire_DestroysDryItems()
        {
            TestLevel.AssertAfter(".A ^ .", "R0:R", ". ^ .");
            TestLevel.AssertAfter(".B ^ .", "R0:R", ". ^ .");
        }

        [Test]
        public void Fire_DriesWetItemsWithoutDestroying()
        {
            TestLevel.AssertAfter(".A(w) ^ .", "R0:R", ". ^A .");
            TestLevel.AssertAfter(".B(w) ^ .", "R0:R", ". ^B .");
        }

        [Test]
        public void Fire_DriedItemThatStays_IsDestroyedNextTurn()
        {
            TestLevel.AssertAfter(".B(w) ^ .\n. . .", "R0:R, R1:R", ". ^ .\n. . .");
        }

        // ── 턴당 1회 기록 ──

        [Test]
        public void AppliedThisTurn_IsRecordedAndClearedNextTurn()
        {
            var board = TestLevel.Make(".S ~ .\n.  . .");
            var sheep = board.Objects.Single();
            TestLevel.Play(board, "R0:R");
            Assert.IsTrue(sheep.AppliedThisTurn.Contains(TerrainType.Water));

            // 같은 턴에 정리 절차를 다시 돌려도 두 번 적용하지 않는다.
            int events = board.Events.Count;
            Settler.Settle(board);
            Assert.AreEqual(events, board.Events.Count);

            TestLevel.Play(board, "R0:R");
            Assert.IsFalse(sheep.AppliedThisTurn.Contains(TerrainType.Water));
        }

        [Test]
        public void SameTerrainKindTwiceInOneTurn_SecondHasNoEffect()
        {
            // 젖은 양이 들불에서 마른 뒤, 같은 턴에 다시 정리 절차가 돌아도 화상을 입지 않는다.
            var board = TestLevel.Make(".S(w) ^ .");
            TestLevel.Play(board, "R0:R");
            Settler.Settle(board);
            TestLevel.AssertMap(". ^S .", board);
        }

        // ── 양동이 ──

        [Test]
        public void Bucket_EmptyBecomesFilledInWater()
        {
            TestLevel.AssertAfter(".B ~ .", "R0:R", ". ~U .");
            TestLevel.AssertAfter(".B(w) ~ .", "R0:R", ". ~U .");
        }

        [Test]
        public void Bucket_FilledIsUnchangedByWater()
        {
            var board = TestLevel.AssertAfter(".U ~ .", "R0:R", ". ~U .");
            Assert.IsFalse(board.Objects.Single().Wet);
        }

        [Test]
        public void Bucket_FilledPutsOutFire_AndBecomesWetEmpty()
        {
            var board = TestLevel.AssertAfter(".U ^ .", "R0:R", ". _B(w) .");
            var changed = board.Events.OfType<TerrainChangedEvent>().Single();
            Assert.AreEqual(new Pos(0, 1), changed.Pos);
            Assert.AreEqual(TerrainType.Fire, changed.From);
            Assert.AreEqual(TerrainType.Ash, changed.To);
        }

        [Test]
        public void Bucket_FilledProtectsOtherObjectsOnTheSameTile()
        {
            // 물 찬 양동이를 먼저 처리한다. 불이 꺼져 재가 되므로 같은 칸의 양은 들불 상호작용을 받지 않는다.
            TestLevel.AssertAfter(".SU ^ .", "R0:R", ". _SB(w) .");
            TestLevel.AssertAfter(".AU ^ .", "R0:R", ". _AB(w) .");
        }

        [Test]
        public void Bucket_TwoFilledOnOneFire_OnlyOneIsUsed()
        {
            TestLevel.AssertAfter(".U2 ^ .", "R0:R", ". _B(w)U .");
        }

        [Test]
        public void Bucket_FillThenExtinguish()
        {
            // 빈 양동이를 물에 담갔다가 불에 넣는다. 젖은 빈 양동이는 다음 불에서 마르기만 한다.
            const string level = ".B ~ ^ ^ .\n. . . . .";
            TestLevel.AssertAfter(level, "R0:R, R0:R", ". ~ _B(w) ^ .\n. . . . .");
            TestLevel.AssertAfter(level, "R0:R, R0:R, R0:R", ". ~ _ ^B .\n. . . . .");
            TestLevel.AssertAfter(level, "R0:R, R0:R, R0:R, R1:R", ". ~ _ ^ .\n. . . . .");
        }

        [Test]
        public void Fire_ItemDestroyed_RecordsRemovedEvent()
        {
            var board = TestLevel.Make(".A ^ .");
            var basket = board.Objects.Single();
            TestLevel.Play(board, "R0:R");
            Assert.IsTrue(basket.Removed);
            var removed = board.Events.OfType<RemovedEvent>().Single();
            Assert.AreEqual(basket.Id, removed.Id);
            Assert.AreEqual(RemoveCause.Burned, removed.Cause);
        }

        [Test]
        public void StatusChanges_AreRecordedAsEvents()
        {
            var board = TestLevel.Make(".S ~ .");
            TestLevel.Play(board, "R0:R");
            var status = board.Events.OfType<StatusChangedEvent>().Single();
            Assert.AreEqual(0, status.Id);
            Assert.AreEqual(StatusKind.Wet, status.Status);
            Assert.IsTrue(status.On);
        }

        [Test]
        public void IsWalkable_ExcludesObstaclesBlanksWaterAndFire()
        {
            var board = TestLevel.Make(". # f t ~ ^ g _ x -");
            string walkable = string.Concat(board.Tiles.Where(t => board.IsWalkable(t.Pos)).Select(LevelWriter.WriteCell));
            Assert.AreEqual(".g_", walkable);
            Assert.IsFalse(board.IsWalkable(new Pos(5, 5)));
        }
    }
}
