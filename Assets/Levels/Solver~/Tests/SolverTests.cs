using System.Linq;
using Sheepft.Core;
using Xunit;

namespace Sheepft.Tests
{
    public class SolverTests
    {
        private const string ReadmeExample = """
            # 예시  shift: R1 : RL, C2 : UD  제한 4턴  목표 양 2
            .   .   g   .
            .S  .   .S  #
            .   .   .   .
            .   .   .   .
            """;

        private const string ReadmeVoidExample = """
            # 빈칸 예시  shift: R0 : R, C4 : UD  제한 3턴  목표 양 1
            .   .S  x   x   .
            .   .   .   .   .
            x   x   x   .   g
            """;

        [Fact]
        public void Solves_the_readme_example_in_three_turns()
        {
            var lv = LevelParser.ParseOne(ReadmeExample);
            var r = Solver.Solve(lv);
            Assert.Equal(3, r.OptimalTurns);
            Assert.Contains("R1:R R1:R C2:U", r.Solutions.Select(s => lv.MovesText(s.Moves)));
            Assert.All(r.Solutions, s => Assert.True((s.Events & Ev.SheepMerge) != 0));
        }

        [Fact]
        public void Solves_the_readme_void_example_exactly_as_described()
        {
            var lv = LevelParser.ParseOne(ReadmeVoidExample);
            var r = Solver.Solve(lv);
            Assert.Equal(2, r.OptimalTurns);
            Assert.Equal(1, (int)r.OptimalSequences);
            Assert.Equal("R0:R C4:U", lv.MovesText(r.Solutions[0].Moves));
            Assert.True((r.Solutions[0].Events & Ev.VoidSkip) != 0);
            Assert.True((r.Solutions[0].Events & Ev.SheepWrap) != 0);
        }

        [Fact]
        public void Reports_the_shortest_solution_that_skips_the_intended_gimmick()
        {
            var lv = LevelParser.ParseOne("""
                # t  shift: R0 : RL  제한 5턴  목표 양 1
                @intent SheepWrap
                .S  .  .  g
                """);
            var r = Solver.Solve(lv);
            Assert.Equal(1, r.OptimalTurns);   // R0← 로 순환
            Assert.Equal(1, r.IntendedTurns);
            Assert.Equal(3, r.BypassTurns);    // R0→ 세 번이면 순환 없이도 깨진다
            Assert.Equal("R0:R R0:R R0:R", lv.MovesText(r.Bypass.Moves));
            Assert.Equal(Ev.SheepWrap, r.BypassMissing);
        }

        [Fact]
        public void Finds_no_bypass_when_the_turn_limit_rules_it_out()
        {
            var lv = LevelParser.ParseOne("""
                # t  shift: R0 : RL  제한 2턴  목표 양 1
                @intent SheepWrap
                .S  .  .  g
                """);
            var r = Solver.Solve(lv);
            Assert.Equal(1, r.OptimalTurns);
            Assert.Equal(-1, r.BypassTurns);
        }

        [Fact]
        public void Counts_move_sequences_and_distinct_state_paths_separately()
        {
            // 최적은 2턴. 빈 줄 R1은 두 방향 모두 같은 "대기"라서 수순으로는 여럿이어도 상태 경로는 하나일 수 있다.
            var lv = LevelParser.ParseOne("""
                # t  shift: R0 : R, R1 : RL  제한 4턴  목표 양 1
                .S  .  g
                .   .  .
                """);
            var r = Solver.Solve(lv);
            Assert.Equal(2, r.OptimalTurns);
            Assert.Equal(1, (int)r.OptimalSequences);
            Assert.Equal(1, (int)r.OptimalStatePaths);
        }

        [Fact]
        public void Waiting_moves_count_as_moves()
        {
            // 밀 수 있는 줄은 빈 줄뿐이다. 두 턴을 기다리면 염소가 양을 풀까지 밀어 준다.
            var lv = LevelParser.ParseOne("""
                # t  shift: R1 : R  제한 5턴  목표 양 1
                .G  .S  .  g
                .   .   .  .
                """);
            var r = Solver.Solve(lv);
            Assert.Equal(2, r.OptimalTurns);
            Assert.Equal("R1:R R1:R", lv.MovesText(r.Solutions[0].Moves));
            Assert.True((r.Solutions[0].Events & Ev.Wait) != 0);
            Assert.True((r.Solutions[0].Events & Ev.GoatPushSheep) != 0);
        }

        [Fact]
        public void A_packed_state_unpacks_to_the_same_state()
        {
            var lv = LevelParser.ParseOne("""
                # t  shift: R0 : R, R3 : R  제한 9턴  목표 양 1
                .W  .W  #    .S(w)  ~B
                .H  .   .A   .      ^
                .M  .   .A   .G(b)  .U
                .   .   .S2  .      g
                """);
            var g = new Game(lv);
            g.Do("R0> R3>"); // 합류(기절), 발견, 냄새, 걷기까지 일어난 상태
            var packed = g.State.Pack();
            var copy = lv.NewState();
            copy.Load(packed);
            Assert.Equal(g.State.Key(), copy.Key());
            Assert.Equal(Board.Render(lv, g.State), Board.Render(lv, copy));

            // 풀어 놓은 상태에서 이어 두어도 결과가 같다.
            var a = Simulator.Step(lv, g.State, lv.Moves[1]);
            var b = Simulator.Step(lv, copy, lv.Moves[1]);
            Assert.Equal(a.Outcome, b.Outcome);
            Assert.Equal(a.Events, b.Events);
            Assert.Equal(g.State.Key(), copy.Key());
        }

        [Fact]
        public void The_state_key_ignores_which_identical_object_is_which()
        {
            var a = new Game(LevelParser.ParseOne("# t  shift: R0 : RL  제한 9턴  목표 양 1\n.S  .  .S  ."));
            var b = new Game(LevelParser.ParseOne("# t  shift: R0 : RL  제한 9턴  목표 양 1\n.  .S  .  .S"));
            a.Do("R0>");
            Assert.Equal(a.State.Key(), b.State.Key());
            b.Do("R0>");
            Assert.NotEqual(a.State.Key(), b.State.Key());
        }

        [Fact]
        public void Reports_unsolvable_levels()
        {
            var lv = LevelParser.ParseOne("""
                # t  shift: R0 : R  제한 5턴  목표 양 1
                .S  #  g
                """);
            var r = Solver.Solve(lv);
            Assert.Equal(-1, r.OptimalTurns);
            Assert.Empty(r.Solutions);
        }

        [Fact]
        public void Every_reported_solution_replays_to_a_win()
        {
            var lv = LevelParser.ParseOne("""
                # t  shift: R0 : RL  제한 6턴  목표 양 1
                .S  .W  #  .  g
                """);
            var r = Solver.Solve(lv);
            Assert.True(r.OptimalTurns > 0);
            foreach (var s in r.Solutions)
            {
                var g = new Game(lv);
                Assert.Equal(Outcome.Win, g.Do(s.Moves));
            }
        }
    }
}
