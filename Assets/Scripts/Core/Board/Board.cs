using System;
using System.Collections.Generic;

namespace Sheepft.Core
{
    public enum GameResult
    {
        InProgress,
        Won,
        Lost
    }

    /// <summary>맵. 칸의 2차원 배열과 레벨 규칙, 진행 상태를 담는다.</summary>
    public sealed class Board
    {
        // null 없음. 빈칸도 Tile이다.
        private readonly Tile[,] tiles;
        private readonly Tile[] flatTiles; // 같은 칸들을 행 우선으로 늘어놓은 것
        private readonly Tile[][] rowLines;
        private readonly Tile[][] colLines;

        public readonly int Height;
        public readonly int Width;
        public LevelRules Rules;
        public int TurnsUsed;
        public GameResult Result;

        /// <summary>
        /// 이번 턴의 2단계 승리 판정이 끝났는가. 턴을 시작할 때 꺼진다.
        /// 그 뒤(기절·식사 해제 뒤의 정리)에 걸린 기절·식사는 이번 턴의 감소를 이미 지났다.
        /// </summary>
        public bool TurnEnding;

        /// <summary>
        /// 지금 새로 걸리는 기절·식사의 남은 기간. 턴 끝마다 1씩 줄고 0이 되면 풀린다.
        /// "걸린 턴의 남은 시간과 다음 턴 끝까지"이므로 보통 2, 이번 턴의 감소가 지난 뒤면 1이다. README 4절.
        /// </summary>
        public int NewStatusDuration => TurnEnding ? 1 : 2;

        /// <summary>이번 턴에 일어난 일. 화면 재생용이다. 턴을 시작할 때 비운다.</summary>
        public readonly List<GameEvent> Events = new List<GameEvent>();

        /// <summary>이번 턴에 쓰인 기믹. 스테이지 검증용이고 화면은 읽지 않는다. 턴을 시작할 때 비운다.</summary>
        public Mechanic Mechanics;

        /// <summary>레벨을 열 때의 지형. 레벨 중에 바뀌지 않는다. "처음에 울타리·나무였던 칸" 판정에 쓴다.</summary>
        private TerrainType[,] initialTerrain;

        public Board(Tile[,] tiles, LevelRules rules)
        {
            this.tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Height = tiles.GetLength(0);
            Width = tiles.GetLength(1);

            flatTiles = new Tile[Height * Width];
            initialTerrain = new TerrainType[Height, Width];
            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    flatTiles[r * Width + c] = tiles[r, c];
                    if (!tiles[r, c].IsBlank) initialTerrain[r, c] = tiles[r, c].Terrain.Type;
                }
            }

            // 빈칸은 레벨 중에 생기거나 사라지지 않으므로 줄을 한 번만 만든다.
            rowLines = new Tile[Height][];
            for (int r = 0; r < Height; r++)
            {
                var line = new List<Tile>();
                for (int c = 0; c < Width; c++)
                    if (!tiles[r, c].IsBlank) line.Add(tiles[r, c]);
                rowLines[r] = line.ToArray();
            }
            colLines = new Tile[Width][];
            for (int c = 0; c < Width; c++)
            {
                var line = new List<Tile>();
                for (int r = 0; r < Height; r++)
                    if (!tiles[r, c].IsBlank) line.Add(tiles[r, c]);
                colLines[c] = line.ToArray();
            }
        }

        // ── 칸 찾기와 판정 ──

        /// <summary>격자 바깥이면 null.</summary>
        public Tile At(Pos p)
        {
            if (p.Row < 0 || p.Row >= Height || p.Col < 0 || p.Col >= Width) return null;
            return tiles[p.Row, p.Col];
        }

        /// <summary>빈칸이거나 격자 바깥이면 true.</summary>
        public bool IsBlank(Pos p)
        {
            var t = At(p);
            return t == null || t.IsBlank;
        }

        /// <summary>shift용 판정: 지형이 장애물인가. 빈칸은 줄에 들어오지 않으므로 false다.</summary>
        public bool BlocksShift(Tile t)
        {
            return t != null && t.Terrain != null && t.Terrain.IsObstacle;
        }

        /// <summary>동물용 판정: 빈칸, 격자 바깥, 장애물 지형이면 true.</summary>
        public bool BlocksAnimal(Pos p)
        {
            var t = At(p);
            return t == null || t.IsBlank || t.Terrain.IsObstacle;
        }

        /// <summary>걷기용 판정: 동물을 막지 않고 물·들불이 아닌 칸. README 6절.</summary>
        public bool IsWalkable(Pos p)
        {
            if (BlocksAnimal(p)) return false;
            var type = At(p).Terrain.Type;
            return type != TerrainType.Water && type != TerrainType.Fire;
        }

        /// <summary>
        /// 그 행(왼→오)이나 열(위→아래)에서 빈칸을 뺀 실제 칸 목록. README 3절.
        /// 장애물 지형 칸은 실제 칸이므로 들어간다. 번호가 격자 밖이면 빈 목록이다.
        /// </summary>
        public IReadOnlyList<Tile> Line(Axis axis, int index)
        {
            var lines = axis == Axis.Row ? rowLines : colLines;
            if (index < 0 || index >= lines.Length) return Array.Empty<Tile>();
            return lines[index];
        }

        /// <summary>
        /// 모든 칸. (0,0)부터 행 우선 순서다. 빈칸도 나온다. 배열 자체는 고치지 않는다.
        /// solver가 한 턴을 수백만 번 돌리므로 열거할 때 메모리를 새로 잡지 않게 배열을 그대로 내준다.
        /// </summary>
        public Tile[] Tiles => flatTiles;

        /// <summary>
        /// 모든 오브젝트. 칸은 행 우선, 같은 칸에서는 Id 순서다.
        /// foreach는 메모리를 새로 잡지 않는다. 열거하는 동안 칸의 오브젝트 목록을 고치지 않는다.
        /// </summary>
        public ObjectList Objects => new ObjectList(flatTiles);

        /// <summary>그 Id의 오브젝트. 없거나 사라졌으면 null.</summary>
        public ObjectEntity Find(int id)
        {
            foreach (var o in Objects)
                if (o.Id == id) return o;
            return null;
        }

        // ── 상태 바꾸기. 바뀐 것은 Events에 남긴다 ──

        /// <summary>오브젝트를 다른 칸으로 옮긴다. 도착 칸의 목록은 Id 순서를 유지한다.</summary>
        public void Move(ObjectEntity o, Pos to, MoveCause cause)
        {
            var dest = At(to);
            if (dest == null || dest.IsBlank) throw new ArgumentException("칸이 없는 자리로는 옮길 수 없다: " + to, nameof(to));
            if (o.Pos == to) return;

            var from = o.Pos;
            At(from).Objects.Remove(o);
            int i = 0;
            while (i < dest.Objects.Count && dest.Objects[i].Id < o.Id) i++;
            dest.Objects.Insert(i, o);
            o.Pos = to;
            Events.Add(new MovedEvent(o.Id, from, to, cause));

            var t0 = initialTerrain[to.Row, to.Col];
            if (t0 == TerrainType.Fence || t0 == TerrainType.Tree)
            {
                Mark(Mechanic.EnterBurnt);
                if (o is Sheep) Mark(Mechanic.SheepEnterBurnt);
            }
        }

        /// <summary>이번 턴에 그 기믹이 쓰였다고 적는다.</summary>
        public void Mark(Mechanic m)
        {
            Mechanics |= m;
        }

        /// <summary>양마다 지금 칸. 한 행동 전후로 비교해 양이 모였는지(SheepMerge) 본다. 양이 둘 미만이면 null.</summary>
        public Dictionary<int, Pos> SheepPositions()
        {
            Dictionary<int, Pos> result = null;
            int n = 0;
            foreach (var o in Objects)
            {
                if (!(o is Sheep)) continue;
                if (result == null) result = new Dictionary<int, Pos>();
                result[o.Id] = o.Pos;
                n++;
            }
            return n >= 2 ? result : null;
        }

        /// <summary>
        /// SheepPositions로 적어 둔 뒤에, 서로 다른 칸에 있던 양이 한 칸에 모였으면 SheepMerge를 켠다.
        /// before가 null이면(양이 둘 미만) 아무것도 하지 않는다.
        /// </summary>
        public void MarkSheepMerge(Dictionary<int, Pos> before)
        {
            if (before == null) return;
            foreach (var t in Tiles)
            {
                if (t.Objects.Count < 2) continue;
                Pos? first = null;
                foreach (var o in t.Objects)
                {
                    Pos was;
                    if (!(o is Sheep) || !before.TryGetValue(o.Id, out was)) continue;
                    if (first == null) first = was;
                    else if (first.Value != was) { Mark(Mechanic.SheepMerge); return; }
                }
            }
        }

        /// <summary>오브젝트를 없앤다. 양이 줄어 목표 수보다 적어지면 그 순간 패배다. README 8절.</summary>
        public void Remove(ObjectEntity o, RemoveCause cause)
        {
            if (o.Removed) return;
            o.Removed = true;
            At(o.Pos).Objects.Remove(o);
            Events.Add(new RemovedEvent(o.Id, cause));
            if (o is Sheep && Result == GameResult.InProgress && SheepCount() < Rules.TargetSheep)
                Result = GameResult.Lost;
        }

        /// <summary>지형을 바꾼다. 인스턴스를 새로 만들어 교체한다.</summary>
        public void SetTerrain(Pos p, TerrainType type)
        {
            var t = At(p);
            if (t == null || t.IsBlank) throw new ArgumentException("칸이 없는 자리다: " + p, nameof(p));
            var before = t.Terrain.Type;
            if (before == type) return;
            t.Terrain = TerrainEntity.Create(type);
            Events.Add(new TerrainChangedEvent(p, before, type));
        }

        /// <summary>바뀌었으면 true.</summary>
        public bool SetWet(ObjectEntity o, bool wet)
        {
            if (o.Wet == wet) return false;
            o.Wet = wet;
            Events.Add(new StatusChangedEvent(o.Id, StatusKind.Wet, wet));
            return true;
        }

        /// <summary>바뀌었으면 true.</summary>
        public bool SetBurned(Animal a, bool burned)
        {
            if (a.Burned == burned) return false;
            a.Burned = burned;
            Events.Add(new StatusChangedEvent(a.Id, StatusKind.Burned, burned));
            return true;
        }

        /// <summary>바뀌었으면 true.</summary>
        public bool SetFilled(Bucket bucket, bool filled)
        {
            if (bucket.Filled == filled) return false;
            bucket.Filled = filled;
            Events.Add(new StatusChangedEvent(bucket.Id, StatusKind.Filled, filled));
            return true;
        }

        /// <summary>늑대 무리의 마리 수를 바꾼다. 바뀌었으면 true.</summary>
        public bool SetCount(Wolf wolf, int count)
        {
            if (wolf.Count == count) return false;
            wolf.Count = count;
            Events.Add(new CountChangedEvent(wolf.Id, count));
            return true;
        }

        /// <summary>기절의 남은 기간을 정한다. 0이면 해제다. 기절 여부가 바뀌었으면 true.</summary>
        public bool SetStun(Wolf wolf, int left)
        {
            bool before = wolf.StunLeft > 0;
            wolf.StunLeft = left;
            if (before == left > 0) return false;
            Events.Add(new StatusChangedEvent(wolf.Id, StatusKind.Stunned, left > 0));
            return true;
        }

        /// <summary>식사의 남은 기간을 정한다. 0이면 해제다. 식사 여부가 바뀌었으면 true.</summary>
        public bool SetMeal(Wolf wolf, int left)
        {
            bool before = wolf.MealLeft > 0;
            wolf.MealLeft = left;
            if (before == left > 0) return false;
            Events.Add(new StatusChangedEvent(wolf.Id, StatusKind.Eating, left > 0));
            return true;
        }

        /// <summary>말·소의 목표 바구니를 정한다. -1이면 목표 없음이다. 바뀌었으면 true.</summary>
        public bool SetTarget(BasketSeeker animal, int targetId)
        {
            if (animal.TargetId == targetId) return false;
            animal.TargetId = targetId;
            Events.Add(new TargetChangedEvent(animal.Id, targetId));
            return true;
        }

        /// <summary>말이 등에 태운 오브젝트를 정한다. -1이면 내린다. 바뀌었으면 true.</summary>
        public bool SetRider(Horse horse, int riderId)
        {
            if (horse.RiderId == riderId) return false;
            horse.RiderId = riderId;
            Events.Add(new RiderChangedEvent(horse.Id, riderId));
            return true;
        }

        /// <summary>
        /// 레벨을 열 때 말의 등에 태울 것을 정한다. 정리 절차를 돌리지 않고 태우기만 한다.
        /// 처음부터 말과 같은 칸에 있는 것을 처음 화면부터 등에 얹어 보여 주기 위해서다.
        /// </summary>
        public void SeatInitialRiders()
        {
            foreach (var t in Tiles)
                foreach (var o in t.Objects.ToArray())
                    if (o is Horse) o.OnOverlap(this, t);
            Events.Clear();
        }

        /// <summary>말·소의 배부름을 정한다. 바뀌었으면 true.</summary>
        public bool SetFull(BasketSeeker animal, bool full)
        {
            if (animal.Full == full) return false;
            animal.Full = full;
            Events.Add(new StatusChangedEvent(animal.Id, StatusKind.Full, full));
            return true;
        }

        // ── 승리와 패배. README 8절 ──

        /// <summary>남아 있는 양의 수.</summary>
        public int SheepCount()
        {
            int n = 0;
            foreach (var o in Objects)
                if (o is Sheep) n++;
            return n;
        }

        /// <summary>풀 칸 위에 있고 젖지도 화상을 입지도 않은 양의 수.</summary>
        public int SheepOnGrass()
        {
            int n = 0;
            foreach (var t in Tiles)
            {
                if (t.IsBlank || t.Terrain.Type != TerrainType.Grass) continue;
                foreach (var o in t.Objects)
                {
                    var sheep = o as Sheep;
                    if (sheep != null && !sheep.Wet && !sheep.Burned) n++;
                }
            }
            return n;
        }

        /// <summary>승리 조건이 지금 성립하는가. 목표 양 수가 0인 레벨은 승리 판정을 하지 않는다.</summary>
        public bool IsWinConditionMet()
        {
            return Rules.TargetSheep > 0 && SheepOnGrass() >= Rules.TargetSheep;
        }

        /// <summary>풀이 하나도 남지 않았다. 패배는 아니지만 승리 조건이 사라진 상태다.</summary>
        public bool CannotWin
        {
            get
            {
                foreach (var t in Tiles)
                    if (!t.IsBlank && t.Terrain.Type == TerrainType.Grass) return false;
                return true;
            }
        }

        // ── undo ──

        /// <summary>undo용 깊은 복사. 오브젝트의 Id는 그대로다. Events는 복사하지 않는다.</summary>
        public Board Clone()
        {
            var copy = new Tile[Height, Width];
            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    var src = tiles[r, c];
                    var terrain = src.IsBlank ? null : TerrainEntity.Create(src.Terrain.Type);
                    var tile = new Tile(src.Pos, src.Kind, terrain);
                    foreach (var o in src.Objects) tile.Objects.Add(o.Clone());
                    copy[r, c] = tile;
                }
            }
            var board = new Board(copy, Rules);
            board.initialTerrain = initialTerrain; // 바뀌지 않으므로 같이 쓴다.
            board.TurnsUsed = TurnsUsed;
            board.Result = Result;
            board.TurnEnding = TurnEnding;
            return board;
        }
    }

    /// <summary>
    /// Board.Objects의 열거. foreach가 구조체 열거자를 쓰므로 메모리를 새로 잡지 않는다.
    /// LINQ 등 IEnumerable로 쓸 때는 보통의 열거처럼 동작한다.
    /// </summary>
    public readonly struct ObjectList : IEnumerable<ObjectEntity>
    {
        private readonly Tile[] tiles;

        public ObjectList(Tile[] tiles)
        {
            this.tiles = tiles;
        }

        public Enumerator GetEnumerator() => new Enumerator(tiles);
        IEnumerator<ObjectEntity> IEnumerable<ObjectEntity>.GetEnumerator() => GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<ObjectEntity>
        {
            private readonly Tile[] tiles;
            private int tile;
            private int index;
            private ObjectEntity current;

            public Enumerator(Tile[] tiles)
            {
                this.tiles = tiles;
                tile = 0;
                index = -1;
                current = null;
            }

            public ObjectEntity Current => current;
            object System.Collections.IEnumerator.Current => current;

            public bool MoveNext()
            {
                while (tile < tiles.Length)
                {
                    var objects = tiles[tile].Objects;
                    index++;
                    if (index < objects.Count)
                    {
                        current = objects[index];
                        return true;
                    }
                    tile++;
                    index = -1;
                }
                current = null;
                return false;
            }

            public void Reset()
            {
                tile = 0;
                index = -1;
                current = null;
            }

            public void Dispose() { }
        }
    }
}
