using System;

namespace Sheepft.Core
{
    /// <summary>소. 냄새 맡은 과일 바구니를 향해 걸으며 앞 칸의 오브젝트를 밀어낸다. README 6절.</summary>
    public sealed class Cow : BasketSeeker
    {
        private const int SmellRange = 3;

        // ── 겹침 상호작용: 먹기, 목표 상실 ──

        /// <summary>
        /// 소와 과일 바구니가 같은 칸이 되면 그 순간 먹는다. 여럿이면 목표 바구니 먼저.
        /// 목표 바구니가 사라졌으면(먹힘, 탐) 목표를 잃는다.
        /// </summary>
        public override bool OnOverlap(Board b, Tile t)
        {
            if (Full) return false;

            FruitBasket basket = null;
            foreach (var o in t.Objects)
            {
                var candidate = o as FruitBasket;
                if (candidate == null) continue;
                if (basket == null || candidate.Id == TargetId) basket = candidate;
            }
            if (basket != null)
            {
                b.Remove(basket, RemoveCause.Eaten);
                b.SetTarget(this, -1);
                b.SetFull(this, true);
                return true;
            }

            if (HasTarget && Target(b) == null)
            {
                b.SetTarget(this, -1);
                return true;
            }
            return false;
        }

        // ── 자동 반응: 냄새 ──

        /// <summary>
        /// 목표가 없을 때 맨해튼 거리 3칸 안의 바구니를 목표로 삼는다. 장애물을 무시하고 순환하지 않는다.
        /// 가장 가까운 것, 동점이면 (0,0)부터 행 우선이다.
        /// </summary>
        public override bool TryAutoReact(Board b, AutoReaction k)
        {
            if (k != AutoReaction.CowSmell || Full || HasTarget) return false;

            FruitBasket best = null;
            int bestDistance = int.MaxValue;
            foreach (var o in b.Objects) // 행 우선, 같은 칸에서는 Id 순서
            {
                var basket = o as FruitBasket;
                if (basket == null) continue;
                int d = Math.Abs(basket.Pos.Row - Pos.Row) + Math.Abs(basket.Pos.Col - Pos.Col);
                if (d > SmellRange || d >= bestDistance) continue;
                best = basket;
                bestDistance = d;
            }
            if (best == null) return false;
            b.SetTarget(this, best.Id);
            return true;
        }

        // ── 종료 후 반응: 전진(밀기) ──

        /// <summary>
        /// 목표를 향해 1칸 걷는다. 들어갈 칸의 오브젝트를 모두 진행 방향으로 1칸 밀어낸 뒤 들어간다.
        /// 밀려날 칸이 막혀 있으면 그 오브젝트는 제자리에 남고 소가 들어가 겹친다.
        /// </summary>
        public override void AfterShift(Board b)
        {
            var dir = StepTowardTarget(b);
            if (dir == null) return;

            var next = Pos.Step(dir.Value);
            TilePusher.Push(b, next, dir.Value, MoveCause.CowPush);
            b.Move(this, next, MoveCause.Walk);
            LastWalkDir = dir;
        }
    }
}
