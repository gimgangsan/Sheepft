using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 음악과 효과음을 재생한다. 어디서든 `SoundManager.Play(Sound.Shift)`처럼 부른다.
    /// 소리를 내는 오브젝트마다 AudioSource를 두지 않고, 숨은 오브젝트 하나에 AudioSource를 모아 돌려 쓴다.
    ///
    /// - GameController가 시작할 때 Init으로 SoundLibrary를 넘긴다. 그 전에 부르거나 칸이 비어 있으면 소리 없이 넘어간다.
    /// - 크기는 설정의 Music·Sound 단계(SaveData)를 따른다. 효과음은 재생할 때마다 읽고, 음악은 RefreshVolume에서 맞춘다.
    /// - 같은 효과음은 한 프레임에 한 번만 낸다. 늑대 여러 마리가 같이 걷거나 불이 여러 칸에 한꺼번에 붙어도 한 번 난다.
    /// </summary>
    public static class SoundManager
    {
        /// <summary>동시에 낼 수 있는 효과음 수. 모두 울리고 있으면 가장 먼저 쓴 것을 끊는다.</summary>
        private const int EffectSources = 12;

        private static SoundLibrary library;
        private static GameObject host;
        private static AudioSource music;
        private static AudioSource[] effects;
        private static int next;
        private static readonly int[] lastFrame = new int[System.Enum.GetValues(typeof(Sound)).Length];

        public static void Init(SoundLibrary sounds)
        {
            library = sounds;
            if (library == null) Debug.LogWarning("[Sheepft] GameSettings의 sounds가 비어 있어 소리를 내지 않는다.");
            for (int i = 0; i < lastFrame.Length; i++) lastFrame[i] = -1;

            // Domain Reload를 끄고 Play해도 지난번 오브젝트는 사라져 있으므로 null 비교로 다시 만든다.
            if (host != null) return;
            host = new GameObject("Sounds");
            Object.DontDestroyOnLoad(host);
            music = NewSource();
            music.loop = true;
            effects = new AudioSource[EffectSources];
            for (int i = 0; i < effects.Length; i++) effects[i] = NewSource();
            next = 0;
        }

        private static AudioSource NewSource()
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private static bool Ready => library != null && host != null;

        private static float EffectScale => SaveData.Sound / (float)(SaveData.VolumeSteps - 1);

        private static float MusicScale => SaveData.Music / (float)(SaveData.VolumeSteps - 1);

        // ── 효과음 ──

        public static void Play(Sound sound)
        {
            if (!Ready) return;
            int index = (int)sound;
            if (lastFrame[index] == Time.frameCount) return;
            var entry = library.Get(sound);
            if (entry == null || entry.clip == null) return;
            lastFrame[index] = Time.frameCount;
            float pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
            PlayEntry(entry, pitch);
        }

        /// <summary>클리어 화면의 i번째 별(0부터). 별마다 피치가 올라간다.</summary>
        public static void PlayStar(int index)
        {
            if (!Ready) return;
            var entry = library.star;
            if (entry == null || entry.clip == null) return;
            PlayEntry(entry, library.StarPitch(index));
        }

        private static void PlayEntry(SoundEntry entry, float pitch)
        {
            float volume = entry.volume * EffectScale;
            if (volume <= 0f) return;

            var source = FreeSource();
            source.Stop();
            source.clip = entry.clip;
            source.pitch = pitch;
            source.volume = volume;
            if (entry.delay > 0f) source.PlayDelayed(entry.delay);
            else source.Play();
        }

        /// <summary>쉬고 있는 AudioSource. 모두 울리고 있으면 차례상 가장 오래된 것.</summary>
        private static AudioSource FreeSource()
        {
            for (int i = 0; i < effects.Length; i++)
            {
                int k = (next + i) % effects.Length;
                if (!effects[k].isPlaying)
                {
                    next = (k + 1) % effects.Length;
                    return effects[k];
                }
            }
            var oldest = effects[next];
            next = (next + 1) % effects.Length;
            return oldest;
        }

        // ── 음악 ──

        public static void PlayTitleMusic()
        {
            if (Ready) PlayMusic(library.titleMusic);
        }

        public static void PlayGameMusic()
        {
            if (Ready) PlayMusic(library.gameMusic);
        }

        /// <summary>이미 그 곡이 나오고 있으면 처음부터 다시 틀지 않는다.</summary>
        private static void PlayMusic(AudioClip clip)
        {
            if (clip == null)
            {
                music.Stop();
                music.clip = null;
                return;
            }
            RefreshVolume();
            if (music.clip == clip && music.isPlaying) return;
            music.clip = clip;
            music.Play();
        }

        /// <summary>설정의 음악 크기를 바꾼 뒤 부른다.</summary>
        public static void RefreshVolume()
        {
            if (!Ready) return;
            music.volume = library.musicVolume * MusicScale;
        }
    }
}
