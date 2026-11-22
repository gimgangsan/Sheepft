using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sheepft.Core;
using Sheepft.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sheepft.Tools
{
    /// <summary>
    /// 메뉴 Sheepft > Stage Editor. stages.txt를 창에서 고친다. STAGE_TOOLS_PLAN.md 6절.
    ///
    /// 편집은 StageFile(직렬화 가능한 값)에 하고, Save를 누를 때만 파일에 쓴다. 저장할 때 모든 스테이지를 게임 파서로
    /// 읽어 보고 LevelWriter로 정돈해 쓴다. 되돌리기는 Unity의 Undo(Ctrl+Z / Ctrl+Y)를 쓴다: 고치기 전에 이 창을
    /// Undo.RecordObject로 기록한다. 드래그 한 번은 Undo 한 번이다.
    /// </summary>
    public sealed class StageEditorWindow : EditorWindow
    {
        private const string StagesPath = "Assets/Levels/stages.txt";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        private enum ToolKind { Terrain, Object, Erase }

        private static readonly char[] TerrainSymbols = { '.', '#', '~', '^', 'g', 'f', 't', '_', '-', 'x' };
        private static readonly string[] TerrainNames = { "땅", "돌", "물", "들불", "풀", "울타리", "나무", "재", "외곽 빈칸", "중간 빈칸" };
        private static readonly char[] ObjectLetters = { 'S', 'W', 'G', 'H', 'M', 'A', 'B', 'U' };
        private static readonly string[] ObjectNames = { "양", "늑대", "염소", "말", "소", "과일 바구니", "빈 양동이", "물 찬 양동이" };

        // ── 편집 상태 (도메인 재로드에도 남고, Undo가 되돌리는 값) ──
        [SerializeField] private StageFile file;
        [SerializeField] private int selected;
        [SerializeField] private int moveTarget = 1;

        [SerializeField] private ToolKind tool = ToolKind.Terrain;
        [SerializeField] private char terrainSymbol = '.';
        [SerializeField] private char objectLetter = 'S';
        [SerializeField] private char objectState = '\0';
        [SerializeField] private float zoom = 1.5f;

        private Vector2 listScroll, mainScroll, resultScroll;

        // ── 저장 상태. Undo가 되돌리면 안 되므로 직렬화하지 않는다(도메인 재로드 뒤에는 디스크에서 다시 구한다) ──
        [NonSerialized] private string savedJson;
        [NonSerialized] private string loadedText;
        [NonSerialized] private string loadError;
        [NonSerialized] private string loadWarning;

        // ── 화면 전용 ──
        [NonSerialized] private StageSprites sprites;
        [NonSerialized] private bool dirty;
        [NonSerialized] private int hoverRow = -1, hoverCol = -1;
        [NonSerialized] private int dragFrom = -1;

        // ── 검증·Prune·채우기 (백그라운드) ──
        [NonSerialized] private CancellationTokenSource cts;
        [NonSerialized] private volatile bool busy;
        [NonSerialized] private bool busyShown; // Layout 이벤트 때의 busy. 화면 구성은 이 값으로 정한다
        [NonSerialized] private int focusStage = -1, focusCount = -1; // 글자 칸 포커스를 풀 때 비교하는 지난 스테이지
        [NonSerialized] private volatile string busyLabel;
        [NonSerialized] private string resultText = "";
        [NonSerialized] private Action pendingApply; // 백그라운드 결과를 메인 스레드에서 적용
        [NonSerialized] private readonly object gate = new object();
        [NonSerialized] private System.Diagnostics.Stopwatch busyWatch;

        [MenuItem("Sheepft/Stage Editor")]
        private static void Open()
        {
            var w = GetWindow<StageEditorWindow>();
            w.minSize = new Vector2(820, 560);
        }

        private void OnEnable()
        {
            saveChangesMessage = "stages.txt에 저장하지 않은 변경이 있다. 저장할까?";
            wantsMouseMove = true;
            if (file == null || file.stages == null) LoadFromDisk();
            else RecoverSavedState();
            RefreshDirty();
            EditorApplication.update += Tick;
            UnityEditor.Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedo;
            if (cts != null) cts.Cancel();
        }

        /// <summary>도메인 재로드(Play, 컴파일) 뒤: 편집 중인 내용은 남아 있으므로 "저장된 상태"만 디스크에서 다시 구한다.</summary>
        private void RecoverSavedState()
        {
            loadedText = ReadDisk();
            savedJson = null;
            if (loadedText == null) return;
            try
            {
                List<int> dropped;
                savedJson = JsonUtility.ToJson(StageFile.Load(loadedText, out dropped));
            }
            catch (LevelParseException) { }
        }

        private void OnUndoRedo()
        {
            if (file == null) return;
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, file.stages.Count - 1));
            ClearTextFocus();
            Changed();
        }

        private void OnFocus()
        {
            // 저장 안 한 변경이 없을 때 파일이 밖에서 바뀌었으면 다시 읽는다.
            if (dirty || file == null) return;
            string disk = ReadDisk();
            if (disk != null && disk != loadedText) LoadFromDisk();
        }

        private void Tick()
        {
            Action apply = null;
            lock (gate)
            {
                apply = pendingApply;
                pendingApply = null;
            }
            if (apply != null) { apply(); Repaint(); }
            if (busy) Repaint();
        }

        public override void SaveChanges()
        {
            if (Save()) base.SaveChanges();
        }

        // ── 파일 ──

        private static string ReadDisk()
        {
            try { return File.ReadAllText(StagesPath); }
            catch (IOException) { return null; }
        }

        private void LoadFromDisk()
        {
            loadError = null;
            loadWarning = null;
            string text = ReadDisk();
            if (text == null) { loadError = StagesPath + " 를 읽지 못했다."; return; }
            try
            {
                List<int> dropped;
                file = StageFile.Load(text, out dropped);
                if (dropped.Count > 0)
                    loadWarning = "저장하면 붙일 곳이 없는 주석이 사라진다. 줄: " + string.Join(", ", dropped);
            }
            catch (LevelParseException e)
            {
                file = null;
                loadError = StagesPath + " 를 읽지 못했다. 텍스트로 고친 뒤 Reload를 누른다.\n" + e.Message;
                return;
            }
            loadedText = text;
            savedJson = JsonUtility.ToJson(file);
            UnityEditor.Undo.ClearUndo(this); // 다시 읽은 뒤에는 예전 편집으로 되돌아가지 않는다
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, file.stages.Count - 1));
            resultText = "";
            RefreshDirty();
        }

        private bool Save()
        {
            if (file == null) return false;
            var errors = new List<string>();
            string text = file.ToFileText(errors);
            if (text == null)
            {
                EditorUtility.DisplayDialog("Stage Editor", "저장하지 못했다. 고칠 스테이지:\n\n" + string.Join("\n", errors), "OK");
                return false;
            }
            File.WriteAllText(StagesPath, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(StagesPath);
            loadedText = text;
            savedJson = JsonUtility.ToJson(file);
            RefreshDirty();
            return true;
        }

        private void RefreshDirty()
        {
            dirty = file != null && JsonUtility.ToJson(file) != savedJson;
            hasUnsavedChanges = dirty;
            titleContent = new GUIContent(dirty ? "Stage Editor *" : "Stage Editor");
        }

        // ── 되돌리기 ──

        /// <summary>고치기 직전에 부른다. Unity Undo가 이 창의 편집 상태를 기록한다. 한 번의 마우스·키 입력 안의 기록은 하나로 묶인다.</summary>
        private void Record(string what)
        {
            UnityEditor.Undo.RecordObject(this, "Stage Editor: " + (what ?? "edit"));
        }

        private void Changed()
        {
            RefreshDirty();
            Repaint();
        }

        /// <summary>
        /// 지금 이벤트가 끝난 뒤에 실행한다. IMGUI는 Layout 이벤트에서 잡은 컨트롤 구성과 같은 구성으로 나머지 이벤트를
        /// 그려야 한다. 그래서 확인 창을 띄우거나(모달 창이 도중에 다른 이벤트를 처리한다) 화면 구성을 바꾸는 일
        /// (스테이지 고르기·옮기기·추가·삭제, 크기 바꾸기, 검증 시작 등)은 OnGUI 도중에 하지 않고 여기로 미룬다.
        /// 그러지 않으면 "EndLayoutGroup: BeginLayoutGroup must be called first" 같은 오류가 난다.
        /// </summary>
        private void Defer(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return; // 그 사이에 창이 닫혔다
                action();
                Repaint();
            };
        }

        private StageDraft Current => file != null && selected >= 0 && selected < file.stages.Count ? file.stages[selected] : null;

        // ── 화면 ──

        private void OnGUI()
        {
            if (sprites == null) sprites = new StageSprites();
            // 검증 중인지는 백그라운드 스레드가 바꾸므로, 화면 구성에 쓰는 값은 Layout 이벤트 때 한 번 정한다.
            if (Event.current.type == EventType.Layout)
            {
                busyShown = busy;
                // 어떤 길로든(선택, 옮기기, 삭제, Undo) 보이는 스테이지가 바뀌었으면 글자 칸의 포커스를 푼다.
                if (focusStage != selected || focusCount != (file != null ? file.stages.Count : 0))
                {
                    ClearTextFocus();
                    focusStage = selected;
                    focusCount = file != null ? file.stages.Count : 0;
                }
            }

            if (file == null)
            {
                EditorGUILayout.HelpBox(loadError ?? "stages.txt를 읽는 중", MessageType.Error);
                if (GUILayout.Button("Reload", GUILayout.Width(100))) Defer(LoadFromDisk);
                return;
            }

            DrawToolbar();
            if (loadWarning != null) EditorGUILayout.HelpBox(loadWarning, MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStageList();
                using (new EditorGUILayout.VerticalScope())
                {
                    var d = Current;
                    if (d == null)
                    {
                        EditorGUILayout.HelpBox("스테이지가 없다. + New로 만든다.", MessageType.Info);
                    }
                    else
                    {
                        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
                        DrawHeader(d);
                        EditorGUILayout.Space(4);
                        DrawPalette();
                        EditorGUILayout.Space(4);
                        DrawGrid(d);
                        DrawResize(d);
                        EditorGUILayout.EndScrollView();
                        DrawBottom(d);
                    }
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!dirty))
                {
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50))) Defer(() => Save());
                    if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(55)))
                        Defer(() =>
                        {
                            if (EditorUtility.DisplayDialog("Stage Editor", "저장하지 않은 변경을 모두 버리고 파일을 다시 읽을까?", "버리기", "취소"))
                                LoadFromDisk();
                        });
                }
                if (GUILayout.Button(new GUIContent("Undo", "Ctrl+Z. Unity의 Undo라서 씬에서 한 일도 함께 되돌린다"), EditorStyles.toolbarButton, GUILayout.Width(45)))
                    UnityEditor.Undo.PerformUndo();
                if (GUILayout.Button(new GUIContent("Redo", "Ctrl+Y"), EditorStyles.toolbarButton, GUILayout.Width(45)))
                    UnityEditor.Undo.PerformRedo();
                GUILayout.Space(10);
                GUILayout.Label("확대", GUILayout.Width(28));
                zoom = GUILayout.HorizontalSlider(zoom, 1f, 3f, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                GUILayout.Label(StagesPath + " — " + file.stages.Count + "개" + (dirty ? ", 저장 안 함" : ""), EditorStyles.miniLabel);
            }
        }

        // ── 스테이지 목록 ──

        private void DrawStageList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(210)))
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
                var e = Event.current;
                for (int i = 0; i < file.stages.Count; i++)
                {
                    var s = file.stages[i];
                    Board unused;
                    string error;
                    bool ok = s.TryParse(out unused, out error);
                    var style = new GUIStyle(i == selected ? EditorStyles.boldLabel : EditorStyles.label);
                    if (!ok) style.normal.textColor = new Color(1f, 0.45f, 0.4f);
                    var rect = GUILayoutUtility.GetRect(new GUIContent((i + 1) + ". " + s.name), style);
                    if (i == selected) EditorGUI.DrawRect(rect, new Color(0.24f, 0.37f, 0.59f, 0.6f));
                    if (dragFrom >= 0 && rect.Contains(e.mousePosition)) EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), Color.yellow);
                    GUI.Label(rect, (i + 1) + ". " + s.name + (ok ? "" : "  ⚠"), style);

                    // 누르면 고르고, 끌어서 다른 줄에 놓으면 그 자리로 옮긴다.
                    if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                    {
                        int index = i;
                        Defer(() => SelectStage(index));
                        dragFrom = i;
                        e.Use();
                    }
                    else if (e.type == EventType.MouseUp && dragFrom >= 0 && rect.Contains(e.mousePosition))
                    {
                        int from = dragFrom, to = i;
                        if (from != to) Defer(() => MoveStage(from, to));
                        dragFrom = -1;
                        e.Use();
                    }
                }
                if (e.type == EventType.MouseUp) dragFrom = -1;
                if (e.type == EventType.MouseDrag && dragFrom >= 0) Repaint();
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ New")) Defer(() => { Record("add stage"); SelectStage(file.AddNew(selected)); Changed(); });
                    using (new EditorGUI.DisabledScope(Current == null))
                    {
                        if (GUILayout.Button("Duplicate")) Defer(() => { Record("duplicate stage"); SelectStage(file.Duplicate(selected)); Changed(); });
                        if (GUILayout.Button("Delete")) Defer(DeleteSelected);
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledScope(Current == null))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(28))) Defer(() => MoveStage(selected, selected - 1));
                    if (GUILayout.Button("▼", GUILayout.Width(28))) Defer(() => MoveStage(selected, selected + 1));
                    GUILayout.Label("→", GUILayout.Width(14));
                    moveTarget = EditorGUILayout.IntField(moveTarget, GUILayout.Width(34));
                    if (GUILayout.Button("번 자리로")) Defer(() => MoveStage(selected, moveTarget - 1));
                }
            }
        }

        private void SelectStage(int index)
        {
            if (index == selected) return;
            selected = Mathf.Clamp(index, 0, Mathf.Max(0, file.stages.Count - 1));
            moveTarget = selected + 1;
            resultText = "";
            ClearTextFocus();
            Repaint();
        }

        /// <summary>
        /// 글자 칸의 입력 포커스를 푼다. 포커스가 남은 채로 다른 스테이지를 고르면, 같은 자리의 글자 칸(이름, 설명 등)이
        /// 앞 스테이지에서 고치던 글자를 그대로 보여 준다(Unity가 편집 중인 글자를 칸 번호로 기억하므로).
        /// GUI.FocusControl은 OnGUI 안에서만 들으므로, 미뤄 둔 동작에서도 듣는 이쪽을 쓴다.
        /// </summary>
        private static void ClearTextFocus()
        {
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }

        private void DeleteSelected()
        {
            var d = Current;
            if (d == null) return;
            if (!EditorUtility.DisplayDialog("Stage Editor", (selected + 1) + ". " + d.name + " 을(를) 지울까? (Save 전까지는 Undo로 되돌릴 수 있다)", "지우기", "취소"))
                return;
            Record("delete stage");
            file.Delete(selected);
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, file.stages.Count - 1));
            moveTarget = selected + 1;
            resultText = "";
            Changed();
        }

        private void MoveStage(int from, int to)
        {
            if (file.stages.Count == 0) return;
            to = Mathf.Clamp(to, 0, file.stages.Count - 1);
            if (from == to) return;
            Record(null);
            selected = file.Move(from, to);
            moveTarget = selected + 1;
            Changed();
        }

        // ── 헤더 ──

        private void DrawHeader(StageDraft d)
        {
            EditorGUIUtility.labelWidth = 70;
            d.name = TextField("Name", d.name, "name");
            using (new EditorGUILayout.HorizontalScope())
            {
                d.turnLimit = IntField("제한 턴", d.turnLimit, "limit", 140);
                bool auto = d.target == StageDraft.AutoTarget;
                EditorGUI.BeginChangeCheck();
                bool newAuto = EditorGUILayout.ToggleLeft("목표 양 = 양 수 (" + d.SheepCount() + ")", auto, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                {
                    // 체크에 따라 옆의 숫자 칸이 생기거나 없어지므로 이벤트가 끝난 뒤에 바꾼다.
                    Defer(() =>
                    {
                        Record("target");
                        d.target = newAuto ? StageDraft.AutoTarget : d.SheepCount();
                        Changed();
                    });
                }
                if (!auto) d.target = IntField("목표 양", d.target, "target", 140);
            }
            d.description = TextField("Desc", d.description, "desc");
            using (new EditorGUILayout.HorizontalScope())
            {
                d.star3 = IntField("별 3개", d.star3, "star3", 120);
                d.star2 = IntField("별 2개", d.star2, "star2", 120);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                MechanicField("Intent", d, true);
                MechanicField("Optimal", d, false);
            }
            d.solution = TextField("풀이", d.solution, "solution");
            if (d.comments.Count > 0)
                EditorGUILayout.LabelField("주석", string.Join("  /  ", d.comments), EditorStyles.miniLabel);
        }

        private string TextField(string label, string value, string key)
        {
            EditorGUI.BeginChangeCheck();
            string v = EditorGUILayout.TextField(label, value ?? "");
            if (!EditorGUI.EndChangeCheck()) return value;
            Record(key);
            // 값이 바뀌기 전 스냅샷이 위에서 저장되므로, 바뀐 값은 돌려준 뒤 Changed가 반영한다.
            EditorApplication.delayCall += Changed;
            return v;
        }

        private int IntField(string label, int value, string key, float width)
        {
            EditorGUI.BeginChangeCheck();
            int v = EditorGUILayout.IntField(label, value, GUILayout.Width(width));
            if (!EditorGUI.EndChangeCheck()) return value;
            Record(key);
            EditorApplication.delayCall += Changed;
            return v;
        }

        private void MechanicField(string label, StageDraft d, bool intent)
        {
            string current = intent ? d.intent : d.optimal;
            var mechanics = StageDraft.ParseNames(current);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(66));
                string shown = mechanics == Mechanic.None ? "(없음)" : StageDraft.Names(mechanics);
                if (GUILayout.Button(new GUIContent(shown, shown), EditorStyles.popup, GUILayout.MinWidth(120)))
                {
                    var menu = new GenericMenu();
                    foreach (Mechanic one in Enum.GetValues(typeof(Mechanic)))
                    {
                        if (one == Mechanic.None) continue;
                        var m = one;
                        bool on = (mechanics & m) != 0;
                        menu.AddItem(new GUIContent(MechanicGroup(m) + "/" + m), on, () =>
                        {
                            Record(null);
                            var now = StageDraft.ParseNames(intent ? d.intent : d.optimal) ^ m;
                            if (intent) d.intent = StageDraft.Names(now);
                            else d.optimal = StageDraft.Names(now);
                            Changed();
                        });
                    }
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("모두 지우기"), false, () =>
                    {
                        Record(null);
                        if (intent) d.intent = ""; else d.optimal = "";
                        Changed();
                    });
                    menu.ShowAsContext();
                }
            }
        }

        /// <summary>Mechanic의 비트 자리로 나눈 무리. 열거형 선언의 주석 묶음과 같다.</summary>
        private static string MechanicGroup(Mechanic m)
        {
            int bit = 0;
            for (ulong v = (ulong)m; v > 1; v >>= 1) bit++;
            if (bit <= 7) return "Shift";
            if (bit <= 20) return "Water & Fire";
            if (bit <= 22) return "Bucket";
            if (bit <= 29) return "Wolf";
            if (bit <= 34) return "Goat";
            if (bit <= 45) return "Horse";
            return "Cow";
        }

        // ── 팔레트 ──

        private void DrawPalette()
        {
            const float size = 30f;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("지형", GUILayout.Width(40));
                for (int i = 0; i < TerrainSymbols.Length; i++)
                {
                    char sym = TerrainSymbols[i];
                    var rect = PaletteButton(size, tool == ToolKind.Terrain && terrainSymbol == sym, TerrainNames[i] + " '" + sym + "'");
                    sprites.DrawTerrain(Inset(rect), sym);
                    if (Clicked(rect)) { tool = ToolKind.Terrain; terrainSymbol = sym; }
                }
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("오브젝트", GUILayout.Width(40));
                for (int i = 0; i < ObjectLetters.Length; i++)
                {
                    char letter = ObjectLetters[i];
                    var rect = PaletteButton(size, tool == ToolKind.Object && objectLetter == letter, ObjectNames[i] + " '" + letter + "'");
                    sprites.DrawObject(Inset(rect), letter, 1, '\0');
                    if (Clicked(rect)) { tool = ToolKind.Object; objectLetter = letter; }
                }
                GUILayout.Space(8);
                int stateIndex = objectState == 'w' ? 1 : objectState == 'b' ? 2 : 0;
                stateIndex = GUILayout.Toolbar(stateIndex, new[] { "보통", "젖음 (w)", "화상 (b)" }, GUILayout.Width(200));
                objectState = stateIndex == 1 ? 'w' : stateIndex == 2 ? 'b' : '\0';
                GUILayout.Space(8);
                var erase = PaletteButton(size, tool == ToolKind.Erase, "지우개: 칸의 오브젝트를 모두 지운다");
                GUI.Label(erase, "✕", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 16 });
                if (Clicked(erase)) tool = ToolKind.Erase;
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.LabelField("왼쪽 클릭·드래그: 칠하기 / 오브젝트 넣기(같은 것은 수가 늘어남)   오른쪽 클릭: 오브젝트 하나 빼기", EditorStyles.miniLabel);
        }

        private Rect PaletteButton(float size, bool on, string tooltip)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            EditorGUI.DrawRect(rect, on ? new Color(1f, 0.85f, 0.3f) : new Color(0.2f, 0.2f, 0.2f));
            GUI.Label(rect, new GUIContent("", tooltip));
            return rect;
        }

        private static Rect Inset(Rect r) => new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);

        private static bool Clicked(Rect r)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition)) return false;
            e.Use();
            return true;
        }

        // ── 격자 ──

        private void DrawGrid(StageDraft d)
        {
            float cell = Mathf.Round(32f * zoom);
            float arrow = Mathf.Max(16f, cell * 0.45f);
            float gap = 4f;
            float w = arrow + gap + cell * d.width + gap + arrow;
            float h = arrow + gap + cell * d.height + gap + arrow;
            var area = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
            var origin = new Vector2(area.x + arrow + gap, area.y + arrow + gap);

            var cells = new CellData[d.height, d.width];
            for (int r = 0; r < d.height; r++)
                for (int c = 0; c < d.width; c++)
                    cells[r, c] = d.Cell(r, c);

            // 칸
            for (int r = 0; r < d.height; r++)
            {
                for (int c = 0; c < d.width; c++)
                {
                    var rect = new Rect(origin.x + c * cell, origin.y + r * cell, cell, cell);
                    sprites.DrawCell(rect, cells[r, c], Mask(cells, r, c));
                    var line = new Color(0f, 0f, 0f, 0.25f);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), line);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), line);
                    if (r == hoverRow && c == hoverCol) EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.15f));
                }
            }

            // shift 화살표: 미는 쪽 가장자리 바깥에 둔다(게임과 같다). 행 왼쪽 ▶ = R, 오른쪽 ◀ = L, 열 위 ▼ = D, 아래 ▲ = U.
            for (int r = 0; r < d.height; r++)
            {
                bool real = d.RealCount(Axis.Row, r) > 0;
                float y = origin.y + r * cell + (cell - arrow) * 0.5f;
                ArrowButton(new Rect(area.x, y, arrow, arrow), d, new ShiftPoint(Axis.Row, r, Dir.Right), "▶", real);
                ArrowButton(new Rect(origin.x + d.width * cell + gap, y, arrow, arrow), d, new ShiftPoint(Axis.Row, r, Dir.Left), "◀", real);
            }
            for (int c = 0; c < d.width; c++)
            {
                bool real = d.RealCount(Axis.Col, c) > 0;
                float x = origin.x + c * cell + (cell - arrow) * 0.5f;
                ArrowButton(new Rect(x, area.y, arrow, arrow), d, new ShiftPoint(Axis.Col, c, Dir.Down), "▼", real);
                ArrowButton(new Rect(x, origin.y + d.height * cell + gap, arrow, arrow), d, new ShiftPoint(Axis.Col, c, Dir.Up), "▲", real);
            }

            HandleGridMouse(d, new Rect(origin.x, origin.y, cell * d.width, cell * d.height), cell);

            string hover = d.InGrid(hoverRow, hoverCol) ? "(" + hoverRow + ", " + hoverCol + ")  " + cells[hoverRow, hoverCol] : " ";
            EditorGUILayout.LabelField(hover, EditorStyles.miniLabel);
        }

        private static int Mask(CellData[,] cells, int r, int c)
        {
            char t = cells[r, c].Terrain;
            int mask = 0;
            if (Same(cells, r - 1, c, t)) mask |= 1;
            if (Same(cells, r, c + 1, t)) mask |= 2;
            if (Same(cells, r + 1, c, t)) mask |= 4;
            if (Same(cells, r, c - 1, t)) mask |= 8;
            return mask;
        }

        private static bool Same(CellData[,] cells, int r, int c, char t)
        {
            return r >= 0 && r < cells.GetLength(0) && c >= 0 && c < cells.GetLength(1) && cells[r, c].Terrain == t;
        }

        private void ArrowButton(Rect rect, StageDraft d, ShiftPoint p, string glyph, bool enabled)
        {
            bool on = d.HasPoint(p);
            var old = GUI.backgroundColor;
            GUI.backgroundColor = on ? new Color(0.45f, 0.9f, 0.45f) : new Color(0.45f, 0.45f, 0.45f);
            using (new EditorGUI.DisabledScope(!enabled && !on))
            {
                var content = new GUIContent(on ? glyph : "·", p + (on ? " 켜짐" : " 꺼짐"));
                if (GUI.Button(rect, content, EditorStyles.miniButton))
                {
                    Record(null);
                    d.TogglePoint(p);
                    Changed();
                }
            }
            GUI.backgroundColor = old;
        }

        private void HandleGridMouse(StageDraft d, Rect grid, float cell)
        {
            var e = Event.current;
            int r = -1, c = -1;
            if (grid.Contains(e.mousePosition))
            {
                r = (int)((e.mousePosition.y - grid.y) / cell);
                c = (int)((e.mousePosition.x - grid.x) / cell);
            }
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                if (r != hoverRow || c != hoverCol) { hoverRow = r; hoverCol = c; Repaint(); }
            }
            if (!d.InGrid(r, c)) return;

            if (e.type == EventType.MouseDown && e.button == 1)
            {
                if (d.Cell(r, c).Objects.Count > 0)
                {
                    Record("remove object");
                    d.RemoveObject(r, c);
                    Changed();
                }
                e.Use();
                return;
            }
            bool down = e.type == EventType.MouseDown && e.button == 0;
            bool drag = e.type == EventType.MouseDrag && e.button == 0;
            if (!down && !drag) return;
            if (tool == ToolKind.Object && !down) return; // 오브젝트는 클릭마다 하나

            // 바뀔 때만 기록한다. 한 번 누르고 끄는 동안의 기록은 Unity가 Undo 하나로 묶는다.
            var cellData = d.Cell(r, c);
            bool willChange;
            switch (tool)
            {
                case ToolKind.Terrain: willChange = cellData.Terrain != terrainSymbol || ((terrainSymbol == '-' || terrainSymbol == 'x') && cellData.Objects.Count > 0); break;
                case ToolKind.Object: willChange = !cellData.IsBlank && !cellData.IsObstacle; break;
                default: willChange = cellData.Objects.Count > 0; break;
            }
            if (willChange)
            {
                Record("paint");
                switch (tool)
                {
                    case ToolKind.Terrain: d.PaintTerrain(r, c, terrainSymbol); break;
                    case ToolKind.Object: d.AddObject(r, c, objectLetter, objectState); break;
                    default:
                        cellData.Objects.Clear();
                        d.SetCell(r, c, cellData);
                        break;
                }
                Changed();
            }
            e.Use();
        }

        private void DrawResize(StageDraft d)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(d.height + " × " + d.width, GUILayout.Width(50));
                if (GUILayout.Button("위 +행")) Resize(() => d.InsertRow(0));
                if (GUILayout.Button("위 −행") && d.height > 1) Resize(() => d.RemoveRow(0));
                if (GUILayout.Button("아래 +행")) Resize(() => d.InsertRow(d.height));
                if (GUILayout.Button("아래 −행") && d.height > 1) Resize(() => d.RemoveRow(d.height - 1));
                if (GUILayout.Button("왼쪽 +열")) Resize(() => d.InsertCol(0));
                if (GUILayout.Button("왼쪽 −열") && d.width > 1) Resize(() => d.RemoveCol(0));
                if (GUILayout.Button("오른쪽 +열")) Resize(() => d.InsertCol(d.width));
                if (GUILayout.Button("오른쪽 −열") && d.width > 1) Resize(() => d.RemoveCol(d.width - 1));
                if (GUILayout.Button(new GUIContent("모든 줄 열기", "실제 칸이 둘 이상인 모든 줄을 양방향으로 연다"))) Resize(d.OpenAllLines);
            }
        }

        private void Resize(Action edit)
        {
            Defer(() =>
            {
                Record("resize");
                edit();
                Changed();
            });
        }

        // ── 아래: 상태와 검증 ──

        private void DrawBottom(StageDraft d)
        {
            Board board;
            string error;
            bool ok = d.TryParse(out board, out error);
            EditorGUILayout.Space(4);
            if (ok) EditorGUILayout.HelpBox("읽기 OK — 양 " + board.SheepCount() + "마리, 목표 " + board.Rules.TargetSheep + ", shift 포인트 " + board.Rules.ShiftPoints.Count + "개", MessageType.None);
            else EditorGUILayout.HelpBox(error, MessageType.Error);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!ok || busyShown))
                {
                    if (GUILayout.Button(new GUIContent("Validate", "지금 내용(저장 전)을 검증 창과 같은 기준으로 검증한다"))) Defer(() => StartValidate(board));
                    if (GUILayout.Button(new GUIContent("Prune → 적용", "모든 줄을 연 상태에서 의도와 다른 해를 만드는 포인트만 뺀 조합을 찾아 적용한다"))) Defer(() => StartPrune(board));
                    if (GUILayout.Button(new GUIContent("Solver로 채우기", "최적해를 구해 풀이 줄과 별 3개 기준을 채운다. 별 2개·제한 턴이 모자라면 늘린다"))) Defer(() => StartFill(board));
                }
                using (new EditorGUI.DisabledScope(!busyShown))
                    if (GUILayout.Button("Cancel", GUILayout.Width(60)) && cts != null) cts.Cancel();
                using (new EditorGUI.DisabledScope(!ok || busyShown || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button(new GUIContent("▶ Play", "저장한 뒤 이 스테이지로 바로 Play한다"), GUILayout.Width(70))) Defer(PlayStage);
            }
            if (busyShown && busyWatch != null) EditorGUILayout.LabelField(busyLabel + "  " + (busyWatch.ElapsedMilliseconds / 1000f).ToString("0.0") + "s", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(resultText))
            {
                resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.Height(160));
                EditorGUILayout.TextArea(resultText, EditorStyles.textArea, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>백그라운드 작업. 결과를 적용할 일이 있으면 work가 Action을 돌려주고, 그것은 메인 스레드에서 돈다.</summary>
        private void RunBackground(string label, Func<CancellationToken, Action> work)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;
            busy = true;
            busyLabel = label;
            busyWatch = System.Diagnostics.Stopwatch.StartNew();
            resultText = label + "...";
            Task.Run(() =>
            {
                Action apply;
                try { apply = work(token); }
                catch (OperationCanceledException) { apply = () => resultText = "취소됨"; }
                catch (Exception ex) { var msg = ex.ToString(); apply = () => resultText = msg; }
                lock (gate) pendingApply = apply;
                busy = false;
            });
        }

        private void StartValidate(Board board)
        {
            int number = selected + 1;
            RunBackground("검증 중", token =>
            {
                var check = StageChecker.Check(board, number, StageChecker.DefaultMaxNodes, 5, token);
                string text = (check.Ok ? "✔ 통과\n\n" : "✖ 확인 필요\n\n") + StageChecker.Details(check);
                return () => resultText = text;
            });
        }

        private void StartPrune(Board board)
        {
            var draft = Current;
            RunBackground("Prune 중", token =>
            {
                var result = Pruner.Run(board, -1, 4, StageChecker.DefaultMaxNodes, token);
                return () =>
                {
                    resultText = result.Text;
                    if (!result.Found) return;
                    if (Current != draft) { resultText += "\n\n(다른 스테이지를 고르는 중이라 적용하지 않았다. shift: " + result.Spec + ")"; return; }
                    Record(null);
                    draft.SetShiftSpec(result.Spec);
                    resultText += "\n\n적용함: shift: " + result.Spec;
                    Changed();
                };
            });
        }

        private void StartFill(Board board)
        {
            var draft = Current;
            RunBackground("최적해 찾는 중", token =>
            {
                int depth = Math.Max(board.Rules.TurnLimit, 20);
                var r = Solver.Solve(board, new SolveOptions { MaxDepth = depth, MaxSolutions = 1, Intent = Mechanic.None, Cancel = token });
                return () =>
                {
                    if (r.OptimalTurns < 0)
                    {
                        resultText = depth + "턴 안에 해가 없다" + (r.Truncated ? " (탐색 한도에 걸림)" : "") + ".";
                        return;
                    }
                    if (Current != draft) { resultText = "다른 스테이지를 고르는 중이라 적용하지 않았다. 최적 " + r.OptimalTurns + "턴: " + r.Solutions[0].MovesText; return; }
                    Record(null);
                    int opt = r.OptimalTurns;
                    draft.solution = string.Join(", ", r.Solutions[0].Moves);
                    draft.star3 = opt;
                    if (draft.star2 <= opt) draft.star2 = opt + 2;
                    if (draft.turnLimit < draft.star2) draft.turnLimit = draft.star2;
                    resultText = "최적 " + opt + "턴 (최적 수순 " + r.OptimalSequences + "개). 채움: 풀이 " + draft.solution
                        + ", 별 " + draft.star3 + "/" + draft.star2 + ", 제한 " + draft.turnLimit + "턴\n의도 기믹 검사는 Validate로 한다.";
                    Changed();
                };
            });
        }

        // ── Play ──

        private void PlayStage()
        {
            if (dirty)
            {
                if (!EditorUtility.DisplayDialog("Stage Editor", "Play하려면 먼저 stages.txt에 저장해야 한다. 저장할까?", "저장하고 Play", "취소")) return;
                if (!Save()) return;
            }
            if (EditorSceneManager.GetActiveScene().path != GameScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(GameScenePath);
            }
            SessionState.SetInt(GameController.EditorPlayStageKey, selected);
            EditorApplication.EnterPlaymode();
        }
    }
}
