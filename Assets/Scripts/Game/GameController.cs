using System.Collections;
using System.Collections.Generic;
using Sheepft.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sheepft.Game
{
    /// <summary>
    /// 씬 진행. 레벨을 읽어 화면을 만들고, 입력을 받아 한 턴을 두고, 일어난 일을 재생한다.
    /// 규칙은 모두 Sheepft.Core가 계산한다. 여기서는 결과를 보여 주기만 한다.
    /// </summary>
    [RequireComponent(typeof(SpriteCatalog))]
    public sealed class GameController : MonoBehaviour
    {
        private const float MoveSeconds = 0.18f;
        private const float StepPauseSeconds = 0.10f;
        private const int UiOrder = 1000;

        private static readonly Color Background = new Color32(0x2b, 0x24, 0x33, 0xff);

        /// <summary>처음 열 스테이지. 0부터 센다.</summary>
        public int startStage;

        private SpriteCatalog catalog;
        private Camera cam;
        private BoardView boardView;
        private Hud hud;
        private Transform uiRoot;

        private int stageIndex;
        private int stageCount;
        private GameSession session;

        private sealed class Arrow
        {
            public ShiftPoint Point;
            public Vector3 Center;
            public SpriteRenderer Renderer;
        }

        private readonly List<Arrow> arrows = new List<Arrow>();
        private Transform arrowRoot;
        private SpriteButton undoButton, restartButton, nextButton;
        private SpriteButton pressedButton;
        private Coroutine playback;
        private float aspect;

        private Board Board => session.Board;

        // ── 시작 ──

        private void Start()
        {
            catalog = GetComponent<SpriteCatalog>();
            cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.backgroundColor = Background;
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            boardView = NewChild("board").gameObject.AddComponent<BoardView>();
            arrowRoot = NewChild("arrows");
            uiRoot = NewChild("ui");
            hud = NewChild("hud").gameObject.AddComponent<Hud>();
            hud.Setup(catalog);

            undoButton = new SpriteButton(uiRoot, catalog.Get("button_undo"), "undo", UiOrder, Undo);
            restartButton = new SpriteButton(uiRoot, catalog.Get("button_restart"), "restart", UiOrder, Restart);
            nextButton = new SpriteButton(uiRoot, catalog.Get("button_next"), "next", UiOrder, NextStage);

            stageCount = LevelParser.ParseAll(catalog.stages.text).Count;
            LoadStage(Mathf.Clamp(startStage, 0, stageCount - 1));
        }

        private Transform NewChild(string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private void LoadStage(int index)
        {
            StopPlayback();
            stageIndex = index;
            // 맵은 진행하면서 바뀌므로, 스테이지를 열 때마다 파일에서 새로 읽는다.
            session = new GameSession(LevelParser.ParseAll(catalog.stages.text)[index]);
            boardView.Build(catalog, Board);
            BuildArrows();
            PlaceUi();
            RefreshUi();
        }

        // ── 배치 ──

        private void BuildArrows()
        {
            for (int i = arrowRoot.childCount - 1; i >= 0; i--) Destroy(arrowRoot.GetChild(i).gameObject);
            arrows.Clear();

            var sheet = catalog.Get("shift_arrow");
            foreach (var point in Board.Rules.ShiftPoints)
            {
                // 화살표는 미는 쪽의 격자 가장자리 바깥에 두고 진행 방향을 가리킨다 (구현 계획 5절).
                Pos cell;
                float angle;
                switch (point.Dir)
                {
                    case Dir.Right: cell = new Pos(point.Index, -1); angle = 0f; break;
                    case Dir.Left: cell = new Pos(point.Index, Board.Width); angle = 180f; break;
                    case Dir.Down: cell = new Pos(-1, point.Index); angle = -90f; break;
                    default: cell = new Pos(Board.Height, point.Index); angle = 90f; break;
                }

                // 그림은 오른쪽을 가리키고 피벗이 아래쪽 가운데다. 칸 가운데를 축으로 돌리려고 한 겹 감싼다.
                var pivot = new GameObject("arrow " + point);
                pivot.transform.SetParent(arrowRoot, false);
                pivot.transform.position = BoardView.TileCenter(cell);
                pivot.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                var renderer = BoardView.NewRenderer(pivot.transform, "sprite", UiOrder);
                renderer.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                renderer.sprite = sheet == null ? null : sheet.TagFrame("normal");

                arrows.Add(new Arrow { Point = point, Center = pivot.transform.position, Renderer = renderer });
            }
        }

        private void PlaceUi()
        {
            // 맵은 x 0..너비, y -높이..0. 화살표가 사방으로 한 칸, 그 위에 HUD 한 줄이 더 있다.
            float left = -1f, right = Board.Width + 1f;
            float bottom = -Board.Height - 1f, top = 1f;
            float hudY = top + 0.25f;
            float hudTop = hudY + 1f;

            hud.Place(new Vector3(left, hudY, 0f), new Vector3((left + right) * 0.5f, hudY, 0f));
            restartButton.SetCenter(new Vector3(right - 0.5f, hudY + 0.5f, 0f));
            undoButton.SetCenter(new Vector3(right - 1.6f, hudY + 0.5f, 0f));
            nextButton.SetCenter(new Vector3((left + right) * 0.5f, hudY + 0.5f, 0f));

            FitCamera(left, right, bottom, hudTop);
        }

        private void FitCamera(float left, float right, float bottom, float top)
        {
            if (cam == null) return;
            const float margin = 0.25f;
            float halfHeight = (top - bottom) * 0.5f + margin;
            float halfWidth = (right - left) * 0.5f + margin;
            aspect = cam.aspect;
            cam.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);
            cam.transform.position = new Vector3((left + right) * 0.5f, (top + bottom) * 0.5f, -10f);
        }

        // ── 매 프레임 ──

        private void Update()
        {
            if (session == null) return;
            if (cam != null && !Mathf.Approximately(cam.aspect, aspect)) PlaceUi();

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool release = mouse != null && mouse.leftButton.wasReleasedThisFrame;
            Vector3 world = Vector3.zero;
            if (mouse != null && cam != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
                world.z = 0f;
            }

            if (keyboard != null)
            {
                // 레벨 선택 화면이 생길 때까지 쓰는 임시 키.
                if (keyboard.leftBracketKey.wasPressedThisFrame && stageIndex > 0) { LoadStage(stageIndex - 1); return; }
                if (keyboard.rightBracketKey.wasPressedThisFrame && stageIndex < stageCount - 1) { LoadStage(stageIndex + 1); return; }
            }

            // 재생 중에는 입력을 막는다. 클릭하거나 키를 누르면 끝까지 빨리 감는다.
            if (playback != null)
            {
                bool anyKey = keyboard != null && keyboard.anyKey.wasPressedThisFrame;
                if (click || anyKey) FinishPlayback();
                return;
            }

            if (keyboard != null)
            {
                if (keyboard.zKey.wasPressedThisFrame || keyboard.uKey.wasPressedThisFrame) { Undo(); return; }
                if (keyboard.rKey.wasPressedThisFrame) { Restart(); return; }
                bool next = keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
                if (next && Board.Result == GameResult.Won) { NextStage(); return; }
            }

            UpdateButtons(world, click, release);
            UpdateArrows(world, click);
        }

        private void UpdateButtons(Vector3 world, bool click, bool release)
        {
            foreach (var button in new[] { undoButton, restartButton, nextButton })
            {
                bool over = button.Contains(world);
                if (click && over) pressedButton = button;
                button.SetState(pressedButton == button ? "pressed" : over ? "hover" : "normal");
            }

            if (release && pressedButton != null)
            {
                var button = pressedButton;
                pressedButton = null;
                if (button.Contains(world)) button.OnClick();
            }

            // 패배하면 undo와 재시작을 눈에 띄게 한다.
            bool lost = Board.Result == GameResult.Lost;
            float pulse = lost ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.time * 4f)) : 1f;
            undoButton.SetPulse(pulse);
            restartButton.SetPulse(pulse);
        }

        private void UpdateArrows(Vector3 world, bool click)
        {
            var sheet = catalog.Get("shift_arrow");
            bool playing = Board.Result == GameResult.InProgress;
            Arrow hovered = null;
            foreach (var arrow in arrows)
            {
                var d = world - arrow.Center;
                bool over = playing && pressedButton == null && Mathf.Abs(d.x) <= 0.5f && Mathf.Abs(d.y) <= 0.5f;
                if (over) hovered = arrow;
                if (sheet != null) arrow.Renderer.sprite = sheet.TagFrame(!playing ? "disabled" : over ? "hover" : "normal");
            }

            boardView.ShowLine(hovered == null ? null : Board.Line(hovered.Point.Axis, hovered.Point.Index));
            if (hovered != null && click) Play(hovered.Point);
        }

        // ── 한 턴 ──

        private void Play(ShiftPoint move)
        {
            if (!session.Play(move)) return;
            boardView.ShowLine(null);
            SetArrowTag("disabled");
            RefreshHud();
            playback = StartCoroutine(PlayEvents(new List<GameEvent>(Board.Events), move));
        }

        private void Undo()
        {
            StopPlayback();
            if (!session.Undo()) return;
            boardView.SyncAll(Board);
            RefreshUi();
        }

        private void Restart()
        {
            StopPlayback();
            session.Restart();
            boardView.SyncAll(Board);
            RefreshUi();
        }

        private void NextStage()
        {
            if (stageIndex < stageCount - 1) LoadStage(stageIndex + 1);
        }

        // ── 이벤트 재생 ──

        /// <summary>
        /// 로직은 한 턴을 이미 끝까지 계산했다. 남긴 기록을 StepEnd 단위로 끊어 차례로 보여 준다.
        /// 한 단계 안의 이동은 동시에 움직이고, 그 밖의 변화는 이동이 끝난 뒤 한꺼번에 나타난다.
        /// </summary>
        private IEnumerator PlayEvents(List<GameEvent> events, ShiftPoint move)
        {
            var step = new List<GameEvent>();
            foreach (var e in events)
            {
                if (!(e is StepEndEvent)) { step.Add(e); continue; }
                if (step.Count == 0) continue;
                yield return PlayStep(step, move);
                step.Clear();
            }
            if (step.Count > 0) yield return PlayStep(step, move);

            playback = null;
            boardView.SyncAll(Board);
            RefreshUi();
        }

        private IEnumerator PlayStep(List<GameEvent> step, ShiftPoint move)
        {
            var moves = new List<MovedEvent>();
            foreach (var e in step)
            {
                var moved = e as MovedEvent;
                if (moved != null && boardView.View(moved.Id) != null) moves.Add(moved);
            }

            if (moves.Count > 0)
            {
                foreach (var m in moves)
                {
                    if (m.To.Col != m.From.Col || m.Cause == MoveCause.Shift && move.Axis == Axis.Row)
                        boardView.View(m.Id).SetFlip(FacesLeft(m, move));
                }

                for (float t = 0f; t < MoveSeconds; t += Time.deltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / MoveSeconds);
                    foreach (var m in moves) PlaceMoving(m, move, k);
                    yield return null;
                }
                foreach (var m in moves)
                {
                    var view = boardView.View(m.Id);
                    view.Pos = m.To;
                    view.SetAlpha(1f);
                }
                boardView.Layout();
            }

            bool other = false;
            foreach (var e in step)
            {
                var status = e as StatusChangedEvent;
                if (status != null)
                {
                    var view = boardView.View(status.Id);
                    if (view != null) view.ApplyStatus(status.Status, status.On);
                    other = true;
                }
                var terrain = e as TerrainChangedEvent;
                if (terrain != null) { boardView.SetTerrain(terrain.Pos, terrain.To); other = true; }
                var removed = e as RemovedEvent;
                if (removed != null) { boardView.RemoveView(removed.Id); other = true; }
            }
            if (other)
            {
                boardView.Layout();
                yield return new WaitForSeconds(StepPauseSeconds);
            }
        }

        private static bool FacesLeft(MovedEvent m, ShiftPoint move)
        {
            if (m.Cause == MoveCause.Shift && move.Axis == Axis.Row) return move.Dir == Dir.Left;
            return m.To.Col < m.From.Col;
        }

        /// <summary>
        /// 이동 중인 오브젝트를 k(0–1) 지점에 놓는다.
        /// shift로 빈칸을 뛰어넘거나 순환한 이동은 From과 To가 이웃이 아니다.
        /// 그때는 진행 방향으로 한 칸 나가며 사라지고, 도착 칸의 반대편에서 한 칸 들어오며 나타난다.
        /// </summary>
        private void PlaceMoving(MovedEvent m, ShiftPoint move, float k)
        {
            var view = boardView.View(m.Id);
            Vector3 from = BoardView.TileBase(m.From), to = BoardView.TileBase(m.To);
            bool adjacent = Mathf.Abs(m.From.Row - m.To.Row) + Mathf.Abs(m.From.Col - m.To.Col) == 1;
            bool wrapsBack = m.Cause == MoveCause.Shift && adjacent && !MovesAlong(m, move.Dir);

            if (adjacent && !wrapsBack)
            {
                view.Place(Vector3.Lerp(from, to, k));
                return;
            }

            Vector3 dir = m.Cause == MoveCause.Shift ? DirVector(move.Dir) : (to - from).normalized;
            if (k < 0.5f)
            {
                view.Place(Vector3.Lerp(from, from + dir, k));
                view.SetAlpha(1f - k * 2f);
            }
            else
            {
                view.Place(Vector3.Lerp(to - dir, to, k));
                view.SetAlpha(k * 2f - 1f);
            }
        }

        /// <summary>실제 칸이 둘뿐인 줄에서는 순환한 이동도 이웃 칸으로 간다. 방향이 반대이면 순환이다.</summary>
        private static bool MovesAlong(MovedEvent m, Dir dir)
        {
            return m.From.Step(dir) == m.To;
        }

        private static Vector3 DirVector(Dir dir)
        {
            switch (dir)
            {
                case Dir.Up: return Vector3.up;
                case Dir.Right: return Vector3.right;
                case Dir.Down: return Vector3.down;
                default: return Vector3.left;
            }
        }

        private void FinishPlayback()
        {
            StopPlayback();
            boardView.SyncAll(Board);
            RefreshUi();
        }

        private void StopPlayback()
        {
            if (playback == null) return;
            StopCoroutine(playback);
            playback = null;
        }

        // ── 표시 ──

        private void RefreshUi()
        {
            RefreshHud();
            undoButton.Visible = true;
            restartButton.Visible = true;
            nextButton.Visible = Board.Result == GameResult.Won && stageIndex < stageCount - 1;
            SetArrowTag(Board.Result == GameResult.InProgress ? "normal" : "disabled");
        }

        private void RefreshHud()
        {
            bool cannotWin = Board.CannotWin && Board.Result != GameResult.Won;
            hud.Show(Board.Rules.TurnLimit - Board.TurnsUsed, Board.SheepOnGrass(), Board.Rules.TargetSheep, cannotWin);
        }

        private void SetArrowTag(string tag)
        {
            var sheet = catalog.Get("shift_arrow");
            if (sheet == null) return;
            foreach (var arrow in arrows) arrow.Renderer.sprite = sheet.TagFrame(tag);
        }
    }
}
