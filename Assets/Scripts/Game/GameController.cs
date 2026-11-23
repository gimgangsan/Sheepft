using System.Collections;
using System.Collections.Generic;
using Sheepft.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Sheepft.Game
{
    /// <summary>
    /// 씬 진행. 레벨을 읽어 화면을 만들고, 입력을 받아 한 턴을 두고, 일어난 일을 재생한다.
    /// 규칙은 모두 Sheepft.Core가 계산한다. 여기서는 결과를 보여 주기만 한다.
    /// 맵은 PrefabCatalog의 프리팹으로, UI는 씬의 GameUI(Canvas)로 그린다.
    /// </summary>
    [RequireComponent(typeof(SpriteCatalog))]
    [RequireComponent(typeof(PrefabCatalog))]
    public sealed class GameController : MonoBehaviour
    {
        private const int ArrowOrder = 1000;

        [Tooltip("켜 두면 타이틀 메뉴부터 시작한다. 끄면 Start Stage를 바로 연다(개발용).")]
        public bool openMenuOnStart = true;

        /// <summary>메뉴 없이 바로 열 스테이지. 0부터 센다.</summary>
        public int startStage;

        [Tooltip("씬의 UI. 비워 두면 씬에서 찾는다.")]
        public GameUI ui;

        private SpriteCatalog catalog;
        private PrefabCatalog prefabs;
        private Camera cam;
        private BoardView boardView;
        private GameSettings Settings => catalog.Settings;

        private int stageIndex;
        private int stageCount;
        private GameSession session;

        /// <summary>줄 끝 하나의 표시. shift 포인트가 있으면 화살표, 없으면 X자다.</summary>
        private sealed class Arrow
        {
            public ShiftPoint Point;
            /// <summary>shift 포인트가 있는 끝인가. false면 X자를 늘 보여 주고 입력을 받지 않는다.</summary>
            public bool Open;
            public Vector3 Center;
            public SpriteRenderer Renderer;
        }

        private readonly List<Arrow> arrows = new List<Arrow>();
        private Transform arrowRoot;
        /// <summary>마우스를 올린 수로 걷는 동물이 턴 끝에 어디 있을지 보여 준다.</summary>
        private ArrivalMarkers arrivals;
        private Coroutine playback;
        /// <summary>둔 수의 재생이 끝나면 승패 소리를 낸다. 재생을 끝까지 보거나 빨리 감았을 때 한 번만.</summary>
        private bool resultSoundPending;
        /// <summary>수를 두기 전에 이미 승리 불가였는가. 처음 승리 불가가 될 때만 패배 소리를 낸다.</summary>
        private bool cannotWinBeforeMove;
        private float aspect;

        private MenuController menu;

        private Board Board => session.Board;

        // ── 시작 ──

        private void Start()
        {
            catalog = GetComponent<SpriteCatalog>();
            prefabs = GetComponent<PrefabCatalog>();
            cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.backgroundColor = Settings.backgroundColor;
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            boardView = NewChild("board").gameObject.AddComponent<BoardView>();
            arrowRoot = NewChild("arrows");
            arrivals = NewChild("arrivals").gameObject.AddComponent<ArrivalMarkers>();
            arrivals.Init(catalog);
            SoundManager.Init(Settings.sounds);

            if (ui == null) ui = FindFirstObjectByType<GameUI>();
            if (ui != null)
            {
                ui.Init(Settings);
                ui.UndoClicked += Undo;
                ui.RestartClicked += Restart;
                ui.NextClicked += NextStage;
                ui.LevelSelectClicked += OpenMenuFromGame;
                ui.LevelChosen += LoadStage;
            }
            else
            {
                Debug.LogWarning("[Sheepft] 씬에 GameUI가 없다. 메뉴 Sheepft > Setup Game Scene을 돌린다.");
            }

            stageCount = LevelParser.ParseAll(catalog.stages.text).Count;
            // 저장 파일을 읽는다. stages.txt가 바뀌었으면 진행 기록을 지운다(설정은 남음).
            SaveData.Init(catalog.stages.text);

            // 메뉴는 같은 씬의 먼 자리에 있고, 카메라를 옮겨 오간다(구현 계획 11절).
            menu = NewChild("menu").gameObject.AddComponent<MenuController>();
            var font = ui != null && ui.titleText != null ? ui.titleText.font : null;
            menu.Init(catalog, prefabs, cam, font, stageCount);
            menu.StageChosen += LoadStage;

#if UNITY_EDITOR
            // 스테이지 에디터의 ▶ Play: 메뉴를 건너뛰고 그 스테이지를 연다. 넘겨받은 값은 한 번만 쓴다.
            int editorStage = UnityEditor.SessionState.GetInt(EditorPlayStageKey, -1);
            UnityEditor.SessionState.EraseInt(EditorPlayStageKey);
            if (editorStage >= 0)
            {
                LoadStage(Mathf.Clamp(editorStage, 0, stageCount - 1));
                return;
            }
#endif
            if (openMenuOnStart) OpenMenu(MenuScreen.Title, SaveData.LastStage(stageCount));
            else LoadStage(Mathf.Clamp(startStage, 0, stageCount - 1));
        }

        /// <summary>스테이지 에디터가 Play 전에 SessionState에 적어 두는 스테이지 번호(0부터)의 키. 에디터 전용.</summary>
        public const string EditorPlayStageKey = "Sheepft.EditorPlayStage";

        private Transform NewChild(string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private void LoadStage(int index)
        {
            StopPlayback();
            CloseMenu();
            stageIndex = index;
            SaveData.SetLastStage(index);
            // 맵은 진행하면서 바뀌므로, 스테이지를 열 때마다 파일에서 새로 읽는다.
            session = new GameSession(LevelParser.ParseAll(catalog.stages.text)[index]);
            boardView.Build(catalog, prefabs, Board);
            BuildArrows();
            FitCamera();
            if (ui != null) ui.ShowStage(index + 1, Board.Rules.Name, Board.Rules.Description);
            RefreshUi();
            SoundManager.PlayGameMusic();
        }

        // ── 배치 ──

        private void BuildArrows()
        {
            for (int i = arrowRoot.childCount - 1; i >= 0; i--) Destroy(arrowRoot.GetChild(i).gameObject);
            arrows.Clear();
            if (prefabs.shiftArrow == null) return;

            // 모든 줄의 양 끝에 표시를 하나씩 둔다. 화살표가 한꺼번에 떠 있으면 어지러우므로
            // shift 포인트가 있는 끝은 마우스를 올렸을 때만 화살표를 보이고, 없는 끝에는 X자를 늘 보인다.
            var open = new HashSet<ShiftPoint>(Board.Rules.ShiftPoints);
            var ends = new List<ShiftPoint>();
            for (int r = 0; r < Board.Height; r++)
            {
                ends.Add(new ShiftPoint(Axis.Row, r, Dir.Right));
                ends.Add(new ShiftPoint(Axis.Row, r, Dir.Left));
            }
            for (int c = 0; c < Board.Width; c++)
            {
                ends.Add(new ShiftPoint(Axis.Col, c, Dir.Down));
                ends.Add(new ShiftPoint(Axis.Col, c, Dir.Up));
            }

            var sheet = catalog.Get("shift_arrow");
            foreach (var point in ends)
            {
                bool isOpen = open.Contains(point);
                // 화살표는 미는 쪽의 격자 가장자리 바깥에 두고 진행 방향을 가리킨다 (구현 계획 5절).
                Pos cell;
                switch (point.Dir)
                {
                    case Dir.Right: cell = new Pos(point.Index, -1); break;
                    case Dir.Left: cell = new Pos(point.Index, Board.Width); break;
                    case Dir.Down: cell = new Pos(-1, point.Index); break;
                    default: cell = new Pos(Board.Height, point.Index); break;
                }

                // 프리팹의 그림은 오른쪽을 가리킨다. 칸 가운데를 축으로 돌린다. X자는 돌리지 않는다.
                var go = Instantiate(prefabs.shiftArrow, arrowRoot);
                go.name = (isOpen ? "arrow " : "blocked ") + point;
                go.transform.position = BoardView.TileCenter(cell);
                go.transform.rotation = isOpen ? Quaternion.Euler(0f, 0f, DirAngle(point.Dir)) : Quaternion.identity;
                var renderer = go.GetComponentInChildren<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = ArrowOrder;
                    if (sheet != null) renderer.sprite = sheet.TagFrame(isOpen ? "hover" : "blocked");
                    if (isOpen) renderer.enabled = false;
                }

                arrows.Add(new Arrow { Point = point, Open = isOpen, Center = go.transform.position, Renderer = renderer });
            }
        }

        /// <summary>
        /// 맵과 화살표(사방으로 한 칸)가 UI의 위쪽 줄과 아래쪽 줄 사이에 들어오게 카메라를 맞춘다.
        /// </summary>
        private void FitCamera()
        {
            if (cam == null) return;

            // 맵은 x 0..너비, y -높이..0.
            float left = -1f, right = Board.Width + 1f;
            float bottom = -Board.Height - 1f, top = 1f;
            float margin = Settings.cameraMargin;
            float halfHeight = (top - bottom) * 0.5f + margin;
            float halfWidth = (right - left) * 0.5f + margin;

            float topBar = 0f, bottomBar = 0f;
            if (ui != null && ui.referenceHeight > 0f)
            {
                topBar = ui.topBarPixels / ui.referenceHeight;
                bottomBar = ui.bottomBarPixels / ui.referenceHeight;
            }
            float free = Mathf.Max(0.1f, 1f - topBar - bottomBar);

            aspect = cam.aspect;
            float size = Mathf.Max(halfHeight / free, halfWidth / aspect);
            cam.orthographicSize = size;
            // 화면 높이는 2 × size다. 위쪽 줄이 더 두꺼우면 맵을 그만큼 아래로 내린다.
            float shift = (topBar - bottomBar) * size;
            cam.transform.position = new Vector3((left + right) * 0.5f, (top + bottom) * 0.5f + shift, -10f);
        }

        // ── 매 프레임 ──

        private void Update()
        {
            // 메뉴가 열려 있으면 메뉴가 입력을 받는다.
            if (menu != null && menu.IsOpen) return;
            if (session == null) return;
            if (cam != null && !Mathf.Approximately(cam.aspect, aspect)) FitCamera();

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            Vector3 world = Vector3.zero;
            if (mouse != null && cam != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
                world.z = 0f;
            }

            // 레벨 선택 화면이 열려 있으면 그쪽 버튼만 받는다. Esc로 닫는다.
            if (ui != null && ui.IsLevelSelectOpen)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ui.CloseLevelSelect();
                boardView.ShowLine(null);
                arrivals.Hide();
                return;
            }

            if (keyboard != null)
            {
                // Esc: 메뉴의 스테이지 선택 화면으로. 양은 하던 스테이지 줄에 놓인다.
                if (keyboard.escapeKey.wasPressedThisFrame) { OpenMenuFromGame(); return; }
                // 개발용 키: 이전·다음 스테이지.
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

            UpdateArrows(world, click);
        }

        private void UpdateArrows(Vector3 world, bool click)
        {
            bool playing = Board.Result == GameResult.InProgress;
            // 마우스가 UI 위에 있으면 그 밑의 화살표는 반응하지 않는다.
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Arrow hovered = null;
            foreach (var arrow in arrows)
            {
                if (!arrow.Open) continue;
                var d = world - arrow.Center;
                bool over = playing && !overUi && Mathf.Abs(d.x) <= 0.5f && Mathf.Abs(d.y) <= 0.5f;
                if (over) hovered = arrow;
                // 화살표는 마우스를 올린 동안에만 보인다.
                if (arrow.Renderer != null) arrow.Renderer.enabled = over;
            }

            boardView.ShowLine(hovered == null ? null : Board.Line(hovered.Point.Axis, hovered.Point.Index));
            // 그 수를 두면 늑대·말·소가 턴 끝에 어디 있을지 미리 보인다.
            if (hovered != null) arrivals.Show(Board, hovered.Point);
            else arrivals.Hide();
            if (hovered != null && click) Play(hovered.Point);
        }

        // ── 한 턴 ──

        private void Play(ShiftPoint move)
        {
            // 풀 위의 양 수는 재생이 끝난 뒤에 바꾼다. 남은 턴만 바로 줄인다.
            int sheepBefore = Board.SheepOnGrass();
            bool cannotWinBefore = Board.CannotWin;
            if (!session.Play(move)) return;
            SoundManager.Play(Sound.Shift);
            // 장애물에 막혀 제자리에 남은 오브젝트가 있다. 이 표시는 플레이어의 shift에서만 켜진다.
            if ((Board.Mechanics & Mechanic.Blocked) != 0) SoundManager.Play(Sound.Impact);
            cannotWinBeforeMove = cannotWinBefore;
            resultSoundPending = true;
            if (Board.Result == GameResult.Won) SaveData.RecordClear(stageIndex, Stars(), stageCount);
            boardView.ShowLine(null);
            HideArrows();
            if (ui != null) ui.ShowCounts(Board.Rules.TurnLimit - Board.TurnsUsed, sheepBefore, Board.Rules.TargetSheep, false);
            playback = StartCoroutine(PlayEvents(new List<GameEvent>(Board.Events), move));
        }

        private void Undo()
        {
            StopPlayback();
            if (!session.Undo()) return;
            SoundManager.Play(Sound.Click);
            boardView.SyncAll(Board);
            RefreshUi();
        }

        private void Restart()
        {
            StopPlayback();
            session.Restart();
            SoundManager.Play(Sound.Click);
            boardView.SyncAll(Board);
            RefreshUi();
        }

        private void NextStage()
        {
            if (stageIndex >= stageCount - 1) return;
            SoundManager.Play(Sound.Click);
            LoadStage(stageIndex + 1);
        }

        // ── 메뉴 ──

        /// <summary>게임 중 Esc나 레벨 선택 버튼: 메뉴의 스테이지 선택 화면, 양은 하던 스테이지 줄.</summary>
        private void OpenMenuFromGame()
        {
            SoundManager.Play(Sound.Click);
            // 재생 중에 나가면 그 수의 승패 소리는 내지 않는다.
            resultSoundPending = false;
            if (playback != null) FinishPlayback();
            OpenMenu(MenuScreen.StageSelect, stageIndex);
        }

        private void OpenMenu(MenuScreen screen, int stage)
        {
            StopPlayback();
            if (boardView != null) boardView.ShowLine(null);
            HideArrows();
            if (ui != null)
            {
                ui.CloseLevelSelect();
                ui.SetVisible(false);
            }
            menu.Open(screen, stage);
            SoundManager.PlayTitleMusic();
        }

        private void CloseMenu()
        {
            if (menu == null || !menu.IsOpen) return;
            menu.Close();
            if (ui != null) ui.SetVisible(true);
            if (cam != null) cam.backgroundColor = Settings.backgroundColor;
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
            PlayResultSound();
        }

        private IEnumerator PlayStep(List<GameEvent> step, ShiftPoint move)
        {
            var moves = new List<MovedEvent>();
            foreach (var e in step)
            {
                var moved = e as MovedEvent;
                if (moved != null && boardView.View(moved.Id) != null) moves.Add(moved);
            }

            // 염소의 공격: 로직은 네 방향을 한꺼번에 처리했지만, 화면은 한 방향씩 차례로 박치기한다.
            foreach (var e in step)
            {
                var attacked = e as AttackedEvent;
                if (attacked != null && boardView.View(attacked.Id) != null)
                    yield return PlayGoatAttack(boardView.View(attacked.Id), attacked.Dirs, moves, move);
            }

            // 소가 앞 칸을 밀며 걸으면 `push` 동작을 쓴다.
            bool cowPush = false;
            foreach (var e in step)
            {
                var pushed = e as MovedEvent;
                if (pushed != null && pushed.Cause == MoveCause.CowPush)
                {
                    if (!cowPush)
                    {
                        boardView.SpawnEffect("cow_impact", pushed.From, DirAngle(DirBetween(pushed.From, pushed.To)));
                        SoundManager.Play(Sound.Impact);
                    }
                    cowPush = true;
                }
            }
            string walkTag = cowPush ? "push" : "walk";

            if (moves.Count > 0)
            {
                foreach (var m in moves)
                {
                    if (m.To.Col != m.From.Col || m.Cause == MoveCause.Shift && move.Axis == Axis.Row)
                        boardView.View(m.Id).SetFlip(FacesLeft(m, move));
                    if (m.Cause == MoveCause.Walk)
                    {
                        boardView.View(m.Id).PlayAction(walkTag, false);
                        SoundManager.Play(Sound.Step);
                    }
                }
                // 말이 돌아섰으면 등에 탄 것도 반대쪽 등으로 옮긴다. 아직 모두 출발 칸에 있으므로 다시 배치해도 된다.
                boardView.Layout();

                float moveSeconds = Settings.moveSeconds * SaveData.SpeedScale;
                for (float t = 0f; t < moveSeconds; t += Time.deltaTime)
                {
                    float k = Mathf.Clamp01(Settings.moveCurve.Evaluate(t / moveSeconds));
                    foreach (var m in moves) PlaceMoving(m, move, k);
                    yield return null;
                }
                foreach (var m in moves) EndMove(m, walkTag);
                boardView.Layout();
            }

            bool other = false;
            foreach (var e in step)
            {
                var status = e as StatusChangedEvent;
                if (status != null)
                {
                    var view = boardView.View(status.Id);
                    if (view != null)
                    {
                        // 양동이는 물이 차고 비는 것만 본다(같은 단계에 젖음도 함께 바뀌므로 두 번 나오지 않게).
                        bool water = status.Status == StatusKind.Filled
                            || status.Status == StatusKind.Wet && !HasFilledChange(step, status.Id);
                        if (water)
                        {
                            boardView.SpawnEffect(status.On ? "splash" : "steam", view.Pos);
                            SoundManager.Play(status.On ? Sound.Splash : Sound.Sizzle);
                        }
                        if (status.Status == StatusKind.Burned && status.On) SoundManager.Play(Sound.Fire);
                        view.ApplyStatus(status.Status, status.On);
                    }
                    other = true;
                }
                var target = e as TargetChangedEvent;
                if (target != null)
                {
                    var view = boardView.View(target.Id);
                    if (view != null)
                    {
                        view.SetTarget(target.TargetId >= 0);
                        // 바구니를 찾은 순간 운다: 말은 발견, 소는 냄새.
                        if (target.TargetId >= 0 && view.Sheet == "horse") SoundManager.Play(Sound.HorseNeigh);
                        if (target.TargetId >= 0 && view.Sheet == "cow") SoundManager.Play(Sound.CowMoo);
                    }
                    other = true;
                }
                var rider = e as RiderChangedEvent;
                if (rider != null)
                {
                    var view = boardView.View(rider.Id);
                    if (view != null) view.RiderId = rider.RiderId;
                    other = true;
                }
                var count = e as CountChangedEvent;
                if (count != null)
                {
                    var view = boardView.View(count.Id);
                    if (view != null) view.SetCount(count.Count);
                    other = true;
                }
                var terrain = e as TerrainChangedEvent;
                if (terrain != null)
                {
                    boardView.SetTerrain(terrain.Pos, terrain.To);
                    if (terrain.To == TerrainType.Fire)
                    {
                        boardView.SpawnEffect("ignite", terrain.Pos);
                        SoundManager.Play(Sound.Fire);
                    }
                    other = true;
                }
                var removed = e as RemovedEvent;
                if (removed != null)
                {
                    var view = boardView.View(removed.Id);
                    if (view != null)
                    {
                        boardView.SpawnEffect(RemoveEffect(view, removed.Cause), view.Pos);
                        PlayRemoveSound(view, removed.Cause);
                    }
                    boardView.RemoveView(removed.Id);
                    other = true;
                }
            }
            if (other)
            {
                boardView.Layout();
                yield return new WaitForSeconds(Settings.stepPauseSeconds * SaveData.SpeedScale);
            }
        }

        /// <summary>이동이 끝난 오브젝트를 도착 칸에 놓는다.</summary>
        private void EndMove(MovedEvent m, string walkTag)
        {
            var view = boardView.View(m.Id);
            view.Pos = m.To;
            view.SetAlpha(1f);
            view.StopAction(walkTag);
            // 양이 풀에 도착했다.
            if (view.Sheet == "sheep" && !view.Wet && !view.Burned && boardView.ShowsGrass(m.To))
            {
                boardView.SpawnEffect("sheep_arrive", m.To);
                SoundManager.Play(Sound.SheepArrive);
            }
        }

        /// <summary>
        /// 염소가 공격한 방향마다 차례로: 그 방향으로 짧게 튀어 나갔다가 빠르게 돌아온다.
        /// 부딪히는 순간 그 방향에 충격파가 나오고, 그 방향으로 밀린 오브젝트가 움직이기 시작한다.
        /// 여기서 보여 준 이동은 moves에서 뺀다.
        /// </summary>
        private IEnumerator PlayGoatAttack(EntityView goat, Dir[] dirs, List<MovedEvent> moves, ShiftPoint move)
        {
            var s = Settings;
            Vector3 home = goat.BasePosition;
            foreach (var dir in dirs)
            {
                // 밀려나는 방향이 곧 공격 방향이다(염소는 바깥으로 1칸씩 민다).
                var pushed = moves.FindAll(m => m.Cause == MoveCause.Push && DirBetween(m.From, m.To) == dir);
                foreach (var m in pushed) moves.Remove(m);

                if (dir == Dir.Left || dir == Dir.Right) goat.SetFlip(dir == Dir.Left);
                goat.PlayAction("attack", true);

                // 이동 속도 설정(SaveData.SpeedScale)이 모든 시간에 곱해진다.
                float scale = SaveData.SpeedScale;
                float moveSeconds = s.moveSeconds * scale, returnSeconds = s.goatReturnSeconds * scale;
                Vector3 lunge = DirVector(dir) * s.goatLungeDistance;
                float hit = s.goatLungeSeconds * scale;
                float end = Mathf.Max(hit + returnSeconds, hit + (pushed.Count > 0 ? moveSeconds : 0f));
                bool impact = false;
                for (float t = 0f; t < end; t += Time.deltaTime)
                {
                    // 나갈 때는 점점 빠르게(가속), 돌아올 때는 일정하게 빠르게.
                    float offset = t < hit ? Mathf.Pow(t / hit, 2f)
                        : Mathf.Clamp01(1f - (t - hit) / returnSeconds);
                    goat.Place(home + lunge * offset);

                    if (t >= hit)
                    {
                        if (!impact)
                        {
                            impact = true;
                            boardView.SpawnEffect("goat_shockwave", goat.Pos.Step(dir), DirAngle(dir));
                            SoundManager.Play(Sound.Impact);
                        }
                        float k = Mathf.Clamp01(s.moveCurve.Evaluate((t - hit) / moveSeconds));
                        foreach (var m in pushed) PlaceMoving(m, move, k);
                    }
                    yield return null;
                }

                // 프레임이 길어 부딪히는 순간을 건너뛰었어도 이펙트는 나온다.
                if (!impact)
                {
                    boardView.SpawnEffect("goat_shockwave", goat.Pos.Step(dir), DirAngle(dir));
                    SoundManager.Play(Sound.Impact);
                }
                goat.Place(home);
                foreach (var m in pushed) EndMove(m, "walk");
                boardView.Layout();
                home = goat.BasePosition;
                if (s.goatAttackGapSeconds > 0f) yield return new WaitForSeconds(s.goatAttackGapSeconds * scale);
            }
        }

        private static bool HasFilledChange(List<GameEvent> step, int id)
        {
            foreach (var e in step)
            {
                var status = e as StatusChangedEvent;
                if (status != null && status.Id == id && status.Status == StatusKind.Filled) return true;
            }
            return false;
        }

        private static string RemoveEffect(EntityView view, RemoveCause cause)
        {
            switch (cause)
            {
                case RemoveCause.Eaten: return view.Sheet == "sheep" ? "sheep_eaten" : "eat_crumbs";
                case RemoveCause.Merged: return "wolf_merge_dust";
                default: return "smoke";
            }
        }

        private static void PlayRemoveSound(EntityView view, RemoveCause cause)
        {
            switch (cause)
            {
                case RemoveCause.Eaten:
                    if (view.Sheet == "sheep")
                    {
                        // 늑대의 덥석과 양의 "메"를 같이 낸다. 양 소리는 SoundLibrary에서 조금 늦춰 둔다.
                        SoundManager.Play(Sound.WolfEat);
                        SoundManager.Play(Sound.SheepEaten);
                    }
                    else
                    {
                        SoundManager.Play(Sound.Eat);
                    }
                    break;
                case RemoveCause.Merged:
                    SoundManager.Play(Sound.Impact);
                    break;
                default:
                    SoundManager.Play(Sound.Fire);
                    break;
            }
        }

        /// <summary>
        /// 둔 수의 재생이 끝났을 때(끝까지 보거나 빨리 감음) 승패 소리를 한 번 낸다.
        /// undo·재시작·스테이지 열기에서는 부르지 않는다.
        /// </summary>
        private void PlayResultSound()
        {
            if (!resultSoundPending) return;
            resultSoundPending = false;
            if (Board.Result == GameResult.Won) SoundManager.Play(Sound.Win);
            else if (Board.Result == GameResult.Lost) SoundManager.Play(Sound.Lose);
            else if (Board.CannotWin && !cannotWinBeforeMove) SoundManager.Play(Sound.Lose);
        }

        private static Dir DirBetween(Pos from, Pos to)
        {
            if (to.Row < from.Row) return Dir.Up;
            if (to.Row > from.Row) return Dir.Down;
            return to.Col < from.Col ? Dir.Left : Dir.Right;
        }

        /// <summary>오른쪽을 보는 그림을 그 방향으로 돌리는 각도.</summary>
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
            PlayResultSound();
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
            if (ui != null)
            {
                bool cannotWin = Board.CannotWin && Board.Result != GameResult.Won;
                ui.ShowCounts(Board.Rules.TurnLimit - Board.TurnsUsed, Board.SheepOnGrass(), Board.Rules.TargetSheep, cannotWin);
                ui.ShowResult(Board.Result, Stars(), stageIndex < stageCount - 1);
            }
            HideArrows();
        }

        /// <summary>쓴 턴 수로 정하는 별 수. 별 1개는 제한 턴 이내다. 구현 계획 2절.</summary>
        private int Stars()
        {
            var rules = Board.Rules;
            if (Board.TurnsUsed <= rules.Star3Turns) return 3;
            if (Board.TurnsUsed <= rules.Star2Turns) return 2;
            return 1;
        }

        /// <summary>
        /// 화살표와 도착 칸 표시를 모두 숨긴다. 다음 프레임에 마우스가 올라가 있는 것만 다시 보인다. X자는 그대로 둔다.
        /// 판이 바뀔 때(수를 둠, undo, 재시작, 스테이지 열기)마다 불리므로 미리보기도 여기서 지운다.
        /// </summary>
        private void HideArrows()
        {
            if (arrivals != null) arrivals.Hide();
            foreach (var arrow in arrows)
                if (arrow.Open && arrow.Renderer != null) arrow.Renderer.enabled = false;
        }
    }
}
