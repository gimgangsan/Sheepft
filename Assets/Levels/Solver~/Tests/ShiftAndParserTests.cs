using System;
using Sheepft.Core;
using Xunit;

namespace Sheepft.Tests
{
    /// <summary>README 3절: shift 판정, 순환, 빈칸, 방향. 10절: 표기.</summary>
    public class ShiftTests
    {
        [Fact]
        public void Shift_moves_every_object_in_the_line_one_cell()
        {
            var g = Kit.G("""
                # t  shift: R0 : RL  제한 9턴
                .S  .  .
                """);
            g.Do("R0>");
            Kit.Board(g, ".  .S  .");
            g.Do("R0<");
            Kit.Board(g, ".S  .  .");
            Assert.True(g.Has(Ev.ShiftMove));
        }

        [Fact]
        public void Shift_wraps_to_the_opposite_end()
        {
            var g = Kit.G("""
                # t  shift: R0 : RL  제한 9턴
                .  .  .S
                """);
            g.Do("R0>");
            Kit.Board(g, ".S  .  .");
            Assert.True(g.Has(Ev.SheepWrap));
            g.Do("R0<");
            Kit.Board(g, ".  .  .S");
        }

        [Fact]
        public void Column_shift_wraps_too()
        {
            var g = Kit.G("""
                # t  shift: C0 : UD  제한 9턴
                .S
                .
                .
                """);
            g.Do("C0^");
            Kit.Board(g, """
                .
                .
                .S
                """);
            g.Do("C0v");
            Kit.Board(g, """
                .S
                .
                .
                """);
        }

        [Fact]
        public void Obstacle_blocks_and_the_follower_overlaps()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .S  .  .S  #
                """);
            g.Do("R0>");
            Kit.Board(g, ".  .S  .S  #");
            Assert.True(g.Has(Ev.Blocked));
            Assert.False(g.Has(Ev.SheepMerge));
            g.Do("R0>");
            Kit.Board(g, ".  .  .S2  #");
            Assert.True(g.Has(Ev.SheepMerge));
        }

        [Fact]
        public void Wrap_is_blocked_when_the_opposite_end_is_an_obstacle()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                #  .  .S
                """);
            g.Do("R0>");
            Kit.Board(g, "#  .  .S");
            Assert.True(g.Has(Ev.Wait));
        }

        [Fact]
        public void Shift_skips_voids_then_wraps_like_the_readme_example()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .  .S  x  x  .
                """);
            g.Do("R0>");
            Kit.Board(g, ".  .  x  x  .S");
            Assert.True(g.Has(Ev.VoidSkip));
            Assert.False(g.Has(Ev.Wrap));
            g.Do("R0>");
            Kit.Board(g, ".S  .  x  x  .");
            Assert.True(g.Has(Ev.Wrap));
        }

        [Fact]
        public void Objects_do_not_block_each_other()
        {
            var g = Kit.G("""
                # t  shift: R0 : R  제한 9턴
                .A  .B  #
                """);
            g.Do("R0>");
            Kit.Board(g, ".  .AB  #");
        }

        [Fact]
        public void Only_the_listed_directions_are_allowed()
        {
            var g = Kit.G("""
                # t  shift: R0 : R, C1 : U  제한 9턴
                .  .  .
                .  .  .
                """);
            Assert.Equal(2, g.Level.Moves.Length);
            Move m;
            Assert.True(g.Level.TryParseMove("R0>", out m));
            Assert.True(g.Level.TryParseMove("C1↑", out m));
            Assert.False(g.Level.TryParseMove("R0<", out m));
            Assert.False(g.Level.TryParseMove("C1v", out m));
            Assert.False(g.Level.TryParseMove("R1>", out m));
            Assert.Throws<ArgumentException>(() => g.Do("R0<"));
        }

        [Fact]
        public void A_shift_that_moves_nothing_is_a_wait_and_costs_a_turn()
        {
            var g = Kit.G("""
                # t  shift: R1 : RL  제한 9턴
                .S  .
                .   .
                """);
            g.Do("R1>");
            Kit.Board(g, """
                .S  .
                .   .
                """);
            Assert.Equal(1, g.Turn);
            Assert.True(g.Has(Ev.Wait));
        }

        [Fact]
        public void Running_out_of_turns_loses_but_winning_on_the_last_turn_counts()
        {
            var lose = Kit.G("""
                # t  shift: R0 : R  제한 2턴  목표 양 1
                .S  .  .  g
                """);
            Assert.Equal(Outcome.Ongoing, lose.Do("R0>"));
            Assert.Equal(Outcome.Lose, lose.Do("R0>"));

            var win = Kit.G("""
                # t  shift: R0 : R  제한 2턴  목표 양 1
                .S  .  g
                """);
            Assert.Equal(Outcome.Win, win.Do("R0> R0>"));
        }
    }

    public class ParserTests
    {
        [Fact]
        public void Reads_several_levels_with_solver_marks()
        {
            var levels = LevelParser.ParseAll("""
                # 하나  shift: R0 : R  제한 3턴  목표 양 1
                @desc 설명 한 줄
                @intent Wrap SheepMerge
                @stars 2 3
                .S  g

                // 주석
                # 둘  shift: C0 : UD, R1 : L  제한 5턴
                #   .
                .S  .  .
                """);
            Assert.Equal(2, levels.Count);

            var a = levels[0];
            Assert.Equal("하나", a.Title);
            Assert.Equal(3, a.TurnLimit);
            Assert.Equal(1, a.TargetSheep);
            Assert.Equal("설명 한 줄", a.Description);
            Assert.Equal(Ev.Wrap | Ev.SheepMerge, a.Intent);
            Assert.Equal(2, a.Star3);
            Assert.Equal(3, a.Star2);

            var b = levels[1];
            Assert.Equal("둘", b.Title);
            Assert.Equal(2, b.Rows);
            Assert.Equal(3, b.Cols);
            Assert.Equal(Terrain.Stone, b.Terrain[b.Pos(0, 0)]);
            Assert.Equal(Terrain.Void, b.Terrain[b.Pos(0, 2)]); // 짧은 줄의 오른쪽은 빈칸
            Assert.Equal(1, b.TargetSheep);                      // 기본값은 모든 양
            Assert.Equal(3, b.Moves.Length);                     // C0↓, C0↑, R1←
        }

        [Fact]
        public void Reads_counts_and_initial_states()
        {
            var lv = LevelParser.ParseOne("""
                # t  shift: R0 : R  제한 3턴  목표 양 2
                .S3  ~S(w)  .W2  .U  .B(w)  .HA
                """);
            Assert.Equal(4, lv.SheepCount);
            var g = new Game(lv);
            Kit.Board(g, ".S3  ~S(w)  .W2  .U  .B(w)  .HA");
        }

        [Theory]
        [InlineData("# t  shift: R0 : R  제한 3턴\n#S  .")]            // 장애물 위 오브젝트
        [InlineData("# t  shift: R0 : R  제한 3턴\nxS  .")]            // 빈칸 위 오브젝트
        [InlineData("# t  shift: R0 : R  제한 3턴\n?  .")]             // 모르는 지형
        [InlineData("# t  shift: R0 : U  제한 3턴\n.  .")]             // 행에 열 방향
        [InlineData("# t  shift: R5 : R  제한 3턴\n.  .")]             // 없는 줄
        [InlineData("# t  shift: R0 : R  제한 3턴  목표 양 2\n.S  .")] // 목표가 양보다 많음
        [InlineData("# t  shift: R0 : R\n.S  .")]                      // 헤더에 제한 턴이 없음
        public void Rejects_bad_levels(string text)
        {
            Assert.Throws<LevelFormatException>(() => LevelParser.ParseOne(text));
        }
    }
}
