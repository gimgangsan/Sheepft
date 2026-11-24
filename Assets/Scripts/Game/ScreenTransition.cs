using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sheepft.Game
{
    /// <summary>
    /// 구름 화면 전환. `transition_cloud.aseprite`(640×360)의 `cover`로 화면을 덮고,
    /// 다 덮인 동안 화면을 바꾼 뒤(스테이지 열기, 재시작, 메뉴로), `reveal`로 걷어 낸다.
    /// 게임 UI(Canvas)보다 위에 그려야 하므로 따로 Screen Space Overlay Canvas를 만든다.
    /// 그림은 비율을 지키며 화면을 꽉 채운다(넘치는 쪽은 잘린다). 프레임 길이는 .aseprite에 적힌 대로다.
    /// 전환하는 동안 입력을 막는다: UI 클릭은 화면 전체의 투명한 판이 받고, 게임·메뉴의 마우스 입력은 Active를 보고 멈춘다.
    /// </summary>
    public sealed class ScreenTransition : MonoBehaviour
    {
        /// <summary>게임 UI Canvas(0)와 레벨 선택 판보다 위.</summary>
        private const int CanvasOrder = 500;
        private const float DefaultFrameSeconds = 0.05f;

        /// <summary>전환 중인가. 이 동안 게임과 메뉴는 입력을 받지 않는다.</summary>
        public static bool Active { get; private set; }

        private SpriteSheet sheet;
        private GameSettings settings;
        private GameObject canvasObject;
        private Image image;

        public void Init(SpriteCatalog catalog)
        {
            sheet = catalog.Get("transition_cloud");
            settings = catalog.Settings;
            Active = false;

            canvasObject = new GameObject("TransitionCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasOrder;

            // 화면 전체를 덮는 투명한 판. 전환하는 동안 UI 클릭을 받아 막는다.
            var blocker = new GameObject("InputBlocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(canvasObject.transform, false);
            var blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = Color.clear;

            // 640×360 그림 캔버스. 화면을 꽉 채우도록 늘린다(UpdateScale).
            var frameObject = new GameObject("CloudCanvas", typeof(RectTransform));
            frameObject.transform.SetParent(canvasObject.transform, false);
            frame = (RectTransform)frameObject.transform;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            // Aseprite Importer는 프레임마다 그림이 있는 부분만 잘라 스프라이트로 만들고,
            // 피벗에 캔버스 아래 가운데(320, 0)의 위치를 적어 둔다. 그 점을 맞춰 놓으면 캔버스 안 제자리에 온다.
            var imageObject = new GameObject("Cloud", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(frame, false);
            image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            var imageRect = image.rectTransform;
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0f);
            imageRect.anchoredPosition = Vector2.zero;

            canvasObject.SetActive(false);
        }

        private const float CanvasWidth = 640f;
        private const float CanvasHeight = 360f;
        private RectTransform frame;

        private void ShowSprite(Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
            if (sprite != null)
            {
                var size = sprite.rect.size;
                image.rectTransform.sizeDelta = size;
                image.rectTransform.pivot = new Vector2(sprite.pivot.x / size.x, sprite.pivot.y / size.y);
            }
            UpdateScale();
        }

        /// <summary>비율을 지키며 화면을 꽉 채운다. 넘치는 쪽은 잘린다.</summary>
        private void UpdateScale()
        {
            // Canvas Scaler가 없는 Overlay Canvas라서 UI 단위가 곧 화면 픽셀이다.
            float scale = Mathf.Max(Screen.width / CanvasWidth, Screen.height / CanvasHeight);
            frame.localScale = new Vector3(scale, scale, 1f);
        }

        private void LateUpdate()
        {
            // 전환 중에 창 크기가 바뀌어도 화면을 꽉 채운다.
            if (Active && frame != null) UpdateScale();
        }

        /// <summary>
        /// 구름으로 덮고, 다 덮였을 때 whileCovered를 부르고, 걷어 낸다. 이미 전환 중이면 아무것도 하지 않는다.
        /// 구름 그림이 없으면 whileCovered만 바로 부른다.
        /// </summary>
        public bool Play(Action whileCovered)
        {
            if (Active) return false;
            if (sheet == null || sheet.Tag("cover") == null || sheet.Tag("reveal") == null)
            {
                if (whileCovered != null) whileCovered();
                return true;
            }
            StartCoroutine(Run(whileCovered));
            return true;
        }

        private IEnumerator Run(Action whileCovered)
        {
            Active = true;
            canvasObject.SetActive(true);
            yield return PlayTag("cover");

            try
            {
                if (whileCovered != null) whileCovered();
            }
            catch (Exception e)
            {
                // 화면을 바꾸다 오류가 나도 구름이 화면을 덮은 채로 남지 않게 한다.
                Debug.LogException(e);
            }

            if (settings.transitionHoldSeconds > 0f) yield return new WaitForSecondsRealtime(settings.transitionHoldSeconds);
            yield return PlayTag("reveal");
            Finish();
        }

        /// <summary>태그의 프레임을 차례로 보인다. 비어 있는 프레임(reveal의 마지막)은 그림을 끈다.</summary>
        private IEnumerator PlayTag(string tagName)
        {
            var tag = sheet.Tag(tagName);
            for (int f = tag.from; f <= tag.to; f++)
            {
                ShowSprite(f < sheet.frames.Length ? sheet.frames[f] : null);
                float seconds = f < sheet.durationsMs.Length && sheet.durationsMs[f] > 0 ? sheet.durationsMs[f] / 1000f : DefaultFrameSeconds;
                yield return new WaitForSecondsRealtime(seconds * settings.transitionFrameScale);
            }
        }

        private void Finish()
        {
            if (canvasObject != null) canvasObject.SetActive(false);
            Active = false;
        }

        private void OnDisable()
        {
            // 코루틴이 멈추면 Active가 켜진 채로 남지 않게 한다.
            StopAllCoroutines();
            Finish();
        }
    }
}
