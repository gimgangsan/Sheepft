using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sheepft.Game
{
    /// <summary>
    /// 도트 아이콘 버튼(힌트 전구). 아이콘을 스프라이트 크기 × pixelScale의 고정 픽셀로 그린다.
    ///
    /// Aseprite Importer는 프레임마다 그림이 있는 부분만 잘라 스프라이트를 만든다(전구 10×14, 빛살이 있는 hover 16×14).
    /// 그래서 Image를 32×32 칸에 늘리면 프레임마다 다르게 찌그러진다. 대신 아이콘(자식 Image)의 크기를
    /// 잘린 스프라이트 크기 × pixelScale로 두고, 스프라이트의 피벗(프레임 아래 가운데)을 이 버튼 칸의 아래 가운데에 맞춘다.
    /// 버튼 칸은 프레임(16×16) × pixelScale이다. 마우스를 올리면 hoverSprite로 바꾼다(Button의 Sprite Swap 대신).
    /// 버튼 뿌리의 Image는 투명한 누름 영역이다.
    /// </summary>
    public sealed class PixelIconButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("그림을 그리는 자식 Image. Raycast Target은 끈다.")]
        public Image icon;
        public Sprite normalSprite;
        public Sprite hoverSprite;
        [Tooltip("스프라이트 한 픽셀을 UI 몇 픽셀로 그릴지. 16×16 아이콘을 32×32 칸에 그리면 2.")]
        [Min(1f)] public float pixelScale = 2f;

        private bool hovered;

        private void OnEnable()
        {
            hovered = false;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            Apply();
        }

        private void Apply()
        {
            if (icon == null) return;
            var sprite = hovered && hoverSprite != null ? hoverSprite : normalSprite;
            icon.sprite = sprite;
            if (sprite == null) return;
            var rect = icon.rectTransform;
            var size = sprite.rect.size;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(sprite.pivot.x / size.x, sprite.pivot.y / size.y);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size * pixelScale;
        }

        /// <summary>에디터에서 프리팹을 만들 때 아이콘 크기를 바로 맞춘다.</summary>
        public void Refresh() => Apply();
    }
}
