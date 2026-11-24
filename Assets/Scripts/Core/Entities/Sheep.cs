namespace Sheepft.Core
{
    /// <summary>
    /// 양. 한 마리가 엔티티 하나다. 같은 칸의 양이 곧 무리이고 따로 합치지 않는다.
    /// 스스로 하는 일이 없다. README 6절.
    /// </summary>
    public sealed class Sheep : Animal
    {
        /// <summary>
        /// 근육질 양. 생강꽃을 먹으면 된다. 늑대에게 먹히지 않고, 같은 칸에 들어온 늑대를 쓰러뜨린다.
        /// 한 번 되면 레벨이 끝날 때까지 그대로다. README 6절 "생강꽃".
        /// </summary>
        public bool Buff;
    }
}
