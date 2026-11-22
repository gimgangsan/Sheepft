using Sheepft.Core;
using Sheepft.Game;
using UnityEditor;
using UnityEngine;

namespace Sheepft.Tools
{
    /// <summary>
    /// 스테이지 에디터가 격자와 팔레트에 그리는 그림. 게임과 같은 SpriteCatalog(GameRoot.prefab)의 첫 프레임을 쓴다.
    /// 물·풀·울타리는 게임처럼 이웃 마스크(위 1, 오른쪽 2, 아래 4, 왼쪽 8)를 프레임 번호로 쓴다.
    /// 카탈로그가 없으면 글자로 그린다.
    /// </summary>
    public sealed class StageSprites
    {
        private const string RootPrefabPath = "Assets/Prefabs/GameRoot.prefab";

        private readonly SpriteCatalog catalog;
        private GUIStyle label;

        public StageSprites()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);
            catalog = root != null ? root.GetComponent<SpriteCatalog>() : null;
        }

        public bool Ready => catalog != null;

        // ── 칸 ──

        /// <summary>칸 하나. 땅 바탕, 지형, 오브젝트, 수와 상태 순서로 그린다. neighbors는 연결 타일용 이웃 마스크.</summary>
        public void DrawCell(Rect r, CellData cell, int neighbors)
        {
            if (cell.Terrain == Notation.OuterBlank)
            {
                EditorGUI.DrawRect(r, new Color(0.10f, 0.10f, 0.12f));
                return;
            }
            if (cell.Terrain == Notation.InnerBlank)
            {
                EditorGUI.DrawRect(r, new Color(0.16f, 0.15f, 0.18f));
                Text(r, "x", new Color(0.5f, 0.5f, 0.55f), TextAnchor.MiddleCenter);
                return;
            }

            TerrainType type;
            bool known = Notation.TryTerrain(cell.Terrain, out type);
            if (!DrawSprite(r, Frame("ground", null, 0), false)) EditorGUI.DrawRect(r, new Color(0.78f, 0.64f, 0.43f));
            if (!known)
            {
                Text(r, cell.Terrain.ToString(), Color.red, TextAnchor.MiddleCenter);
            }
            else if (type != TerrainType.Ground)
            {
                bool connected = type == TerrainType.Water || type == TerrainType.Grass || type == TerrainType.Fence;
                var sprite = Frame(BoardView.SheetNameOf(type), null, connected ? neighbors : 0);
                if (!DrawSprite(r, sprite, true)) Text(r, cell.Terrain.ToString(), Color.black, TextAnchor.MiddleCenter);
            }

            int n = cell.Objects.Count;
            for (int i = 0; i < n; i++)
            {
                // 여럿이면 조금씩 비켜 그린다. 마지막(가장 위)이 오른쪽 아래.
                float shift = n == 1 ? 0f : (i - (n - 1) * 0.5f) * r.width * 0.18f;
                var o = cell.Objects[i];
                var or = new Rect(r.x + shift, r.y + shift * 0.5f, r.width, r.height);
                DrawObject(or, o.Letter, o.Count, o.State);
            }
        }

        /// <summary>오브젝트 하나(팔레트에도 쓴다). 2마리 이상이면 수를, 젖음·화상이면 표시를 붙인다.</summary>
        public void DrawObject(Rect r, char letter, int count, char state)
        {
            var sprite = ObjectSprite(letter);
            if (!DrawSprite(r, sprite, true)) Text(r, letter.ToString(), Color.white, TextAnchor.MiddleCenter);
            if (count > 1) Text(new Rect(r.x, r.y, r.width - 2, r.height * 0.4f), "×" + count, Color.white, TextAnchor.UpperRight);
            if (state == 'w' || state == 'b')
            {
                var icon = Frame("status_icons", state == 'w' ? "wet" : "burned", 0);
                var ir = new Rect(r.x, r.y, r.width * 0.45f, r.height * 0.45f);
                if (!DrawSprite(ir, icon, false)) Text(ir, state == 'w' ? "w" : "b", state == 'w' ? Color.cyan : new Color(1f, 0.5f, 0.2f), TextAnchor.UpperLeft);
            }
        }

        /// <summary>팔레트의 지형 단추 그림.</summary>
        public void DrawTerrain(Rect r, char symbol)
        {
            var cell = new CellData { Terrain = symbol };
            DrawCell(r, cell, 0);
        }

        // ── 그림 찾기 ──

        private Sprite ObjectSprite(char letter)
        {
            switch (letter)
            {
                case 'S': return Frame("sheep", "idle", 0);
                case 'W': return Frame("wolf", "idle", 0);
                case 'G': return Frame("goat", "idle", 0);
                case 'H': return Frame("horse", "idle", 0);
                case 'M': return Frame("cow", "idle", 0);
                case 'A': return Frame("fruit_basket", "idle", 0);
                case 'B': return Frame("wooden_bucket", "empty", 0);
                case 'U': return Frame("wooden_bucket", "full", 0);
                default: return null;
            }
        }

        private Sprite Frame(string sheetName, string tag, int index)
        {
            if (catalog == null) return null;
            var sheet = catalog.Get(sheetName);
            if (sheet == null) return null;
            return tag != null ? sheet.TagFrame(tag) : sheet.Frame(index);
        }

        /// <summary>
        /// 스프라이트를 칸 크기에 맞춰 그린다. 32픽셀 = 한 칸으로 보고, 칸보다 큰 그림(나무 등)은 아래를 칸 바닥에 맞춘다.
        /// 그림이 없으면 false.
        /// </summary>
        private static bool DrawSprite(Rect cell, Sprite sprite, bool bottomAligned)
        {
            if (sprite == null || sprite.texture == null) return false;
            var tex = sprite.texture;
            var tr = sprite.textureRect;
            float scale = cell.width / 32f;
            float w = sprite.rect.width * scale;
            float h = sprite.rect.height * scale;
            var dest = bottomAligned
                ? new Rect(cell.center.x - w * 0.5f, cell.yMax - h, w, h)
                : new Rect(cell.center.x - w * 0.5f, cell.center.y - h * 0.5f, w, h);
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(dest, tex, uv, true);
            return true;
        }

        private void Text(Rect r, string text, Color color, TextAnchor anchor)
        {
            if (label == null) label = new GUIStyle(EditorStyles.boldLabel);
            label.alignment = anchor;
            label.normal.textColor = color;
            label.fontSize = Mathf.Max(9, (int)(r.height * 0.3f));
            GUI.Label(r, text, label);
        }
    }
}
