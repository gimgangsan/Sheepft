namespace Sheepft.Core
{
    /// <summary>과일 바구니. 나무 재질 물건. 스스로 하는 일은 없고 말과 소를 유인한다. README 6절.</summary>
    public sealed class FruitBasket : ObjectEntity
    {
        public override bool IsWooden => true;
    }
}
