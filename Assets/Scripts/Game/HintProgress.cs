using System.Collections.Generic;
using Sheepft.Core;

namespace Sheepft.Game
{
    /// <summary>힌트가 지금 가리키는 것.</summary>
    public enum HintKind
    {
        /// <summary>보여 줄 것이 없다(풀이를 끝까지 따라왔다).</summary>
        None,
        /// <summary>풀이의 다음 수. 그 shift 화살표를 보여 준다.</summary>
        Move,
        /// <summary>풀이에서 벗어났다. 갈라진 곳까지 undo하라고 Undo 버튼을 깜빡인다.</summary>
        Undo,
    }

    /// <summary>
    /// 한 스테이지의 힌트·건너뛰기 진행. 구현 계획 13절. 게임을 켜 둔 동안만 기억한다(저장하지 않음).
    ///
    /// 왔다 갔다만 해서 힌트를 받지 못하게, shift 수가 아니라 **실패한 시도**를 센다.
    /// 패배하거나, 턴을 restartMinTurns 이상 쓰고 재시작하면 실패 1회다. 한 시도에서는 한 번만 센다
    /// (패배 뒤 undo로 다시 지면 같은 시도). 재시작하거나 undo로 처음 상태까지 돌아가면 새 시도다.
    /// 실패가 needFails 이상이고 스테이지에서 needSeconds 이상 보냈으면 힌트와 건너뛰기가 열린다.
    ///
    /// 힌트 버튼을 한 번 누르면 풀이의 다음 한 수만 알려 준다(Active). 그 수를 두면 꺼진다.
    /// 힌트를 썼으면(Used) 다음에 깰 때 별을 줄인다. 힌트를 쓰고 깬 뒤 새 시도(재시작, undo로 처음까지,
    /// 스테이지 다시 열기)를 시작하면 Used를 지운다. 깬 판을 undo해서 다시 깨는 것은 같은 시도라 그대로다.
    /// </summary>
    public sealed class HintProgress
    {
        private readonly int needFails;
        private readonly float needSeconds;
        private readonly int restartMinTurns;
        /// <summary>이번 시도를 이미 실패로 셌다.</summary>
        private bool attemptCounted;
        /// <summary>힌트를 쓰고 깼다. 새 시도를 시작하면 Used를 지운다.</summary>
        private bool clearedWithHint;

        public int FailedAttempts { get; private set; }
        public float Seconds { get; private set; }
        /// <summary>힌트와 건너뛰기가 열렸다. 한 번 열리면 닫히지 않는다.</summary>
        public bool Unlocked { get; private set; }
        /// <summary>힌트를 눌렀고, 그 뒤로 깨고 새 시도를 시작하지 않았다. 켜져 있으면 별을 줄인다.</summary>
        public bool Used { get; private set; }
        /// <summary>힌트를 눌러 다음 한 수를 보는 중.</summary>
        public bool Active { get; private set; }

        public HintProgress(int needFails, float needSeconds, int restartMinTurns)
        {
            this.needFails = needFails;
            this.needSeconds = needSeconds;
            this.restartMinTurns = restartMinTurns;
        }

        /// <summary>스테이지에서 보낸 시간을 더한다. 이번에 힌트가 열렸으면 true.</summary>
        public bool AddTime(float seconds)
        {
            if (seconds > 0f) Seconds += seconds;
            return CheckUnlock();
        }

        /// <summary>패배했다. 이번에 힌트가 열렸으면 true.</summary>
        public bool OnLost()
        {
            CountAttempt();
            return CheckUnlock();
        }

        /// <summary>재시작했다. turnsUsed는 재시작하기 전에 쓴 턴 수. 이번에 힌트가 열렸으면 true.</summary>
        public bool OnRestart(int turnsUsed)
        {
            if (turnsUsed >= restartMinTurns) CountAttempt();
            NewAttempt();
            return CheckUnlock();
        }

        /// <summary>
        /// undo로 처음 상태까지 돌아왔거나, 깬 판을 재시작했다. 새 시도지만 실패로 세지는 않는다(undo를 반복해 세지 않게).
        /// </summary>
        public void OnBackToStart()
        {
            NewAttempt();
        }

        /// <summary>스테이지를 (다시) 열었다. 새 시도다.</summary>
        public void OnEnterStage()
        {
            NewAttempt();
        }

        /// <summary>깼다. 힌트를 쓴 클리어라면 이번 별은 줄이고, 새 시도부터는 별을 제대로 준다.</summary>
        public void OnWon()
        {
            Active = false;
            if (Used) clearedWithHint = true;
        }

        private void NewAttempt()
        {
            attemptCounted = false;
            if (!clearedWithHint) return;
            clearedWithHint = false;
            Used = false;
        }

        /// <summary>힌트 버튼. 열려 있을 때만 받는다.</summary>
        public bool Request()
        {
            if (!Unlocked) return false;
            Active = true;
            Used = true;
            // 깬 뒤 undo해서 다시 힌트를 보면, 다음에 깰 때까지 별 제한이 이어진다.
            clearedWithHint = false;
            return true;
        }

        /// <summary>힌트가 가리키던 수를 두었다.</summary>
        public void Done()
        {
            Active = false;
        }

        private void CountAttempt()
        {
            if (attemptCounted) return;
            attemptCounted = true;
            FailedAttempts++;
        }

        private bool CheckUnlock()
        {
            if (Unlocked || FailedAttempts < needFails || Seconds < needSeconds) return false;
            Unlocked = true;
            return true;
        }

        /// <summary>
        /// 지금까지 둔 수와 풀이를 견준다. 둔 수가 풀이의 앞부분이면 다음 수(Move),
        /// 벗어났으면 Undo, 풀이를 다 두었으면 None.
        /// </summary>
        public static HintKind Next(IReadOnlyList<ShiftPoint> moves, IReadOnlyList<ShiftPoint> solution, out ShiftPoint move)
        {
            move = default;
            for (int i = 0; i < moves.Count; i++)
                if (i >= solution.Count || moves[i] != solution[i]) return HintKind.Undo;
            if (moves.Count >= solution.Count) return HintKind.None;
            move = solution[moves.Count];
            return HintKind.Move;
        }
    }
}
