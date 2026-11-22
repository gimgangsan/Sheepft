#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Sheepft.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Sheepft.Game
{
    /// <summary>
    /// 에디터 전용. 메뉴 Sheepft > Setup Game Scene이 부른다.
    /// 오브젝트·지형·이펙트 프리팹, 폰트 에셋, UI 프리팹을 **없을 때만** 만든다.
    /// 이미 있는 것은 건드리지 않으므로 만든 뒤에는 에디터에서 마음대로 고치면 된다.
    /// 처음부터 다시 만들고 싶으면 그 프리팹을 지우고 메뉴를 다시 돌린다.
    /// </summary>
    public static class GamePrefabBuilder
    {
        private const string SpriteFolder = "Assets/Sprites";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string UiPrefabPath = "Assets/Prefabs/UI/GameUI.prefab";
        private const string ArrowPrefabPath = "Assets/Prefabs/UI/ShiftArrow.prefab";
        private const string HighlightPrefabPath = "Assets/Prefabs/UI/LineHighlight.prefab";
        private const string FontFolder = "Assets/Fonts";
        private const string TmpFolder = "Assets/TextMesh Pro";
        private const float Pixel = 1f / 32f;

        private static readonly string[] EntityNames = { "sheep", "wolf", "goat", "horse", "cow", "fruit_basket", "wooden_bucket" };
        private static readonly string[] TerrainNames = { "ground", "stone", "fence", "tree", "water", "wild_fire", "grass", "ash" };
        private static readonly string[] EffectNames =
        {
            "splash", "steam", "ignite", "smoke", "goat_shockwave", "cow_impact",
            "wolf_merge_dust", "sheep_eaten", "sheep_arrive", "eat_crumbs",
        };

        private static readonly Color TextColor = new Color32(0xf4, 0xf0, 0xe8, 0xff);
        private static readonly Color PanelColor = new Color32(0x2b, 0x24, 0x33, 0xf2);
        private static readonly Color LevelColor = new Color32(0x4f, 0x40, 0x44, 0xff);

        private static Dictionary<string, string> spritePaths;

        /// <summary>프리팹을 만들고 GameRoot의 PrefabCatalog를 채운다. UI를 만들었으면 그 프리팹을 돌려준다.</summary>
        public static GameObject Build(string rootPrefabPath)
        {
            IndexSprites();
            EnsureFolder(PrefabFolder, "Entities");
            EnsureFolder(PrefabFolder, "Terrain");
            EnsureFolder(PrefabFolder, "Effects");
            EnsureFolder(PrefabFolder, "UI");

            foreach (var name in EntityNames) CreateIfMissing(EntityPath(name), () => NewEntity(name));
            foreach (var name in TerrainNames) CreateIfMissing(TerrainPath(name), () => NewTerrain(name));
            foreach (var name in EffectNames) CreateIfMissing(EffectPath(name), () => NewEffect(name));
            CreateIfMissing(ArrowPrefabPath, NewArrow);
            CreateIfMissing(HighlightPrefabPath, NewHighlight);

            FillCatalog(rootPrefabPath);
            return BuildUi(rootPrefabPath);
        }

        // ── 프리팹 ──

        private static string EntityPath(string name) => PrefabFolder + "/Entities/" + Pascal(name) + ".prefab";
        private static string TerrainPath(string name) => PrefabFolder + "/Terrain/" + Pascal(name) + ".prefab";
        private static string EffectPath(string name) => PrefabFolder + "/Effects/" + Pascal(name) + ".prefab";

        /// <summary>오브젝트: 뿌리에 EntityView, 자식으로 몸(Body)과 상태 아이콘(Status).</summary>
        private static GameObject NewEntity(string name)
        {
            var root = new GameObject(Pascal(name));
            var view = root.AddComponent<EntityView>();

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            view.body = body.AddComponent<SpriteRenderer>();
            view.body.sprite = FirstSprite(name);
            view.bodyAnimator = AddAnimator(body, name);

            var status = new GameObject("Status");
            status.transform.SetParent(root.transform, false);
            status.transform.localPosition = new Vector3(0f, 26f * Pixel, 0f);
            view.icon = status.AddComponent<SpriteRenderer>();
            view.icon.enabled = false;
            view.iconAnimator = AddAnimator(status, "status_icons");
            return root;
        }

        /// <summary>
        /// 지형: SpriteRenderer 하나. 들불만 Animator가 붙는다.
        /// 물·풀·울타리는 이웃에 따라 그림이 달라지므로 BoardView가 프레임을 골라 넣는다(Animator를 붙이지 않는다).
        /// </summary>
        private static GameObject NewTerrain(string name)
        {
            var root = new GameObject(Pascal(name));
            root.AddComponent<SpriteRenderer>().sprite = FirstSprite(name);
            if (name == "wild_fire") AddAnimator(root, name);
            return root;
        }

        /// <summary>이펙트: 뿌리는 칸 가운데에 놓이고 돌아간다. 그림은 피벗(아래쪽 가운데)만큼 내려 둔다.</summary>
        private static GameObject NewEffect(string name)
        {
            var root = new GameObject(Pascal(name));
            var effect = root.AddComponent<OneShotEffect>();
            var sprite = new GameObject("Sprite");
            sprite.transform.SetParent(root.transform, false);
            sprite.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            sprite.AddComponent<SpriteRenderer>().sprite = FirstSprite(name);
            effect.animator = AddAnimator(sprite, name);
            return root;
        }

        private static GameObject NewArrow()
        {
            var root = new GameObject("ShiftArrow");
            var sprite = new GameObject("Sprite");
            sprite.transform.SetParent(root.transform, false);
            sprite.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            sprite.AddComponent<SpriteRenderer>().sprite = FirstSprite("shift_arrow");
            return root;
        }

        private static GameObject NewHighlight()
        {
            var root = new GameObject("LineHighlight");
            root.AddComponent<SpriteRenderer>().sprite = FirstSprite("line_highlight");
            AddAnimator(root, "line_highlight");
            return root;
        }

        private static void FillCatalog(string rootPrefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(rootPrefabPath);
            try
            {
                var catalog = root.GetComponent<PrefabCatalog>();
                if (catalog == null) catalog = root.AddComponent<PrefabCatalog>();
                foreach (var name in EntityNames) FillEntry(catalog.entities, name, EntityPath(name));
                foreach (var name in TerrainNames) FillEntry(catalog.terrain, name, TerrainPath(name));
                foreach (var name in EffectNames) FillEntry(catalog.effects, name, EffectPath(name));
                if (catalog.shiftArrow == null) catalog.shiftArrow = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
                if (catalog.lineHighlight == null) catalog.lineHighlight = AssetDatabase.LoadAssetAtPath<GameObject>(HighlightPrefabPath);
                PrefabUtility.SaveAsPrefabAsset(root, rootPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>비어 있는 칸만 채운다. 다른 프리팹으로 바꿔 끼운 것은 그대로 둔다.</summary>
        private static void FillEntry(List<NamedPrefab> list, string name, string path)
        {
            var entry = list.Find(e => e.name == name);
            if (entry == null) { entry = new NamedPrefab { name = name }; list.Add(entry); }
            if (entry.prefab == null) entry.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ── UI ──

        private static GameObject BuildUi(string rootPrefabPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath);
            if (existing != null) return existing;

            // TMP 필수 리소스(셰이더, 설정)가 있어야 폰트 에셋을 만들 수 있다.
            if (!AssetDatabase.IsValidFolder(TmpFolder))
            {
                EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
                Debug.LogWarning("[Sheepft] TMP 필수 리소스를 불러왔다. 가져오기가 끝나면 Sheepft > Setup Game Scene을 한 번 더 돌린다.");
                return null;
            }

            var regular = FontAsset("AgravePro-Regular");
            var bold = FontAsset("AgravePro-Bold");
            if (regular == null || bold == null)
            {
                Debug.LogError("[Sheepft] 폰트 에셋을 만들지 못했다. " + FontFolder + "에 AgravePro-Regular.ttf와 AgravePro-Bold.ttf가 있어야 한다.");
                return null;
            }

            var sprites = AssetDatabase.LoadAssetAtPath<GameObject>(rootPrefabPath).GetComponent<SpriteCatalog>();
            var root = NewUi(sprites, regular, bold);
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, UiPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static TMP_FontAsset FontAsset(string fontName)
        {
            string assetPath = FontFolder + "/" + fontName + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/" + fontName + ".ttf");
            if (font == null) return null;

            // 동적 아틀라스: 쓰는 글자만 그때그때 채운다.
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic);
            if (asset == null) return null;
            AssetDatabase.CreateAsset(asset, assetPath);
            if (asset.material != null)
            {
                asset.material.name = fontName + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            if (asset.atlasTexture != null)
            {
                asset.atlasTexture.name = fontName + " Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>기준 해상도 640×360의 Canvas. 위쪽 줄 40px, 아래쪽 줄 48px, 그 사이가 맵이다.</summary>
        private static GameObject NewUi(SpriteCatalog sprites, TMP_FontAsset regular, TMP_FontAsset bold)
        {
            var root = new GameObject("GameUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var ui = root.AddComponent<GameUI>();

            Vector2 left = new Vector2(0f, 0.5f), right = new Vector2(1f, 0.5f), middle = new Vector2(0.5f, 0.5f);
            var icons = sprites.Get("hud_icons");

            // 위쪽 줄
            var top = Rect("TopBar", root.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 40f));
            Icon("TurnIcon", top, left, new Vector2(8f, 0f), icons == null ? null : icons.TagFrame("turn"));
            ui.turnsText = Text("TurnsText", top, bold, 26f, TextAlignmentOptions.Left, left, left, new Vector2(44f, 0f), new Vector2(52f, 32f));
            Icon("SheepIcon", top, left, new Vector2(100f, 0f), icons == null ? null : icons.TagFrame("goal_sheep"));
            ui.sheepText = Text("SheepText", top, bold, 26f, TextAlignmentOptions.Left, left, left, new Vector2(136f, 0f), new Vector2(64f, 32f));
            var warning = sprites.Get("cannot_win");
            var cannotWin = Icon("CannotWin", top, left, new Vector2(204f, 0f), warning == null ? null : warning.Frame(0));
            cannotWin.gameObject.SetActive(false);
            ui.cannotWinIcon = cannotWin.gameObject;
            ui.titleText = Text("TitleText", top, regular, 18f, TextAlignmentOptions.Center, middle, middle, new Vector2(40f, 0f), new Vector2(240f, 32f));
            ui.undoButton = SpriteButton("UndoButton", top, right, new Vector2(-44f, 0f), sprites.Get("button_undo"));
            ui.restartButton = SpriteButton("RestartButton", top, right, new Vector2(-8f, 0f), sprites.Get("button_restart"));

            // 아래쪽 줄
            var bottom = Rect("BottomBar", root.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 48f));
            ui.levelSelectButton = SpriteButton("LevelSelectButton", bottom, left, new Vector2(8f, 0f), sprites.Get("button_level_select"));
            ui.nextButton = SpriteButton("NextButton", bottom, right, new Vector2(-8f, 0f), sprites.Get("button_next"));
            ui.nextButton.gameObject.SetActive(false);
            ui.descriptionText = Text("DescriptionText", bottom, regular, 13f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var description = ui.descriptionText.rectTransform;
            description.offsetMin = new Vector2(48f, 4f);
            description.offsetMax = new Vector2(-48f, -4f);
            ui.descriptionText.textWrappingMode = TextWrappingModes.Normal;

            // 결과
            var result = Rect("ResultPanel", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(260f, 56f));
            result.gameObject.AddComponent<Image>().color = PanelColor;
            ui.resultPanel = result.gameObject;
            ui.resultText = Text("ResultText", result, bold, 20f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddStarRow(ui, icons);
            result.gameObject.SetActive(false);

            // 레벨 선택: 화면 전체를 덮는 판. 빈 곳을 누르면 닫힌다.
            var panel = Rect("LevelSelectPanel", root.transform, Vector2.zero, Vector2.one, middle, Vector2.zero, Vector2.zero);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            ui.levelSelectCloseButton = panel.gameObject.AddComponent<Button>();
            ui.levelSelectCloseButton.transition = Selectable.Transition.None;
            ui.levelSelectPanel = panel.gameObject;

            var grid = Rect("Grid", panel, middle, middle, middle, Vector2.zero, new Vector2(520f, 252f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(80f, 44f);
            layout.spacing = new Vector2(8f, 8f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
            layout.childAlignment = TextAnchor.MiddleCenter;
            ui.levelGrid = grid;

            var template = Rect("LevelButtonTemplate", grid, middle, middle, middle, Vector2.zero, new Vector2(80f, 44f));
            var templateImage = template.gameObject.AddComponent<Image>();
            templateImage.color = LevelColor;
            ui.levelButtonTemplate = template.gameObject.AddComponent<Button>();
            ui.levelButtonTemplate.targetGraphic = templateImage;
            var number = Text("Number", template, bold, 22f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            number.text = "1";
            template.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);

            ui.turnsText.text = "0";
            ui.sheepText.text = "0/1";
            ui.titleText.text = "1. Stage Name";
            ui.descriptionText.text = "Stage description.";
            ui.resultText.text = "CLEAR!";
            return root;
        }

        /// <summary>
        /// 결과 판에 클리어 별 줄(별 3개, 16×16 아이콘을 두 배로)을 넣는다.
        /// 판은 세로로 쌓고 내용에 맞춰 높이를 바꾼다. 별 줄을 끄면(패배) 글자만 남은 높이가 된다.
        /// 예전 GameUI.prefab에 별 줄을 더할 때도 이 함수를 쓴다.
        /// </summary>
        public static void AddStarRow(GameUI ui, SpriteSheet icons)
        {
            var result = (RectTransform)ui.resultPanel.transform;
            var layout = result.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = result.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 6, 10);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = result.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = result.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // 글자 한 줄의 높이는 소수(약 28.8)라서, 최소 높이를 정수로 주어 별 줄이 정수 픽셀에 오게 한다.
            var textElement = ui.resultText.GetComponent<LayoutElement>();
            if (textElement == null) textElement = ui.resultText.gameObject.AddComponent<LayoutElement>();
            textElement.minHeight = 30f;

            var old = result.Find("StarRow");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var middle = new Vector2(0.5f, 0.5f);
            var row = Rect("StarRow", result, middle, middle, middle, Vector2.zero, new Vector2(0f, 32f));
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 32f;
            element.preferredHeight = 32f;
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            ui.starSprite = icons == null ? null : icons.TagFrame("star");
            ui.starEmptySprite = icons == null ? null : icons.TagFrame("star_empty");
            ui.starImages = new Image[3];
            for (int i = 0; i < 3; i++)
                ui.starImages[i] = Icon("Star" + (i + 1), row, middle, Vector2.zero, i < 2 ? ui.starSprite : ui.starEmptySprite);
            ui.starRow = row.gameObject;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>16×16 아이콘을 두 배(32×32)로 그린다. anchor 쪽 가장자리에 붙인다.</summary>
        private static Image Icon(string name, Transform parent, Vector2 anchor, Vector2 position, Sprite sprite)
        {
            var rect = Rect(name, parent, anchor, anchor, anchor, position, new Vector2(32f, 32f));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 rectSize)
        {
            var pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            var rect = Rect(name, parent, anchorMin, anchorMax, pivot, position, rectSize);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = TextColor;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        /// <summary>도트 버튼. 태그 `normal`, `hover`, `pressed`를 Sprite Swap으로 쓴다.</summary>
        private static Button SpriteButton(string name, Transform parent, Vector2 anchor, Vector2 position, SpriteSheet sheet)
        {
            var rect = Rect(name, parent, anchor, anchor, anchor, position, new Vector2(32f, 32f));
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (sheet != null)
            {
                image.sprite = sheet.TagFrame("normal");
                button.transition = Selectable.Transition.SpriteSwap;
                var state = new SpriteState();
                state.highlightedSprite = sheet.TagFrame("hover");
                state.pressedSprite = sheet.TagFrame("pressed");
                state.selectedSprite = sheet.TagFrame("normal");
                button.spriteState = state;
            }
            return button;
        }

        // ── 씬 ──

        /// <summary>
        /// 열려 있는 씬에 GameUI와 EventSystem이 없으면 넣는다. 이미 있으면 건드리지 않는다.
        /// 넣은 것이 있으면 true.
        /// </summary>
        public static bool EnsureSceneObjects(GameObject uiPrefab)
        {
            bool changed = false;
            if (uiPrefab != null && UnityEngine.Object.FindFirstObjectByType<GameUI>(FindObjectsInactive.Include) == null)
            {
                PrefabUtility.InstantiatePrefab(uiPrefab, EditorSceneManager.GetActiveScene());
                changed = true;
            }
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                go.AddComponent<InputSystemUIInputModule>();
                changed = true;
            }
            return changed;
        }

        // ── 도우미 ──

        private static void IndexSprites()
        {
            spritePaths = new Dictionary<string, string>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { SpriteFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase))
                    spritePaths[Path.GetFileNameWithoutExtension(path)] = path;
            }
        }

        /// <summary>0번 프레임. Aseprite Importer는 프레임을 `Frame_번호`로, 한 장짜리는 파일 이름으로 만든다.</summary>
        private static Sprite FirstSprite(string name)
        {
            string path;
            if (!spritePaths.TryGetValue(name, out path)) return null;
            Sprite first = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var sprite = asset as Sprite;
                if (sprite == null) continue;
                if (sprite.name.EndsWith("_0")) return sprite;
                if (first == null) first = sprite;
            }
            return first;
        }

        /// <summary>Aseprite Importer가 태그마다 만든 클립과 Animator Controller를 그대로 쓴다. 상태 이름이 태그 이름이다.</summary>
        private static Animator AddAnimator(GameObject go, string name)
        {
            var animator = go.AddComponent<Animator>();
            string path;
            if (!spritePaths.TryGetValue(name, out path)) return animator;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var controller = asset as RuntimeAnimatorController;
                if (controller != null) { animator.runtimeAnimatorController = controller; break; }
            }
            return animator;
        }

        private static void CreateIfMissing(string path, Func<GameObject> make)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var go = make();
            try
            {
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>`fruit_basket` → `FruitBasket`</summary>
        private static string Pascal(string snake)
        {
            var parts = snake.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join("", parts);
        }
    }

    /// <summary>
    /// 에디터 전용. 메뉴 Sheepft > Preview Stage.
    /// Play하지 않고 스테이지 하나를 씬에 펼쳐 본다. 프리팹의 크기와 배치를 보면서 고치는 데 쓴다.
    /// 미리 보기는 씬에 저장되지 않는다. 애니메이션은 재생되지 않고 첫 그림만 보인다.
    /// </summary>
    public sealed class StagePreviewWindow : EditorWindow
    {
        private const string PreviewName = "Stage Preview (not saved)";
        private const string RootPrefabPath = "Assets/Prefabs/GameRoot.prefab";

        private int stage = 1;

        [MenuItem("Sheepft/Preview Stage")]
        public static void Open()
        {
            GetWindow<StagePreviewWindow>("Stage Preview");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Play하지 않고 스테이지를 씬에 펼쳐 본다. 씬에 저장되지 않는다.", MessageType.Info);
            stage = EditorGUILayout.IntField("Stage (1부터)", stage);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Show")) Show(stage);
                if (GUILayout.Button("Clear")) Clear();
            }
        }

        public static void Show(int stageNumber)
        {
            Clear();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);
            if (root == null) { Debug.LogError("[Sheepft] " + RootPrefabPath + "가 없다."); return; }
            var sprites = root.GetComponent<SpriteCatalog>();
            var prefabs = root.GetComponent<PrefabCatalog>();
            if (sprites == null || prefabs == null || sprites.stages == null)
            {
                Debug.LogError("[Sheepft] GameRoot가 준비되지 않았다. Sheepft > Setup Game Scene을 돌린다.");
                return;
            }

            var boards = LevelParser.ParseAll(sprites.stages.text);
            var board = boards[Mathf.Clamp(stageNumber, 1, boards.Count) - 1];

            var go = new GameObject(PreviewName);
            go.AddComponent<BoardView>().Build(sprites, prefabs, board);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            Selection.activeGameObject = go;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }

        public static void Clear()
        {
            var go = GameObject.Find(PreviewName);
            if (go != null) DestroyImmediate(go);
        }
    }
}
#endif
