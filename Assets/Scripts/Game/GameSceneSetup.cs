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
    /// Assets/Sprites의 .aseprite를 읽어 Import 설정을 맞추고, 스프라이트 목록을 담은
    /// GameRoot 프리팹과 Game 씬을 만든다. 스프라이트를 추가하거나 태그를 바꾸면 다시 돌린다.
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
        private const float PixelsPerUnit = 32f;

        [MenuItem("Sheepft/Setup Game Scene")]
        public static void Setup()
        {
            var paths = FindAsepriteFiles();
            int reimported = FixImportSettings(paths);
            var prefab = BuildPrefab(paths);
            BuildScene(prefab);
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

            var root = new GameObject("GameRoot");
            try
            {
                var catalog = root.AddComponent<SpriteCatalog>();
                root.AddComponent<GameController>();
                catalog.stages = AssetDatabase.LoadAssetAtPath<TextAsset>(StagesPath);
                if (catalog.stages == null) Debug.LogError("[Sheepft] 레벨 파일이 없다: " + StagesPath);
                foreach (var path in paths) catalog.sheets.Add(ReadSheet(path));
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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
            // 카메라와 Global Light 2D가 들어 있는 기본 씬을 복사해서 시작한다.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(TemplateScenePath, ScenePath))
                    throw new InvalidOperationException(TemplateScenePath + "를 복사하지 못했다.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == prefab.name) UnityEngine.Object.DestroyImmediate(go);
            PrefabUtility.InstantiatePrefab(prefab, scene);
            EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
#endif
