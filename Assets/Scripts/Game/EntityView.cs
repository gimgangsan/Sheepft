using System.Collections.Generic;
using Sheepft.Core;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 오브젝트 하나의 화면 모습. 로직의 엔티티를 Id로 따라간다.
    /// 화면에 보이는 상태(젖음, 화상, 물 참, 마리 수)는 따로 갖고 있다가,
    /// 이벤트를 재생하며 하나씩 바꾼다.
    /// </summary>
    public sealed class EntityView : MonoBehaviour
    {
        private const float Pixel = 1f / 32f;

        public int Id { get; private set; }
        public Pos Pos;

        /// <summary>칸의 아래쪽 가운데. 같은 칸에 여럿이면 여기에 LayoutOffset을 더해 놓는다.</summary>
        public Vector3 BasePosition;
        public Vector3 LayoutOffset;

        public bool Wet, Burned, Filled;
        public int Count = 1;

        private SpriteCatalog catalog;
        private SpriteSheet sheet;
        private SpriteRenderer body;
        private SpriteAnimator bodyAnimator;
        private SpriteRenderer icon;
        private SpriteAnimator iconAnimator;
        private readonly List<SpriteRenderer> packCopies = new List<SpriteRenderer>();
        private bool isBucket;
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
            sheet = catalog.Get(SheetName(o));

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
            SetAlpha(1f);
            RefreshVisuals();
        }

        public void ApplyStatus(StatusKind status, bool on)
        {
            if (status == StatusKind.Wet) Wet = on;
            else if (status == StatusKind.Burned) Burned = on;
            else if (status == StatusKind.Filled) Filled = on;
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            string bodyTag = isBucket ? (Filled ? "full" : "empty") : "idle";
            if (bodyAnimator.CurrentTag != bodyTag) bodyAnimator.Play(sheet, bodyTag);

            // 상태 아이콘은 머리 위에 하나만 보여 준다. 화상이 젖음보다 먼저다.
            string iconTag = Burned ? "burned" : Wet ? "wet" : null;
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
