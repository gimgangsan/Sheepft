using System.Runtime.InteropServices;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 진행과 설정 저장. 구현 계획 11절. 내용은 SaveFile 하나를 JSON으로 통째로 읽고 쓴다.
    ///
    /// 저장소:
    /// - WebGL 빌드: 브라우저 localStorage의 고정 키(StorageKey). SaveStorage.jslib가 읽고 쓴다.
    ///   PlayerPrefs는 WebGL에서 페이지 주소로 경로를 정하는데, itch.io는 빌드를 올릴 때마다 주소가 바뀌어
    ///   기록이 사라진다. localStorage는 같은 출처(html-classic.itch.zone)면 주소가 바뀌어도 남는다.
    ///   그 출처를 다른 게임들과 함께 쓰므로 키에 게임 이름을 넣어 겹치지 않게 한다.
    /// - 에디터와 그 밖의 플랫폼: PlayerPrefs의 같은 키에 같은 JSON.
    ///
    /// stages.txt가 바뀌면(지문이 다르면) 진행 기록을 지운다. 설정은 남는다. Init을 게임 시작 때 한 번 부른다.
    /// 스테이지 번호는 0부터 센다.
    /// </summary>
    public static class SaveData
    {
        public const string StorageKey = "sheepft.gameoff2026.save";

        /// <summary>음악·효과음 크기 단계 수(0–5).</summary>
        public const int VolumeSteps = 6;

        /// <summary>이동 속도 단계별 시간 배율: 매우 느림, 느림, 보통, 빠름.</summary>
        public static readonly float[] SpeedScales = { 2f, 1.5f, 1f, 0.6f };
        public static readonly string[] SpeedNames = { "Very Slow", "Slow", "Normal", "Fast" };
        public const int DefaultSpeed = SaveFile.DefaultSpeed;

        private static SaveFile file;

        private static SaveFile File
        {
            get
            {
                if (file == null) file = SaveFile.FromJson(ReadStorage());
                return file;
            }
        }

        /// <summary>
        /// 게임 시작 때 부른다. 저장 파일을 읽고, stages.txt 지문이 다르면 진행 기록을 지운 뒤 저장한다.
        /// </summary>
        public static void Init(string stagesText)
        {
            file = SaveFile.FromJson(ReadStorage());
            if (file.MatchStages(SaveFile.StagesFingerprint(stagesText)))
                Debug.Log("[Sheepft] stages.txt가 바뀌어 진행 기록을 지웠다(설정은 남김).");
            Write();
        }

        // ── 진행 ──

        /// <summary>열린 스테이지 수. 처음에는 1번 하나만 열려 있다.</summary>
        public static int Unlocked(int stageCount)
        {
            return Mathf.Clamp(File.unlocked, 1, Mathf.Max(1, stageCount));
        }

        /// <summary>마지막으로 연 스테이지. 메뉴에 들어올 때 양을 이 줄에 놓는다.</summary>
        public static int LastStage(int stageCount)
        {
            return Mathf.Clamp(File.lastStage, 0, Mathf.Max(0, stageCount - 1));
        }

        public static void SetLastStage(int stage)
        {
            if (File.lastStage == stage) return;
            File.lastStage = stage;
            Write();
        }

        /// <summary>그 스테이지에서 얻은 가장 많은 별 수. 깨지 않았으면 0.</summary>
        public static int Stars(int stage) => File.Stars(stage);

        public static bool IsCleared(int stage) => Stars(stage) > 0;

        /// <summary>스테이지를 깼다. 별 수는 더 많을 때만 바꾸고, 다음 스테이지를 연다.</summary>
        public static void RecordClear(int stage, int stars, int stageCount)
        {
            File.RecordClear(stage, stars, stageCount);
            Write();
        }

        /// <summary>스테이지를 건너뛰었다. 별은 그대로 두고 다음 스테이지만 연다.</summary>
        public static void RecordSkip(int stage, int stageCount)
        {
            File.RecordSkip(stage, stageCount);
            Write();
        }

        // ── 설정 ──

        /// <summary>음악 크기 0–5. 기본 3.</summary>
        public static int Music
        {
            get => Mathf.Clamp(File.music, 0, VolumeSteps - 1);
            set { File.music = value; Write(); }
        }

        /// <summary>효과음 크기 0–5. 기본 3.</summary>
        public static int Sound
        {
            get => Mathf.Clamp(File.sound, 0, VolumeSteps - 1);
            set { File.sound = value; Write(); }
        }

        /// <summary>이동 속도 단계 0–3(SpeedScales의 번호). 기본 2(보통).</summary>
        public static int Speed
        {
            get => Mathf.Clamp(File.speed, 0, SpeedScales.Length - 1);
            set { File.speed = Mathf.Clamp(value, 0, SpeedScales.Length - 1); Write(); }
        }

        /// <summary>연출 시간(이동, 쉬는 시간, 염소 박치기)에 곱하는 배율.</summary>
        public static float SpeedScale => SpeedScales[Speed];

        // ── 저장소 ──

        private static void Write()
        {
            WriteStorage(File.ToJson());
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string SheepftStorageGet(string key);
        [DllImport("__Internal")] private static extern void SheepftStorageSet(string key, string value);
        [DllImport("__Internal")] private static extern void SheepftStorageRemove(string key);

        private static string ReadStorage() => SheepftStorageGet(StorageKey);
        private static void WriteStorage(string json) => SheepftStorageSet(StorageKey, json);
        private static void DeleteStorage() => SheepftStorageRemove(StorageKey);
#else
        private static string ReadStorage() => PlayerPrefs.GetString(StorageKey, "");

        private static void WriteStorage(string json)
        {
            PlayerPrefs.SetString(StorageKey, json);
            PlayerPrefs.Save();
        }

        private static void DeleteStorage()
        {
            PlayerPrefs.DeleteKey(StorageKey);
            PlayerPrefs.Save();
        }
#endif

#if UNITY_EDITOR
        // 예전(2026-11-16) 형식은 값마다 키가 따로 있었다. 기록 지우기에서 함께 지운다.
        private static readonly string[] OldKeys =
            { "sheepft.unlocked", "sheepft.lastStage", "sheepft.music", "sheepft.sound", "sheepft.speed" };

        [UnityEditor.MenuItem("Sheepft/Reset Save Data")]
        private static void ResetFromMenu()
        {
            DeleteStorage();
            foreach (var key in OldKeys) PlayerPrefs.DeleteKey(key);
            for (int i = 0; i < 100; i++) PlayerPrefs.DeleteKey("sheepft.stars." + i);
            PlayerPrefs.Save();
            file = null;
            Debug.Log("[Sheepft] 저장 기록을 지웠다(진행과 설정 모두).");
        }

        [UnityEditor.MenuItem("Sheepft/Unlock All Stages")]
        private static void UnlockAllFromMenu()
        {
            File.unlocked = 1000;
            Write();
            Debug.Log("[Sheepft] 모든 스테이지를 열었다.");
        }
#endif
    }
}
