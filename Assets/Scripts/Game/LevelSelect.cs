using System.Collections.Generic;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 레벨 선택 화면. 게임 화면 위에 어두운 판을 덮고 스테이지 번호를 격자로 늘어놓는다.
    /// 글자 폰트가 없어서 번호는 `digits` 스프라이트로 그린다. 클릭 판정은 GameController가 한다.
    /// </summary>
    public sealed class LevelSelect : MonoBehaviour
    {
        private const int Order = 2000;
        private const int Columns = 6;
        private const float DigitScale = 2f;
        private const float DigitWidth = 8f / 32f * DigitScale;

        private static readonly Color PanelColor = new Color32(0x2b, 0x24, 0x33, 0xf0);
        private static readonly Color CellColor = new Color32(0x4f, 0x40, 0x44, 0xff);
        private static readonly Color CurrentColor = new Color32(0x6e, 0x5c, 0x5f, 0xff);
        private static readonly Color HoverColor = new Color32(0xd9, 0xa4, 0x41, 0xff);
        private static readonly Color ClearedColor = new Color32(0x47, 0x7e, 0x2f, 0xff);

        private sealed class Cell
        {
            public SpriteRenderer Back;
            public Vector3 Center;
            public Color BaseColor;
        }

        private SpriteCatalog catalog;
        private SpriteRenderer panel;
        private readonly List<Cell> cells = new List<Cell>();
        private float cellSize;

        public bool IsOpen { get; private set; }

        public void Setup(SpriteCatalog spriteCatalog)
        {
            catalog = spriteCatalog;
            gameObject.SetActive(false);
        }

        /// <summary>카메라 화면에 맞춰 번호판을 새로 만들어 보여 준다.</summary>
        public void Open(Camera cam, int stageCount, int current, ICollection<int> cleared)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            cells.Clear();
            gameObject.SetActive(true);
            IsOpen = true;

            float viewHeight = cam.orthographicSize * 2f;
            float viewWidth = viewHeight * cam.aspect;
            Vector3 center = cam.transform.position;
            center.z = 0f;

            panel = BoardView.NewRenderer(transform, "panel", Order);
            panel.sprite = BoardView.WhiteSquare();
            panel.color = PanelColor;
            panel.transform.position = center;
            panel.transform.localScale = new Vector3(viewWidth, viewHeight, 1f);

            int rows = (stageCount + Columns - 1) / Columns;
            cellSize = Mathf.Min(viewWidth * 0.9f / Columns, viewHeight * 0.9f / rows);
            float left = center.x - cellSize * Columns * 0.5f;
            float top = center.y + cellSize * rows * 0.5f;

            var digits = catalog.Get("digits");
            for (int i = 0; i < stageCount; i++)
            {
                var cell = new Cell();
                cell.Center = new Vector3(left + (i % Columns + 0.5f) * cellSize, top - (i / Columns + 0.5f) * cellSize, 0f);
                cell.BaseColor = cleared.Contains(i) ? ClearedColor : i == current ? CurrentColor : CellColor;
                cell.Back = BoardView.NewRenderer(transform, "stage " + (i + 1), Order + 1);
                cell.Back.sprite = BoardView.WhiteSquare();
                cell.Back.color = cell.BaseColor;
                cell.Back.transform.position = cell.Center;
                cell.Back.transform.localScale = new Vector3(cellSize * 0.88f, cellSize * 0.88f, 1f);
                cells.Add(cell);

                // 번호는 1부터 센다. 숫자 스프라이트의 피벗은 아래쪽 가운데다.
                string text = (i + 1).ToString();
                float scale = Mathf.Min(DigitScale, cellSize * 0.7f / (text.Length * DigitWidth / DigitScale));
                float width = 8f / 32f * scale;
                float x = cell.Center.x - width * text.Length * 0.5f;
                for (int k = 0; k < text.Length; k++)
                {
                    var digit = BoardView.NewRenderer(transform, "digit", Order + 2);
                    digit.sprite = digits == null ? null : digits.Frame(text[k] - '0');
                    digit.transform.localScale = new Vector3(scale, scale, 1f);
                    digit.transform.position = new Vector3(x + width * (k + 0.5f), cell.Center.y - width * 0.5f, 0f);
                }
            }
        }

        public void Close()
        {
            IsOpen = false;
            gameObject.SetActive(false);
        }

        /// <summary>그 자리의 스테이지 번호(0부터). 없으면 -1. 마우스가 올라간 칸은 색을 바꾼다.</summary>
        public int Hover(Vector3 world)
        {
            int hit = -1;
            float half = cellSize * 0.44f;
            for (int i = 0; i < cells.Count; i++)
            {
                var d = world - cells[i].Center;
                bool over = Mathf.Abs(d.x) <= half && Mathf.Abs(d.y) <= half;
                cells[i].Back.color = over ? HoverColor : cells[i].BaseColor;
                if (over) hit = i;
            }
            return hit;
        }
    }
}
