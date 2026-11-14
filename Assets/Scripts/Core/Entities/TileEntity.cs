namespace Sheepft.Core
{
    /// <summary>자동 반응의 종류. 선언 순서가 정리 절차의 처리 순서다. README 2절.</summary>
    public enum AutoReaction
    {
        WolfCatch,
        HorseEat,
        HorseSpot,
        CowSmell
    }

    /// <summary>
    /// 칸에 놓이는 모든 것(지형, 오브젝트)의 공통 부모.
    /// 여기의 함수는 "무엇을 하는가"만 맡는다. 순서는 턴 진행과 정리 절차가 정한다.
    /// </summary>
    public abstract class TileEntity
    {
        /// <summary>
        /// 오브젝트는 레벨 파일을 읽은 순서(0부터). 같은 칸·같은 종류의 처리 순서로도 쓴다.
        /// 지형은 번호를 쓰지 않으며 -1이다.
        /// </summary>
        public int Id = -1;

        /// <summary>종료 후 반응.</summary>
        public virtual void AfterShift(Board b) { }

        /// <summary>겹침 상호작용. 그 칸 전체를 받는다. 상태를 바꿨으면 true.</summary>
        public virtual bool OnOverlap(Board b, Tile t) => false;

        /// <summary>자동 반응. 상태를 바꿨으면 true.</summary>
        public virtual bool TryAutoReact(Board b, AutoReaction k) => false;
    }
}
