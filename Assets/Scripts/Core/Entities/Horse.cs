using System;

namespace Sheepft.Core
{
    /// <summary>말. 보이는 과일 바구니를 향해 걸으며 같은 칸의 오브젝트를 싣고 간다. README 6절.</summary>
    public sealed class Horse : BasketSeeker
    {
        private static readonly Dir[] Dirs = { Dir.Up, Dir.Right, Dir.Down, Dir.Left };

        /// <summary>발견 상태. 이때만 같은 칸의 다른 동물을 탑승시킨다.</summary>
        public bool IsCarrying => HasTarget && !Full;

        // ── 겹침 상호작용: 목표 상실 ──

        /// <summary>목표 바구니가 사라졌거나(먹힘, 탐) 말의 칸에 들어오면 발견이 풀린다.</summary>
        public override bool OnOverlap(Board b, Tile t)
        {
            if (Full || !HasTarget) return false;
            var target = Target(b);
            if (target != null && target.Pos != Pos) return false;
            b.SetTarget(this, -1);
            return true;
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
        /// 말과 같은 칸의 다른 오브젝트(다른 말 제외)는 바구니가 있던 칸으로 옮긴다.
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
            foreach (var o in b.At(Pos).Objects.ToArray())
            {
                if (o == this || o is Horse) continue;
                b.Move(o, dest, MoveCause.HorseEat);
            }
            b.SetTarget(this, -1);
            b.SetFull(this, true);
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
            return true;
        }

        // ── 종료 후 반응: 이동과 운반 ──

        /// <summary>목표 바구니를 향해 1칸 걷는다. 같은 칸의 다른 오브젝트(다른 말 제외)를 모두 데리고 간다.</summary>
        public override void AfterShift(Board b)
        {
            var dir = StepTowardTarget(b);
            if (dir == null) return;

            var next = Pos.Step(dir.Value);
            foreach (var o in b.At(Pos).Objects.ToArray())
            {
                if (o == this || o is Horse) continue;
                b.Move(o, next, MoveCause.Carry);
            }
            b.Move(this, next, MoveCause.Walk);
            LastWalkDir = dir;
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
