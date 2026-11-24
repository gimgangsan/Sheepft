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
        Destroyed,
        /// <summary>늑대가 근육질 양에게 맞아 쓰러짐. 엔딩 컷씬이 나온다.</summary>
        KnockedOut
    }

    /// <summary>켜지고 꺼지는 상태. 양동이의 물 참도 여기에 넣는다.</summary>
    public enum StatusKind
    {
        Wet,
        Burned,
        Filled,
        /// <summary>늑대의 기절(싸움).</summary>
        Stunned,
        /// <summary>늑대의 식사.</summary>
        Eating,
        /// <summary>말·소의 배부름.</summary>
        Full,
        /// <summary>양이 생강꽃을 먹고 근육질 양이 됨.</summary>
        Buff
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

    /// <summary>늑대 무리의 마리 수가 바뀌었다(합류).</summary>
    public sealed class CountChangedEvent : GameEvent
    {
        public readonly int Id;
        public readonly int Count;

        public CountChangedEvent(int id, int count)
        {
            Id = id;
            Count = count;
        }

        public override string ToString() => "Count #" + Id + " " + Count;
    }

    /// <summary>염소가 공격해 무언가를 밀쳐냈다. 화면이 공격 동작을 보여 주는 데 쓴다.</summary>
    public sealed class AttackedEvent : GameEvent
    {
        public readonly int Id;

        /// <summary>실제로 무언가를 밀쳐낸 방향. 공격 순서(상 > 우 > 하 > 좌)대로다.</summary>
        public readonly Dir[] Dirs;

        public AttackedEvent(int id, Dir[] dirs)
        {
            Id = id;
            Dirs = dirs;
        }

        public override string ToString() => "Attacked #" + Id + " " + string.Join(",", Dirs);
    }

    /// <summary>말이 등에 태운 오브젝트가 바뀌었다. 내리면 RiderId가 -1이다.</summary>
    public sealed class RiderChangedEvent : GameEvent
    {
        public readonly int Id;
        public readonly int RiderId;

        public RiderChangedEvent(int id, int riderId)
        {
            Id = id;
            RiderId = riderId;
        }

        public override string ToString() => "Rider #" + Id + " → " + (RiderId < 0 ? "없음" : "#" + RiderId);
    }

    /// <summary>말·소의 목표 바구니가 바뀌었다. 목표를 잃으면 TargetId가 -1이다.</summary>
    public sealed class TargetChangedEvent : GameEvent
    {
        public readonly int Id;
        public readonly int TargetId;

        public TargetChangedEvent(int id, int targetId)
        {
            Id = id;
            TargetId = targetId;
        }

        public override string ToString() => "Target #" + Id + " → " + (TargetId < 0 ? "없음" : "#" + TargetId);
    }

    /// <summary>
    /// 한 단계의 끝. shift, 동물 한 마리의 행동, 들불 확산, 기절·식사 해제, 정리 한 바퀴마다 나온다.
    /// 화면이 여기서 잠깐 쉬어 연쇄 반응을 읽을 수 있게 한다.
    /// </summary>
    public sealed class StepEndEvent : GameEvent
    {
        public override string ToString() => "StepEnd";
    }
}
