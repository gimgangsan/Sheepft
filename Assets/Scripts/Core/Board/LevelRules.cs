using System;
using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>레벨 헤더와 `@` 줄에 적힌 값. 레벨 중에 바뀌지 않는다.</summary>
    public sealed class LevelRules
    {
        /// <summary>헤더의 스테이지 이름.</summary>
        public string Name = "";

        /// <summary>플레이어가 누를 수 있는 shift 목록. 헤더에 적힌 순서다.</summary>
        public IReadOnlyList<ShiftPoint> ShiftPoints = Array.Empty<ShiftPoint>();

        public int TurnLimit;

        /// <summary>헤더에 없으면 레벨의 양 수.</summary>
        public int TargetSheep;

        /// <summary>`# 풀이:` 줄의 수순. 줄이 없으면 비어 있다. 테스트용이다.</summary>
        public IReadOnlyList<ShiftPoint> Solution = Array.Empty<ShiftPoint>();

        /// <summary>`@desc`. 플레이어용 설명. 없으면 빈 문자열.</summary>
        public string Description = "";

        /// <summary>`@stars`. 별 3개 기준 턴. 줄이 없으면 0.</summary>
        public int Star3Turns;

        /// <summary>`@stars`. 별 2개 기준 턴. 줄이 없으면 0. 별 1개는 제한 턴 이내다.</summary>
        public int Star2Turns;

        /// <summary>`@intent`. 제한 턴 안의 모든 해에 나와야 하는 기믹. 스테이지 검증용이다.</summary>
        public Mechanic Intent;

        /// <summary>`@optimal`. 최적 길이의 모든 해에 나와야 하는 기믹. 더 긴 해에는 없어도 된다. 스테이지 검증용이다.</summary>
        public Mechanic OptimalIntent;
    }
}
