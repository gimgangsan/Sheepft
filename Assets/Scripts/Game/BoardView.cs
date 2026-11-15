using System.Collections.Generic;
using Sheepft.Core;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 맵을 그린다. 칸 (행 r, 열 c)는 월드에서 x가 c..c+1, y가 -(r+1)..-r인 정사각형이다.
    /// 지형, 오브젝트, 이펙트는 모두 PrefabCatalog의 프리팹을 찍어 낸다. 배치는 레벨 데이터가 정한다.
    /// 스프라이트의 피벗은 캔버스의 아래쪽 가운데이므로, 모든 그림을 칸의 아래쪽 가운데에 놓는다.
    /// 솟아오르는 장애물(32×48)은 그대로 윗칸을 가린다.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        // 정렬 순서: 바닥 0–1, 줄 표시 2, 그 위로 행마다 20씩. 같은 행에서는 장애물 다음에 오브젝트, 맨 위에 이펙트.
        private const int FloorOrder = 0;
        private const int TerrainOrder = 1;
        private const int HighlightOrder = 2;
        private const int RowOrderBase = 10;
        private const int RowOrderStep = 20;
        private const float Pixel = 1f / 32f;

        private SpriteCatalog catalog;
        private PrefabCatalog prefabs;
        private int height, width;
        private TileKind[,] kinds;
        private TerrainType[,] shownTerrain;
        private TerrainType[,] builtTerrain;
        private GameObject[,] terrainObjects;
        private readonly Dictionary<int, EntityView> views = new Dictionary<int, EntityView>();
        private readonly List<GameObject> highlights = new List<GameObject>();
        private Transform terrainRoot, entityRoot, highlightRoot, effectRoot;
        private static Sprite whiteSquare;

        public int Height => height;
        public int Width => width;

        public static Vector3 TileBase(Pos p) => new Vector3(p.Col + 0.5f, -(p.Row + 1), 0f);
        public static Vector3 TileCenter(Pos p) => new Vector3(p.Col + 0.5f, -(p.Row + 0.5f), 0f);

        // ── 만들기 ──

        /// <summary>새 레벨을 그린다. 이전에 그린 것은 모두 지운다.</summary>
        public void Build(SpriteCatalog spriteCatalog, PrefabCatalog prefabCatalog, Board board)
        {
            catalog = spriteCatalog;
            prefabs = prefabCatalog;
            for (int i = transform.childCount - 1; i >= 0; i--) Remove(transform.GetChild(i).gameObject);
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
            builtTerrain = new TerrainType[height, width];
            terrainObjects = new GameObject[height, width];

            var groundPrefab = prefabs.Terrain("ground");
            foreach (var tile in board.Tiles)
            {
                var p = tile.Pos;
                kinds[p.Row, p.Col] = tile.Kind;
                builtTerrain[p.Row, p.Col] = TerrainType.Ground;
                if (tile.Kind == TileKind.OuterBlank) continue;

                if (tile.Kind == TileKind.InnerBlank)
                {
                    // 구멍 스프라이트가 아직 없어 단색 사각형으로 그린다 (구현 계획 10절).
                    var hole = NewRenderer(terrainRoot, "hole " + p, FloorOrder);
                    hole.sprite = WhiteSquare();
                    hole.color = catalog.Settings.holeColor;
                    hole.transform.position = TileCenter(p);
                    continue;
                }

                // 모든 칸 밑에 땅을 깐다. 다른 지형(물, 풀, 들불, 재, 장애물)은 가장자리가 투명한 덮개라서 그 사이로 땅이 보인다.
                if (groundPrefab != null)
                {
                    var floor = Instantiate(groundPrefab, terrainRoot);
                    floor.name = "floor " + p;
                    floor.transform.position = TileBase(p);
                    SetOrder(floor, FloorOrder);
                }
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
                    var prefab = prefabs.Entity(EntityView.SheetName(o));
                    if (prefab == null) continue;
                    var go = Instantiate(prefab, entityRoot);
                    go.name = o.GetType().Name + " #" + o.Id;
                    view = go.GetComponent<EntityView>();
                    if (view == null) view = go.AddComponent<EntityView>();
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
            Remove(view.gameObject);
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

            var settings = catalog.Settings;
            foreach (var pair in byTile)
            {
                var all = pair.Value;
                all.Sort((a, b) => a.Id.CompareTo(b.Id));

                // 말의 등에 탄 것은 옆으로 늘어놓지 않고 그 말 위에 얹는다.
                var riders = new Dictionary<int, EntityView>();
                foreach (var horse in all)
                {
                    if (horse.RiderId < 0) continue;
                    foreach (var other in all)
                        if (other.Id == horse.RiderId) riders[horse.Id] = other;
                }
                var list = all.FindAll(v => !IsRider(riders, v));

                // 정렬: 말 몸 < 탄 것(무리 사본 포함) < 탄 것의 아이콘 < 말의 느낌표. 느낌표가 늘 맨 위에 보이게 한다.
                int order = RowOrderBase + pair.Key.Row * RowOrderStep + 8;
                for (int i = 0; i < list.Count; i++)
                {
                    var view = list[i];
                    float dx = (i - (list.Count - 1) * 0.5f) * settings.stackSpacingPixels * Pixel;
                    view.LayoutOffset = new Vector3(dx, 0f, 0f);
                    view.Place(TileBase(pair.Key));
                    view.SetSortingOrder(order);

                    EntityView rider;
                    if (riders.TryGetValue(view.Id, out rider))
                    {
                        var back = settings.riderOffsetPixels * Pixel;
                        if (view.FacesLeft) back.x = -back.x;
                        rider.LayoutOffset = view.LayoutOffset + (Vector3)back;
                        rider.Place(TileBase(pair.Key));
                        rider.SetSortingOrder(order + 3);
                        view.SetIconOrder(order + 5);
                        order += 6;
                    }
                    else
                    {
                        order += 2;
                    }
                }
            }
        }

        private static bool IsRider(Dictionary<int, EntityView> riders, EntityView view)
        {
            foreach (var rider in riders.Values)
                if (rider == view) return true;
            return false;
        }

        // ── 이펙트 ──

        /// <summary>
        /// 그 칸에 이펙트 프리팹을 하나 놓는다. 프리팹이 없으면 아무것도 하지 않는다.
        /// angle은 오른쪽을 보는 그림을 칸 가운데를 축으로 돌리는 각도다. 이펙트는 스스로 사라진다.
        /// </summary>
        public void SpawnEffect(string effectName, Pos p, float angle = 0f)
        {
            var prefab = prefabs.Effect(effectName);
            if (prefab == null || effectRoot == null) return;

            var effect = Instantiate(prefab, effectRoot);
            effect.name = "effect " + effectName;
            effect.transform.position = TileCenter(p);
            effect.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            SetOrder(effect, RowOrderBase + p.Row * RowOrderStep + RowOrderStep - 1);
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
            while (highlights.Count < count && prefabs.lineHighlight != null)
            {
                var go = Instantiate(prefabs.lineHighlight, highlightRoot);
                go.name = "highlight";
                SetOrder(go, HighlightOrder);
                highlights.Add(go);
            }
            for (int i = 0; i < highlights.Count; i++)
            {
                highlights[i].SetActive(i < count);
                if (i < count) highlights[i].transform.position = TileBase(line[i].Pos);
            }
        }

        // ── 지형 그리기 ──

        private void RedrawTerrain()
        {
            // 지형이 바뀐 칸은 프리팹을 새로 찍는다. 땅은 바닥에 이미 깔려 있으므로 따로 두지 않는다.
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    if (kinds[r, c] != TileKind.Normal) continue;
                    var type = shownTerrain[r, c];
                    if (builtTerrain[r, c] == type) continue;

                    if (terrainObjects[r, c] != null) Remove(terrainObjects[r, c]);
                    terrainObjects[r, c] = null;
                    builtTerrain[r, c] = type;
                    if (type == TerrainType.Ground) continue;

                    var prefab = prefabs.Terrain(SheetNameOf(type));
                    if (prefab == null) continue;
                    var p = new Pos(r, c);
                    var go = Instantiate(prefab, terrainRoot);
                    go.name = type + " " + p;
                    go.transform.position = TileBase(p);
                    bool obstacle = type == TerrainType.Stone || type == TerrainType.Fence || type == TerrainType.Tree;
                    SetOrder(go, obstacle ? RowOrderBase + r * RowOrderStep : TerrainOrder);
                    terrainObjects[r, c] = go;
                }
            }

            // 연결 타일(물, 풀, 울타리)은 이웃에 맞는 그림으로 바꾼다. 프레임 번호가 이웃 마스크다 (SPRITES.md).
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    var go = terrainObjects[r, c];
                    if (go == null) continue;
                    var type = shownTerrain[r, c];
                    if (type != TerrainType.Water && type != TerrainType.Grass && type != TerrainType.Fence) continue;
                    var sheet = catalog.Get(SheetNameOf(type));
                    var renderer = go.GetComponentInChildren<SpriteRenderer>();
                    if (sheet != null && renderer != null) renderer.sprite = sheet.Frame(NeighbourMask(r, c, type));
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

        public static string SheetNameOf(TerrainType type)
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

        /// <summary>프리팹 안의 모든 SpriteRenderer에 같은 정렬 순서를 준다.</summary>
        private static void SetOrder(GameObject go, int order)
        {
            foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sortingOrder = order;
        }

        /// <summary>편집 모드(스테이지 미리 보기)에서도 쓸 수 있게 지운다.</summary>
        private static void Remove(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
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
