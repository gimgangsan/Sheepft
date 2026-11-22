namespace Sheepft.Core
{
    /// <summary>과일 바구니. 나무 재질 물건. 스스로 하는 일은 없고 말과 소를 유인한다. README 6절.</summary>
    public sealed class FruitBasket : ObjectEntity
    {
        public override bool IsWooden => true;

        /// <summary>
        /// 말이 싣고 간 적이 있거나 내려놓은 바구니. 규칙에는 영향이 없고,
        /// 검증용 기믹 HorseLure·CowLure("옮겨진 바구니에 끌림")를 가리는 데만 쓴다.
        /// </summary>
        public bool Carried;
    }
}
