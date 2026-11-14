using Sheepft.Core;
using Xunit;

namespace Sheepft.Tests
{
    /// <summary>README 2절 "지형 상호작용은 턴당 1회", 4절 젖음·화상.</summary>
    public class TerrainTests
    {
        [Fact]
        public void Water_wets_and_a_wet_sheep_on_grass_does_not_count()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴  목표 양 1
                .S  ~  g
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R0>"));
            Kit.Board(g, ".  ~S(w)  g");
            Assert.Equal(Outcome.Ongoing, g.Do("R0>"));
            Kit.Board(g, ".  ~  gS(w)");
            Assert.True(g.Has(Ev.SheepWet));
        }

        [Fact]
        public void A_wet_sheep_dries_in_fire_without_harm()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴  목표 양 1
                .S(w)  ^  .  g
                """);
            g.Do("R0>");
            Kit.Board(g, ".  ^S  .  g");
            Assert.True(g.Has(Ev.SheepDried));
            Assert.False(g.Has(Ev.SheepBurned));
        }

        [Fact]
        public void A_sheep_that_dried_in_fire_burns_if_it_is_still_there_next_turn()
        {
            var g = Kit.G("""
                # t  shift: R0 : R, R1 : R  제한 9턴  목표 양 1
                .S(w)  ^  .
                .      .  .
                """);
            g.Do("R0>");
            Kit.Board(g, """
                .  ^S  .
                .  .   .
                """);
            g.Do("R1>"); // 대기. 머물러 있으면 shift 정리 때 적용된다.
            Kit.Board(g, """
                .  ^S(b)  .
                .  .      .
                """);
        }

        [Fact]
        public void Burn_then_water_then_fire_restores_a_sheep()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 12턴  목표 양 1
                .S  ^  .  g  .  ~  .  ^  .  g
                """);
            g.Do("R0>");
            Kit.Board(g, ".  ^S(b)  .  g  .  ~  .  ^  .  g");
            Assert.True(g.Has(Ev.SheepBurned));

            // 화상 입은 양은 풀 위에 있어도 세지 않는다.
            Assert.Equal(Outcome.Ongoing, g.Do("R0> R0>"));
            Kit.Board(g, ".  ^  .  gS(b)  .  ~  .  ^  .  g");

            g.Do("R0> R0>");
            Kit.Board(g, ".  ^  .  g  .  ~S(w)  .  ^  .  g");
            Assert.True(g.Has(Ev.SheepCured));

            g.Do("R0> R0>");
            Kit.Board(g, ".  ^  .  g  .  ~  .  ^S  .  g");

            Assert.Equal(Outcome.Ongoing, g.Do("R0>"));
            Assert.Equal(Outcome.Win, g.Do("R0>"));
        }

        [Fact]
        public void Entering_the_same_terrain_twice_in_one_turn_has_no_second_effect()
        {
            // 젖은 양이 shift로 들불에 들어가 마른 뒤, 같은 턴에 염소에게 밀려 다른 들불 칸에 들어간다.
            var g = Kit.G("""
                # t  shift: C1 : D  제한 9턴  목표 양 1
                .   .S(w)  .
                .G  ^      ^
                """);
            g.Do("C1v");
            Kit.Board(g, """
                .   .  .
                .G  ^  ^S
                """);
            Assert.False(g.Has(Ev.SheepBurned));

            // 다음 턴에는 기록이 지워져 다시 적용된다.
            g.Do("C1v");
            Kit.Board(g, """
                .   .  .
                .G  ^  ^S(b)
                """);
        }

        [Fact]
        public void Terrain_applies_the_moment_the_cell_changes()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴  목표 양 2
                ^   gS
                .S  .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                ^  ^S(b)
                .  .S
                """);
        }
    }

    /// <summary>README 7절: 들불 상호작용과 확산.</summary>
    public class FireTests
    {
        [Fact]
        public void Fire_spreads_one_ring_per_turn_to_wood_terrain_only()
        {
            var g = Kit.G("""
                # t  shift: R3 : R  제한 9턴
                ^  t  t  .
                .  f  .  g
                g  .  .  .
                .  .  .  .
                """);
            g.Do("R3>");
            Kit.Board(g, """
                ^  ^  t  .
                .  ^  .  g
                g  .  .  .
                .  .  .  .
                """);
            Assert.True(g.Has(Ev.ObstacleBurned));
            Assert.False(g.Has(Ev.GrassBurned));

            g.Do("R3>");
            Kit.Board(g, """
                ^  ^  ^  .
                .  ^  .  g
                ^  .  .  .
                .  .  .  .
                """);
            Assert.True(g.Has(Ev.GrassBurned));

            g.Do("R3>");
            Kit.Board(g, """
                ^  ^  ^  .
                .  ^  .  ^
                ^  .  .  .
                .  .  .  .
                """);
        }

        [Fact]
        public void Spread_destroys_dry_wooden_items_and_ignites_their_cell()
        {
            var g = Kit.G("""
                # t  shift: R2 : R  제한 9턴
                ^      .A  .
                .A(w)  .   .
                .      .   .
                """);
            g.Do("R2>");
            Kit.Board(g, """
                ^      ^  .
                .A(w)  .  .
                .      .  .
                """);
            Assert.True(g.Has(Ev.ItemBurned));
            g.Do("R2>");
            Kit.Board(g, """
                ^      ^  .
                .A(w)  .  .
                .      .  .
                """);
        }

        [Fact]
        public void A_dry_item_entering_fire_is_destroyed()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .A  ^  .
                """);
            g.Do("R0>");
            Kit.Board(g, ".  ^  .");
        }

        [Fact]
        public void A_wet_item_dries_in_fire_and_then_burns_like_a_dry_one()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .A(w)  ^  .
                """);
            g.Do("R0>");
            Kit.Board(g, ".  ^A  .");
            // 마른 채로 들불 옆 칸으로 나오면 확산에 타고, 그 칸이 들불이 된다.
            g.Do("R0>");
            Kit.Board(g, ".  ^  ^");
        }

        [Fact]
        public void A_burnt_fence_can_be_passed_by_shift()
        {
            var g = Kit.G("""
                # t  shift: R0 : R, R2 : R  제한 9턴  목표 양 1
                .S(w)  f  .
                ^      .  .
                .      .  .
                """);
            g.Do("R0>"); // 울타리에 막힌다. 턴 끝에 울타리가 탄다.
            Kit.Board(g, """
                .S(w)  ^  .
                ^      .  .
                .      .  .
                """);
            g.Do("R0>");
            Kit.Board(g, """
                .  ^S  .
                ^  .   .
                .  .   .
                """);
            Assert.True(g.Has(Ev.SheepEnterBurnt));
        }
    }

    /// <summary>README 6절 나무 양동이.</summary>
    public class BucketTests
    {
        [Fact]
        public void An_empty_bucket_fills_in_water()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .B  ~  .
                """);
            g.Do("R0>");
            Kit.Board(g, ".  ~U  .");
            Assert.True(g.Has(Ev.BucketFilled));
            g.Do("R0>");
            Kit.Board(g, ".  ~  .U");
        }

        [Fact]
        public void A_full_bucket_puts_out_the_fire_first_and_shields_its_cell()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴  목표 양 1
                .SU  ^  .
                """);
            g.Do("R0>");
            Kit.Board(g, ".  _SB(w)  .");
            Assert.True(g.Has(Ev.Extinguish));
            Assert.False(g.Has(Ev.SheepBurned));
        }

        [Fact]
        public void A_dry_empty_bucket_burns_and_a_wet_one_only_dries()
        {
            var dry = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .B  ^  .
                """);
            dry.Do("R0>");
            Kit.Board(dry, ".  ^  .");

            var wet = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .B(w)  ^  .
                """);
            wet.Do("R0>");
            Kit.Board(wet, ".  ^B  .");
        }

        [Fact]
        public void A_wet_empty_bucket_fills_again()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .B(w)  ~  .
                """);
            g.Do("R0>");
            Kit.Board(g, ".  ~U  .");
        }

        [Fact]
        public void Only_one_of_two_full_buckets_is_spent()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .U2  ^  .
                """);
            g.Do("R0>");
            Kit.Board(g, ".  _B(w)U  .");
        }

        [Fact]
        public void A_full_bucket_cannot_save_the_grass_under_it()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                ^  gU
                .  .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                ^  _B(w)
                .  .
                """);
        }

        [Fact]
        public void A_dry_empty_bucket_in_spread_range_burns_and_ignites_its_cell()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                ^  .B
                .  .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                ^  ^
                .  .
                """);
        }
    }
}
