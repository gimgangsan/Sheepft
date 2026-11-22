using System.Collections.Generic;
using Sheepft.Core;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 도착 칸 표시. shift 화살표에 마우스를 올리면 그 수로 한 턴을 미리 돌려 보고(TurnPreview),
    /// 스스로 걸은 동물(늑대, 말, 소)이 턴이 끝났을 때 있을 칸에 아래 방향 화살표와 그 동물의 작은 그림을 띄운다.
    /// 같은 칸에 여럿이 오면 옆으로 나란히 놓는다. 다른 모든 오브젝트보다 위에 그린다.
    /// 크기와 위치는 GameSettings의 "도착 칸 표시"에서 고친다.
    /// </summary>
    public sealed class ArrivalMarkers : MonoBehaviour
    {
        /// <summary>맵의 오브젝트(줄마다 20씩, 맵 8줄이면 170 남짓)와 이펙트보다 위. shift 화살표(1000)와도 겹치지 않는다.</summary>
        private const int MarkerOrder = 1100;
        private const float Pixel = 1f / 32f;

        private sealed class Marker
        {
            public GameObject Root;
            public SpriteRenderer Arrow;
            public SpriteRenderer Icon;
        }

        private SpriteCatalog catalog;
        private readonly List<Marker> markers = new List<Marker>();

        // 지금 보이는 미리보기의 판과 수. 같으면 다시 계산하지 않는다.
        private Board shownBoard;
        private int shownTurns = -1;
        private ShiftPoint shownMove;

        public void Init(SpriteCatalog spriteCatalog)
        {
            catalog = spriteCatalog;
        }

        /// <summary>board에 move를 두면 걷는 동물이 어디로 가는지 보인다. 같은 판과 수로 다시 부르면 그대로 둔다.</summary>
        public void Show(Board board, ShiftPoint move)
        {
            if (board == shownBoard && board.TurnsUsed == shownTurns && move == shownMove) return;
            shownBoard = board;
            shownTurns = board.TurnsUsed;
            shownMove = move;

            var arrivals = TurnPreview.WalkerArrivals(board, move);
            var s = catalog.Settings;
            var arrowSheet = catalog.Get("shift_arrow");
            var arrowSprite = arrowSheet != null ? arrowSheet.TagFrame("hover") : null;

            // 같은 칸에 오는 동물끼리 묶는다. 목록이 Id 순이므로 칸 안의 순서도 Id 순이다.
            var byCell = new Dictionary<Pos, List<WalkerArrival>>();
            var cells = new List<Pos>();
            foreach (var a in arrivals)
            {
                List<WalkerArrival> group;
                if (!byCell.TryGetValue(a.Pos, out group))
                {
                    group = new List<WalkerArrival>();
                    byCell[a.Pos] = group;
                    cells.Add(a.Pos);
                }
                group.Add(a);
            }

            int used = 0;
            foreach (var cell in cells)
            {
                var group = byCell[cell];
                for (int i = 0; i < group.Count; i++)
                {
                    var o = board.Find(group[i].Id);
                    var sheet = o == null ? null : catalog.Get(EntityView.SheetName(o));
                    if (sheet == null) continue;

                    var marker = Get(used++);
                    marker.Root.name = "arrival #" + group[i].Id + " " + cell;
                    // 칸 바닥 가운데에서 잰다. 여럿이면 가운데를 기준으로 좌우로 벌린다.
                    float x = (i - (group.Count - 1) * 0.5f) * s.arrivalSpacingPixels * Pixel;
                    Vector3 basePos = BoardView.TileBase(cell) + new Vector3(x, 0f, 0f);
                    marker.Arrow.sprite = arrowSprite;
                    Place(marker.Arrow, basePos + new Vector3(0f, s.arrivalArrowHeightPixels * Pixel, 0f), s.arrivalArrowScale, -90f);
                    marker.Icon.sprite = sheet.TagFrame("idle");
                    Place(marker.Icon, basePos + new Vector3(0f, s.arrivalIconHeightPixels * Pixel, 0f), s.arrivalIconScale, 0f);
                    marker.Root.SetActive(true);
                }
            }
            for (int i = used; i < markers.Count; i++) markers[i].Root.SetActive(false);
        }

        public void Hide()
        {
            shownBoard = null;
            shownTurns = -1;
            foreach (var marker in markers) marker.Root.SetActive(false);
        }

        private Marker Get(int index)
        {
            while (markers.Count <= index)
            {
                var root = new GameObject("arrival");
                root.transform.SetParent(transform, false);
                var marker = new Marker
                {
                    Root = root,
                    Arrow = BoardView.NewRenderer(root.transform, "arrow", MarkerOrder),
                    Icon = BoardView.NewRenderer(root.transform, "icon", MarkerOrder + 1),
                };
                root.SetActive(false);
                markers.Add(marker);
            }
            return markers[index];
        }

        /// <summary>그림의 가운데가 center에 오게 놓는다. 스프라이트마다 피벗이 달라도 같은 자리에 온다.</summary>
        private static void Place(SpriteRenderer renderer, Vector3 center, float scale, float angle)
        {
            var rotation = Quaternion.Euler(0f, 0f, angle);
            var t = renderer.transform;
            t.localScale = new Vector3(scale, scale, 1f);
            t.rotation = rotation;
            Vector3 spriteCenter = renderer.sprite != null ? renderer.sprite.bounds.center : Vector3.zero;
            t.position = center - rotation * (spriteCenter * scale);
        }
    }
}
