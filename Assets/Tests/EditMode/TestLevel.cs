using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sheepft.Core;

namespace Sheepft.Tests
{
    /// <summary>
    /// 규칙 테스트용 도우미. 테스트를 "맵 문자열 + 수 → 기대하는 맵 문자열"로 적게 해 준다.
    /// 구현 계획 7절.
    /// </summary>
    internal static class TestLevel
    {
        /// <summary>
        /// 헤더 없는 맵을 읽는다. 실제 칸이 있는 모든 줄을 양방향으로 열고,
        /// 제한은 99턴, 목표는 레벨의 모든 양이다. target을 주면 그 수가 목표다(0이면 승리·패배 판정 없음).
        /// </summary>
        public static Board Make(string grid, int target = -1)
        {
            var rows = new List<string[]>();
            foreach (var raw in grid.Replace("\r", "").Split('\n'))
            {
                var cells = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (cells.Length > 0) rows.Add(cells);
            }

            var points = new List<string>();
            for (int r = 0; r < rows.Count; r++)
                if (Array.Exists(rows[r], IsReal)) points.Add("R" + r + ":RL");
            for (int c = 0; c < rows[0].Length; c++)
            {
                bool real = false;
                foreach (var row in rows)
                    if (c < row.Length && IsReal(row[c])) real = true;
                if (real) points.Add("C" + c + ":UD");
            }

            return LevelParser.Parse("# test  shift: " + string.Join(", ", points) + "  제한 99턴"
                + (target < 0 ? "" : "  목표 양 " + target) + "\n" + grid);
        }

        private static bool IsReal(string cell)
        {
            return cell != "-" && cell != "x";
        }

        /// <summary>`R0:R, C1:U`처럼 적은 수를 차례로 둔다. 거부되는 수가 있으면 실패다.</summary>
        public static void Play(Board board, string moves)
        {
            foreach (var raw in moves.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ShiftPoint point;
                Assert.IsTrue(ShiftPoint.TryParse(raw, out point), "수 '" + raw.Trim() + "'를 읽지 못했다.");
                Assert.IsTrue(TurnRunner.PlayTurn(board, point), "수 " + point + "가 거부되었다.");
            }
        }

        /// <summary>맵에 수를 둔 뒤의 모습이 기대와 같은지 본다. 칸 사이 공백 수는 따지지 않는다.</summary>
        public static Board AssertAfter(string level, string moves, string expected, int target = -1)
        {
            var board = Make(level, target);
            Play(board, moves);
            AssertMap(expected, board);
            return board;
        }

        public static void AssertMap(string expected, Board board)
        {
            Assert.AreEqual(Normalize(expected), Normalize(LevelWriter.Write(board)));
        }

        private static string Normalize(string map)
        {
            var lines = new List<string>();
            foreach (var raw in map.Replace("\r", "").Split('\n'))
            {
                string line = Regex.Replace(raw.Trim(), "[ \t]+", " ");
                if (line.Length > 0) lines.Add(line);
            }
            return string.Join("\n", lines);
        }
    }
}
