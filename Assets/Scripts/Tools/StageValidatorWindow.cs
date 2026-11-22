using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sheepft.Core;
using UnityEditor;
using UnityEngine;

namespace Sheepft.Tools
{
    /// <summary>
    /// 메뉴 Sheepft > Stage Validator. 버튼 하나로 stages.txt의 모든 스테이지를 게임 규칙으로 검증한다.
    /// 검증은 백그라운드 스레드에서 돌고, 스테이지 하나가 끝날 때마다 표가 채워진다.
    /// 파일은 누를 때마다 디스크에서 다시 읽으므로, 고친 뒤 Unity가 다시 import하기를 기다리지 않아도 된다.
    /// </summary>
    public sealed class StageValidatorWindow : EditorWindow
    {
        private const string StagesPath = "Assets/Levels/stages.txt";

        private enum RowState { Waiting, Running, Done, Cancelled, Failed }

        private sealed class Row
        {
            public Board Level;
            public volatile RowState State;
            public StageCheck Check;
            public string Error;
            public string Prune;
            public string PruneSpec;
            public bool Pruning;
        }

        // 도메인 리로드 때 사라져도 되는 값들이다. 결과는 창을 닫으면 사라진다.
        [NonSerialized] private Row[] rows = new Row[0];
        [NonSerialized] private string parseError;
        [NonSerialized] private CancellationTokenSource cts;
        [NonSerialized] private volatile string runningLabel;
        [NonSerialized] private Stopwatch runWatch;
        [NonSerialized] private volatile bool busy;
        [NonSerialized] private volatile bool dirty;

        private int maxNodes = StageChecker.DefaultMaxNodes;
        private int selected = -1;
        [NonSerialized] private int shownSelected = -1;     // Layout 이벤트 때의 selected
        [NonSerialized] private string shownPruneSpec;      // Layout 이벤트 때의 PruneSpec (백그라운드가 채운다)

        /// <summary>
        /// 지금 이벤트가 끝난 뒤에 실행한다. 목록을 다시 읽거나 고른 줄을 바꾸면 화면 구성이 바뀌므로 OnGUI 도중에 하지 않는다
        /// (IMGUI는 Layout 이벤트와 같은 구성으로 나머지 이벤트를 그려야 한다).
        /// </summary>
        private void Defer(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                action();
                Repaint();
            };
        }
        private Vector2 tableScroll, detailScroll;
        private GUIStyle mono, monoArea;

        [MenuItem("Sheepft/Stage Validator")]
        private static void Open()
        {
            var w = GetWindow<StageValidatorWindow>("Stage Validator");
            w.minSize = new Vector2(720, 400);
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (cts != null) cts.Cancel();
        }

        private void Tick()
        {
            if (busy || dirty)
            {
                dirty = false;
                Repaint();
            }
        }

        // ── 실행 ──

        /// <summary>디스크에서 stages.txt를 읽는다. 스테이지 수가 그대로면 지난 결과를 남겨 둔다. 실패하면 false.</summary>
        private bool Reload()
        {
            parseError = null;
            List<Board> levels;
            try
            {
                levels = LevelParser.ParseAll(File.ReadAllText(StagesPath));
            }
            catch (Exception e) when (e is LevelParseException || e is IOException)
            {
                parseError = StagesPath + " 를 읽지 못했다.\n" + e.Message;
                return false;
            }

            var next = new Row[levels.Count];
            for (int i = 0; i < next.Length; i++)
            {
                next[i] = i < rows.Length && levels.Count == rows.Length ? rows[i] : new Row();
                next[i].Level = levels[i];
            }
            rows = next;
            if (selected >= rows.Length) selected = -1;
            return true;
        }

        private void ValidateAll()
        {
            if (busy || !Reload()) return;
            foreach (var row in rows)
            {
                row.State = RowState.Waiting;
                row.Check = null;
                row.Error = null;
            }
            var targets = new List<int>();
            for (int i = 0; i < rows.Length; i++) targets.Add(i);
            StartChecks(targets);
        }

        private void ValidateOne(int index)
        {
            if (busy || !Reload() || index >= rows.Length) return;
            rows[index].State = RowState.Waiting;
            rows[index].Check = null;
            rows[index].Error = null;
            StartChecks(new List<int> { index });
        }

        private void StartChecks(List<int> targets)
        {
            int nodes = maxNodes;
            var snapshot = rows;
            Begin(token =>
            {
                for (int k = 0; k < targets.Count; k++)
                {
                    int i = targets[k];
                    var row = snapshot[i];
                    if (token.IsCancellationRequested)
                    {
                        row.State = RowState.Cancelled;
                        continue;
                    }
                    runningLabel = (k + 1) + "/" + targets.Count + "  " + (i + 1) + ". " + row.Level.Rules.Name;
                    row.State = RowState.Running;
                    dirty = true;
                    try
                    {
                        row.Check = StageChecker.Check(row.Level, i + 1, nodes, 5, token);
                        row.State = RowState.Done;
                    }
                    catch (OperationCanceledException)
                    {
                        row.State = RowState.Cancelled;
                    }
                    catch (Exception e)
                    {
                        row.Error = e.ToString();
                        row.State = RowState.Failed;
                    }
                    dirty = true;
                }
            });
        }

        private void PruneOne(int index)
        {
            if (busy || !Reload() || index >= rows.Length) return;
            var row = rows[index];
            row.Prune = null;
            row.PruneSpec = null;
            row.Pruning = true;
            int nodes = maxNodes;
            selected = index;
            Begin(token =>
            {
                runningLabel = "prune  " + (index + 1) + ". " + row.Level.Rules.Name;
                try
                {
                    var result = Pruner.Run(row.Level, -1, 4, nodes, token);
                    row.Prune = result.Text;
                    row.PruneSpec = result.Spec;
                }
                catch (OperationCanceledException)
                {
                    row.Prune = "취소됨";
                }
                catch (Exception e)
                {
                    row.Prune = e.ToString();
                }
                row.Pruning = false;
                dirty = true;
            });
        }

        private void Begin(Action<CancellationToken> work)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;
            busy = true;
            runWatch = Stopwatch.StartNew();
            Task.Run(() =>
            {
                try { work(token); }
                finally
                {
                    runningLabel = null;
                    busy = false;
                    dirty = true;
                }
            });
        }

        // ── 화면 ──

        private void OnGUI()
        {
            if (mono == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "D2Coding", "Courier New" }, 12);
                mono = new GUIStyle(EditorStyles.label) { font = font, richText = false };
                monoArea = new GUIStyle(EditorStyles.textArea) { font = font, wordWrap = false };
            }

            // 백그라운드 스레드가 바꾸는 값 중 화면 구성에 쓰는 것은 Layout 이벤트 때 한 번 정한다.
            if (Event.current.type == EventType.Layout)
            {
                shownSelected = selected;
                shownPruneSpec = selected >= 0 && selected < rows.Length ? rows[selected].PruneSpec : null;
            }

            DrawToolbar();
            if (parseError != null)
            {
                EditorGUILayout.HelpBox(parseError, MessageType.Error);
                return;
            }
            if (rows.Length == 0)
            {
                EditorGUILayout.HelpBox("Validate All을 누르면 " + StagesPath + " 의 모든 스테이지를 검증한다.", MessageType.Info);
                return;
            }

            tableScroll = EditorGUILayout.BeginScrollView(tableScroll, GUILayout.Height(Mathf.Max(160, position.height * 0.5f)));
            EditorGUILayout.LabelField(StageChecker.TableHeader, mono);
            for (int i = 0; i < rows.Length; i++) DrawRow(i);
            EditorGUILayout.EndScrollView();

            DrawDetails();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(busy))
                {
                    if (GUILayout.Button("Validate All", EditorStyles.toolbarButton, GUILayout.Width(90))) Defer(ValidateAll);
                }
                using (new EditorGUI.DisabledScope(!busy))
                {
                    if (GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(60)) && cts != null) cts.Cancel();
                }
                GUILayout.Space(8);
                GUILayout.Label("상태 한도", GUILayout.Width(52));
                maxNodes = Mathf.Max(1000, EditorGUILayout.IntField(maxNodes, EditorStyles.toolbarTextField, GUILayout.Width(80)));
                using (new EditorGUI.DisabledScope(busy || rows.Length == 0))
                {
                    if (GUILayout.Button("Copy Report", EditorStyles.toolbarButton, GUILayout.Width(90)))
                        EditorGUIUtility.systemCopyBuffer = Report();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(Status(), EditorStyles.miniLabel);
            }
        }

        private string Status()
        {
            if (busy) return runningLabel + "  (" + (runWatch.ElapsedMilliseconds / 1000f).ToString("0.0") + "s)";
            int done = 0, bad = 0;
            foreach (var row in rows)
            {
                if (row.State != RowState.Done) continue;
                done++;
                if (!row.Check.Ok) bad++;
            }
            if (done == 0) return "";
            string time = runWatch != null ? "  " + (runWatch.ElapsedMilliseconds / 1000f).ToString("0.0") + "s" : "";
            return bad == 0 ? done + "개 통과" + time : "확인 필요 " + bad + "개 / " + done + "개" + time;
        }

        private void DrawRow(int i)
        {
            var row = rows[i];
            string text;
            Color color;
            switch (row.State)
            {
                case RowState.Done:
                    text = StageChecker.Row(row.Check);
                    color = row.Check.Ok ? new Color(0.45f, 0.85f, 0.45f) : new Color(1f, 0.45f, 0.4f);
                    break;
                case RowState.Running:
                    text = (i + 1).ToString().PadRight(4) + "검증 중...  " + row.Level.Rules.Name;
                    color = new Color(1f, 0.85f, 0.4f);
                    break;
                case RowState.Failed:
                    text = (i + 1).ToString().PadRight(4) + "오류  " + row.Level.Rules.Name;
                    color = new Color(1f, 0.45f, 0.4f);
                    break;
                case RowState.Cancelled:
                    text = (i + 1).ToString().PadRight(4) + "취소됨  " + row.Level.Rules.Name;
                    color = Color.gray;
                    break;
                default:
                    text = (i + 1).ToString().PadRight(4) + "-  " + row.Level.Rules.Name;
                    color = Color.gray;
                    break;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var old = GUI.contentColor;
                GUI.contentColor = color;
                var style = new GUIStyle(mono);
                if (selected == i) style.fontStyle = FontStyle.Bold;
                if (GUILayout.Button(text, style)) Defer(() => selected = i);
                GUI.contentColor = old;
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(busy))
                {
                    if (GUILayout.Button("Solve", EditorStyles.miniButtonLeft, GUILayout.Width(46))) Defer(() => { selected = i; ValidateOne(i); });
                    if (GUILayout.Button("Prune", EditorStyles.miniButtonRight, GUILayout.Width(46))) Defer(() => PruneOne(i));
                }
            }
        }

        private void DrawDetails()
        {
            if (shownSelected < 0 || shownSelected >= rows.Length)
            {
                EditorGUILayout.HelpBox("줄을 누르면 자세한 결과가 여기 나온다. Solve는 그 스테이지만 다시 검증하고, Prune은 shift 포인트 조합을 찾는다.", MessageType.None);
                return;
            }
            var row = rows[shownSelected];
            var sb = new StringBuilder();
            if (row.State == RowState.Done) sb.Append(StageChecker.Details(row.Check));
            else if (row.State == RowState.Failed) sb.Append(row.Error);
            else sb.Append((shownSelected + 1) + ". " + row.Level.Rules.Name + "\n" + LevelWriter.Write(row.Level));

            if (row.Pruning) sb.Append("\n\nprune 중...");
            else if (row.Prune != null) sb.Append("\n\nprune:\n").Append(row.Prune);

            if (shownPruneSpec != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("shift: " + shownPruneSpec, mono);
                    if (GUILayout.Button("Copy", GUILayout.Width(50))) EditorGUIUtility.systemCopyBuffer = shownPruneSpec;
                }
            }

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            EditorGUILayout.TextArea(sb.ToString(), monoArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        /// <summary>예전 `check` 명령과 같은 모양의 표 전체. 비교나 공유에 쓴다.</summary>
        private string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine(StageChecker.TableHeader);
            foreach (var row in rows)
            {
                if (row.State == RowState.Done) sb.AppendLine(StageChecker.Row(row.Check));
                else sb.AppendLine(row.Level.Rules.Name + "  (" + row.State + ")");
            }
            return sb.ToString();
        }
    }
}
