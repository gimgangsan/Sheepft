using System;
using System.Collections.Generic;

namespace Sheepft.Core
{
    /// <summary>맵. 칸의 2차원 배열과 레벨 규칙을 담는다.</summary>
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
    }
}
