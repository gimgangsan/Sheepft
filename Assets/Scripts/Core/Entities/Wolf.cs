using System;
using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>
    /// 늑대 무리. 무리 하나가 엔티티 하나이고 마리 수를 갖는다. 한 마리도 크기 1인 무리다.
    /// README 6절 "늑대와 늑대 무리".
    /// </summary>
    public sealed class Wolf : Animal
    {
        /// <summary>마리 수. 잡는 범위는 Count − 1이다.</summary>
        public int Count = 1;

        /// <summary>기절이 풀릴 때까지 남은 턴 끝의 수. 0이면 기절이 아니다.</summary>
        public int StunLeft;

        /// <summary>식사가 끝날 때까지 남은 턴 끝의 수. 0이면 식사 중이 아니다.</summary>
        public int MealLeft;

        public bool IsStunned => StunLeft > 0;
        public bool IsEating => MealLeft > 0;

        /// <summary>지금 양을 잡는가. 화상, 기절, 식사 중에는 잡지 않는다. 탑승 중에는 잡는다.</summary>
        public bool CanCatch => !Burned && StunLeft == 0 && MealLeft == 0;

        public override bool IsIncapacitated(Board b)
        {
            return StunLeft > 0 || MealLeft > 0 || base.IsIncapacitated(b);
        }

        // ── 겹침 상호작용: 합류 ──

        /// <summary>
        /// 화상이 아닌 늑대끼리 같은 칸이 되면 하나의 무리가 된다.
        /// 그 칸에서 Id가 가장 작은 화상 아닌 늑대가 나머지를 한 번에 흡수한다. 구현 계획 2절 표.
        /// </summary>
        public override bool OnOverlap(Board b, Tile t)
        {
            if (Burned) return false;

            List<Wolf> others = null;
            foreach (var o in t.Objects)
            {
                var wolf = o as Wolf;
                if (wolf == null || wolf.Burned || wolf == this) continue;
                if (wolf.Id < Id) return false; // 흡수는 Id가 가장 작은 늑대가 맡는다.
                if (others == null) others = new List<Wolf>();
                others.Add(wolf);
            }
            if (others == null) return false;

            int count = Count;
            bool wet = Wet;
            int meal = MealLeft;
            foreach (var other in others)
            {
                count += other.Count;
                wet |= other.Wet;
                meal = Math.Max(meal, other.MealLeft);
                AppliedThisTurn.UnionWith(other.AppliedThisTurn);
                b.Remove(other, RemoveCause.Merged);
            }

            b.SetCount(this, count);
            b.SetWet(this, wet);
            b.SetMeal(this, meal);
            b.SetStun(this, b.NewStatusDuration); // 싸우느라 기절한다. 이미 기절 중이어도 처음부터 다시 센다.
            LastWalkDir = null;
            b.Mark(Mechanic.WolfMerge);
            return true;
        }

        // ── 자동 반응: 양 잡기 ──

        /// <summary>
        /// 잡는 범위(무리를 중심으로 한 정사각형, 장애물 무시, 순환 없음) 안의 양을
        /// 한 번에 최대 마리 수만큼 먹는다. 가까운 칸부터, 거리가 같으면 행 우선,
        /// 한 칸 안에서는 젖거나 화상 입은 양 먼저다.
        /// </summary>
        public override bool TryAutoReact(Board b, AutoReaction k)
        {
            if (k != AutoReaction.WolfCatch || Burned) return false;

            int range = Count - 1;
            List<Sheep> inRange = null; // 정리 절차마다 불리므로 범위에 양이 있을 때만 목록을 만든다.
            foreach (var o in b.Objects)
            {
                var sheep = o as Sheep;
                if (sheep == null || Ring(sheep.Pos) > range) continue;
                if (inRange == null) inRange = new List<Sheep>();
                inRange.Add(sheep);
            }
            if (inRange == null) return false;

            // 기절·식사 중에는 잡지 않는다. 검증용으로 "범위에 양이 있었지만 잡지 않음"을 적는다.
            if (StunLeft > 0) { b.Mark(Mechanic.StunSpared); return false; }
            if (MealLeft > 0) { b.Mark(Mechanic.MealSpared); return false; }

            inRange.Sort(EatOrder);
            int eat = Math.Min(Count, inRange.Count);
            for (int i = 0; i < eat; i++)
            {
                if (inRange[i].Pos != Pos) b.Mark(Mechanic.PackCatch);
                b.Remove(inRange[i], RemoveCause.Eaten);
            }
            b.Mark(Mechanic.WolfEat);
            b.SetMeal(this, b.NewStatusDuration);
            return true;
        }

        /// <summary>무리에서 그 칸까지의 거리. 같은 칸이 0, 주변 8칸이 1이다.</summary>
        private int Ring(Pos p)
        {
            return Math.Max(Math.Abs(p.Row - Pos.Row), Math.Abs(p.Col - Pos.Col));
        }

        private int EatOrder(Sheep a, Sheep b)
        {
            int c = Ring(a.Pos).CompareTo(Ring(b.Pos));
            if (c != 0) return c;
            c = a.Pos.Row.CompareTo(b.Pos.Row);
            if (c != 0) return c;
            c = a.Pos.Col.CompareTo(b.Pos.Col);
            if (c != 0) return c;
            bool aFirst = a.Wet || a.Burned, bFirst = b.Wet || b.Burned;
            if (aFirst != bFirst) return aFirst ? -1 : 1;
            return a.Id.CompareTo(b.Id);
        }

        // ── 종료 후 반응: 걷기 ──

        /// <summary>
        /// 갈 수 있는 양 중 경로 거리로 가장 가까운 양을 향해 1칸 걷는다.
        /// 같은 칸에 다른 오브젝트가 있어도 혼자만 간다. 갈 수 있는 양이 없으면 대기한다.
        /// </summary>
        public override void AfterShift(Board b)
        {
            var targets = new List<Pos>();
            foreach (var o in b.Objects)
            {
                if (!(o is Sheep)) continue;
                if (o.Pos == Pos) return; // 같은 칸의 양은 걸어갈 대상이 아니다.
                targets.Add(o.Pos);
            }

            if (targets.Count == 0) return;

            var dir = Pathfinder.FirstStep(b, Pos, targets, LastWalkDir);
            if (dir == null)
            {
                b.Mark(Mechanic.WolfNoPath);
                return;
            }
            b.Move(this, Pos.Step(dir.Value), MoveCause.Walk);
            LastWalkDir = dir;
        }

        public override bool IsHeldByRide(Board b)
        {
            return StunLeft == 0 && MealLeft == 0 && base.IsHeldByRide(b);
        }
    }
}
