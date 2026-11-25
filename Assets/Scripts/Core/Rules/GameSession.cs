using System;
using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>
    /// 레벨 하나를 플레이하는 동안의 진행. 지금 맵과 undo 스택을 갖는다.
    /// 턴마다 맵을 통째로 복사해 쌓는다. 구현 계획 5절.
    /// </summary>
    public sealed class GameSession
    {
        private readonly Board initial;
        private readonly Stack<Board> history = new Stack<Board>();
        private readonly List<ShiftPoint> moves = new List<ShiftPoint>();

        /// <summary>지금 맵. undo와 재시작 때 다른 인스턴스로 바뀐다.</summary>
        public Board Board { get; private set; }

        public bool CanUndo => history.Count > 0;

        /// <summary>처음 상태부터 지금까지 둔 수. undo하면 줄고 재시작하면 빈다. 힌트가 풀이와 견줄 때 쓴다.</summary>
        public IReadOnlyList<ShiftPoint> Moves => moves;

        public GameSession(Board level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            initial = level.Clone();
            Board = level;
        }

        /// <summary>한 턴을 둔다. 거부된 수는 undo 스택에 남지 않는다.</summary>
        public bool Play(ShiftPoint move)
        {
            var snapshot = Board.Clone();
            if (!TurnRunner.PlayTurn(Board, move)) return false;
            history.Push(snapshot);
            moves.Add(move);
            return true;
        }

        /// <summary>한 턴 되돌린다. 승리·패배로 끝난 뒤에도 되돌릴 수 있다.</summary>
        public bool Undo()
        {
            if (history.Count == 0) return false;
            Board = history.Pop();
            moves.RemoveAt(moves.Count - 1);
            return true;
        }

        /// <summary>레벨을 처음 상태로 되돌린다. undo 스택도 비운다.</summary>
        public void Restart()
        {
            history.Clear();
            moves.Clear();
            Board = initial.Clone();
        }
    }
}
