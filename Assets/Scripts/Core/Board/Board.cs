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
        private readonly Tile[][] rowLines;
        private readonly Tile[][] colLines;

        public readonly int Height;
        public readonly int Width;
        public LevelRules Rules;
        public int TurnsUsed;
        public GameResult Result;

        /// <summary>이번 턴에 일어난 일. 화면 재생용이다. 턴을 시작할 때 비운다.</summary>
        public readonly List<GameEvent> Events = new List<GameEvent>();

        public Board(Tile[,] tiles, LevelRules rules)
        {
            this.tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Height = tiles.GetLength(0);
            Width = tiles.GetLength(1);

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

        /// <summary>모든 칸. (0,0)부터 행 우선 순서다. 빈칸도 나온다.</summary>
        public IEnumerable<Tile> Tiles
        {
            get
            {
                for (int r = 0; r < Height; r++)
                    for (int c = 0; c < Width; c++)
                        yield return tiles[r, c];
            }
        }

        /// <summary>모든 오브젝트. 칸은 행 우선, 같은 칸에서는 Id 순서다.</summary>
        public IEnumerable<ObjectEntity> Objects
        {
            get
            {
                foreach (var t in Tiles)
                    foreach (var o in t.Objects)
                        yield return o;
            }
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
            board.TurnsUsed = TurnsUsed;
            board.Result = Result;
            return board;
        }
    }
}
