namespace Sheepft.Core
{
    // 아직 행동을 구현하지 않은 오브젝트. 레벨 파일을 읽고 쓰는 데 필요한 값만 있다.
    // 반응 함수와 나머지 필드(기절·식사 기간, 목표, 배부름)는 구현 계획 8절의
    // 해당 단계(늑대 4, 바구니 5, 염소 6, 말 7, 소 8)에서 채우고 파일을 나눈다.

    /// <summary>늑대 무리. 무리 하나가 엔티티 하나이고 마리 수를 갖는다.</summary>
    public sealed class Wolf : Animal
    {
        /// <summary>마리 수. 잡는 범위는 Count − 1이다.</summary>
        public int Count = 1;
    }

    public sealed class Goat : Animal
    {
    }

    public sealed class Horse : Animal
    {
    }

    public sealed class Cow : Animal
    {
    }

    /// <summary>과일 바구니. 나무 재질 물건.</summary>
    public sealed class FruitBasket : ObjectEntity
    {
        public override bool IsWooden => true;
    }
}
