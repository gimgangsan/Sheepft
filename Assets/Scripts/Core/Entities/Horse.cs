using System;

namespace Sheepft.Core
{
    /// <summary>말. 보이는 과일 바구니를 향해 걸으며 같은 칸의 오브젝트를 싣고 간다. README 6절.</summary>
    public sealed class Horse : BasketSeeker
    {
        private static readonly Dir[] Dirs = { Dir.Up, Dir.Right, Dir.Down, Dir.Left };

        /// <summary>발견 상태. 이때만 등에 태운 동물을 붙잡아 둔다(탑승).</summary>
        public bool IsCarrying => HasTarget && !Full;

        /// <summary>
        /// 등에 태운 오브젝트의 Id. 없으면 -1. 말은 한 번에 하나만 태운다.
        /// 먼저 탄 것이 말의 칸을 떠나거나 사라질 때까지 바뀌지 않는다. README 6절.
        /// </summary>
        public int RiderId = -1;

        /// <summary>등에 태운 오브젝트. 없으면 null.</summary>
        public ObjectEntity Rider(Board b)
        {
            if (RiderId < 0) return null;
            var rider = b.Find(RiderId);
            return rider != null && rider.Pos == Pos ? rider : null;
        }

        // ── 겹침 상호작용: 태우기, 목표 상실 ──

        /// <summary>
        /// 등에 태울 것을 정하고, 목표 바구니가 사라졌거나(먹힘, 탐) 말의 칸에 들어오면 발견이 풀린다.
        /// </summary>
        public override bool OnOverlap(Board b, Tile t)
        {
            bool changed = UpdateRiders(b, t);
            if (Full || !HasTarget) return changed;
            var target = Target(b);
            if (target != null && target.Pos != Pos) return changed;
            b.SetTarget(this, -1);
            return true;
        }

        /// <summary>
        /// 그 칸의 말들이 등에 태울 것을 정한다. 배부른 말은 아무것도 태우지 않는다.
        /// 등이 비었으면 아직 아무 말에도 타지 않은 오브젝트(말 제외) 가운데 Id가 가장 작은 것을 태운다.
        /// 말이 여럿이면 Id가 작은 말부터 고른다. 칸의 목록은 Id 순서다.
        /// </summary>
        private static bool UpdateRiders(Board b, Tile t)
        {
            bool changed = false;
            foreach (var o in t.Objects)
            {
                var horse = o as Horse;
                if (horse == null || horse.RiderId < 0) continue;
                if (horse.Full || horse.Rider(b) == null) changed |= b.SetRider(horse, -1);
            }
            foreach (var o in t.Objects)
            {
                var horse = o as Horse;
                if (horse == null || horse.Full || horse.RiderId >= 0) continue;
                foreach (var candidate in t.Objects)
                {
                    if (candidate is Horse || candidate.Removed || IsRiderOf(t, candidate.Id)) continue;
                    changed |= b.SetRider(horse, candidate.Id);
                    break;
                }
            }
            return changed;
        }

        private static bool IsRiderOf(Tile t, int id)
        {
            foreach (var o in t.Objects)
            {
                var horse = o as Horse;
                if (horse != null && horse.RiderId == id) return true;
            }
            return false;
        }

        // ── 자동 반응: 먹기, 발견 ──

        public override bool TryAutoReact(Board b, AutoReaction k)
        {
            if (Full) return false;
            if (k == AutoReaction.HorseEat) return TryEat(b);
            if (k == AutoReaction.HorseSpot) return TrySpot(b);
            return false;
        }

        /// <summary>
        /// 상하좌우로 붙은 칸의 바구니 하나를 먹는다. 목표 바구니 먼저, 그다음 상 > 우 > 하 > 좌.
        /// 등에 태운 것은 바구니가 있던 칸에 내려놓는다. 같은 칸의 다른 오브젝트는 그대로 둔다.
        /// </summary>
        private bool TryEat(Board b)
        {
            FruitBasket basket = null;
            var target = Target(b);
            if (target != null && IsAdjacent(target.Pos, Pos)) basket = target;
            for (int i = 0; i < Dirs.Length && basket == null; i++)
            {
                var p = Pos.Step(Dirs[i]);
                if (b.BlocksAnimal(p)) continue;
                basket = FirstBasket(b.At(p));
            }
            if (basket == null) return false;

            var dest = basket.Pos;
            b.Remove(basket, RemoveCause.Eaten);
            var rider = Rider(b);
            if (rider != null)
            {
                var sheepBefore = b.SheepPositions();
                b.Move(rider, dest, MoveCause.HorseEat);
                b.Mark(Mechanic.HorseDrop);
                if (rider is Sheep) b.Mark(Mechanic.HorseDropSheep);
                var carried = rider as FruitBasket;
                if (carried != null) carried.Carried = true;
                b.MarkSheepMerge(sheepBefore);
            }
            b.SetRider(this, -1);
            b.SetTarget(this, -1);
            b.SetFull(this, true);
            b.Mark(Mechanic.HorseEat);
            return true;
        }

        /// <summary>
        /// 목표가 없을 때 같은 행이나 열에 보이는 바구니를 목표로 삼는다.
        /// 직선 거리가 가장 가까운 것, 동점이면 상 > 우 > 하 > 좌. 같은 칸의 바구니는 보지 않는다.
        /// </summary>
        private bool TrySpot(Board b)
        {
            if (HasTarget) return false;

            FruitBasket best = null;
            int bestSteps = int.MaxValue;
            foreach (var dir in Dirs)
            {
                int steps = 0;
                foreach (var p in Sight.Ray(b, Pos, dir))
                {
                    steps++;
                    var basket = FirstBasket(b.At(p));
                    if (basket == null) continue;
                    if (steps < bestSteps) { best = basket; bestSteps = steps; }
                    break;
                }
            }
            if (best == null) return false;
            b.SetTarget(this, best.Id);
            b.Mark(Mechanic.HorseDiscover);
            if (best.Carried) b.Mark(Mechanic.HorseLure);
            return true;
        }

        // ── 종료 후 반응: 이동과 운반 ──

        /// <summary>목표 바구니를 향해 1칸 걷는다. 등에 태운 것만 데리고 간다(운반).</summary>
        public override void AfterShift(Board b)
        {
            bool noPath;
            var dir = StepTowardTarget(b, out noPath);
            if (noPath) b.Mark(Mechanic.HorseNoPath);
            if (dir == null) return;

            var next = Pos.Step(dir.Value);
            var sheepBefore = b.SheepPositions();
            var rider = Rider(b);
            if (rider != null)
            {
                b.Move(rider, next, MoveCause.Carry);
                b.Mark(Mechanic.HorseCarry);
                if (rider is Sheep) b.Mark(Mechanic.HorseCarrySheep);
                else if (rider is Wolf) b.Mark(Mechanic.HorseCarryWolf);
                var carried = rider as FruitBasket;
                if (carried != null) carried.Carried = true;
            }
            b.Move(this, next, MoveCause.Walk);
            LastWalkDir = dir;
            b.MarkSheepMerge(sheepBefore);
        }

        private static FruitBasket FirstBasket(Tile t)
        {
            foreach (var o in t.Objects)
            {
                var basket = o as FruitBasket;
                if (basket != null) return basket;
            }
            return null;
        }

        private static bool IsAdjacent(Pos a, Pos b)
        {
            return Math.Abs(a.Row - b.Row) + Math.Abs(a.Col - b.Col) == 1;
        }
    }
}
