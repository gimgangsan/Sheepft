using System;
using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>
    /// 한 판을 진행하는 얇은 껍데기. 턴 수와 제한 턴, 누적 이벤트를 관리한다.
    /// Unity 쪽에서는 이 클래스나 Simulator.Step을 그대로 불러 쓰면 된다.
    /// </summary>
    public sealed class Game
    {
        public readonly Level Level;
        public GameState State;
        public int Turn;
        public Outcome Outcome = Outcome.Ongoing;
        /// <summary>지금까지 일어난 모든 이벤트.</summary>
        public Ev Events;
        /// <summary>마지막 턴에 일어난 이벤트.</summary>
        public Ev LastEvents;
        /// <summary>마지막 턴의 진행 기록. 생성자에서 log를 켰을 때만 채운다.</summary>
        public List<string> Log;

        private readonly bool logging;

        public Game(Level level, bool log = false)
        {
            Level = level;
            State = level.NewState();
            logging = log;
        }

        public static Game FromText(string text, bool log = false)
        {
            return new Game(LevelParser.ParseOne(text), log);
        }

        public Outcome Do(Move move)
        {
            if (Outcome != Outcome.Ongoing) throw new InvalidOperationException("이미 끝난 판이다.");
            Log = logging ? new List<string>() : null;
            var r = Simulator.Step(Level, State, move, Log);
            Turn++;
            LastEvents = r.Events;
            Events |= r.Events;
            Outcome = r.Outcome;
            // 제한 턴을 다 쓰고도 승리하지 못하면 패배.
            if (Outcome == Outcome.Ongoing && Turn >= Level.TurnLimit) Outcome = Outcome.Lose;
            return Outcome;
        }

        public Outcome Do(IEnumerable<Move> moves)
        {
            foreach (var m in moves) Do(m);
            return Outcome;
        }

        /// <summary>공백으로 구분한 수순. 예: "R1→ C2↑" 또는 "R1> C2^"</summary>
        public Outcome Do(string moves)
        {
            foreach (var token in moves.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Move m;
                if (!Level.TryParseMove(token, out m))
                    throw new ArgumentException("'" + token + "'는 이 레벨에서 둘 수 없는 수다.");
                Do(m);
            }
            return Outcome;
        }

        public bool Has(Ev events)
        {
            return (Events & events) == events;
        }

        public string Board()
        {
            return Core.Board.Render(Level, State);
        }

        public string Cell(int row, int col)
        {
            return Core.Board.CellText(Level, State, Level.Pos(row, col));
        }
    }
}
