using System;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>게임이 내는 효과음. 소리 하나를 여러 순간에 같이 쓴다. 어디서 나는지는 SoundLibrary의 툴팁에 있다.</summary>
    public enum Sound
    {
        Shift,
        Step,
        Impact,
        Eat,
        WolfEat,
        SheepEaten,
        SheepArrive,
        HorseNeigh,
        CowMoo,
        Splash,
        Sizzle,
        Fire,
        Click,
        Win,
        Lose,
        Star,
        Transition,
        SheepBuff,
        UppercutCharge,
        WolfHit,
        WolfYelp
    }

    [Serializable]
    public sealed class SoundEntry : ISerializationCallbackReceiver
    {
        [Tooltip("여러 개 넣으면 재생할 때마다 하나를 무작위로 고른다. 같은 소리가 연달아 나지 않게 직전에 고른 것은 피한다.")]
        public AudioClip[] clips = new AudioClip[0];

        /// <summary>예전(소리 하나만 넣던 때)의 칸. 불러올 때 clips 맨 앞으로 옮기고 비운다.</summary>
        [SerializeField, HideInInspector] private AudioClip clip;

        [NonSerialized] private int lastPick = -1;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("재생할 때마다 피치를 이 비율만큼 무작위로 흔든다. 0.1이면 0.9–1.1배. 자주 나는 소리가 덜 지루해진다.")]
        [Range(0f, 0.3f)] public float pitchJitter;

        [Tooltip("재생을 이만큼(초) 늦춘다. 같은 순간에 나는 다른 소리와 겹치지 않게 할 때 쓴다.")]
        [Min(0f)] public float delay;

        public SoundEntry()
        {
        }

        public SoundEntry(float pitchJitter, float delay = 0f)
        {
            this.pitchJitter = pitchJitter;
            this.delay = delay;
        }

        /// <summary>넣어 둔 소리가 하나라도 있는가.</summary>
        public bool HasClip
        {
            get
            {
                if (clips == null) return false;
                foreach (var c in clips) if (c != null) return true;
                return false;
            }
        }

        /// <summary>재생할 소리 하나. 둘 이상이면 직전에 고른 것을 빼고 무작위로 고른다. 없으면 null.</summary>
        public AudioClip Pick()
        {
            if (clips == null) return null;
            int count = 0;
            foreach (var c in clips) if (c != null) count++;
            if (count == 0) return null;

            int pick = UnityEngine.Random.Range(0, count);
            if (count > 1 && pick == lastPick) pick = (pick + 1 + UnityEngine.Random.Range(0, count - 1)) % count;
            lastPick = pick;
            foreach (var c in clips)
            {
                if (c == null) continue;
                if (pick-- == 0) return c;
            }
            return null;
        }

        public bool Contains(AudioClip audioClip)
        {
            return audioClip != null && clips != null && Array.IndexOf(clips, audioClip) >= 0;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (clip == null) return;
            if (clips == null) clips = new AudioClip[0];
            if (Array.IndexOf(clips, clip) < 0)
            {
                var moved = new AudioClip[clips.Length + 1];
                moved[0] = clip;
                Array.Copy(clips, 0, moved, 1, clips.Length);
                clips = moved;
            }
            clip = null;
        }
    }

    /// <summary>
    /// 음악과 효과음 목록. `Assets/Data/SoundLibrary.asset`에 소리 파일을 끌어다 놓으면 SoundManager가 재생한다.
    /// 비어 있는 칸은 소리 없이 넘어간다. GameSettings의 sounds 칸이 이 에셋을 가리킨다.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "Sheepft/Sound Library")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Header("음악 (반복 재생)")]
        [Tooltip("메뉴(타이틀, 스테이지 선택, 설정)에서 튼다.")]
        public AudioClip titleMusic;

        [Tooltip("스테이지 안에서 튼다.")]
        public AudioClip gameMusic;

        [Range(0f, 1f)] public float musicVolume = 0.7f;

        [Header("판")]
        [Tooltip("줄 밀기. 메뉴에서 화살표로 화면 넘기기, 스테이지 스크롤, 스테이지 들어가기, 설정 값 바꾸기도 이 소리다.")]
        public SoundEntry shift = new SoundEntry(0.05f);

        [Tooltip("늑대·말·소가 1칸 걸을 때. 여러 마리가 같이 걸어도 한 번만 난다.")]
        public SoundEntry step = new SoundEntry(0.1f);

        [Tooltip("염소 박치기, 소가 앞 칸을 밈, 늑대 합류(싸움). 메뉴에서 잠긴 스테이지와 설정 줄 끝의 돌에 막힐 때도. (shift가 장애물에 막힐 때는 잠시 껐다: GameController.Play)")]
        public SoundEntry impact = new SoundEntry(0.1f);

        [Header("먹기")]
        [Tooltip("말이나 소가 과일 바구니를 먹을 때.")]
        public SoundEntry eat = new SoundEntry(0.08f);

        [Tooltip("늑대가 양을 먹을 때 (덥석). sheepEaten과 같이 난다.")]
        public SoundEntry wolfEat = new SoundEntry(0.08f);

        [Tooltip("늑대에게 먹히는 양의 짧은 \"메\". wolfEat과 같이 나므로 조금 늦춘다.")]
        public SoundEntry sheepEaten = new SoundEntry(0.05f, 0.1f);

        [Header("동물")]
        [Tooltip("젖지 않고 화상 입지 않은 양이 풀에 도착할 때.")]
        public SoundEntry sheepArrive = new SoundEntry(0.05f);

        [Tooltip("말이 과일 바구니를 발견할 때.")]
        public SoundEntry horseNeigh = new SoundEntry(0.05f);

        [Tooltip("소가 냄새로 목표 바구니를 정할 때.")]
        public SoundEntry cowMoo = new SoundEntry(0.05f);

        [Header("물과 불")]
        [Tooltip("물에 젖을 때, 빈 양동이에 물이 찰 때. 물에서 화상이 나을 때도 함께 젖으므로 이 소리가 난다.")]
        public SoundEntry splash = new SoundEntry(0.08f);

        [Tooltip("젖은 것이 들불에서 마를 때, 물 찬 양동이가 불을 꿀 때.")]
        public SoundEntry sizzle = new SoundEntry(0.08f);

        [Tooltip("칸에 불이 붙을 때(확산 포함), 동물이 화상을 입을 때, 바구니나 양동이가 탈 때.")]
        public SoundEntry fire = new SoundEntry(0.08f);

        [Header("UI")]
        [Tooltip("게임 화면의 버튼과 단축키: Undo(Z, U), Restart(R), Next(Enter, Space), 메뉴로(Esc, Level Select 버튼).")]
        public SoundEntry click = new SoundEntry();

        [Tooltip("클리어. 첫 별이 박히기 전(GameSettings의 starFirstDelaySeconds)에 끝나는 짧은 소리가 좋다.")]
        public SoundEntry win = new SoundEntry();

        [Tooltip("패배. \"승리 불가\" 표시가 처음 뜰 때도 난다.")]
        public SoundEntry lose = new SoundEntry();

        [Tooltip("클리어 화면에서 별이 박힐 때. 별마다 starPitches의 피치로 난다. 피치 흔들기는 쓰지 않는다.")]
        public SoundEntry star = new SoundEntry();

        [Tooltip("별 번호(왼쪽부터)마다의 피치. 기본은 도·미·솔.")]
        public float[] starPitches = { 1f, 1.26f, 1.5f };

        [Tooltip("구름 화면 전환이 화면을 덮기 시작할 때: 메뉴에서 스테이지 들어가기, Restart, Next, 게임에서 메뉴로.")]
        public SoundEntry transition = new SoundEntry();

        [Header("엔딩")]
        [Tooltip("양이 생강꽃(spicebush)을 먹고 근육질 양이 될 때의 굵은 \"메\".")]
        public SoundEntry sheepBuff = new SoundEntry();

        [Tooltip("엔딩 컷씬에서 주먹에 기운(power_charge)이 모이기 시작할 때. 소리가 길어도 주먹이 늑대를 치는 순간 끊긴다.")]
        public SoundEntry uppercutCharge = new SoundEntry();

        [Tooltip("엔딩 컷씬에서 어퍼컷이 늑대에 맞아 시간이 멈추는 순간의 경쾌한 타격음.")]
        public SoundEntry wolfHit = new SoundEntry();

        [Tooltip("엔딩 컷씬에서 시간이 다시 흘러 늑대가 날아갈 때의 \"깨갱\".")]
        public SoundEntry wolfYelp = new SoundEntry();

        public SoundEntry Get(Sound sound)
        {
            switch (sound)
            {
                case Sound.Shift: return shift;
                case Sound.Step: return step;
                case Sound.Impact: return impact;
                case Sound.Eat: return eat;
                case Sound.WolfEat: return wolfEat;
                case Sound.SheepEaten: return sheepEaten;
                case Sound.SheepArrive: return sheepArrive;
                case Sound.HorseNeigh: return horseNeigh;
                case Sound.CowMoo: return cowMoo;
                case Sound.Splash: return splash;
                case Sound.Sizzle: return sizzle;
                case Sound.Fire: return fire;
                case Sound.Click: return click;
                case Sound.Win: return win;
                case Sound.Lose: return lose;
                case Sound.Star: return star;
                case Sound.Transition: return transition;
                case Sound.SheepBuff: return sheepBuff;
                case Sound.UppercutCharge: return uppercutCharge;
                case Sound.WolfHit: return wolfHit;
                case Sound.WolfYelp: return wolfYelp;
                default: return null;
            }
        }

        /// <summary>i번째 별(0부터)의 피치. 목록보다 별이 많으면 마지막 값을 쓴다.</summary>
        public float StarPitch(int index)
        {
            if (starPitches == null || starPitches.Length == 0) return 1f;
            return starPitches[Mathf.Clamp(index, 0, starPitches.Length - 1)];
        }
    }
}
