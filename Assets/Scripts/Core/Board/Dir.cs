namespace Sheepft.Core
{
    /// <summary>방향. 선언 순서가 동점 처리 순서(상 > 우 > 하 > 좌)와 같다. README 6절.</summary>
    public enum Dir
    {
        Up,
        Right,
        Down,
        Left
    }

    /// <summary>shift하는 줄의 종류. 행 또는 열.</summary>
    public enum Axis
    {
        Row,
        Col
    }
}
