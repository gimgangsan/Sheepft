using System.Collections.Generic;
using Sheepft.Core;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 맵을 그린다. 칸 (행 r, 열 c)는 월드에서 x가 c..c+1, y가 -(r+1)..-r인 정사각형이다.
    /// 스프라이트의 피벗은 캔버스의 아래쪽 가운데이므로, 모든 그림을 칸의 아래쪽 가운데에 놓는다.
    /// 솟아오르는 장애물(32×48)은 그대로 윗칸을 가린다.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        // 정렬 순서: 바닥 0–1, 줄 표시 2, 그 위로 행마다 10씩. 같은 행에서는 장애물 다음에 오브젝트.
        private const int FloorOrder = 0;
        private const int TerrainOrder = 1;
        private const int HighlightOrder = 2;
        private const int RowOrderBase = 10;
        private const int RowOrderStep = 20;
        private const float Pixel = 1f / 32f;

        private static readonly Color HoleColor = new Color32(0x1a, 0x15, 0x20, 0xff);

        private SpriteCatalog catalog;
        private int height, width;
        private TileKind[,] kinds;
        private TerrainType[,] shownTerrain;
        private SpriteRenderer[,] terrainRenderers;
        private SpriteAnimator[,] terrainAnimators;
        private readonly Dictionary<int, EntityView> views = new Dictionary<int, EntityView>();
        private readonly List<SpriteRenderer> highlights = new List<SpriteRenderer>();
        private Transform terrainRoot, entityRoot, highlightRoot, effectRoot;
        private static Sprite whiteSquare;

        public int Height => height;
        public int Width => width;

        public static Vector3 TileBase(Pos p) => new Vector3(p.Col + 0.5f, -(p.Row + 1), 0f);
        public static Vector3 TileCenter(Pos p) => new Vector3(p.Col + 0.5f, -(p.Row + 0.5f), 0f);

        // ── 만들기 ──

        /// <summary>새 레벨을 그린다. 이전에 그린 것은 모두 지운다.</summary>
        public void Build(SpriteCatalog spriteCatalog, Board board)
        {
            catalog = spriteCatalog;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            views.Clear();
            highlights.Clear();

            terrainRoot = NewChild("terrain");
            highlightRoot = NewChild("highlights");
            entityRoot = NewChild("entities");
            effectRoot = NewChild("effects");

            height = board.Height;
            width = board.Width;
            kinds = new TileKind[height, width];
            shownTerrain = new TerrainType[height, width];
            terrainRenderers = new SpriteRenderer[height, width];
            terrainAnimators = new SpriteAnimator[height, width];

            var ground = catalog.Get("ground");
            foreach (var tile in board.Tiles)
            {
                var p = tile.Pos;
                kinds[p.Row, p.Col] = tile.Kind;
                if (tile.Kind == TileKind.OuterBlank) continue;

                if (tile.Kind == TileKind.InnerBlank)
                {
                    // 구멍 스프라이트가 아직 없어 단색 사각형으로 그린다 (구현 계획 10절).
                    var hole = NewRenderer(terrainRoot, "hole " + p, FloorOrder);
                    hole.sprite = WhiteSquare();
                    hole.color = HoleColor;
                    hole.transform.position = TileCenter(p);
                    continue;
                }

                // 모든 칸 밑에 땅을 깐다. 장애물과 물가의 투명한 부분으로 땅이 보인다.
                var floor = NewRenderer(terrainRoot, "floor " + p, FloorOrder);
                floor.sprite = ground == null ? null : ground.Frame(0);
                floor.transform.position = TileBase(p);

                var terrain = NewRenderer(terrainRoot, "terrain " + p, TerrainOrder);
                terrain.transform.position = TileBase(p);
                terrainRenderers[p.Row, p.Col] = terrain;
                terrainAnimators[p.Row, p.Col] = terrain.gameObject.AddComponent<SpriteAnimator>();
            }

            SyncAll(board);
        }

        /// <summary>화면을 로직의 지금 상태와 똑같이 맞춘다. 애니메이션 없이 바로 바뀐다.</summary>
        public void SyncAll(Board board)
        {
            foreach (var tile in board.Tiles)
                if (!tile.IsBlank) shownTerrain[tile.Pos.Row, tile.Pos.Col] = tile.Terrain.Type;
            RedrawTerrain();

            var alive = new HashSet<int>();
            foreach (var o in board.Objects)
            {
                alive.Add(o.Id);
                EntityView view;
                if (!views.TryGetValue(o.Id, out view))
                {
                    if (EntityView.SheetName(o) == null) continue;
                    var go = new GameObject(o.GetType().Name + " #" + o.Id);
                    go.transform.SetParent(entityRoot, false);
                    view = go.AddComponent<EntityView>();
                    view.Setup(catalog, o);
                    views[o.Id] = view;
                }
                else
                {
                    view.CopyState(o);
                }
            }

            var gone = new List<int>();
            foreach (var id in views.Keys)
                if (!alive.Contains(id)) gone.Add(id);
            foreach (var id in gone) RemoveView(id);

            Layout();
        }

        // ── 이벤트 재생이 쓰는 것 ──

        public EntityView View(int id)
        {
            EntityView view;
            return views.TryGetValue(id, out view) ? view : null;
        }

        public void RemoveView(int id)
        {
            EntityView view;
            if (!views.TryGetValue(id, out view)) return;
            views.Remove(id);
            Destroy(view.gameObject);
        }

        public void SetTerrain(Pos p, TerrainType type)
        {
            shownTerrain[p.Row, p.Col] = type;
            RedrawTerrain();
        }

        /// <summary>
        /// 칸마다 그 칸의 오브젝트를 Id 순서로 조금씩 어긋나게 놓는다.
        /// 양 무리처럼 같은 칸에 겹친 것이 모두 보이게 하려는 것이다.
        /// </summary>
        public void Layout()
        {
            var byTile = new Dictionary<Pos, List<EntityView>>();
            foreach (var view in views.Values)
            {
                List<EntityView> list;
                if (!byTile.TryGetValue(view.Pos, out list)) byTile[view.Pos] = list = new List<EntityView>();
                list.Add(view);
            }

            foreach (var pair in byTile)
            {
                var list = pair.Value;
                list.Sort((a, b) => a.Id.CompareTo(b.Id));
                int order = RowOrderBase + pair.Key.Row * RowOrderStep + 8;
                for (int i = 0; i < list.Count; i++)
                {
                    float dx = (i - (list.Count - 1) * 0.5f) * 6f * Pixel;
                    list[i].LayoutOffset = new Vector3(dx, 0f, 0f);
                    list[i].Place(TileBase(pair.Key));
                    list[i].SetSortingOrder(order + i * 2);
                }
            }
        }

        // ── 이펙트 ──

        /// <summary>
        /// 그 칸에 이펙트(태그 `play`)를 한 번 재생한다. 스프라이트가 없으면 아무것도 하지 않는다.
        /// angle은 오른쪽을 보는 그림을 칸 가운데를 축으로 돌리는 각도다.
        /// </summary>
        public void SpawnEffect(string sheetName, Pos p, float angle = 0f)
        {
            var sheet = catalog.Get(sheetName);
            if (sheet == null || effectRoot == null) return;

            var pivot = new GameObject("effect " + sheetName);
            pivot.transform.SetParent(effectRoot, false);
            pivot.transform.position = TileCenter(p);
            pivot.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var renderer = NewRenderer(pivot.transform, "sprite", RowOrderBase + p.Row * RowOrderStep + RowOrderStep - 1);
            renderer.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            var animator = renderer.gameObject.AddComponent<SpriteAnimator>();
            animator.Play(sheet, "play", false);
            pivot.AddComponent<OneShotEffect>().Animator = animator;
        }

        public bool InGrid(Pos p)
        {
            return p.Row >= 0 && p.Row < height && p.Col >= 0 && p.Col < width && kinds[p.Row, p.Col] == TileKind.Normal;
        }

        /// <summary>화면에 지금 그려진 지형이 풀인가.</summary>
        public bool ShowsGrass(Pos p)
        {
            return InGrid(p) && shownTerrain[p.Row, p.Col] == TerrainType.Grass;
        }

        // ── 줄 표시 ──

        /// <summary>마우스를 올린 shift 포인트의 줄을 표시한다. null이면 지운다.</summary>
        public void ShowLine(IReadOnlyList<Tile> line)
        {
            int count = line == null ? 0 : line.Count;
            var sheet = catalog.Get("line_highlight");
            while (highlights.Count < count)
            {
                var r = NewRenderer(highlightRoot, "highlight", HighlightOrder);
                r.gameObject.AddComponent<SpriteAnimator>().Play(sheet, "idle");
                highlights.Add(r);
            }
            for (int i = 0; i < highlights.Count; i++)
            {
                highlights[i].enabled = i < count;
                if (i < count) highlights[i].transform.position = TileBase(line[i].Pos);
            }
        }

        // ── 지형 그리기 ──

        private void RedrawTerrain()
        {
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    var renderer = terrainRenderers[r, c];
                    if (renderer == null) continue;
                    var type = shownTerrain[r, c];
                    var animator = terrainAnimators[r, c];
                    bool obstacle = type == TerrainType.Stone || type == TerrainType.Fence || type == TerrainType.Tree;
                    renderer.sortingOrder = obstacle ? RowOrderBase + r * RowOrderStep : TerrainOrder;

                    switch (type)
                    {
                        case TerrainType.Ground:
                            // 땅은 바닥에 이미 깔려 있다.
                            animator.Play(null, null);
                            break;
                        case TerrainType.Fire:
                            if (animator.CurrentTag != "idle") animator.Play(catalog.Get("wild_fire"), "idle");
                            break;
                        case TerrainType.Water:
                        case TerrainType.Grass:
                        case TerrainType.Fence:
                            // 연결 타일. 프레임 번호가 이웃 마스크다 (SPRITES.md).
                            animator.Play(null, null);
                            var connected = catalog.Get(SheetNameOf(type));
                            renderer.sprite = connected == null ? null : connected.Frame(NeighbourMask(r, c, type));
                            break;
                        default:
                            animator.Play(null, null);
                            var single = catalog.Get(SheetNameOf(type));
                            renderer.sprite = single == null ? null : single.Frame(0);
                            break;
                    }
                }
            }
        }

        /// <summary>위 = 1, 오른쪽 = 2, 아래 = 4, 왼쪽 = 8. 그 방향 이웃이 같은 지형이면 켜진다.</summary>
        private int NeighbourMask(int r, int c, TerrainType type)
        {
            int mask = 0;
            if (Same(r - 1, c, type)) mask |= 1;
            if (Same(r, c + 1, type)) mask |= 2;
            if (Same(r + 1, c, type)) mask |= 4;
            if (Same(r, c - 1, type)) mask |= 8;
            return mask;
        }

        private bool Same(int r, int c, TerrainType type)
        {
            if (r < 0 || r >= height || c < 0 || c >= width) return false;
            return kinds[r, c] == TileKind.Normal && shownTerrain[r, c] == type;
        }

        private static string SheetNameOf(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Stone: return "stone";
                case TerrainType.Fence: return "fence";
                case TerrainType.Tree: return "tree";
                case TerrainType.Water: return "water";
                case TerrainType.Fire: return "wild_fire";
                case TerrainType.Grass: return "grass";
                case TerrainType.Ash: return "ash";
                default: return "ground";
            }
        }

        // ── 도우미 ──

        private Transform NewChild(string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        public static SpriteRenderer NewRenderer(Transform parent, string objectName, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>1×1 칸 크기의 흰 사각형. 피벗은 가운데다.</summary>
        public static Sprite WhiteSquare()
        {
            if (whiteSquare == null)
            {
                var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                whiteSquare = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return whiteSquare;
        }
    }
}
