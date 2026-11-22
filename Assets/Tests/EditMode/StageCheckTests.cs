using System.Collections.Generic;
using NUnit.Framework;
using Sheepft.Core;
using Sheepft.Tools;

namespace Sheepft.Tests
{
    /// <summary>
    /// stages.txt의 스테이지마다 검증 창(Sheepft > Stage Validator)과 같은 검증을 한다.
    /// 30개 전체에 2분쯤 걸리므로(30번 혼자 2분 가까이) 평소 테스트에서는 돌지 않는다.
    /// 카테고리 StageCheck를 골라서 돌린다. 실패 메시지는 검증 창의 "← …" 문구와 같다.
    /// </summary>
    [Explicit("느림. 카테고리 StageCheck로 골라서 돌린다.")]
    [Category("StageCheck")]
    public class StageCheckTests
    {
        private static IEnumerable<TestCaseData> Stages()
        {
            var boards = LevelParser.ParseAll(StagesFileTests.ReadStagesText());
            for (int i = 0; i < boards.Count; i++)
                yield return new TestCaseData(i).SetName("Stage" + (i + 1).ToString("00") + "_" + boards[i].Rules.Name.Replace(' ', '_'));
        }

        [Test, TestCaseSource(nameof(Stages)), Timeout(600000)]
        public void StagePassesValidation(int index)
        {
            var level = LevelParser.ParseAll(StagesFileTests.ReadStagesText())[index];
            var check = StageChecker.Check(level, index + 1);
            Assert.IsTrue(check.Ok, StageChecker.Row(check));
        }
    }
}
