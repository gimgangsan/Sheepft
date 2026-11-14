using System;
using System.Collections.Generic;
using System.Text;

namespace Sheepft.Core
{
    /// <summary>
    /// 한 턴 동안 일어난 일. 해법이 의도한 기믹을 썼는지 판정하는 데 쓴다.
    /// 레벨 파일의 @intent 줄에 이 이름을 그대로 적는다.
    /// </summary>
    [Flags]
    public enum Ev : ulong
    {
        None = 0,

        // shift
        ShiftMove = 1UL << 0,       // shift로 오브젝트가 움직임
        Wait = 1UL << 1,            // 아무것도 움직이지 않은 shift
        Wrap = 1UL << 2,            // 줄 끝에서 순환
        SheepWrap = 1UL << 3,
        WolfWrap = 1UL << 4,
        VoidSkip = 1UL << 5,        // 빈칸을 건너뜀
        Blocked = 1UL << 6,         // 장애물에 막혀 제자리
        SheepMerge = 1UL << 7,      // 서로 다른 칸의 양이 한 칸에 모임

        // 물과 들불
        SheepWet = 1UL << 8,
        SheepDried = 1UL << 9,      // 젖은 양이 들불에서 마름
        SheepBurned = 1UL << 10,
        SheepCured = 1UL << 11,     // 화상 입은 양이 물에서 회복
        WolfBurned = 1UL << 12,
        WolfCured = 1UL << 13,
        AnimalBurned = 1UL << 14,   // 양과 늑대가 아닌 동물의 화상
        ItemBurned = 1UL << 15,     // 바구니나 빈 양동이가 탐
        FireSpread = 1UL << 16,     // 확산으로 들불 칸이 늘어남
        GrassBurned = 1UL << 17,
        ObstacleBurned = 1UL << 18, // 울타리나 나무가 탐
        EnterBurnt = 1UL << 19,     // 처음에 울타리나 나무였던 칸에 오브젝트가 들어감
        SheepEnterBurnt = 1UL << 20,

        // 양동이
        BucketFilled = 1UL << 21,
        Extinguish = 1UL << 22,

        // 늑대
        WolfWalk = 1UL << 23,
        WolfNoPath = 1UL << 24,     // 양은 있는데 걸어갈 길이 없음 (감옥, 물, 들불)
        WolfMerge = 1UL << 25,
        WolfEat = 1UL << 26,        // 양이 먹힘
        PackCatch = 1UL << 27,      // 같은 칸이 아닌 범위 안의 양을 잡음
        StunSpared = 1UL << 28,     // 기절한 무리의 범위 안에 양이 있었지만 잡히지 않음
        MealSpared = 1UL << 29,     // 식사 중인 무리의 범위 안에 양이 있었지만 잡히지 않음

        // 염소
        GoatPush = 1UL << 30,
        GoatPushSheep = 1UL << 31,
        GoatPushWolf = 1UL << 32,
        GoatPushItem = 1UL << 33,
        GoatSightBlocked = 1UL << 34, // 장애물이나 빈칸 뒤 2칸 거리에 오브젝트가 있었지만 밀지 못함

        // 말
        HorseDiscover = 1UL << 35,
        HorseWalk = 1UL << 36,
        HorseCarry = 1UL << 37,
        HorseCarrySheep = 1UL << 38,
        HorseCarryWolf = 1UL << 39,
        HorseEat = 1UL << 40,
        HorseDrop = 1UL << 41,      // 먹으면서 짐을 바구니 칸으로 옮김
        HorseDropSheep = 1UL << 42,
        HorseNoPath = 1UL << 43,    // 발견 상태인데 갈 길이 없음 (말 감옥)
        HorseLure = 1UL << 44,      // 다른 말이 옮긴 바구니를 발견
        RideHeld = 1UL << 45,       // 탑승 때문에 종료 후 반응을 하지 못함

        // 소
        CowSmell = 1UL << 46,
        CowWalk = 1UL << 47,
        CowPush = 1UL << 48,
        CowPushSheep = 1UL << 49,
        CowPushWolf = 1UL << 50,
        CowPushBasket = 1UL << 51,
        CowEat = 1UL << 52,
        CowNoPath = 1UL << 53,
        CowLure = 1UL << 54,        // 말이 옮긴 바구니의 냄새를 맡음
    }

    public static class EvText
    {
        public static string Describe(Ev events)
        {
            if (events == Ev.None) return "(없음)";
            var sb = new StringBuilder();
            for (int i = 0; i < 64; i++)
            {
                ulong bit = 1UL << i;
                if (((ulong)events & bit) == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(((Ev)bit).ToString());
            }
            return sb.ToString();
        }

        public static bool TryParse(string text, out Ev result)
        {
            result = Ev.None;
            var parts = text.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                Ev one;
                if (!Enum.TryParse(p, true, out one) || !IsSingleFlag(one)) return false;
                result |= one;
            }
            return true;
        }

        private static bool IsSingleFlag(Ev e)
        {
            ulong v = (ulong)e;
            return v != 0 && (v & (v - 1)) == 0;
        }

        public static IEnumerable<Ev> Split(Ev events)
        {
            for (int i = 0; i < 64; i++)
            {
                ulong bit = 1UL << i;
                if (((ulong)events & bit) != 0) yield return (Ev)bit;
            }
        }
    }
}
