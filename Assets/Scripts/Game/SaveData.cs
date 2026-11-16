using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 진행 저장. PlayerPrefs를 쓰므로 WebGL 빌드에서도 브라우저에 남는다. 구현 계획 11절.
    /// 스테이지 번호는 0부터 센다.
    /// </summary>
    public static class SaveData
    {
        private const string UnlockedKey = "sheepft.unlocked";
        private const string LastStageKey = "sheepft.lastStage";
        private const string StarsKeyPrefix = "sheepft.stars.";
        private const string MusicKey = "sheepft.music";
        private const string SoundKey = "sheepft.sound";
        private const string SpeedKey = "sheepft.speed";

        // ── 설정 (구현 계획 11절) ──

        /// <summary>음악·효과음 크기 단계 수(0–5).</summary>
        public const int VolumeSteps = 6;

        /// <summary>이동 속도 단계별 시간 배율: 매우 느림, 느림, 보통, 빠름.</summary>
        public static readonly float[] SpeedScales = { 2f, 1.5f, 1f, 0.6f };
        public static readonly string[] SpeedNames = { "Very Slow", "Slow", "Normal", "Fast" };
        public const int DefaultSpeed = 2;

        private static int speedCache = -1;

        /// <summary>음악 크기 0–5. 기본 3.</summary>
        public static int Music
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(MusicKey, 3), 0, VolumeSteps - 1);
            set { PlayerPrefs.SetInt(MusicKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>효과음 크기 0–5. 기본 3.</summary>
        public static int Sound
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SoundKey, 3), 0, VolumeSteps - 1);
            set { PlayerPrefs.SetInt(SoundKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>이동 속도 단계 0–3(SpeedScales의 번호). 기본 2(보통).</summary>
        public static int Speed
        {
            get
            {
                if (speedCache < 0) speedCache = Mathf.Clamp(PlayerPrefs.GetInt(SpeedKey, DefaultSpeed), 0, SpeedScales.Length - 1);
                return speedCache;
            }
            set
            {
                speedCache = Mathf.Clamp(value, 0, SpeedScales.Length - 1);
                PlayerPrefs.SetInt(SpeedKey, speedCache);
                PlayerPrefs.Save();
            }
        }

        /// <summary>연출 시간(이동, 쉬는 시간, 염소 박치기)에 곱하는 배율.</summary>
        public static float SpeedScale => SpeedScales[Speed];

        /// <summary>열린 스테이지 수. 처음에는 1번 하나만 열려 있다.</summary>
        public static int Unlocked(int stageCount)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, Mathf.Max(1, stageCount));
        }

        /// <summary>마지막으로 연 스테이지. 메뉴에 들어올 때 양을 이 줄에 놓는다.</summary>
        public static int LastStage(int stageCount)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(LastStageKey, 0), 0, Mathf.Max(0, stageCount - 1));
        }

        public static void SetLastStage(int stage)
        {
            PlayerPrefs.SetInt(LastStageKey, stage);
            PlayerPrefs.Save();
        }

        /// <summary>그 스테이지에서 얻은 가장 많은 별 수. 깨지 않았으면 0.</summary>
        public static int Stars(int stage)
        {
            return PlayerPrefs.GetInt(StarsKeyPrefix + stage, 0);
        }

        public static bool IsCleared(int stage) => Stars(stage) > 0;

        /// <summary>스테이지를 깼다. 별 수는 더 많을 때만 바꾸고, 다음 스테이지를 연다.</summary>
        public static void RecordClear(int stage, int stars, int stageCount)
        {
            if (stars > Stars(stage)) PlayerPrefs.SetInt(StarsKeyPrefix + stage, stars);
            int unlocked = Mathf.Min(stage + 2, stageCount);
            if (unlocked > PlayerPrefs.GetInt(UnlockedKey, 1)) PlayerPrefs.SetInt(UnlockedKey, unlocked);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Sheepft/Reset Save Data")]
        private static void ResetFromMenu()
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(LastStageKey);
            PlayerPrefs.DeleteKey(MusicKey);
            PlayerPrefs.DeleteKey(SoundKey);
            PlayerPrefs.DeleteKey(SpeedKey);
            speedCache = -1;
            for (int i = 0; i < 100; i++) PlayerPrefs.DeleteKey(StarsKeyPrefix + i);
            PlayerPrefs.Save();
            Debug.Log("[Sheepft] 저장 기록을 지웠다.");
        }

        [UnityEditor.MenuItem("Sheepft/Unlock All Stages")]
        private static void UnlockAllFromMenu()
        {
            PlayerPrefs.SetInt(UnlockedKey, 100);
            PlayerPrefs.Save();
            Debug.Log("[Sheepft] 모든 스테이지를 열었다.");
        }
#endif
    }
}
