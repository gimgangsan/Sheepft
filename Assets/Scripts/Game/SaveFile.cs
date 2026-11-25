using System;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 저장 파일 한 개의 내용. JSON 한 덩어리로 저장한다(SaveData가 읽고 쓴다). 구현 계획 11절.
    /// 저장소와 상관없는 순수한 값과 규칙만 둔다. 스테이지 번호는 0부터 센다.
    /// </summary>
    [Serializable]
    public sealed class SaveFile
    {
        /// <summary>파일 형식이 바뀌면 올린다. 다른 버전의 파일은 버리고 새로 시작한다.</summary>
        public const int CurrentVersion = 1;

        public const int DefaultVolume = 3;
        public const int DefaultSpeed = 2;

        public int version = CurrentVersion;

        /// <summary>
        /// 진행 기록을 남길 때의 stages.txt 지문(StagesFingerprint). 스테이지 구성이 바뀌면 달라지고,
        /// 그러면 진행 기록(열림, 별, 마지막 스테이지)을 지운다. 설정은 남긴다.
        /// </summary>
        public string stages = "";

        /// <summary>열린 스테이지 수. 1번은 처음부터 열려 있다.</summary>
        public int unlocked = 1;

        public int lastStage;

        /// <summary>스테이지별 가장 많이 얻은 별 수(0–3). 길이는 필요할 때 늘린다.</summary>
        public int[] stars = new int[0];

        public int music = DefaultVolume;
        public int sound = DefaultVolume;
        public int speed = DefaultSpeed;

        // ── 진행 ──

        public int Stars(int stage) => stage >= 0 && stage < stars.Length ? stars[stage] : 0;

        /// <summary>스테이지를 깼다. 별 수는 더 많을 때만 바꾸고, 다음 스테이지를 연다.</summary>
        public void RecordClear(int stage, int starCount, int stageCount)
        {
            if (stage < 0) return;
            if (stage >= stars.Length) Array.Resize(ref stars, stage + 1);
            if (starCount > stars[stage]) stars[stage] = starCount;
            unlocked = Mathf.Max(unlocked, Mathf.Min(stage + 2, stageCount));
        }

        /// <summary>건너뛰기: 그 스테이지는 깨지 않은 채로(별 0개) 다음 스테이지를 연다.</summary>
        public void RecordSkip(int stage, int stageCount)
        {
            if (stage < 0) return;
            unlocked = Mathf.Max(unlocked, Mathf.Min(stage + 2, stageCount));
        }

        /// <summary>진행 기록만 처음으로 돌린다. 설정(음악, 효과음, 이동 속도)은 그대로 둔다.</summary>
        public void ResetProgress()
        {
            unlocked = 1;
            lastStage = 0;
            stars = new int[0];
        }

        /// <summary>
        /// 지금의 stages.txt 지문과 맞춘다. 저장된 지문과 다르면 스테이지 구성이 바뀐 것이므로 진행 기록을 지운다.
        /// 지운 경우 true.
        /// </summary>
        public bool MatchStages(string fingerprint)
        {
            if (stages == fingerprint) return false;
            bool hadProgress = !string.IsNullOrEmpty(stages);
            stages = fingerprint;
            if (!hadProgress) return false;
            ResetProgress();
            return true;
        }

        // ── 읽고 쓰기 ──

        public string ToJson() => JsonUtility.ToJson(this);

        /// <summary>JSON을 읽는다. 비었거나, 깨졌거나, 다른 버전이면 새 파일을 돌려준다.</summary>
        public static SaveFile FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return new SaveFile();
            try
            {
                var file = JsonUtility.FromJson<SaveFile>(json);
                if (file == null || file.version != CurrentVersion) return new SaveFile();
                if (file.stars == null) file.stars = new int[0];
                if (file.stages == null) file.stages = "";
                return file;
            }
            catch (ArgumentException)
            {
                return new SaveFile();
            }
        }

        /// <summary>
        /// stages.txt 내용의 지문. 줄 끝(CRLF/LF)과 앞뒤 공백 차이는 무시한다(git이 줄 끝을 바꿔도 같다).
        /// 플랫폼마다 같은 값이 나와야 하므로 string.GetHashCode 대신 FNV-1a 64비트를 쓴다.
        /// </summary>
        public static string StagesFingerprint(string stagesText)
        {
            if (stagesText == null) return "";
            ulong hash = 14695981039346656037UL;
            foreach (var raw in stagesText.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                foreach (char ch in line)
                {
                    hash ^= ch;
                    hash *= 1099511628211UL;
                }
                hash ^= '\n';
                hash *= 1099511628211UL;
            }
            return hash.ToString("x16");
        }
    }
}
