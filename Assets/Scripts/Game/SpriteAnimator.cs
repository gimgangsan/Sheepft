using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>SpriteSheet의 태그 하나를 SpriteRenderer에 재생한다. Animator를 쓰지 않는다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        private SpriteRenderer target;
        private SpriteSheet sheet;
        private int from, to, frame;
        private bool loop;
        private float elapsedMs;

        public string CurrentTag { get; private set; }

        /// <summary>태그를 처음부터 재생한다. 태그가 없으면 0번 프레임만 보여 준다.</summary>
        public void Play(SpriteSheet newSheet, string tagName, bool looping = true)
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            sheet = newSheet;
            CurrentTag = tagName;
            loop = looping;
            elapsedMs = 0f;

            var tag = sheet == null ? null : sheet.Tag(tagName);
            from = tag == null ? 0 : tag.from;
            to = tag == null ? 0 : tag.to;
            frame = from;
            target.sprite = sheet == null ? null : sheet.Frame(frame);
        }

        private void Update()
        {
            if (sheet == null || to <= from) return;
            if (!loop && frame >= to) return;

            elapsedMs += Time.deltaTime * 1000f;
            int duration = FrameDuration(frame);
            bool changed = false;
            while (elapsedMs >= duration)
            {
                elapsedMs -= duration;
                if (frame < to) frame++;
                else if (loop) frame = from;
                else break;
                duration = FrameDuration(frame);
                changed = true;
            }
            if (changed) target.sprite = sheet.Frame(frame);
        }

        private int FrameDuration(int index)
        {
            int ms = index < sheet.durationsMs.Length ? sheet.durationsMs[index] : 100;
            return ms > 0 ? ms : 100;
        }
    }
}
