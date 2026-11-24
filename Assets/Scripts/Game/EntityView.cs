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

        /// <summary>말: 등에 태운 오브젝트의 Id. 없으면 -1.</summary>
        [HideInInspector] public int RiderId = -1;

        /// <summary>몸 그림이 왼쪽을 보는가.</summary>
        public bool FacesLeft => body != null && body.flipX;

        private Vector3 iconBaseLocal;
        private bool iconBaseKnown;

        private SpriteCatalog catalog;
        private readonly List<SpriteRenderer> packCopies = new List<SpriteRenderer>();
        private bool isBucket;
        private string bodyState;
        private string iconState;
        private string wantedBody;
        private string wantedIcon;

        /// <summary>잠깐 재생하는 동작 태그(`walk`, `eat`). 없으면 null이고 평소 태그를 쓴다.</summary>
        private string actionTag;
        private bool actionOnce;

        public static string SheetName(ObjectEntity o)
        {
            var sheep = o as Sheep;
            if (sheep != null) return sheep.Buff ? "buff_sheep" : "sheep";
            if (o is Spicebush) return "spicebush";
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
            Sheet = SheetName(o);
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            PlaceIcon();
            CopyState(o);
        }

        /// <summary>
        /// 같은 오브젝트가 다른 그림(프리팹)으로 바뀔 때, 예전 모습의 화면 상태를 그대로 넘겨받는다.
        /// 양이 생강꽃을 먹고 근육질 양이 되는 순간에 쓴다.
        /// </summary>
        public void SetupFrom(SpriteCatalog spriteCatalog, EntityView old, string sheet)
        {
            catalog = spriteCatalog;
            Id = old.Id;
            Sheet = sheet;
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            Pos = old.Pos;
            LayoutOffset = old.LayoutOffset;
            Wet = old.Wet;
            Burned = old.Burned;
            Filled = old.Filled;
            Stunned = old.Stunned;
            Eating = old.Eating;
            HasTarget = old.HasTarget;
            Full = old.Full;
            Count = old.Count;
            RiderId = old.RiderId;
            SetFlip(old.FacesLeft);
            Place(old.BasePosition);
            RefreshVisuals();
        }

        /// <summary>
        /// 작게 시작해 살짝 넘치게 커졌다가 제 크기로 돌아온다. 근육질 양이 되는 순간에 쓴다.
        /// 뿌리(발밑)를 축으로 커진다.
        /// </summary>
        public void PlayPop(float startScale, float overshoot, float seconds)
        {
            if (pop != null) StopCoroutine(pop);
            pop = StartCoroutine(Pop(startScale, overshoot, Mathf.Max(0.01f, seconds)));
        }

        private Coroutine pop;

        private System.Collections.IEnumerator Pop(float startScale, float overshoot, float seconds)
        {
            float peakAt = seconds * 0.6f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float s = t < peakAt
                    ? Mathf.Lerp(startScale, overshoot, Mathf.SmoothStep(0f, 1f, t / peakAt))
                    : Mathf.Lerp(overshoot, 1f, Mathf.SmoothStep(0f, 1f, (t - peakAt) / (seconds - peakAt)));
                transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            transform.localScale = Vector3.one;
            pop = null;
        }

        private void OnDisable()
        {
            // 꺼지면 코루틴이 멈춘다. 커지던 도중이면 제 크기로 돌려 둔다(엔딩 컷씬이 양을 잠시 숨길 때).
            if (pop == null) return;
            pop = null;
            transform.localScale = Vector3.one;
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
            var horse = o as Horse;
            RiderId = horse != null ? horse.RiderId : -1;
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

            // 상태 아이콘은 하나만 보여 준다. 화상 > 기절 > 식사 > 배부름 > 발견 > 젖음 순이다.
            // 말의 발견과 소의 냄새는 같은 느낌표(`found`)로 보여 준다.
            string iconTag = Burned ? "burned"
                : Stunned ? "stunned"
                : Eating ? "eating"
                : Full ? "full"
                : HasTarget ? "found"
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

        /// <summary>상태 아이콘만 따로 정렬한다. 등에 태운 것보다 말의 느낌표를 위에 그릴 때 쓴다.</summary>
        public void SetIconOrder(int order)
        {
            if (icon != null) icon.sortingOrder = order;
        }

        /// <summary>
        /// 상태 아이콘을 바라보는 쪽에 맞춘다. 프리팹의 Status 위치는 오른쪽을 볼 때 기준이고(말·소는 머리 앞),
        /// 왼쪽을 보면 x를 뒤집는다.
        /// </summary>
        private void PlaceIcon()
        {
            if (icon == null) return;
            if (!iconBaseKnown)
            {
                iconBaseLocal = icon.transform.localPosition;
                iconBaseKnown = true;
            }
            var p = iconBaseLocal;
            if (FacesLeft) p.x = -p.x;
            icon.transform.localPosition = p;
        }

        public void SetFlip(bool faceLeft)
        {
            if (body != null) body.flipX = faceLeft;
            PlaceIcon();
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
