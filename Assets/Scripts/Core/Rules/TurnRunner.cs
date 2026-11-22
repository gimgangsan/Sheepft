using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>턴 진행. README 2절, 구현 계획 4절. "언제, 어떤 순서로"를 맡는다.</summary>
    public static class TurnRunner
    {
        /// <summary>
        /// 한 턴을 끝까지 계산한다. 일어난 일은 board.Events에 남는다.
        /// 게임이 이미 끝났거나 이 레벨의 shift 포인트가 아닌 수는 거부한다(false, 턴 소모 없음).
        /// </summary>
        public static bool PlayTurn(Board board, ShiftPoint move)
        {
            if (board.Result != GameResult.InProgress) return false;
            if (!IsAllowed(board, move)) return false;

            board.Events.Clear();
            board.Mechanics = Mechanic.None;
            board.TurnsUsed++;
            board.TurnEnding = false;
            foreach (var o in board.Objects) o.AppliedThisTurn.Clear();

            // 1단계: shift. 아무것도 움직이지 않아도 턴을 쓴다.
            LineShifter.Shift(board, move);
            board.Events.Add(new StepEndEvent());
            Settler.Settle(board);
            if (board.Result == GameResult.Lost) return true;
            if (TryWin(board)) return true;

            // 2단계: 종료 후 반응. 염소 → 말 → 소 → 늑대 → 들불 확산.
            // 한 마리의 행동이 끝날 때마다 정리 절차를 돌린다.
            if (!RunPhase<Goat>(board)) return true;
            if (!RunPhase<Horse>(board)) return true;
            if (!RunPhase<Cow>(board)) return true;
            if (!RunPhase<Wolf>(board)) return true;
            // 정리 절차는 바뀌는 것이 없을 때까지 돌므로, 그 뒤로 아무것도 바뀌지 않았으면 다시 돌려도 할 일이 없다.
            // 그래서 확산이 없었으면 건너뛴다(solver가 한 턴을 수백만 번 돌리므로).
            if (FireSpread.Spread(board))
            {
                board.Events.Add(new StepEndEvent());
                Settler.Settle(board);
                if (board.Result == GameResult.Lost) return true;
            }
            if (TryWin(board)) return true;

            // 기절과 식사는 2단계 승리 판정 뒤에 풀린다. 풀린 것이 있으면 정리 절차를 한 번 더 돌린다.
            // 여럿이 풀려도 모두 한꺼번에 푼 뒤 한 번만 돈다 (구현 계획 10절).
            board.TurnEnding = true;
            if (ReleaseStunAndMeal(board))
            {
                board.Events.Add(new StepEndEvent());
                Settler.Settle(board);
                if (board.Result == GameResult.Lost) return true;
            }

            if (board.TurnsUsed >= board.Rules.TurnLimit) board.Result = GameResult.Lost;
            return true;
        }

        /// <summary>
        /// 한 종류의 종료 후 반응. 차례가 시작될 때의 위치 기준으로 (0,0)부터 행 우선(같은 칸이면 Id) 순서다.
        /// 차례가 오기 전에 사라졌거나 행동 불가인 것은 건너뛴다. 한 마리가 끝날 때마다 정리 절차를 돌린다.
        /// 패배하면 false.
        /// </summary>
        private static bool RunPhase<T>(Board board) where T : Animal
        {
            List<T> actors = null;
            foreach (var o in board.Objects)
            {
                var actor = o as T;
                if (actor == null) continue;
                if (actors == null) actors = new List<T>();
                actors.Add(actor);
            }
            if (actors == null) return true;

            foreach (var actor in actors)
            {
                if (actor.Removed) continue;
                if (actor.IsIncapacitated(board))
                {
                    if (actor.IsHeldByRide(board)) board.Mark(Mechanic.RideHeld);
                    continue;
                }
                // 상태를 바꾸는 일은 모두 Board를 거쳐 이벤트를 남긴다. 이벤트가 없으면 바뀐 것이 없으므로
                // 정리 절차를 다시 돌려도 할 일이 없다(바로 앞의 정리 절차가 끝까지 돌았다).
                int before = board.Events.Count;
                actor.AfterShift(board);
                if (board.Events.Count == before) continue;
                board.Events.Add(new StepEndEvent());
                Settler.Settle(board);
                if (board.Result == GameResult.Lost) return false;
            }
            return true;
        }

        /// <summary>기절·식사의 남은 기간을 1씩 줄인다. 풀린 것이 있으면 true.</summary>
        private static bool ReleaseStunAndMeal(Board board)
        {
            bool released = false;
            foreach (var o in board.Objects)
            {
                var wolf = o as Wolf;
                if (wolf == null) continue;
                if (wolf.StunLeft > 0 && board.SetStun(wolf, wolf.StunLeft - 1)) released = true;
                if (wolf.MealLeft > 0 && board.SetMeal(wolf, wolf.MealLeft - 1)) released = true;
            }
            return released;
        }

        private static bool IsAllowed(Board board, ShiftPoint move)
        {
            foreach (var p in board.Rules.ShiftPoints)
                if (p == move) return true;
            return false;
        }

        private static bool TryWin(Board board)
        {
            if (!board.IsWinConditionMet()) return false;
            board.Result = GameResult.Won;
            return true;
        }
    }
}
