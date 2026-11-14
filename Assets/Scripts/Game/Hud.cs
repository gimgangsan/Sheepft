using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>도트 스프라이트 버튼. 태그 `normal`, `hover`, `pressed`를 쓴다. 클릭 판정은 GameController가 한다.</summary>
    public sealed class SpriteButton
    {
        public readonly SpriteRenderer Renderer;
        public readonly Action OnClick;
        private readonly SpriteSheet sheet;

        public bool Visible
        {
            get { return Renderer.enabled; }
            set { Renderer.enabled = value; }
        }

        public SpriteButton(Transform parent, SpriteSheet buttonSheet, string name, int order, Action onClick)
        {
            sheet = buttonSheet;
            OnClick = onClick;
            Renderer = BoardView.NewRenderer(parent, name, order);
            SetState("normal");
        }

        /// <summary>스프라이트의 피벗이 아래쪽 가운데이므로, 가운데 좌표를 받아 반 칸 내려 놓는다.</summary>
        public void SetCenter(Vector3 center)
        {
            Renderer.transform.position = center + new Vector3(0f, -0.5f * Renderer.transform.lossyScale.y, 0f);
        }

        public Vector3 Center => Renderer.transform.position + new Vector3(0f, 0.5f * Renderer.transform.lossyScale.y, 0f);

        public bool Contains(Vector3 world)
        {
            if (!Visible) return false;
            var d = world - Center;
            float half = 0.5f * Renderer.transform.lossyScale.x;
            return Mathf.Abs(d.x) <= half && Mathf.Abs(d.y) <= half;
        }

        public void SetState(string tag)
        {
            if (sheet != null) Renderer.sprite = sheet.TagFrame(tag);
        }

        /// <summary>패배했을 때 눈에 띄도록 크기를 살짝 키웠다 줄인다. 1이면 보통 크기다.</summary>
        public void SetPulse(float scale)
        {
            var center = Center;
            Renderer.transform.localScale = new Vector3(scale, scale, 1f);
            SetCenter(center);
        }
    }

    /// <summary>
    /// 남은 턴과 목표 양 수, 승리 불가 표시. 모두 도트 스프라이트로 그린다.
    /// 글자(스테이지 이름, 설명, 결과 문구)는 폰트를 정한 뒤에 넣는다.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        private const int Order = 1000;
        private const float Scale = 2f;          // 8×8 숫자와 16×16 아이콘을 두 배로 그린다
        private const float DigitWidth = 8f / 32f * Scale;
        private const float IconWidth = 16f / 32f * Scale;

        private SpriteCatalog catalog;
        private SpriteRenderer turnIcon, goalIcon, cannotWin;
        private readonly List<SpriteRenderer> turnDigits = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> goalDigits = new List<SpriteRenderer>();
        private Vector3 origin;

        public void Setup(SpriteCatalog spriteCatalog)
        {
            catalog = spriteCatalog;
            var icons = catalog.Get("hud_icons");
            turnIcon = NewScaled("turn icon");
            goalIcon = NewScaled("goal icon");
            if (icons != null)
            {
                turnIcon.sprite = icons.TagFrame("turn");
                goalIcon.sprite = icons.TagFrame("goal_sheep");
            }

            cannotWin = BoardView.NewRenderer(transform, "cannot win", Order);
            var warning = catalog.Get("cannot_win");
            cannotWin.gameObject.AddComponent<SpriteAnimator>().Play(warning, "idle");
            cannotWin.enabled = false;
        }

        /// <summary>HUD의 왼쪽 아래 기준점과, 승리 불가 아이콘의 아래쪽 가운데.</summary>
        public void Place(Vector3 leftBottom, Vector3 cannotWinBase)
        {
            origin = leftBottom;
            cannotWin.transform.position = cannotWinBase;
        }

        public void Show(int turnsLeft, int sheepOnGrass, int targetSheep, bool showCannotWin)
        {
            // [턴 아이콘][남은 턴]   [양 아이콘][풀 위의 양][목표]
            // 슬래시 글자가 없어서 풀 위의 양 수와 목표를 반 글자 띄워 나란히 적는다.
            float x = origin.x;
            x = PlaceIcon(turnIcon, x);
            x = PlaceNumber(turnDigits, Mathf.Max(0, turnsLeft), x, 0);
            x += DigitWidth * 1.5f;
            x = PlaceIcon(goalIcon, x);
            x = PlaceNumber(goalDigits, sheepOnGrass, x, 0);
            x += DigitWidth * 0.5f;
            PlaceNumber(goalDigits, targetSheep, x, CountDigits(sheepOnGrass));
            HideFrom(goalDigits, CountDigits(sheepOnGrass) + CountDigits(targetSheep));

            cannotWin.enabled = showCannotWin;
        }

        private float PlaceIcon(SpriteRenderer icon, float x)
        {
            icon.transform.position = new Vector3(x + IconWidth * 0.5f, origin.y, 0f);
            return x + IconWidth + DigitWidth * 0.25f;
        }

        /// <summary>pool의 startIndex번째부터 숫자를 그린다. 그린 뒤의 x를 돌려준다.</summary>
        private float PlaceNumber(List<SpriteRenderer> pool, int value, float x, int startIndex)
        {
            var digits = catalog.Get("digits");
            string text = value.ToString();
            for (int i = 0; i < text.Length; i++)
            {
                int index = startIndex + i;
                while (pool.Count <= index) pool.Add(NewScaled("digit"));
                var r = pool[index];
                r.enabled = true;
                r.sprite = digits == null ? null : digits.Frame(text[i] - '0');
                // 숫자는 아이콘(16px)보다 낮으므로(8px) 세로 가운데를 맞춘다.
                r.transform.position = new Vector3(x + DigitWidth * 0.5f, origin.y + (IconWidth - DigitWidth) * 0.5f, 0f);
                x += DigitWidth;
            }
            if (pool == turnDigits) HideFrom(pool, text.Length);
            return x;
        }

        private static void HideFrom(List<SpriteRenderer> pool, int index)
        {
            for (int i = index; i < pool.Count; i++) pool[i].enabled = false;
        }

        private static int CountDigits(int value)
        {
            return value.ToString().Length;
        }

        private SpriteRenderer NewScaled(string objectName)
        {
            var r = BoardView.NewRenderer(transform, objectName, Order);
            r.transform.localScale = new Vector3(Scale, Scale, 1f);
            return r;
        }
    }
}
