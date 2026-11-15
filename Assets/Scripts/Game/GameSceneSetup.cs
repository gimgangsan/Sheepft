#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 에디터 전용. 메뉴 Sheepft > Setup Game Scene.
    /// Assets/Sprites의 .aseprite를 읽어 Import 설정을 맞추고, GameRoot 프리팹의 스프라이트 목록을 채운다.
    /// 스프라이트를 추가하거나 태그를 바꾸면 다시 돌린다.
    /// 손으로 고친 것은 덮어쓰지 않는다: 프리팹은 스프라이트 목록과 레벨 파일 참조만 갱신하고,
    /// Game 씬과 GameSettings 에셋은 없을 때만 만든다.
    /// 빌드에는 들어가지 않는다.
    /// </summary>
    public static class GameSceneSetup
    {
        private const string SpriteFolder = "Assets/Sprites";
        private const string StagesPath = "Assets/Levels/stages.txt";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath = "Assets/Prefabs/GameRoot.prefab";
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string TemplateScenePath = "Assets/Scenes/SampleScene.unity";
        private const string DataFolder = "Assets/Data";
        private const string SettingsPath = "Assets/Data/GameSettings.asset";
        private const float PixelsPerUnit = 32f;

        [MenuItem("Sheepft/Setup Game Scene")]
        public static void Setup()
        {
            var paths = FindAsepriteFiles();
            int reimported = FixImportSettings(paths);
            var prefab = BuildPrefab(paths);
            // 오브젝트·지형·이펙트·UI 프리팹은 없을 때만 만든다. 있는 것은 건드리지 않는다.
            var uiPrefab = GamePrefabBuilder.Build(PrefabPath);
            BuildScene(prefab);

            // 씬에 UI와 EventSystem이 없으면 넣는다. Game 씬이 열려 있을 때만 한다.
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath)
            {
                if (GamePrefabBuilder.EnsureSceneObjects(uiPrefab)) EditorSceneManager.SaveScene(active);
            }
            else
            {
                Debug.LogWarning("[Sheepft] " + ScenePath + "가 열려 있지 않아 씬의 UI는 확인하지 않았다. 그 씬을 열고 다시 돌린다.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Sheepft] 설정 완료: .aseprite " + paths.Count + "개 (Import 설정을 고친 것 " + reimported + "개), "
                + PrefabPath + ", " + ScenePath);
        }

        private static List<string> FindAsepriteFiles()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { SpriteFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase) && !paths.Contains(path)) paths.Add(path);
            }
            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        // ── Import 설정 (CLAUDE.md: Pixels Per Unit 32, Filter Mode Point, Compression None) ──

        private static int FixImportSettings(List<string> paths)
        {
            int count = 0;
            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path);
                if (importer == null) continue;

                // Aseprite Importer의 어셈블리를 참조하지 않으려고 직렬화된 속성을 이름으로 찾는다.
                var so = new SerializedObject(importer);
                bool changed = false;
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    string name = it.name.ToLowerInvariant();
                    if (it.propertyType == SerializedPropertyType.Float && name.EndsWith("spritepixelstounits"))
                    {
                        if (!Mathf.Approximately(it.floatValue, PixelsPerUnit)) { it.floatValue = PixelsPerUnit; changed = true; }
                    }
                    else if (it.propertyType == SerializedPropertyType.Integer || it.propertyType == SerializedPropertyType.Enum)
                    {
                        bool isFilter = name.EndsWith("filtermode");
                        bool isCompression = name.EndsWith("texturecompression");
                        if ((isFilter || isCompression) && it.intValue != 0) { it.intValue = 0; changed = true; }
                    }
                }

                if (!changed) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
                count++;
            }
            return count;
        }

        // ── 프리팹 ──

        private static GameObject BuildPrefab(List<string> paths)
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets", "Prefabs");
            var settings = FindOrCreateSettings();

            // 프리팹이 이미 있으면 그 내용을 열어 필요한 값만 갱신한다. 손으로 붙인 컴포넌트와 값은 그대로 남는다.
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("GameRoot");
            try
            {
                var catalog = root.GetComponent<SpriteCatalog>();
                if (catalog == null) catalog = root.AddComponent<SpriteCatalog>();
                if (root.GetComponent<PrefabCatalog>() == null) root.AddComponent<PrefabCatalog>();
                if (root.GetComponent<GameController>() == null) root.AddComponent<GameController>();

                if (catalog.stages == null) catalog.stages = AssetDatabase.LoadAssetAtPath<TextAsset>(StagesPath);
                if (catalog.stages == null) Debug.LogError("[Sheepft] 레벨 파일이 없다: " + StagesPath);
                if (catalog.settings == null) catalog.settings = settings;

                // 스프라이트 목록만은 매번 .aseprite에서 다시 읽는다. 손으로 고치지 않는다.
                catalog.sheets.Clear();
                foreach (var path in paths) catalog.sheets.Add(ReadSheet(path));
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>조정값 에셋. 있으면 그대로 쓰고, 없을 때만 기본값으로 만든다.</summary>
        private static GameSettings FindOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(SettingsPath);
            if (settings != null) return settings;

            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets", "Data");
            settings = ScriptableObject.CreateInstance<GameSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static SpriteSheet ReadSheet(string path)
        {
            var sheet = new SpriteSheet();
            sheet.name = Path.GetFileNameWithoutExtension(path);
            ReadAsepriteFile(path, sheet);

            // Aseprite Importer는 프레임마다 스프라이트를 `Frame_번호` 이름으로 만든다. 빈 프레임에는 스프라이트가 없다.
            sheet.frames = new Sprite[sheet.durationsMs.Length];
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var sprite = asset as Sprite;
                if (sprite == null) continue;
                // 프레임이 하나뿐인 파일은 스프라이트 이름이 파일 이름과 같다.
                if (sheet.frames.Length == 1) { sheet.frames[0] = sprite; continue; }
                int underscore = sprite.name.LastIndexOf('_');
                int index;
                if (underscore < 0 || !int.TryParse(sprite.name.Substring(underscore + 1), out index)
                    || index < 0 || index >= sheet.frames.Length)
                {
                    Debug.LogWarning("[Sheepft] " + path + ": 프레임 번호를 알 수 없는 스프라이트 '" + sprite.name + "'");
                    continue;
                }
                sheet.frames[index] = sprite;
            }
            return sheet;
        }

        /// <summary>.aseprite 파일에서 프레임 길이와 태그를 읽는다. 그림은 읽지 않는다.</summary>
        private static void ReadAsepriteFile(string path, SpriteSheet sheet)
        {
            const int HeaderMagic = 0xA5E0, FrameMagic = 0xF1FA, TagsChunk = 0x2018;
            var durations = new List<int>();
            var tags = new List<SpriteTag>();

            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                reader.ReadUInt32();
                if (reader.ReadUInt16() != HeaderMagic) throw new InvalidDataException(path + ": Aseprite 파일이 아니다.");
                int frameCount = reader.ReadUInt16();
                reader.BaseStream.Position = 128;

                for (int f = 0; f < frameCount; f++)
                {
                    long frameStart = reader.BaseStream.Position;
                    uint frameBytes = reader.ReadUInt32();
                    if (reader.ReadUInt16() != FrameMagic) throw new InvalidDataException(path + ": 프레임 " + f + "을 읽지 못했다.");
                    uint oldChunks = reader.ReadUInt16();
                    durations.Add(reader.ReadUInt16());
                    reader.ReadBytes(2);
                    uint newChunks = reader.ReadUInt32();
                    uint chunks = newChunks != 0 ? newChunks : oldChunks;

                    for (uint c = 0; c < chunks; c++)
                    {
                        long chunkStart = reader.BaseStream.Position;
                        uint chunkBytes = reader.ReadUInt32();
                        int type = reader.ReadUInt16();
                        if (type == TagsChunk)
                        {
                            int n = reader.ReadUInt16();
                            reader.ReadBytes(8);
                            for (int i = 0; i < n; i++)
                            {
                                var tag = new SpriteTag();
                                tag.from = reader.ReadUInt16();
                                tag.to = reader.ReadUInt16();
                                reader.ReadBytes(1 + 2 + 6 + 3 + 1);
                                int length = reader.ReadUInt16();
                                tag.name = Encoding.UTF8.GetString(reader.ReadBytes(length));
                                tags.Add(tag);
                            }
                        }
                        reader.BaseStream.Position = chunkStart + chunkBytes;
                    }
                    reader.BaseStream.Position = frameStart + frameBytes;
                }
            }

            sheet.durationsMs = durations.ToArray();
            sheet.tags = tags.ToArray();
        }

        // ── 씬 ──

        private static void BuildScene(GameObject prefab)
        {
            // 씬이 이미 있으면 건드리지 않는다. 씬의 GameRoot는 프리팹 인스턴스라서 프리팹이 바뀌면 따라온다.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;

            // 카메라와 Global Light 2D가 들어 있는 기본 씬을 복사해서 시작한다.
            if (!AssetDatabase.CopyAsset(TemplateScenePath, ScenePath))
                throw new InvalidOperationException(TemplateScenePath + "를 복사하지 못했다.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            PrefabUtility.InstantiatePrefab(prefab, scene);
            EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
#endif
