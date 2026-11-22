namespace Sheepft.Core
{
    /// <summary>
    /// 과일 바구니를 목표로 삼아 걸어가는 동물(말, 소)의 공통 값.
    /// 목표는 undo 복사에서 참조가 끊기지 않게 Id로 갖는다.
    /// </summary>
    public abstract class BasketSeeker : Animal
    {
        /// <summary>목표 바구니의 Id. 없으면 -1.</summary>
        public int TargetId = -1;

        /// <summary>배부름. 바구니를 먹은 뒤에는 모든 반응과 상호작용을 하지 않는다. 풀리지 않는다.</summary>
        public bool Full;

        public bool HasTarget => TargetId >= 0;

        public override bool IgnoresInteractions => Full;

        /// <summary>목표 바구니. 목표가 없거나 이미 사라졌으면 null.</summary>
        public FruitBasket Target(Board b)
        {
            return TargetId < 0 ? null : b.Find(TargetId) as FruitBasket;
        }

        /// <summary>
        /// 목표를 향한 첫 걸음. 목표가 없거나, 같은 칸이거나, 경로가 없으면 null.
        /// noPath는 "가려는 목표가 있는데 경로가 없다"일 때만 true다(검증용 기믹 HorseNoPath·CowNoPath).
        /// </summary>
        protected Dir? StepTowardTarget(Board b, out bool noPath)
        {
            noPath = false;
            if (Full) return null;
            var target = Target(b);
            if (target == null || target.Pos == Pos) return null;
            var dir = Pathfinder.FirstStep(b, Pos, new[] { target.Pos }, LastWalkDir);
            noPath = dir == null;
            return dir;
        }

        /// <summary>배부르지 않고 목표가 있을 때만 탑승 때문에 멈춘 것으로 본다. 목표가 없으면 어차피 걷지 않는다.</summary>
        public override bool IsHeldByRide(Board b)
        {
            return !Full && HasTarget && base.IsHeldByRide(b);
        }
    }
}
