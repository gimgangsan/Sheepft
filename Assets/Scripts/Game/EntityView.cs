using System.Collections.Generic;
using Sheepft.Core;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 오브젝트 하나의 화면 모습. 로직의 엔티티를 Id로 따라간다.
    /// 화면에 보이는 상태(젖음, 화상, 물 참, 기절, 식사, 마리 수)는 따로 갖고 있다가,
    /// 이벤트를 재생하며 하나씩 바꾼다.
    /// </summary>
    public sealed class EntityView : MonoBehaviour
    {
        private const float Pixel = 1f / 32f;

        public int Id { get; private set; }

        /// <summary>스프라이트 파일 이름. 예: `sheep`, `fruit_basket`</summary>
        public string Sheet { get; private set; }
        public Pos Pos;

        /// <summary>칸의 아래쪽 가운데. 같은 칸에 여럿이면 여기에 LayoutOffset을 더해 놓는다.</summary>
        public Vector3 BasePosition;
        public Vector3 LayoutOffset;

        public bool Wet, Burned, Filled, Stunned, Eating, HasTarget, Full;
        public int Count = 1;

        private SpriteCatalog catalog;
        private SpriteSheet sheet;
        private SpriteRenderer body;
        private SpriteAnimator bodyAnimator;
        private SpriteRenderer icon;
        private SpriteAnimator iconAnimator;
        private readonly List<SpriteRenderer> packCopies = new List<SpriteRenderer>();
        private bool isBucket;
        private bool isCow;

        /// <summary>잠깐 재생하는 동작 태그(`walk`, `eat`). 없으면 null이고 평소 태그를 쓴다.</summary>
        private string actionTag;
        private bool actionOnce;
        private float alpha = 1f;

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
            sheet = catalog.Get(Sheet);

            body = gameObject.AddComponent<SpriteRenderer>();
            bodyAnimator = gameObject.AddComponent<SpriteAnimator>();

            var iconObject = new GameObject("status");
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localPosition = new Vector3(0f, 26f * Pixel, 0f);
            icon = iconObject.AddComponent<SpriteRenderer>();
            iconAnimator = iconObject.AddComponent<SpriteAnimator>();

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
        /// 동작 태그를 재생한다. 그 태그가 없는 스프라이트면 아무것도 하지 않는다.
        /// once이면 한 번 재생한 뒤 평소 태그로 돌아가고, 아니면 StopAction을 부를 때까지 반복한다.
        /// </summary>
        public void PlayAction(string tag, bool once)
        {
            if (sheet == null || sheet.Tag(tag) == null) return;
            actionTag = tag;
            actionOnce = once;
            bodyAnimator.Play(sheet, tag, !once);
        }

        public void StopAction(string tag)
        {
            if (actionTag != tag) return;
            actionTag = null;
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            string bodyTag = isBucket ? (Filled ? "full" : "empty") : "idle";
            if (actionTag == null && bodyAnimator.CurrentTag != bodyTag) bodyAnimator.Play(sheet, bodyTag);

            // 상태 아이콘은 머리 위에 하나만 보여 준다. 화상 > 기절 > 식사 > 배부름 > 발견·냄새 > 젖음 순이다.
            string iconTag = Burned ? "burned"
                : Stunned ? "stunned"
                : Eating ? "eating"
                : Full ? "full"
                : HasTarget ? (isCow ? "smell" : "found")
                : Wet ? "wet"
                : null;
            var icons = catalog.Get("status_icons");
            if (iconTag == null || icons == null)
            {
                icon.enabled = false;
                iconAnimator.Play(null, null);
            }
            else
            {
                icon.enabled = true;
                if (iconAnimator.CurrentTag != iconTag) iconAnimator.Play(icons, iconTag);
            }

            // 늑대 무리는 같은 그림을 몇 px씩 어긋나게 겹쳐 그린다.
            while (packCopies.Count < Count - 1)
            {
                var copy = new GameObject("pack");
                copy.transform.SetParent(transform, false);
                int n = packCopies.Count + 1;
                copy.transform.localPosition = new Vector3(-4f * Pixel * n, 3f * Pixel * n, 0f);
                packCopies.Add(copy.AddComponent<SpriteRenderer>());
            }
            for (int i = 0; i < packCopies.Count; i++) packCopies[i].enabled = i < Count - 1;
        }

        public void SetSortingOrder(int order)
        {
            body.sortingOrder = order;
            icon.sortingOrder = order + 1;
            for (int i = 0; i < packCopies.Count; i++) packCopies[i].sortingOrder = order - 1 - i;
        }

        public void SetFlip(bool faceLeft)
        {
            body.flipX = faceLeft;
        }

        public void SetAlpha(float value)
        {
            alpha = value;
            var color = new Color(1f, 1f, 1f, alpha);
            body.color = color;
            icon.color = color;
        }

        /// <summary>움직이는 동안 칸 안의 자리(LayoutOffset)는 그대로 두고 기준 위치만 바꾼다.</summary>
        public void Place(Vector3 basePosition)
        {
            BasePosition = basePosition;
            transform.position = basePosition + LayoutOffset;
        }

        private void LateUpdate()
        {
            if (actionTag != null && actionOnce && bodyAnimator.Finished) StopAction(actionTag);

            foreach (var copy in packCopies)
            {
                if (!copy.enabled) continue;
                copy.sprite = body.sprite;
                copy.flipX = body.flipX;
                copy.color = body.color;
            }
        }
    }
}
