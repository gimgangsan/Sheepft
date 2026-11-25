using System.Collections.Generic;
using NUnit.Framework;
using Sheepft.Core;
using Sheepft.Game;

namespace Sheepft.Tests
{
    /// <summary>힌트·건너뛰기가 열리는 조건과 힌트가 가리키는 수. 구현 계획 13절.</summary>
    public class HintProgressTests
    {
        private static readonly ShiftPoint A = new ShiftPoint(Axis.Row, 0, Dir.Right);
        private static readonly ShiftPoint B = new ShiftPoint(Axis.Col, 1, Dir.Down);
        private static readonly ShiftPoint C = new ShiftPoint(Axis.Row, 2, Dir.Left);

        // 실패 3회, 180초, 재시작은 2턴 이상 썼을 때만 실패.
        private static HintProgress Make() => new HintProgress(3, 180f, 2);

        [Test]
        public void Unlocks_OnlyWithEnoughFailuresAndTime()
        {
            var hint = Make();
            Assert.IsFalse(hint.OnLost());
            Assert.IsFalse(hint.OnRestart(5)); // 진 판을 재시작: 같은 시도라 세지 않는다
            Assert.IsFalse(hint.OnRestart(5));
            Assert.IsFalse(hint.OnRestart(5));
            Assert.AreEqual(3, hint.FailedAttempts);
            Assert.IsFalse(hint.Unlocked, "시간이 모자라다");

            Assert.IsFalse(hint.AddTime(100f));
            Assert.IsTrue(hint.AddTime(80f), "이번에 열렸다");
            Assert.IsTrue(hint.Unlocked);
            Assert.IsFalse(hint.AddTime(10f), "이미 열렸으면 다시 알리지 않는다");
        }

        [Test]
        public void TimeAlone_DoesNotUnlock()
        {
            var hint = Make();
            hint.AddTime(10000f);
            Assert.IsFalse(hint.Unlocked);
        }

        [Test]
        public void ShortRestart_IsNotAFailure()
        {
            var hint = Make();
            hint.OnRestart(1);
            hint.OnRestart(0);
            Assert.AreEqual(0, hint.FailedAttempts);
        }

        [Test]
        public void LosingAgainAfterUndo_IsTheSameAttempt()
        {
            var hint = Make();
            hint.OnLost();
            hint.OnLost();
            hint.OnLost();
            Assert.AreEqual(1, hint.FailedAttempts);
            // 진 뒤 재시작해도 그 시도는 이미 셌다.
            hint.OnRestart(7);
            Assert.AreEqual(1, hint.FailedAttempts);
            // 새 시도에서 지면 다시 센다.
            hint.OnLost();
            Assert.AreEqual(2, hint.FailedAttempts);
        }

        [Test]
        public void UndoToStart_StartsANewAttemptWithoutCounting()
        {
            var hint = Make();
            hint.OnLost();
            hint.OnBackToStart();
            Assert.AreEqual(1, hint.FailedAttempts);
            hint.OnLost();
            Assert.AreEqual(2, hint.FailedAttempts);
        }

        [Test]
        public void Request_OnlyWhenUnlocked_AndMarksUsed()
        {
            var hint = new HintProgress(0, 0f, 2);
            var locked = Make();
            Assert.IsFalse(locked.Request());
            Assert.IsFalse(locked.Used);

            hint.AddTime(0f);
            Assert.IsTrue(hint.Request());
            Assert.IsTrue(hint.Active);
            Assert.IsTrue(hint.Used);
            hint.Done();
            Assert.IsFalse(hint.Active);
            Assert.IsTrue(hint.Used, "한 번 썼으면 계속 썼다");
        }

        [Test]
        public void StarCap_EndsWithTheClear_OnNextAttempt()
        {
            var hint = new HintProgress(0, 0f, 2);
            hint.AddTime(0f);
            hint.Request();
            hint.OnRestart(5);
            Assert.IsTrue(hint.Used, "깨기 전에는 재시작해도 제한이 남는다");

            hint.OnWon();
            Assert.IsTrue(hint.Used, "힌트를 쓴 이번 클리어는 별을 줄인다");
            hint.OnBackToStart();
            Assert.IsFalse(hint.Used, "깬 뒤 새 시도부터는 별을 제대로 준다");
        }

        [Test]
        public void StarCap_ClearsOnEnterOrRestart_AfterHintClear()
        {
            var hint = new HintProgress(0, 0f, 2);
            hint.AddTime(0f);
            hint.Request();
            hint.OnWon();
            hint.OnEnterStage();
            Assert.IsFalse(hint.Used);

            hint.Request();
            hint.OnWon();
            hint.OnRestart(0);
            Assert.IsFalse(hint.Used);
        }

        [Test]
        public void StarCap_StaysIfHintUsedAgainAfterClear()
        {
            var hint = new HintProgress(0, 0f, 2);
            hint.AddTime(0f);
            hint.Request();
            hint.OnWon();
            // 깬 판을 undo해서 다시 힌트를 보면, 다음 클리어까지 제한이 이어진다.
            hint.Request();
            hint.OnBackToStart();
            Assert.IsTrue(hint.Used);
        }

        [Test]
        public void Next_FollowsSolution()
        {
            var solution = new List<ShiftPoint> { A, B, C };
            Assert.AreEqual(HintKind.Move, HintProgress.Next(new List<ShiftPoint>(), solution, out var move));
            Assert.AreEqual(A, move);
            Assert.AreEqual(HintKind.Move, HintProgress.Next(new List<ShiftPoint> { A, B }, solution, out move));
            Assert.AreEqual(C, move);
            Assert.AreEqual(HintKind.None, HintProgress.Next(new List<ShiftPoint> { A, B, C }, solution, out move));
        }

        [Test]
        public void Next_OffThePath_AsksForUndo()
        {
            var solution = new List<ShiftPoint> { A, B, C };
            Assert.AreEqual(HintKind.Undo, HintProgress.Next(new List<ShiftPoint> { B }, solution, out _));
            Assert.AreEqual(HintKind.Undo, HintProgress.Next(new List<ShiftPoint> { A, C }, solution, out _));
            Assert.AreEqual(HintKind.Undo, HintProgress.Next(new List<ShiftPoint> { A, B, C, A }, solution, out _));
        }
    }
}
