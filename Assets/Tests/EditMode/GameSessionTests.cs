using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>undo와 재시작.</summary>
    public class GameSessionTests
    {
        private static readonly ShiftPoint R0Right = new ShiftPoint(Axis.Row, 0, Dir.Right);
        private static readonly ShiftPoint R0Left = new ShiftPoint(Axis.Row, 0, Dir.Left);

        private static GameSession Make(string grid, string header = "shift: R0:R  제한 3턴")
        {
            return new GameSession(LevelParser.Parse("# t  " + header + "\n" + grid));
        }

        [Test]
        public void Undo_RestoresMapTurnsAndStatus()
        {
            var session = Make(".S ~ . g");
            Assert.IsFalse(session.CanUndo);
            Assert.IsTrue(session.Play(R0Right));
            Assert.IsTrue(session.Play(R0Right));
            TestLevel.AssertMap(". ~ .S(w) g", session.Board);

            Assert.IsTrue(session.Undo());
            TestLevel.AssertMap(". ~S(w) . g", session.Board);
            Assert.AreEqual(1, session.Board.TurnsUsed);

            Assert.IsTrue(session.Undo());
            TestLevel.AssertMap(".S ~ . g", session.Board);
            Assert.AreEqual(0, session.Board.TurnsUsed);
            Assert.IsFalse(session.CanUndo);
            Assert.IsFalse(session.Undo());
        }

        [Test]
        public void Moves_FollowPlayUndoAndRestart()
        {
            var session = Make(".S . . . g", "shift: R0:RL  제한 5턴");
            session.Play(R0Right);
            session.Play(R0Left);
            CollectionAssert.AreEqual(new[] { R0Right, R0Left }, session.Moves);
            session.Undo();
            CollectionAssert.AreEqual(new[] { R0Right }, session.Moves);
            session.Restart();
            Assert.AreEqual(0, session.Moves.Count);
        }

        [Test]
        public void Undo_WorksAfterTheGameEnded()
        {
            var session = Make(".S . . . g", "shift: R0:R  제한 1턴");
            session.Play(R0Right);
            Assert.AreEqual(GameResult.Lost, session.Board.Result);
            Assert.IsFalse(session.Play(R0Right));

            Assert.IsTrue(session.Undo());
            Assert.AreEqual(GameResult.InProgress, session.Board.Result);
            Assert.IsTrue(session.Play(R0Right));
        }

        [Test]
        public void RejectedMove_IsNotPushedToHistory()
        {
            var session = Make(".S . g");
            Assert.IsFalse(session.Play(R0Left));
            Assert.IsFalse(session.CanUndo);
            Assert.AreEqual(0, session.Board.TurnsUsed);
        }

        [Test]
        public void Restart_ReturnsToInitialStateAndClearsHistory()
        {
            var session = Make(".S ^ . g");
            session.Play(R0Right);
            session.Play(R0Right);
            session.Restart();
            TestLevel.AssertMap(".S ^ . g", session.Board);
            Assert.AreEqual(0, session.Board.TurnsUsed);
            Assert.IsFalse(session.CanUndo);

            // 다시 시작한 뒤에도 처음부터 똑같이 둘 수 있고, 또 재시작할 수 있다.
            session.Play(R0Right);
            TestLevel.AssertMap(". ^S(b) . g", session.Board);
            session.Restart();
            TestLevel.AssertMap(".S ^ . g", session.Board);
        }
    }
}
