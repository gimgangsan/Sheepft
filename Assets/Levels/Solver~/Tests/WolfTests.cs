using Sheepft.Core;
using Xunit;

namespace Sheepft.Tests
{
    /// <summary>README 6절 늑대와 늑대 무리: 추적, 양 잡기, 식사, 합류, 범위.</summary>
    public class WolfTests
    {
        [Fact]
        public void A_wolf_walks_one_cell_per_turn_and_eats_on_contact()
        {
            var g = Kit.G("""
                # t  shift: R2 : R  제한 9턴  목표 양 1
                .W  .  .  .S
                .   .  .  .
                .   .  .  .
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R2>"));
            Assert.Equal(".W", g.Cell(0, 1));
            Assert.Equal(Outcome.Ongoing, g.Do("R2>"));
            Assert.Equal(Outcome.Lose, g.Do("R2>"));
            Assert.True(g.Has(Ev.WolfWalk | Ev.WolfEat));
        }

        [Fact]
        public void Direction_ties_go_up_right_down_left()
        {
            var g = Kit.G("""
                # t  shift: R3 : R  제한 9턴  목표 양 1
                .   .  .S
                .   .  .
                .W  .  .
                .   .  .
                """);
            g.Do("R3>");
            Assert.Equal(".W", g.Cell(1, 0)); // 위와 오른쪽이 동점이면 위
        }

        [Fact]
        public void Direction_ties_prefer_the_last_walked_direction()
        {
            var g = Kit.G("""
                # t  shift: R3 : R  제한 9턴  목표 양 1
                .   .  .S
                #   .  .
                .W  .  .
                .   .  .
                """);
            g.Do("R3>");
            Assert.Equal(".W", g.Cell(2, 1)); // 위가 돌이라 오른쪽으로
            g.Do("R3>");
            Assert.Equal(".W", g.Cell(2, 2)); // 위와 오른쪽이 동점이면 방금 걸은 오른쪽
        }

        [Fact]
        public void With_several_nearest_sheep_every_closer_cell_is_a_candidate()
        {
            // README 6절: 양을 하나로 고르지 않는다. 왼쪽·오른쪽·아래가 후보이고 상 > 우 > 하 > 좌로 오른쪽.
            var g = Kit.G("""
                # t  shift: R3 : R  제한 9턴  목표 양 1
                .   .  .   .
                .S  .  .W  .
                .   .  .   .S
                .   .  .   .
                """);
            g.Do("R3>");
            Assert.Equal(".W", g.Cell(1, 3));
            Assert.Equal(Outcome.Ongoing, g.Do("R3>")); // 한 마리를 먹어도 목표 1마리는 남는다.
            Assert.Equal(".W", g.Cell(2, 3));
        }

        [Fact]
        public void A_wolf_cannot_reach_a_sheep_standing_on_water()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴  목표 양 1
                .W  .  ~S(w)
                .   .  .
                """);
            g.Do("R1>");
            Assert.Equal(".W", g.Cell(0, 0));
            Assert.True(g.Has(Ev.WolfNoPath));
        }

        [Fact]
        public void A_wolf_walks_around_water()
        {
            var g = Kit.G("""
                # t  shift: R2 : R  제한 9턴  목표 양 1
                .W  ~  .S
                .   .  .
                .   .  .
                """);
            g.Do("R2>");
            Assert.Equal(".W", g.Cell(1, 0));
        }

        [Fact]
        public void Walking_does_not_wrap_so_a_walled_in_wolf_waits()
        {
            var g = Kit.G("""
                # t  shift: R2 : R  제한 9턴  목표 양 1
                .W  #  .S
                #   #  .
                .   .  .
                """);
            g.Do("R2>");
            Assert.Equal(".W", g.Cell(0, 0));
            Assert.True(g.Has(Ev.WolfNoPath));
        }

        [Fact]
        public void A_meal_lasts_through_the_end_of_the_next_turn()
        {
            var g = Kit.G("""
                # t  shift: R0 : R, R2 : R  제한 9턴  목표 양 1
                .S  .S  .W  #
                .   .   .   .
                .   .   .   .
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R0>")); // 앞 양이 먹히고 늑대는 식사에 들어간다.
            Kit.Board(g, """
                .  .S  .W  #
                .  .   .   .
                .  .   .   .
                """);
            // 다음 턴: 식사 중이라 같은 칸에 들어온 양을 잡지 않는다. 그 턴이 끝나면 식사가 풀리며 잡는다.
            Assert.Equal(Outcome.Lose, g.Do("R0>"));
            Assert.True(g.Has(Ev.MealSpared));
        }

        [Fact]
        public void A_meal_that_starts_when_the_last_one_ends_runs_through_the_next_turn()
        {
            // 1턴: 한 마리 먹음. 2턴 끝: 식사가 풀리며 또 한 마리. 이 식사는 3턴 끝까지 가고, 풀리면서 마지막 양을 먹는다.
            var g = Kit.G("""
                # t  shift: R0 : R, R2 : R  제한 9턴  목표 양 1
                .S3  .W  #
                .    .   .
                .    .   .
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R0>"));
            Assert.Equal(".S2W", g.Cell(0, 1));
            Assert.Equal(Outcome.Ongoing, g.Do("R2>"));
            Assert.Equal(".SW", g.Cell(0, 1));
            Assert.Equal(Outcome.Lose, g.Do("R2>"));
        }

        [Fact]
        public void A_sheep_can_win_next_to_an_eating_wolf()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴  목표 양 1
                .S  .S  gW  #
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R0>"));
            Assert.Equal(Outcome.Win, g.Do("R0>"));
        }

        [Fact]
        public void Merged_wolves_are_stunned_and_do_not_catch_until_the_stun_ends()
        {
            var g = Kit.G("""
                # t  shift: R0 : R, R2 : R  제한 9턴  목표 양 1
                .W  .W  #
                .   .   .S
                .   .   .
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R0>"));
            Assert.Equal(".W2", g.Cell(0, 1));
            Assert.True(g.Has(Ev.WolfMerge));
            Assert.True(g.Has(Ev.StunSpared));  // 주변 8칸 안이지만 기절 중이라 잡지 않는다.
            Assert.False(g.Has(Ev.WolfEat));

            // 다음 턴 끝에 기절이 풀리면서 새 범위(주변 8칸)로 잡는다.
            Assert.Equal(Outcome.Lose, g.Do("R2>"));
            Assert.True(g.Has(Ev.PackCatch));
        }

        [Fact]
        public void Merging_comes_before_catching_in_the_same_cell()
        {
            var g = Kit.G("""
                # t  shift: R0 : R, R2 : R  제한 9턴  목표 양 1
                .SW  .W  #
                .    .   .
                .    .   .
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R0>"));
            Assert.Equal(".SW2", g.Cell(0, 1));
            Assert.Equal(Outcome.Lose, g.Do("R2>")); // 기절이 풀리는 순간 먹힌다.
        }

        [Fact]
        public void A_pack_eats_at_most_its_size_at_once()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴  목표 양 1
                .S3  .W2  #
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R0>"));
            Kit.Board(g, ".  .SW2  #");
        }

        [Fact]
        public void A_pack_of_two_catches_in_the_eight_cells_around_it()
        {
            var g = Kit.G("""
                # t  shift: R2 : R  제한 9턴  목표 양 1
                .W2  .   .
                .    .S  .S
                .    .   .
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R2>"));
            Kit.Board(g, """
                .W2  .  .
                .    .  .S
                .    .  .
                """);
            Assert.True(g.Has(Ev.PackCatch));
        }

        [Fact]
        public void The_catch_range_ignores_voids()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴  목표 양 1
                .W2  x
                x    .S
                """);
            Assert.Equal(Outcome.Lose, g.Do("R1>"));
        }

        [Fact]
        public void A_burned_wolf_neither_catches_nor_merges()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴  목표 양 1
                .S  .W(b)  #
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R0>"));
            Kit.Board(g, ".  .SW(b)  #");

            var two = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .W  .W(b)  #
                """);
            two.Do("R0>");
            Kit.Board(two, ".  .WW(b)  #");
            Assert.False(two.Has(Ev.WolfMerge));
        }

        [Fact]
        public void A_wolf_catches_the_moment_its_burn_is_cured()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴  목표 양 1
                .SW(b)  ~  .
                """);
            Assert.Equal(Outcome.Lose, g.Do("R0>"));
            Assert.True(g.Has(Ev.WolfCured));
        }

        [Fact]
        public void Wolves_walk_before_the_fire_spreads()
        {
            // 늑대가 풀로 걸어 들어간 뒤 그 풀이 탄다. 확산이 먼저였다면 늑대는 들불을 피했을 것이다.
            var g = Kit.G("""
                # t  shift: R2 : R  제한 9턴  목표 양 1
                ^   .  .
                .W  g  .S
                .   .  .
                """);
            g.Do("R2>");
            Assert.Equal("^W(b)", g.Cell(1, 1));
            Assert.True(g.Has(Ev.WolfBurned));
        }
    }

    /// <summary>README 2절, 8절: 승리와 패배의 판정 시점.</summary>
    public class OutcomeTests
    {
        [Fact]
        public void Reaching_grass_wins_before_an_adjacent_wolf_walks()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴  목표 양 1
                .S  g   .
                .   .W  .
                """);
            Assert.Equal(Outcome.Win, g.Do("R0>"));
        }

        [Fact]
        public void Being_caught_on_arrival_comes_before_the_win_check()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴  목표 양 1
                .S  gW  #
                """);
            Assert.Equal(Outcome.Lose, g.Do("R0>"));
        }

        [Fact]
        public void The_target_count_is_enough_to_win()
        {
            var g = Kit.G("""
                # t  shift: R0 : R, R1 : R  제한 9턴  목표 양 2
                .S  g
                .S  g
                .S  .
                """);
            Assert.Equal(Outcome.Ongoing, g.Do("R0>"));
            Assert.Equal(Outcome.Win, g.Do("R1>"));
        }

        [Fact]
        public void Falling_below_the_target_loses_at_once()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴  목표 양 2
                .S  .W  #
                .S  .   g
                """);
            Assert.Equal(Outcome.Lose, g.Do("R0>"));
        }

        [Fact]
        public void A_win_is_also_checked_after_the_end_of_turn_reactions()
        {
            var g = Kit.G("""
                # t  shift: R1 : R  제한 9턴  목표 양 1
                .G  .S  g
                .   .   .
                """);
            Assert.Equal(Outcome.Win, g.Do("R1>")); // 염소가 양을 풀로 밀어 준다.
        }
    }
}
