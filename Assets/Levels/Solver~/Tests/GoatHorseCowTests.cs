using Sheepft.Core;
using Xunit;

namespace Sheepft.Tests
{
    /// <summary>README 6절 염소.</summary>
    public class GoatTests
    {
        [Fact]
        public void A_goat_pushes_cells_one_and_two_away_outward_in_all_four_directions()
        {
            var g = Kit.G("""
                # t  shift: R6 : R  제한 9턴
                .  .   .   .   .   .   .
                .  .   .   .A  .   .   .
                .  .   .   .B  .   .   .
                .  .A  .B  .G  .A  .B  .
                .  .   .   .A  .   .   .
                .  .   .   .B  .   .   .
                .  .   .   .   .   .   .
                """);
            g.Do("R6>");
            Kit.Board(g, """
                .   .   .  .A  .  .   .
                .   .   .  .B  .  .   .
                .   .   .  .   .  .   .
                .A  .B  .  .G  .  .A  .B
                .   .   .  .   .  .   .
                .   .   .  .A  .  .   .
                .   .   .  .B  .  .   .
                """);
            Assert.True(g.Has(Ev.GoatPushItem));
        }

        [Fact]
        public void A_goat_cannot_see_past_an_obstacle_or_a_void()
        {
            var g = Kit.G("""
                # t  shift: R3 : R  제한 9턴
                .   .  .A  .   .
                .   .  #   .   .
                .A  x  .G  .A  .
                .   .  .   .   .
                """);
            g.Do("R3>");
            Kit.Board(g, """
                .   .  .A  .  .
                .   .  #   .  .
                .A  x  .G  .  .A
                .   .  .   .  .
                """);
            Assert.True(g.Has(Ev.GoatSightBlocked));
        }

        [Fact]
        public void A_push_does_not_wrap_and_the_near_object_overlaps_the_stuck_one()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                .G  .A  .B
                .   .   .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                .G  .  .AB
                .   .  .
                """);
        }

        [Fact]
        public void A_push_is_blocked_by_a_void()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                .G  .A  x  .
                .   .   .  .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                .G  .A  x  .
                .   .   .  .
                """);
        }

        [Fact]
        public void A_goat_does_not_push_what_shares_its_cell()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                .GA  .B  .
                .    .   .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                .GA  .  .B
                .    .  .
                """);
        }

        [Fact]
        public void A_burned_goat_does_not_attack()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                .G(b)  .B  .
                .      .   .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                .G(b)  .B  .
                .      .   .
                """);
        }

        [Fact]
        public void Goats_attack_in_row_major_order_from_where_they_stand()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                .G  .G  .
                .   .   .
                """);
            g.Do("R1>");
            // 첫 염소가 둘째를 민다. 둘째는 옮겨진 자리에서 첫 염소를 밀지만 맵 끝이라 제자리다.
            Kit.Board(g, """
                .G  .  .G
                .   .  .
                """);
        }
    }

    /// <summary>README 6절 말.</summary>
    public class HorseTests
    {
        [Fact]
        public void A_horse_discovers_walks_carries_eats_and_drops_its_load_on_the_basket_cell()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴  목표 양 1
                .HS  .  .  gA
                .    .  .  .
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R1>"));
            Kit.Board(g, """
                .  .SH  .  gA
                .  .    .  .
                """);
            Assert.True(g.Has(Ev.HorseDiscover | Ev.HorseCarrySheep));

            // 바구니 옆 칸에 닿으면 먹고, 양을 바구니가 있던 풀 칸에 내려놓는다.
            Assert.Equal(Outcome.Win, g.Do("R1>"));
            Kit.Board(g, """
                .  .  .H  gS
                .  .  .   .
                """);
            Assert.True(g.Has(Ev.HorseEat | Ev.HorseDropSheep));
        }

        [Theory]
        [InlineData(".H  #  .A")]
        [InlineData(".H  x  .A")]
        public void Sight_is_blocked_by_obstacles_and_voids(string row)
        {
            var g = Kit.G("# t  shift: R1 : R  제한 9턴\n" + row + "\n.  .  .");
            g.Do("R1>");
            Assert.False(g.Has(Ev.HorseDiscover));
            Assert.Equal(".H", g.Cell(0, 0));
        }

        [Fact]
        public void A_horse_ignores_a_basket_in_its_own_cell()
        {
            var same = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .HA  .  .
                """);
            same.Do("R0>");
            Assert.False(same.Has(Ev.HorseDiscover));
            Assert.False(same.Has(Ev.HorseEat));
            Kit.Board(same, ".  .HA  .");
        }

        [Fact]
        public void A_discovering_horse_that_cannot_reach_its_basket_holds_its_rider()
        {
            // 말 감옥: 바구니가 물 위라 갈 길이 없다. 탄 늑대는 걷지 못한다.
            var g = Kit.G("""
                # t  shift: R3 : R  제한 9턴  목표 양 1
                .HW  .  ~A
                .    .  .
                .S   .  .
                .    .  .
                """);
            g.Do("R3>");
            Assert.Equal(".WH", g.Cell(0, 0));
            Assert.True(g.Has(Ev.HorseNoPath));
            Assert.True(g.Has(Ev.RideHeld));
        }

        [Fact]
        public void A_horse_without_a_target_does_not_hold_its_rider()
        {
            var g = Kit.G("""
                # t  shift: R3 : R  제한 9턴  목표 양 1
                .HW  .  .
                .    .  .
                .S   .  .
                .    .  .
                """);
            g.Do("R3>");
            Assert.Equal(".H", g.Cell(0, 0));
            Assert.Equal(".W", g.Cell(1, 0));
        }

        [Fact]
        public void A_horse_loses_its_target_when_the_basket_burns()
        {
            var g = Kit.G("""
                # t  shift: R2 : R  제한 9턴
                .H  .  .  .  .A
                .   .  .  .  ^
                .   .  .  .  .
                """);
            g.Do("R2>");
            g.Do("R2>");
            Kit.Board(g, """
                .  .H  .  .  ^
                .  .   .  .  ^
                .  .   .  .  .
                """);
            Assert.True(g.Has(Ev.ItemBurned));
        }

        [Fact]
        public void A_basket_dropped_by_one_horse_lures_another()
        {
            var g = Kit.G("""
                # t  shift: R3 : R  제한 9턴
                .HA  .  .A  .
                .    .  .   .
                .    .  .H  .
                .    .  .   .
                """);
            g.Do("R3>");
            // 첫 말이 싣고 온 바구니를 내려놓고, 둘째 말이 그 바구니를 발견해 다가가 먹는다.
            Kit.Board(g, """
                .  .H  .   .
                .  .   .H  .
                .  .   .   .
                .  .   .   .
                """);
            Assert.True(g.Has(Ev.HorseLure));
        }
    }

    /// <summary>README 6절 소.</summary>
    public class CowTests
    {
        [Fact]
        public void A_cow_smells_walks_pushes_one_cell_ahead_and_eats_at_the_wall()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                .M  .B  .B  .A  .  .
                .   .   .   .   .  .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                .  .M  .B2  .A  .  .
                .  .   .    .   .  .
                """);
            Assert.True(g.Has(Ev.CowSmell | Ev.CowPush));
            g.Do("R1>");
            Kit.Board(g, """
                .  .  .M  .AB2  .  .
                .  .  .   .     .  .
                """);
            g.Do("R1> R1>");
            Kit.Board(g, """
                .  .  .  .  .M  .AB2
                .  .  .  .  .   .
                """);
            g.Do("R1>"); // 바구니가 맵 끝에 닿아 더 밀리지 않는다. 소가 겹치며 먹는다.
            Kit.Board(g, """
                .  .  .  .  .  .MB2
                .  .  .  .  .  .
                """);
            Assert.True(g.Has(Ev.CowEat));
        }

        [Fact]
        public void A_cow_does_not_smell_beyond_three_cells()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                .M  .  .  .  .A
                .   .  .  .  .
                """);
            g.Do("R1>");
            Assert.False(g.Has(Ev.CowSmell));
            Assert.Equal(".M", g.Cell(0, 0));
        }

        [Fact]
        public void A_cow_smells_across_a_void_but_cannot_walk_across_it()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                .M  x  .A
                x   x  .
                """);
            g.Do("R1>");
            Assert.True(g.Has(Ev.CowSmell));
            Assert.True(g.Has(Ev.CowNoPath));
            Assert.Equal(".M", g.Cell(0, 0));
        }

        [Fact]
        public void A_basket_shifted_onto_a_cow_is_eaten_at_once()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .A  .M  #
                """);
            g.Do("R0>");
            Kit.Board(g, ".  .M  #");
            Assert.True(g.Has(Ev.CowEat));
        }

        [Fact]
        public void A_cow_eats_an_overlapping_basket_before_an_adjacent_horse_can()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .A  .M  #
                .   .H  .
                """);
            g.Do("R0>");
            Assert.True(g.Has(Ev.CowEat));
            Assert.False(g.Has(Ev.HorseEat));
        }

        [Fact]
        public void A_cow_eats_a_basket_stuck_against_a_void()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                .M  .A  x  .
                .   .   .  .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                .  .M  x  .
                .  .   .  .
                """);
        }

        [Fact]
        public void A_cow_waits_when_its_basket_is_on_water()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴
                .M  .  ~A
                .   .  .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                .M  .  ~A(w)
                .   .  .
                """);
            Assert.True(g.Has(Ev.CowNoPath));
        }

        [Fact]
        public void A_cow_avoids_water_itself_but_pushes_a_wolf_into_it()
        {
            var g = Kit.G("""
                # t  shift: R3 : R  제한 9턴  목표 양 1
                .M  .W  ~  .A
                .   .   .  .
                .   .   .  .S
                .   .   .  .
                """);
            g.Do("R3>");
            // 소는 물을 피해 오른쪽으로 한 칸 가며 늑대를 물로 민다. 젖은 늑대는 물에서 걸어 나온다.
            Kit.Board(g, """
                .  .M  ~  .W(w)A
                .  .   .  .
                .  .   .  .S
                .  .   .  .
                """);
            Assert.True(g.Has(Ev.CowPushWolf));
        }
    }

    /// <summary>README 2절 정리 절차: 겹침 상호작용 → 자동 반응(말 발견 → 말 먹기 → 소 냄새 → 늑대 양 잡기).</summary>
    public class CleanupOrderTests
    {
        [Fact]
        public void A_horse_eats_and_drops_its_sheep_before_the_pack_catches()
        {
            // 말이 (1,1)에 들어서면 무리(2,2)의 범위 안이지만, 먼저 위쪽 바구니를 먹어 양을 (0,1)로 옮긴다.
            var g = Kit.G("""
                # t  shift: R3 : R  제한 9턴  목표 양 1
                #    .A  .    .
                .HS  .   ~    .A
                .    ~   .W2  ~
                x    x   x    .
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R3>"));
            Assert.Equal(".S", g.Cell(0, 1));
            Assert.Equal(".H", g.Cell(1, 1));
            Assert.True(g.Has(Ev.HorseEat));
            Assert.False(g.Has(Ev.WolfEat));
        }

        [Fact]
        public void No_cleanup_runs_before_the_first_shift()
        {
            // 처음부터 겹쳐 있어도 첫 shift 뒤의 정리에서 처리된다.
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴  목표 양 1
                .SW  .
                .    .
                """);
            Assert.Equal(".SW", g.Cell(0, 0));
            Assert.Equal(Outcome.Lose, g.Do("R1>"));
        }
    }
}
