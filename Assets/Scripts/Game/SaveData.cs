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
