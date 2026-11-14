namespace Sheepft.Core
{
    /// <summary>
    /// 나무 양동이. 나무 재질 물건. README 6절.
    /// 스스로 하는 일은 없고, 모든 동작은 지형(Water, Fire)과의 겹침 상호작용으로 일어난다.
    /// </summary>
    public sealed class Bucket : ObjectEntity
    {
        /// <summary>true면 물 찬 양동이(`U`), false면 빈 양동이(`B`). 물 찬 양동이에는 젖음 상태가 없다.</summary>
        public bool Filled;

        public override bool IsWooden => true;
    }
}
