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
            board.TurnsUsed++;
            foreach (var o in board.Objects) o.AppliedThisTurn.Clear();

            // 1단계: shift. 아무것도 움직이지 않아도 턴을 쓴다.
            LineShifter.Shift(board, move);
            board.Events.Add(new StepEndEvent());
            Settler.Settle(board);
            if (board.Result == GameResult.Lost) return true;
            if (TryWin(board)) return true;

            // 2단계: 종료 후 반응. 염소 → 말 → 소 → 늑대 → 들불 확산.
            // 한 마리의 행동이 끝날 때마다 정리 절차를 돌린다.
            // (늑대는 4단계, 들불 확산은 5단계, 염소·말·소는 6–8단계에서 넣는다.)
            Settler.Settle(board);
            if (board.Result == GameResult.Lost) return true;
            if (TryWin(board)) return true;

            // 기절·식사 해제 (4단계에서 넣는다.)

            if (board.TurnsUsed >= board.Rules.TurnLimit) board.Result = GameResult.Lost;
            return true;
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
