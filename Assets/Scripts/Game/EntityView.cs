using System.Collections.Generic;
using Sheepft.Core;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 오브젝트 하나의 화면 모습. `Assets/Prefabs/Entities/`의 프리팹에 붙어 있고, 로직의 엔티티를 Id로 따라간다.
    /// 그림과 애니메이션은 프리팹의 SpriteRenderer와 Animator가 맡는다. Animator Controller와 클립은
    /// Aseprite Importer가 `.aseprite`의 태그(`idle`, `walk` 등)마다 만들어 준 것이고, 상태 이름이 태그 이름이다.
    /// 화면에 보이는 상태(젖음, 화상, 물 참, 기절, 식사, 마리 수)는 따로 갖고 있다가 이벤트를 재생하며 바꾼다.
    /// </summary>
    public sealed class EntityView : MonoBehaviour
    {
        private const float Pixel = 1f / 32f;

        [Header("프리팹 안의 부품")]
        [Tooltip("몸 그림.")]
        public SpriteRenderer body;
        [Tooltip("몸 애니메이션. 상태 이름이 .aseprite의 태그 이름이다.")]
        public Animator bodyAnimator;
        [Tooltip("머리 위 상태 아이콘.")]
        public SpriteRenderer icon;
        [Tooltip("상태 아이콘 애니메이션 (status_icons).")]
        public Animator iconAnimator;

        public int Id { get; private set; }

        /// <summary>스프라이트 파일 이름. 예: `sheep`, `fruit_basket`</summary>
        public string Sheet { get; private set; }

        [HideInInspector] public Pos Pos;

        /// <summary>칸의 아래쪽 가운데. 같은 칸에 여럿이면 여기에 LayoutOffset을 더해 놓는다.</summary>
        [HideInInspector] public Vector3 BasePosition;
        [HideInInspector] public Vector3 LayoutOffset;

        [HideInInspector] public bool Wet, Burned, Filled, Stunned, Eating, HasTarget, Full;
        [HideInInspector] public int Count = 1;

        private SpriteCatalog catalog;
        private readonly List<SpriteRenderer> packCopies = new List<SpriteRenderer>();
        private bool isBucket;
        private bool isCow;
        private string bodyState;
        private string iconState;
        private string wantedBody;
        private string wantedIcon;

        /// <summary>잠깐 재생하는 동작 태그(`walk`, `eat`). 없으면 null이고 평소 태그를 쓴다.</summary>
        private string actionTag;
        private bool actionOnce;

        public static string SheetName(ObjectEntity o)
        {
            if (o is Sheep) return "sheep";
            if (o is Wolf) return "wolf";
            if (o is Goat) return "goat";
            if (o is Horse) return "horse";
            if (o is Cow) return "cow";
            if (o is FruitBasket) return "fruit_basket";
            if (o is Bucket) return "wooden_bucket";
            return null;
        }

        public void Setup(SpriteCatalog spriteCatalog, ObjectEntity o)
        {
            catalog = spriteCatalog;
            Id = o.Id;
            isBucket = o is Bucket;
            isCow = o is Cow;
            Sheet = SheetName(o);
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            CopyState(o);
        }

        /// <summary>로직의 상태를 그대로 옮긴다. undo, 재시작, 재생이 끝난 뒤에 쓴다.</summary>
        public void CopyState(ObjectEntity o)
        {
            Pos = o.Pos;
            Wet = o.Wet;
            var animal = o as Animal;
            Burned = animal != null && animal.Burned;
            var bucket = o as Bucket;
            Filled = bucket != null && bucket.Filled;
            var wolf = o as Wolf;
            Count = wolf != null ? wolf.Count : 1;
            Stunned = wolf != null && wolf.IsStunned;
            Eating = wolf != null && wolf.IsEating;
            var seeker = o as BasketSeeker;
            HasTarget = seeker != null && seeker.HasTarget;
            Full = seeker != null && seeker.Full;
            actionTag = null;
            SetAlpha(1f);
            RefreshVisuals();
        }

        public void ApplyStatus(StatusKind status, bool on)
        {
            if (status == StatusKind.Wet) Wet = on;
            else if (status == StatusKind.Burned) Burned = on;
            else if (status == StatusKind.Filled) Filled = on;
            else if (status == StatusKind.Stunned) Stunned = on;
            else if (status == StatusKind.Eating)
            {
                Eating = on;
                if (on) PlayAction("eat", true); // 양을 덮쳐 먹는 동작을 한 번 보여 준다.
            }
            else if (status == StatusKind.Full)
            {
                Full = on;
                if (on) PlayAction("eat", true); // 말·소가 바구니를 먹는 동작.
            }
            RefreshVisuals();
        }

        /// <summary>말·소가 목표 바구니를 얻거나 잃었다.</summary>
        public void SetTarget(bool hasTarget)
        {
            HasTarget = hasTarget;
            RefreshVisuals();
        }

        public void SetCount(int count)
        {
            Count = count;
            RefreshVisuals();
        }

        /// <summary>
        /// 동작 태그를 재생한다. 그 상태가 없는 Animator면 아무것도 하지 않는다.
        /// once이면 한 번 재생한 뒤 평소 태그로 돌아가고, 아니면 StopAction을 부를 때까지 반복한다.
        /// </summary>
        public void PlayAction(string tag, bool once)
        {
            if (!AnimatorUtil.Play(bodyAnimator, tag)) return;
            actionTag = tag;
            actionOnce = once;
            bodyState = tag;
        }

        public void StopAction(string tag)
        {
            if (actionTag != tag) return;
            actionTag = null;
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            wantedBody = isBucket ? (Filled ? "full" : "empty") : "idle";

            // 상태 아이콘은 머리 위에 하나만 보여 준다. 화상 > 기절 > 식사 > 배부름 > 발견·냄새 > 젖음 순이다.
            string iconTag = Burned ? "burned"
                : Stunned ? "stunned"
                : Eating ? "eating"
                : Full ? "full"
                : HasTarget ? (isCow ? "smell" : "found")
                : Wet ? "wet"
                : null;
            wantedIcon = iconTag;
            if (icon != null) icon.enabled = iconTag != null;
            if (iconTag == null) iconState = null;
            SyncAnimators();

            // 늑대 무리는 같은 그림을 몇 px씩 어긋나게 겹쳐 그린다.
            while (packCopies.Count < Count - 1)
            {
                var copy = new GameObject("pack");
                copy.transform.SetParent(body.transform, false);
                int n = packCopies.Count + 1;
                Vector2 offset = catalog != null ? catalog.Settings.packOffsetPixels : new Vector2(-4f, 3f);
                copy.transform.localPosition = (Vector3)(offset * (Pixel * n));
                packCopies.Add(copy.AddComponent<SpriteRenderer>());
            }
            for (int i = 0; i < packCopies.Count; i++) packCopies[i].enabled = i < Count - 1;
        }

        /// <summary>
        /// Animator를 원하는 상태로 맞춘다. 갓 만든 Animator는 아직 준비되지 않아 재생이 안 될 수 있으므로,
        /// 맞을 때까지 매 프레임 다시 시도한다.
        /// </summary>
        private void SyncAnimators()
        {
            if (actionTag == null && wantedBody != null && bodyState != wantedBody && AnimatorUtil.Play(bodyAnimator, wantedBody))
                bodyState = wantedBody;
            if (wantedIcon != null && iconState != wantedIcon && AnimatorUtil.Play(iconAnimator, wantedIcon))
                iconState = wantedIcon;
        }

        public void SetSortingOrder(int order)
        {
            if (body != null) body.sortingOrder = order;
            if (icon != null) icon.sortingOrder = order + 1;
            for (int i = 0; i < packCopies.Count; i++) packCopies[i].sortingOrder = order - 1 - i;
        }

        public void SetFlip(bool faceLeft)
        {
            if (body != null) body.flipX = faceLeft;
        }

        public void SetAlpha(float value)
        {
            var color = new Color(1f, 1f, 1f, value);
            if (body != null) body.color = color;
            if (icon != null) icon.color = color;
        }

        /// <summary>움직이는 동안 칸 안의 자리(LayoutOffset)는 그대로 두고 기준 위치만 바꾼다.</summary>
        public void Place(Vector3 basePosition)
        {
            BasePosition = basePosition;
            transform.position = basePosition + LayoutOffset;
        }

        private void LateUpdate()
        {
            if (actionTag != null && actionOnce && AnimatorUtil.Finished(bodyAnimator, actionTag)) StopAction(actionTag);
            SyncAnimators();

            foreach (var copy in packCopies)
            {
                if (!copy.enabled) continue;
                copy.sprite = body.sprite;
                copy.flipX = body.flipX;
                copy.color = body.color;
            }
        }
    }

    /// <summary>Animator를 태그 이름으로 다루는 도우미. 상태가 없거나 Animator가 꺼져 있으면 조용히 넘어간다.</summary>
    public static class AnimatorUtil
    {
        public static bool Has(Animator animator, string state)
        {
            return animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null
                && Application.isPlaying && animator.HasState(0, Animator.StringToHash(state));
        }

        /// <summary>그 상태를 처음부터 재생한다. 재생했으면 true.</summary>
        public static bool Play(Animator animator, string state)
        {
            if (!Has(animator, state)) return false;
            animator.Play(state, 0, 0f);
            return true;
        }

        /// <summary>그 상태를 한 번 끝까지 재생했는가.</summary>
        public static bool Finished(Animator animator, string state)
        {
            if (animator == null || !animator.isActiveAndEnabled) return true;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(state) && info.normalizedTime >= 1f;
        }
    }
}
