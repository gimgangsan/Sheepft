using System;
using System.Collections;
using System.Collections.Generic;
using Sheepft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sheepft.Game
{
    public enum MenuScreen
    {
        Settings,
        Title,
        StageSelect
    }

    /// <summary>
    /// 메뉴 화면. 타이틀, 스테이지 선택, 설정을 shift로 오간다. 구현 계획 11절, SPRITES.md 6절.
    /// 헛간 그림(1920×360) 위에 화면 3개가 가로로 이어지고, 카메라가 640px씩 옆으로 움직인다.
    /// Core 규칙은 쓰지 않고 이동 연출만 빌린다. 화면 사이는 빈칸이라서 양은 빈칸을 건너뛰듯 옆 화면의 칸으로 간다.
    /// 배치는 GameSettings의 "메뉴 배치"에 메뉴 그림의 픽셀로 적는다. 메뉴를 열 때마다 그 값으로 다시 그린다.
    /// </summary>
    public sealed class MenuController : MonoBehaviour
    {
        private const float Pixel = 1f / 32f;
        private const int ScreenWidth = 640;
        private const int ScreenHeight = 360;
        private const float BackgroundWidth = 1920f;

        [Tooltip("메뉴 그림의 왼쪽 위가 오는 월드 좌표. 게임판(0,0 근처)과 겹치지 않는 먼 곳에 둔다.")]
        public Vector2 origin = new Vector2(-200f, 60f);

        [Header("글자")]
        public string playText = "Play";
        public string settingText = "Setting";
        public string backText = "Back";
        public string musicText = "Music";
        public string soundText = "Sound";
        public string speedText = "Speed";

        [Header("꿈 풍선")]
        [Tooltip("3별 조건 글자. {0}은 3별 턴 수.")]
        public string threeStarFormat = "3 stars in {0} turns";
        [Tooltip("아직 열리지 않은 스테이지의 이름 자리.")]
        public string lockedText = "Locked";
        [Tooltip("풍선 안 글자 색(바깥선 색). 풍선 판은 양털 밝음이다.")]
        public Color bubbleTextColor = new Color32(0x2b, 0x24, 0x33, 0xff);

        [Header("설정 줄의 오브젝트 (스프라이트 이름과 태그)")]
        public string musicProp = "setting_props:music";
        public string soundProp = "setting_props:sound";
        [Tooltip("이동 속도 줄의 오브젝트. 전용 그림이 생기면 바꾼다.")]
        public string speedProp = "horse:idle";
        public Color labelColor = new Color32(0xf4, 0xf0, 0xe8, 0xff);
        [Tooltip("깬 스테이지의 번호 색(불꽃 노랑).")]
        public Color clearedLabelColor = new Color32(0xfc, 0xc9, 0x4b, 0xff);
        [Tooltip("풀 위에 얹는 번호의 바깥선 색. 밝은 풀 위에서도 읽히게 한다.")]
        public Color numberOutlineColor = new Color32(0x2b, 0x24, 0x33, 0xff);

        /// <summary>양이 그 스테이지(0부터)의 풀에 들어갔다.</summary>
        public event Action<int> StageChosen;

        public bool IsOpen { get; private set; }

        private sealed class Arrow
        {
            public Vector2 Cell;
            public Dir Dir;
            public Action OnClick;
            public TextMeshPro Label;
            public readonly List<Vector2> Line = new List<Vector2>();
            public GameObject Object;
            public SpriteRenderer Renderer;
        }

        /// <summary>
        /// 설정 줄 하나. 6칸 줄에서 오브젝트가 있는 칸이 값이다.
        /// 음악·효과음은 끝에서 순환하고, 이동 속도는 양 끝 칸이 돌이라 끝에서 멈춘다.
        /// </summary>
        private sealed class SettingRow
        {
            public Vector2 TopLeft;
            /// <summary>값의 개수. 값 v는 TopLeft에서 (v + FirstCell)번째 칸이다.</summary>
            public int Count;
            public int FirstCell;
            public bool Wrap;
            public int Value;
            public Action<int> Save;
            public Func<int, string> Text;
            public string Name;
            public SpriteRenderer Prop;
            public TextMeshPro Label;

            public Vector2 Cell(int value) => TopLeft + new Vector2(32f * (value + FirstCell), 0f);
        }

        private sealed class Row
        {
            public GameObject Root;
            public readonly List<SpriteRenderer> Sprites = new List<SpriteRenderer>();
            public TextMeshPro Label;
            public float K;
        }

        private SpriteCatalog catalog;
        private PrefabCatalog prefabs;
        private Camera cam;
        private TMP_FontAsset font;
        private int stageCount;

        private Transform root, layoutRoot, rowRoot, highlightRoot, bubbleContent;
        private IReadOnlyList<Board> levels;
        private EntityView sheep;
        private readonly List<Arrow> titleArrows = new List<Arrow>();
        private readonly List<Arrow> stageArrows = new List<Arrow>();
        private readonly List<Arrow> settingsArrows = new List<Arrow>();
        private readonly List<Row> rows = new List<Row>();
        private readonly List<GameObject> highlights = new List<GameObject>();
        private Arrow columnUp, columnDown;

        private MenuScreen screen;
        private int entries;
        private int unlocked;
        private int selected;
        /// <summary>지금 보이는 스테이지 줄의 높이 범위. 고른 줄이 0, 위로 +1.</summary>
        private int kLow, kHigh;
        private bool busy;

        private GameSettings Settings => catalog.Settings;
        private Vector2 TitleCell => Settings.menuTitleCell;
        private Vector2 SettingsCell => Settings.menuSettingsCell;
        private Vector2 StageCell => Settings.menuStageCell;

        // ── 만들기 ──

        public void Init(SpriteCatalog spriteCatalog, PrefabCatalog prefabCatalog, Camera camera, TMP_FontAsset labelFont, int stages)
        {
            catalog = spriteCatalog;
            prefabs = prefabCatalog;
            cam = camera;
            font = labelFont != null ? labelFont : TMP_Settings.defaultFontAsset;
            stageCount = stages;
            // 꿈 풍선에 보일 스테이지 정보(이름, 별 조건, 맵). 읽기만 한다.
            levels = catalog.stages != null ? LevelParser.ParseAll(catalog.stages.text) : new List<Board>();

            root = NewChild(transform, "menu");
            layoutRoot = NewChild(root, "layout");
            rowRoot = NewChild(root, "stage rows");
            highlightRoot = NewChild(root, "highlights");

            var sheepPrefab = prefabs.Entity("sheep");
            if (sheepPrefab != null)
            {
                var go = Instantiate(sheepPrefab, root);
                go.name = "menu sheep";
                sheep = go.GetComponent<EntityView>();
                if (sheep == null) sheep = go.AddComponent<EntityView>();
                sheep.Setup(catalog, new Sheep());
                sheep.SetSortingOrder(10);
            }

            root.gameObject.SetActive(false);
        }

        /// <summary>배경, 로고, 땅 칸, 글자, 화살표를 GameSettings의 배치대로 새로 놓는다.</summary>
        private void BuildLayout()
        {
            for (int i = layoutRoot.childCount - 1; i >= 0; i--) Destroy(layoutRoot.GetChild(i).gameObject);
            titleArrows.Clear();
            stageArrows.Clear();
            settingsArrows.Clear();

            // 배경과 로고. 모든 스프라이트의 피벗은 캔버스의 아래쪽 가운데다.
            PlaceSheet("menu_background", Vector2.zero, new Vector2(BackgroundWidth, ScreenHeight), -50);
            PlaceSheet("title_logo", Settings.menuLogoTopLeft, Settings.menuLogoCanvasSize, -40);

            // 꿈 풍선. 안의 내용은 고른 스테이지가 바뀔 때마다 UpdateBubble이 다시 채운다.
            PlaceSheet("dream_bubble", Settings.menuBubbleTopLeft, Settings.menuBubbleCanvasSize, -30);
            bubbleContent = NewChild(layoutRoot, "bubble content");

            // 땅 칸: 타이틀의 양 칸, 설정의 돌아가는 칸.
            PlaceTile("ground", TitleCell, 0);
            PlaceTile("ground", SettingsCell, 0);

            // 타이틀: ▶(스테이지 선택으로) 아래 Play, ◀(설정으로) 아래 Setting.
            titleArrows.Add(NewArrow(TitleCell + new Vector2(-32f, 0f), Dir.Right, () => StartCoroutine(GoAcross(MenuScreen.StageSelect)), playText, TitleCell));
            titleArrows.Add(NewArrow(TitleCell + new Vector2(32f, 0f), Dir.Left, () => StartCoroutine(GoAcross(MenuScreen.Settings)), settingText, TitleCell));

            // 설정: 돌아가는 칸의 ▶ 아래 Back, 그리고 설정 줄 3개.
            settingsArrows.Add(NewArrow(SettingsCell + new Vector2(-32f, 0f), Dir.Right, () => StartCoroutine(GoAcross(MenuScreen.Title)), backText, SettingsCell));
            var top = Settings.menuSettingsRowTopLeft;
            float gap = Settings.menuSettingsRowSpacing;
            AddSettingRow(new SettingRow
            {
                Name = musicText, TopLeft = top, Count = SaveData.VolumeSteps, FirstCell = 0, Wrap = true,
                Value = SaveData.Music, Save = v => SaveData.Music = v, Text = v => v.ToString()
            }, musicProp);
            AddSettingRow(new SettingRow
            {
                Name = soundText, TopLeft = top + new Vector2(0f, gap), Count = SaveData.VolumeSteps, FirstCell = 0, Wrap = true,
                Value = SaveData.Sound, Save = v => SaveData.Sound = v, Text = v => v.ToString()
            }, soundProp);
            // 이동 속도: 돌, 매우 느림, 느림, 보통, 빠름, 돌.
            AddSettingRow(new SettingRow
            {
                Name = speedText, TopLeft = top + new Vector2(0f, gap * 2f), Count = SaveData.SpeedScales.Length, FirstCell = 1, Wrap = false,
                Value = SaveData.Speed, Save = v => SaveData.Speed = v, Text = v => SaveData.SpeedNames[v]
            }, speedProp);

            // 스테이지 선택: 고른 줄의 ▶(풀로 들어가 시작), ◀(타이틀로), 땅 열의 위아래 끝(BuildRows가 자리를 정한다).
            var grassCell = StageCell + new Vector2(32f, 0f);
            stageArrows.Add(NewArrow(StageCell + new Vector2(-32f, 0f), Dir.Right, () => StartCoroutine(EnterSelected()), null, StageCell, grassCell));
            stageArrows.Add(NewArrow(grassCell + new Vector2(32f, 0f), Dir.Left, () => StartCoroutine(GoAcross(MenuScreen.Title)), null, StageCell, grassCell));
            columnUp = NewArrow(StageCell, Dir.Up, () => StartCoroutine(Scroll(1)), null);
            columnDown = NewArrow(StageCell, Dir.Down, () => StartCoroutine(Scroll(-1)), null);
            stageArrows.Add(columnUp);
            stageArrows.Add(columnDown);
        }

        // ── 열고 닫기 ──

        /// <summary>메뉴를 연다. stage는 스테이지 선택 화면에서 양을 놓을 스테이지(0부터)다.</summary>
        public void Open(MenuScreen at, int stage)
        {
            StopAllCoroutines();
            busy = false;
            IsOpen = true;
            root.gameObject.SetActive(true);

            BuildLayout();
            unlocked = SaveData.Unlocked(stageCount);
            entries = unlocked < stageCount ? unlocked + 1 : unlocked;
            selected = Mathf.Clamp(stage, 0, unlocked - 1);
            BuildRows();

            screen = at;
            PlaceSheep(SheepCell(at));
            if (sheep != null)
            {
                sheep.SetAlpha(1f);
                sheep.SetFlip(false);
            }

            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = ScreenHeight * 0.5f * Pixel;
                cam.backgroundColor = Color.black;
                cam.transform.position = CameraPosition(at);
            }
            ShowLine(null);
        }

        public void Close()
        {
            StopAllCoroutines();
            busy = false;
            IsOpen = false;
            root.gameObject.SetActive(false);
        }

        // ── 매 프레임 ──

        private void Update()
        {
            if (!IsOpen || busy || cam == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 screenPos = mouse.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            bool click = mouse.leftButton.wasPressedThisFrame;

            // 화살표는 게임과 같이 마우스를 올려야 보인다. 타이틀은 아래 글자에 올려도 보인다.
            var active = ArrowsOf(screen);
            Arrow shown = null, clicked = null;
            foreach (var arrow in AllArrows())
            {
                bool usable = active.Contains(arrow) && IsUsable(arrow);
                bool overCell = usable && Contains(arrow.Cell, world);
                bool overLabel = usable && arrow.Label != null && LabelContains(arrow.Label, world);
                bool visible = overCell || overLabel;
                if (arrow.Renderer != null) arrow.Renderer.enabled = visible;
                if (visible) shown = arrow;
                if (overCell && click) clicked = arrow;
            }
            ShowLine(shown);
            if (clicked != null)
            {
                foreach (var arrow in AllArrows()) if (arrow.Renderer != null) arrow.Renderer.enabled = false;
                ShowLine(null);
                clicked.OnClick();
            }
        }

        private IEnumerable<Arrow> AllArrows()
        {
            foreach (var a in titleArrows) yield return a;
            foreach (var a in stageArrows) yield return a;
            foreach (var a in settingsArrows) yield return a;
        }

        private List<Arrow> ArrowsOf(MenuScreen s)
        {
            return s == MenuScreen.Title ? titleArrows : s == MenuScreen.StageSelect ? stageArrows : settingsArrows;
        }

        /// <summary>땅 열이 한 칸뿐이면(열린 스테이지가 하나이고 잠긴 줄도 없음) 위아래로 밀 것이 없다.</summary>
        private bool IsUsable(Arrow arrow)
        {
            if (arrow == columnUp || arrow == columnDown) return entries > 1;
            return true;
        }

        // ── 화면 넘기기 ──

        /// <summary>
        /// 양이 줄을 따라 옆 화면으로 건너간다. 빈칸을 건너뛰는 shift처럼
        /// 진행 방향으로 한 칸 나가며 사라지고, 도착 칸의 한 칸 앞에서 나타나며 들어온다. 그동안 카메라가 옮겨 간다.
        /// 두 칸의 높이가 다르면 사라졌다가 다른 높이에서 나타난다.
        /// </summary>
        private IEnumerator GoAcross(MenuScreen to)
        {
            busy = true;
            var from = screen;
            float seconds = Settings.menuPanSeconds;
            Vector3 start = CellBase(SheepCell(from)), end = CellBase(SheepCell(to));
            Vector3 dir = to > from ? Vector3.right : Vector3.left;
            if (sheep != null) sheep.SetFlip(to < from);
            Vector3 camFrom = CameraPosition(from), camTo = CameraPosition(to);

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(Settings.moveCurve.Evaluate(t / seconds));
                if (cam != null) cam.transform.position = Vector3.Lerp(camFrom, camTo, k);
                if (sheep != null)
                {
                    if (k < 0.5f)
                    {
                        sheep.Place(Vector3.Lerp(start, start + dir, k));
                        sheep.SetAlpha(1f - k * 2f);
                    }
                    else
                    {
                        sheep.Place(Vector3.Lerp(end - dir, end, k));
                        sheep.SetAlpha(k * 2f - 1f);
                    }
                }
                yield return null;
            }

            if (cam != null) cam.transform.position = camTo;
            screen = to;
            PlaceSheep(SheepCell(to));
            if (sheep != null) sheep.SetAlpha(1f);
            busy = false;
        }

        /// <summary>고른 줄을 오른쪽으로 민다. 풀이면 양이 들어가 그 스테이지를 연다. 잠긴 줄은 돌에 막힌다.</summary>
        private IEnumerator EnterSelected()
        {
            busy = true;
            Vector3 start = CellBase(StageCell);
            if (sheep != null) sheep.SetFlip(false);
            float seconds = Settings.moveSeconds * SaveData.SpeedScale;

            if (selected >= unlocked)
            {
                // 막힘: 돌 쪽으로 살짝 나갔다가 돌아온다.
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    float k = Mathf.Sin(Mathf.Clamp01(t / seconds) * Mathf.PI) * 0.2f;
                    if (sheep != null) sheep.Place(start + Vector3.right * k);
                    yield return null;
                }
                PlaceSheep(StageCell);
                busy = false;
                yield break;
            }

            Vector3 end = start + Vector3.right;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(Settings.moveCurve.Evaluate(t / seconds));
                if (sheep != null) sheep.Place(Vector3.Lerp(start, end, k));
                yield return null;
            }
            if (sheep != null)
            {
                sheep.Place(end);
                sheep.PlayAction("happy", true);
            }
            yield return new WaitForSeconds(Settings.menuEnterDelaySeconds);

            busy = false;
            int stage = selected;
            SaveData.SetLastStage(stage);
            if (StageChosen != null) StageChosen(stage);
        }

        /// <summary>
        /// 땅 열을 민다. step이 1이면 위로(위쪽의 다음 스테이지로), -1이면 아래로.
        /// 양과 카메라는 그대로 두고 줄들이 반대로 흘러, 고른 줄이 늘 같은 자리에 온다.
        /// </summary>
        private IEnumerator Scroll(int step)
        {
            busy = true;
            float seconds = Settings.moveSeconds * SaveData.SpeedScale;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(Settings.moveCurve.Evaluate(t / seconds));
                foreach (var row in rows) PlaceRow(row, row.K - step * k);
                yield return null;
            }
            selected = Mod(selected + step, entries);
            BuildRows();
            busy = false;
        }

        // ── 설정 줄 ──

        /// <summary>설정 줄 하나를 그린다: 땅 칸(막는 줄은 양 끝 돌), 오브젝트, 위쪽 이름과 값, 양 끝 바깥의 화살표.</summary>
        private void AddSettingRow(SettingRow row, string prop)
        {
            int cells = row.Count + row.FirstCell * 2;
            var line = new List<Vector2>();
            for (int i = 0; i < cells; i++)
            {
                var cell = row.TopLeft + new Vector2(32f * i, 0f);
                bool stone = !row.Wrap && (i < row.FirstCell || i >= row.FirstCell + row.Count);
                PlaceTile("ground", cell, 0);
                if (stone) PlaceTile("stone", cell, 5);
                line.Add(cell);
            }

            row.Prop = BoardView.NewRenderer(layoutRoot, "setting " + row.Name, 10);
            row.Prop.sprite = PropSprite(prop);
            row.Prop.transform.position = CellBase(row.Cell(row.Value));

            row.Label = NewLabel(layoutRoot, "", TextAlignmentOptions.Left, new Vector2(0f, 0f));
            row.Label.transform.position = Px(row.TopLeft.x, row.TopLeft.y - 2f);
            UpdateSettingLabel(row);

            var left = NewArrow(row.TopLeft + new Vector2(-32f, 0f), Dir.Right, () => StartCoroutine(ShiftSetting(row, 1)), null, line.ToArray());
            var right = NewArrow(row.TopLeft + new Vector2(32f * cells, 0f), Dir.Left, () => StartCoroutine(ShiftSetting(row, -1)), null, line.ToArray());
            settingsArrows.Add(left);
            settingsArrows.Add(right);
        }

        private void UpdateSettingLabel(SettingRow row)
        {
            if (row.Label != null) row.Label.text = row.Name + "  " + row.Text(row.Value);
        }

        /// <summary>"시트:태그" 형식의 이름으로 그림 한 장을 고른다. 태그가 없으면 첫 프레임.</summary>
        private Sprite PropSprite(string prop)
        {
            var parts = prop.Split(':');
            var sheet = catalog.Get(parts[0]);
            if (sheet == null) return null;
            return parts.Length > 1 ? sheet.TagFrame(parts[1]) : sheet.Frame(0);
        }

        /// <summary>
        /// 설정 줄을 민다. step이 1이면 오른쪽, -1이면 왼쪽.
        /// 순환하는 줄은 끝에서 한 칸 나가며 사라지고 반대쪽 끝으로 들어온다. 돌로 막힌 줄은 끝에서 움찔하고 멈춘다.
        /// </summary>
        private IEnumerator ShiftSetting(SettingRow row, int step)
        {
            busy = true;
            float seconds = Settings.moveSeconds * SaveData.SpeedScale;
            int from = row.Value, to = from + step;
            Vector3 start = CellBase(row.Cell(from));
            Vector3 dir = step > 0 ? Vector3.right : Vector3.left;
            var t0 = row.Prop.transform;

            if (to < 0 || to >= row.Count)
            {
                if (!row.Wrap)
                {
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        t0.position = start + dir * (Mathf.Sin(Mathf.Clamp01(t / seconds) * Mathf.PI) * 0.2f);
                        yield return null;
                    }
                    t0.position = start;
                    busy = false;
                    yield break;
                }

                // 순환: 진행 방향으로 한 칸 나가며 사라지고, 반대쪽 끝의 한 칸 바깥에서 나타나며 들어온다.
                to = Mod(to, row.Count);
                Vector3 end = CellBase(row.Cell(to));
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    float k = Mathf.Clamp01(Settings.moveCurve.Evaluate(t / seconds));
                    if (k < 0.5f)
                    {
                        t0.position = Vector3.Lerp(start, start + dir, k);
                        row.Prop.color = new Color(1f, 1f, 1f, 1f - k * 2f);
                    }
                    else
                    {
                        t0.position = Vector3.Lerp(end - dir, end, k);
                        row.Prop.color = new Color(1f, 1f, 1f, k * 2f - 1f);
                    }
                    yield return null;
                }
                row.Prop.color = Color.white;
            }
            else
            {
                Vector3 end = CellBase(row.Cell(to));
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    t0.position = Vector3.Lerp(start, end, Mathf.Clamp01(Settings.moveCurve.Evaluate(t / seconds)));
                    yield return null;
                }
            }

            row.Value = to;
            t0.position = CellBase(row.Cell(to));
            row.Save(to);
            UpdateSettingLabel(row);
            busy = false;
        }

        // ── 스테이지 줄 ──

        /// <summary>
        /// 고른 줄(k = 0)을 가운데에 두고 위아래로 menuStageRowsAround줄씩 그린다. 위쪽(+k)이 다음 스테이지다.
        /// 흐를 때 들어오고 나가는 줄을 위해 양쪽 끝 밖에 한 줄씩 더 만들어 흐리게 둔다.
        /// 열린 스테이지가 적으면 같은 스테이지가 두 번 나오지 않게 가까운 줄부터 채운다.
        /// </summary>
        private void BuildRows()
        {
            foreach (var row in rows) Destroy(row.Root);
            rows.Clear();

            var used = new HashSet<int>();
            int around = Mathf.Max(0, Settings.menuStageRowsAround);
            kLow = 0;
            kHigh = 0;
            AddRow(0, used);
            for (int d = 1; d <= around; d++)
            {
                if (AddRow(d, used)) kHigh = d;
                if (AddRow(-d, used)) kLow = -d;
            }
            AddRow(kHigh + 1, used);
            AddRow(kLow - 1, used);
            foreach (var row in rows) PlaceRow(row, row.K);

            // 땅 열의 화살표: 위쪽 끝은 아래로 미는 ▼, 아래쪽 끝은 위로 미는 ▲.
            columnDown.Cell = CellAt(kHigh + 1);
            columnUp.Cell = CellAt(kLow - 1);
            columnDown.Line.Clear();
            columnUp.Line.Clear();
            for (int k = kLow; k <= kHigh; k++)
            {
                columnDown.Line.Add(CellAt(k));
                columnUp.Line.Add(CellAt(k));
            }
            UpdateArrowPosition(columnUp);
            UpdateArrowPosition(columnDown);
            UpdateBubble();
        }

        // ── 꿈 풍선 ──

        /// <summary>
        /// 고른 스테이지의 이름, 얻은 별(3개 중 몇 개), 3별 조건, 맵 미리보기를 꿈 풍선 안에 그린다.
        /// 잠긴 스테이지는 번호와 Locked만 적는다.
        /// </summary>
        private void UpdateBubble()
        {
            if (bubbleContent == null) return;
            for (int i = bubbleContent.childCount - 1; i >= 0; i--) Destroy(bubbleContent.GetChild(i).gameObject);
            if (selected < 0 || selected >= levels.Count) return;

            var s = Settings;
            var level = levels[selected];
            bool locked = selected >= unlocked;

            // 이름
            string title = (selected + 1) + ". " + (locked ? lockedText : level.Rules.Name);
            var titleLabel = NewBubbleLabel(title, TextAlignmentOptions.Center, new Vector2(0.5f, 1f));
            titleLabel.transform.position = BubblePx(s.menuBubbleNameTop);
            if (locked) return;

            // 얻은 별: 3칸 중 얻은 만큼 별, 나머지는 빈 별.
            var icons = catalog.Get("hud_icons");
            int stars = SaveData.Stars(selected);
            const float iconSize = 16f, iconGap = 2f;
            float starsWidth = iconSize * 3f + iconGap * 2f;
            for (int i = 0; i < 3; i++)
            {
                var icon = icons == null ? null : icons.TagFrame(i < stars ? "star" : "star_empty");
                var topLeft = s.menuBubbleStarsTop + new Vector2(-starsWidth * 0.5f + i * (iconSize + iconGap), 0f);
                PlaceBubbleSprite(icon, topLeft, iconSize, -27);
            }

            // 3별 조건: 턴 아이콘 + "3 stars in N turns". 둘을 묶어 가운데에 맞춘다. @stars가 없는 스테이지는 줄을 비운다.
            if (level.Rules.Star3Turns <= 0)
            {
                DrawMinimap(level, s.menuBubbleMapCenter);
                return;
            }
            var condition = NewBubbleLabel(string.Format(threeStarFormat, level.Rules.Star3Turns), TextAlignmentOptions.Left, new Vector2(0f, 0.5f));
            float textWidth = condition.GetPreferredValues(condition.text).x / Pixel;
            float groupWidth = iconSize + 3f + textWidth;
            var groupLeft = s.menuBubbleConditionTop + new Vector2(-groupWidth * 0.5f, 0f);
            PlaceBubbleSprite(icons == null ? null : icons.TagFrame("turn"), groupLeft, iconSize, -27);
            condition.transform.position = BubblePx(groupLeft + new Vector2(iconSize + 3f, iconSize * 0.5f));

            // 맵 미리보기: 칸 하나 16px. 지형을 먼저 깔고 그 위에 오브젝트 표시를 얹는다.
            DrawMinimap(level, s.menuBubbleMapCenter);
        }

        /// <summary>minimap_tiles의 프레임 번호: 0–7 지형(TerrainType 순서와 같다), 8 구멍, 9– 오브젝트.</summary>
        private void DrawMinimap(Board level, Vector2 center)
        {
            var tiles = catalog.Get("minimap_tiles");
            if (tiles == null) return;
            const float cell = 16f;
            var origin = center - new Vector2(level.Width * cell * 0.5f, level.Height * cell * 0.5f);

            foreach (var tile in level.Tiles)
            {
                if (tile.Kind == TileKind.OuterBlank) continue;
                var topLeft = origin + new Vector2(tile.Pos.Col * cell, tile.Pos.Row * cell);
                if (tile.Kind == TileKind.InnerBlank)
                {
                    PlaceBubbleSprite(tiles.Frame(8), topLeft, cell, -29);
                    continue;
                }

                // 돌·나무·울타리·들불·재는 땅 위에 얹는 그림이라 땅을 먼저 깐다.
                var type = tile.Terrain.Type;
                PlaceBubbleSprite(tiles.Frame(0), topLeft, cell, -29);
                if (type != TerrainType.Ground) PlaceBubbleSprite(tiles.Frame((int)type), topLeft, cell, -28);

                foreach (var o in tile.Objects)
                {
                    int frame = MinimapFrame(o);
                    if (frame >= 0) PlaceBubbleSprite(tiles.Frame(frame), topLeft, cell, -27);
                }
            }
        }

        private static int MinimapFrame(ObjectEntity o)
        {
            if (o is Sheep) return 9;
            if (o is Wolf) return 10;
            if (o is Goat) return 11;
            if (o is Horse) return 12;
            if (o is Cow) return 13;
            if (o is FruitBasket) return 14;
            if (o is Bucket) return 15;
            return -1;
        }

        /// <summary>풍선 캔버스 기준 픽셀의 월드 좌표.</summary>
        private Vector3 BubblePx(Vector2 local)
        {
            var p = Settings.menuBubbleTopLeft + local;
            return Px(p.x, p.y);
        }

        /// <summary>size×size 캔버스의 그림을 왼쪽 위가 topLeft(풍선 기준)에 오게 놓는다. 피벗은 캔버스 아래쪽 가운데다.</summary>
        private void PlaceBubbleSprite(Sprite sprite, Vector2 topLeft, float size, int order)
        {
            if (sprite == null) return;
            var renderer = BoardView.NewRenderer(bubbleContent, sprite.name, order);
            renderer.sprite = sprite;
            renderer.transform.position = BubblePx(topLeft + new Vector2(size * 0.5f, size));
        }

        private TextMeshPro NewBubbleLabel(string text, TextAlignmentOptions align, Vector2 pivot)
        {
            var label = NewLabel(bubbleContent, text, align, pivot);
            label.fontSize = Settings.menuBubbleFontSize;
            label.color = bubbleTextColor;
            label.sortingOrder = -26;
            return label;
        }

        private Vector2 CellAt(float k) => StageCell - new Vector2(0f, 32f * k);

        private bool AddRow(int k, HashSet<int> used)
        {
            if (entries <= 0) return false;
            int entry = Mod(selected + k, entries);
            if (!used.Add(entry)) return false;

            var row = new Row { K = k };
            row.Root = new GameObject("stage row " + (entry + 1));
            row.Root.transform.SetParent(rowRoot, false);

            bool locked = entry >= unlocked;
            row.Sprites.AddRange(TileSprites("ground", row.Root.transform, 0));
            row.Sprites.AddRange(TileSprites("ground", row.Root.transform, 0, 1));
            row.Sprites.AddRange(TileSprites(locked ? "stone" : "grass", row.Root.transform, locked ? 5 : 1, 1));

            // 번호는 그 줄의 풀(잠기면 돌) 칸 위에 얹는다. 땅 칸은 양이 서 있어 가린다.
            // 깬 스테이지는 노란색, 나머지는 하얀색이다.
            row.Label = NewLabel(row.Root.transform, (entry + 1).ToString(), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
            row.Label.transform.localPosition = new Vector3(48f * Pixel, -16f * Pixel, 0f);
            row.Label.color = SaveData.IsCleared(entry) ? clearedLabelColor : labelColor;
            row.Label.outlineWidth = 0.25f;
            row.Label.outlineColor = numberOutlineColor;
            row.Label.sortingOrder = 6;

            rows.Add(row);
            return true;
        }

        /// <summary>줄을 높이 k(고른 줄 0, 위로 +1씩)에 놓는다. 보이는 범위 밖은 한 줄에 걸쳐 흐려진다.</summary>
        private void PlaceRow(Row row, float k)
        {
            // row.Root는 땅 칸의 왼쪽 위에 두고, 칸 그림은 그 기준으로 놓았다(TileSprites).
            var cell = CellAt(k);
            row.Root.transform.position = Px(cell.x, cell.y);
            float alpha = k < kLow ? 1f - (kLow - k) : k > kHigh ? 1f - (k - kHigh) : 1f;
            alpha = Mathf.Clamp01(alpha);
            foreach (var r in row.Sprites) r.color = new Color(1f, 1f, 1f, alpha);
            if (row.Label != null)
            {
                var c = row.Label.color;
                c.a = alpha;
                row.Label.color = c;
            }
        }

        // ── 줄 표시 ──

        private void ShowLine(Arrow arrow)
        {
            int count = arrow == null ? 0 : arrow.Line.Count;
            while (highlights.Count < count && prefabs.lineHighlight != null)
            {
                var go = Instantiate(prefabs.lineHighlight, highlightRoot);
                go.name = "highlight";
                SetOrder(go, 3);
                highlights.Add(go);
            }
            for (int i = 0; i < highlights.Count; i++)
            {
                highlights[i].SetActive(i < count);
                if (i < count) highlights[i].transform.position = CellBase(arrow.Line[i]);
            }
        }

        // ── 도우미: 만들기 ──

        private Arrow NewArrow(Vector2 cell, Dir dir, Action onClick, string label, params Vector2[] line)
        {
            var arrow = new Arrow { Cell = cell, Dir = dir, OnClick = onClick };
            arrow.Line.AddRange(line);
            if (prefabs.shiftArrow != null)
            {
                arrow.Object = Instantiate(prefabs.shiftArrow, layoutRoot);
                arrow.Object.name = "menu arrow " + dir;
                arrow.Object.transform.rotation = Quaternion.Euler(0f, 0f, DirAngle(dir));
                arrow.Renderer = arrow.Object.GetComponentInChildren<SpriteRenderer>();
                var sheet = catalog.Get("shift_arrow");
                if (arrow.Renderer != null)
                {
                    if (sheet != null) arrow.Renderer.sprite = sheet.TagFrame("hover");
                    arrow.Renderer.sortingOrder = 20;
                    arrow.Renderer.enabled = false;
                }
            }
            if (label != null)
            {
                // 글자는 화살표 칸 바로 아래 가운데.
                arrow.Label = NewLabel(layoutRoot, label, TextAlignmentOptions.Center, new Vector2(0.5f, 1f));
                arrow.Label.transform.position = Px(cell.x + 16f, cell.y + 34f);
            }
            UpdateArrowPosition(arrow);
            return arrow;
        }

        private void UpdateArrowPosition(Arrow arrow)
        {
            if (arrow.Object != null) arrow.Object.transform.position = Px(arrow.Cell.x + 16f, arrow.Cell.y + 16f);
        }

        private TextMeshPro NewLabel(Transform parent, string text, TextAlignmentOptions align, Vector2 pivot)
        {
            var go = new GameObject("label " + text);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.fontSize = Settings.menuLabelFontSize;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.pivot = pivot;
            label.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            label.color = labelColor;
            label.sortingOrder = 20;
            label.text = text;
            return label;
        }

        /// <summary>한 칸짜리 지형 그림을 parent 아래에 놓는다. parent는 칸의 왼쪽 위이고, column은 거기서 오른쪽으로 몇 칸인지다.</summary>
        private List<SpriteRenderer> TileSprites(string sheetName, Transform parent, int order, int column = 0)
        {
            var list = new List<SpriteRenderer>();
            var prefab = prefabs.Terrain(sheetName);
            if (prefab == null) return list;
            var go = Instantiate(prefab, parent);
            go.name = sheetName;
            // 그림의 피벗은 칸의 아래쪽 가운데다.
            go.transform.localPosition = new Vector3((column * 32f + 16f) * Pixel, -32f * Pixel, 0f);
            var sheet = catalog.Get(sheetName);
            foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                r.sortingOrder = order;
                // 연결 타일(풀)은 이웃이 없는 그림(0번)으로 둔다.
                if (sheetName == "grass" && sheet != null) r.sprite = sheet.Frame(0);
                list.Add(r);
            }
            return list;
        }

        private void PlaceTile(string sheetName, Vector2 cell, int order)
        {
            var holder = new GameObject(sheetName + " " + cell);
            holder.transform.SetParent(layoutRoot, false);
            holder.transform.position = Px(cell.x, cell.y);
            TileSprites(sheetName, holder.transform, order);
        }

        /// <summary>
        /// 스프라이트 한 장을 그 캔버스의 왼쪽 위가 topLeft에 오게 놓는다. 피벗은 캔버스의 아래쪽 가운데다.
        /// Importer가 투명한 가장자리를 잘라 내므로 캔버스 크기는 따로 받는다.
        /// </summary>
        private void PlaceSheet(string sheetName, Vector2 topLeft, Vector2 canvasSize, int order)
        {
            var sheet = catalog.Get(sheetName);
            var sprite = sheet == null ? null : sheet.Frame(0);
            if (sprite == null) return;
            var renderer = BoardView.NewRenderer(layoutRoot, sheetName, order);
            renderer.sprite = sprite;
            renderer.transform.position = Px(topLeft.x + canvasSize.x * 0.5f, topLeft.y + canvasSize.y);
        }

        // ── 도우미: 좌표 ──

        /// <summary>메뉴 그림의 픽셀 (x, y)의 월드 좌표.</summary>
        private Vector3 Px(float x, float y)
        {
            return new Vector3(origin.x + x * Pixel, origin.y - y * Pixel, 0f);
        }

        /// <summary>칸의 아래쪽 가운데. 오브젝트 그림의 피벗이 오는 곳이다.</summary>
        private Vector3 CellBase(Vector2 cell) => Px(cell.x + 16f, cell.y + 32f);

        private bool Contains(Vector2 cell, Vector3 world)
        {
            Vector3 min = Px(cell.x, cell.y + 32f), max = Px(cell.x + 32f, cell.y);
            return world.x >= min.x && world.x <= max.x && world.y >= min.y && world.y <= max.y;
        }

        private static bool LabelContains(TextMeshPro label, Vector3 world)
        {
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer == null) return false;
            var b = renderer.bounds;
            b.Expand(new Vector3(0.15f, 0.15f, 10f));
            return b.Contains(new Vector3(world.x, world.y, b.center.z));
        }

        private Vector2 SheepCell(MenuScreen s)
        {
            return s == MenuScreen.Title ? TitleCell : s == MenuScreen.StageSelect ? StageCell : SettingsCell;
        }

        private void PlaceSheep(Vector2 cell)
        {
            if (sheep != null) sheep.Place(CellBase(cell));
        }

        private Vector3 CameraPosition(MenuScreen s)
        {
            int index = (int)s;
            var p = Px(ScreenWidth * (index + 0.5f), ScreenHeight * 0.5f);
            p.z = -10f;
            return p;
        }

        private static float DirAngle(Dir dir)
        {
            switch (dir)
            {
                case Dir.Up: return 90f;
                case Dir.Left: return 180f;
                case Dir.Down: return -90f;
                default: return 0f;
            }
        }

        private static int Mod(int a, int n) => n <= 0 ? 0 : ((a % n) + n) % n;

        private static Transform NewChild(Transform parent, string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void SetOrder(GameObject go, int order)
        {
            foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sortingOrder = order;
        }
    }
}
