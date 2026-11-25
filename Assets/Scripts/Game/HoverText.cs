using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sheepft.Game
{
    /// <summary>
    /// 버튼 그림 없이 글자만 있는 버튼. 마우스를 올리면 글자 색이 바뀌고, 누르면 Clicked.
    /// 게임 화면의 건너뛰기(Skip)에 쓴다. 글자의 Raycast Target이 켜져 있어야 한다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class HoverText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Color normalColor = new Color32(0xf4, 0xf0, 0xe8, 0xff);
        [Tooltip("마우스를 올렸을 때 색. 기본은 UI 버튼 hover의 노랑(#fcc94b).")]
        public Color hoverColor = new Color32(0xfc, 0xc9, 0x4b, 0xff);

        public event Action Clicked;

        private TMP_Text text;

        private TMP_Text Text => text != null ? text : (text = GetComponent<TMP_Text>());

        private void OnEnable() => Text.color = normalColor;

        public void OnPointerEnter(PointerEventData eventData) => Text.color = hoverColor;

        public void OnPointerExit(PointerEventData eventData) => Text.color = normalColor;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Text.color = normalColor;
            if (Clicked != null) Clicked();
        }
    }
}
