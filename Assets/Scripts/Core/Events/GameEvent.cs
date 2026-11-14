namespace Sheepft.Core
{
    /// <summary>오브젝트가 움직인 까닭.</summary>
    public enum MoveCause
    {
        Shift,
        Push,
        Walk,
        Carry,
        CowPush,
        HorseEat
    }

    /// <summary>오브젝트가 사라진 까닭.</summary>
    public enum RemoveCause
    {
        Eaten,
        Burned,
        Merged,
        Destroyed
    }

    /// <summary>켜지고 꺼지는 상태. 양동이의 물 참도 여기에 넣는다.</summary>
    public enum StatusKind
    {
        Wet,
        Burned,
        Filled
    }

    /// <summary>
    /// 한 턴 동안 일어난 일의 기록. 로직은 한 턴을 즉시 끝까지 계산하고,
    /// 화면이 Board.Events를 차례로 재생한다. 구현 계획 5절.
    /// </summary>
    public abstract class GameEvent
    {
    }

    /// <summary>
    /// 오브젝트가 다른 칸으로 갔다. Shift는 빈칸을 뛰어넘거나 순환할 수 있으므로
    /// From과 To가 이웃이 아닐 수 있다.
    /// </summary>
    public sealed class MovedEvent : GameEvent
    {
        public readonly int Id;
        public readonly Pos From;
        public readonly Pos To;
        public readonly MoveCause Cause;

        public MovedEvent(int id, Pos from, Pos to, MoveCause cause)
        {
            Id = id;
            From = from;
            To = to;
            Cause = cause;
        }

        public override string ToString() => "Moved #" + Id + " " + From + " → " + To + " " + Cause;
    }

    public sealed class RemovedEvent : GameEvent
    {
        public readonly int Id;
        public readonly RemoveCause Cause;

        public RemovedEvent(int id, RemoveCause cause)
        {
            Id = id;
            Cause = cause;
        }

        public override string ToString() => "Removed #" + Id + " " + Cause;
    }

    public sealed class TerrainChangedEvent : GameEvent
    {
        public readonly Pos Pos;
        public readonly TerrainType From;
        public readonly TerrainType To;

        public TerrainChangedEvent(Pos pos, TerrainType from, TerrainType to)
        {
            Pos = pos;
            From = from;
            To = to;
        }

        public override string ToString() => "Terrain " + Pos + " " + From + " → " + To;
    }

    public sealed class StatusChangedEvent : GameEvent
    {
        public readonly int Id;
        public readonly StatusKind Status;
        public readonly bool On;

        public StatusChangedEvent(int id, StatusKind status, bool on)
        {
            Id = id;
            Status = status;
            On = on;
        }

        public override string ToString() => "Status #" + Id + " " + Status + (On ? " on" : " off");
    }

    /// <summary>
    /// 한 단계의 끝. shift, 정리 한 바퀴마다 나온다.
    /// 화면이 여기서 잠깐 쉬어 연쇄 반응을 읽을 수 있게 한다.
    /// </summary>
    public sealed class StepEndEvent : GameEvent
    {
        public override string ToString() => "StepEnd";
    }
}
