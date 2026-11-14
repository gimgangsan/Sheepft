using System;
using System.Linq;
using Sheepft.Core;
using Xunit;

namespace Sheepft.Tests
{
    internal static class Kit
    {
        public static Game G(string text)
        {
            return Game.FromText(text);
        }

        /// <summary>공백 차이를 무시하고 맵 전체를 비교한다.</summary>
        public static void Board(Game g, string expected)
        {
            Assert.Equal(Norm(expected), Norm(g.Board()));
        }

        private static string Norm(string s)
        {
            var lines = s.Replace("\r", "").Split('\n')
                .Select(l => string.Join(" ", l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)))
                .Where(l => l.Length > 0);
            return string.Join("\n", lines);
        }
    }
}
