using System;
using System.Collections.Generic;
using Sheepft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sheepft.Game
{
    /// <summary>
    /// 게임 화면의 UI. `Assets/Prefabs/UI/GameUI.prefab`(Canvas, 기준 해상도 640×360)에 붙어 있다.
    /// 배치, 글자 크기, 색은 프리팹에서 고친다. 여기서는 내용만 바꾸고 버튼을 GameController에 잇는다.
    /// 인게임 글자는 모두 영어다.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        [Header("위쪽 줄")]
        public TMP_Text turnsText;
        public TMP_Text sheepText;
        public TMP_Text titleText;
        public GameObject cannotWinIcon;
        public Button undoButton;
        public Button restartButton;
        [Tooltip("힌트(전구) 버튼. 실패를 여러 번 하면 나타난다. 구현 계획 13절.")]
        public Button hintButton;

        [Header("아래쪽 줄")]
        public Button levelSelectButton;
        public Button nextButton;
        public TMP_Text descriptionText;
        [Tooltip("건너뛰기 글자. 힌트와 함께 열리고, Next 버튼 자리에 보인다.")]
        public HoverText skipText;

        [Header("결과")]
        public GameObject resultPanel;
        public TMP_Text resultText;
        [Tooltip("클리어했을 때만 켜는 별 줄. 결과 판의 자식이다.")]
        public GameObject starRow;
        [Tooltip("별 3개. 왼쪽부터 1, 2, 3번째 별이다.")]
        public Image[] starImages = new Image[0];
        [Tooltip("hud_icons의 star 태그.")]
        public Sprite starSprite;
        [Tooltip("hud_icons의 star_empty 태그.")]
        public Sprite starEmptySprite;

        [Header("레벨 선택")]
        public GameObject levelSelectPanel;
        [Tooltip("번호 버튼이 들어갈 곳 (GridLayoutGroup).")]
        public Transform levelGrid;
        [Tooltip("번호 버튼의 본. 꺼 둔 채로 두면 스테이지 수만큼 복사한다.")]
        public Button levelButtonTemplate;
        public Button levelSelectCloseButton;
        public Color levelColor = new Color32(0x4f, 0x40, 0x44, 0xff);
        public Color currentLevelColor = new Color32(0x8c, 0x78, 0x7a, 0xff);
        public Color clearedLevelColor = new Color32(0x47, 0x7e, 0x2f, 0xff);

        [Header("맵이 들어갈 자리")]
        [Tooltip("위쪽 줄의 높이(기준 해상도의 픽셀). 맵은 이 아래에 그린다.")]
        public float topBarPixels = 40f;
        [Tooltip("아래쪽 줄의 높이(기준 해상도의 픽셀). 맵은 이 위에 그린다.")]
        public float bottomBarPixels = 48f;
        public float referenceHeight = 360f;

        [Header("문구")]
        public string wonText = "CLEAR!";
        public string lostText = "FAILED";
        public string lostHint = "UNDO OR RESTART";

        public event Action UndoClicked;
        public event Action RestartClicked;
        public event Action NextClicked;
        public event Action LevelSelectClicked;
        public event Action HintClicked;
        public event Action SkipClicked;
        public event Action<int> LevelChosen;

        private readonly List<Button> levelButtons = new List<Button>();
        private bool pulse;
        /// <summary>힌트가 풀이에서 벗어났다고 알려 주는 중: Undo 버튼만 깜빡인다.</summary>
        private bool undoHint;
        /// <summary>힌트 버튼이 처음 나타나는 연출의 지난 시간. 연출 중이 아니면 음수.</summary>
        private float hintPopTime = -1f;

        private GameSettings settings;
        private GameResult shownResult = GameResult.InProgress;
        // 별 연출: 얻은 별 수, 시작한 뒤 지난 시간, 시작한 프레임. 연출 중이 아니면 starTime < 0.
        private int earnedStars;
        private float starTime = -1f;
        private int starStartFrame;
        /// <summary>박히는 소리를 이미 낸 별 수. 별은 왼쪽부터 차례로 박힌다.</summary>
        private int landedStars;
        private RectTransform resultRect;
        private Vector2 resultHome;

        public bool IsLevelSelectOpen => levelSelectPanel != null && levelSelectPanel.activeSelf;

        /// <summary>연출 조정값. 비어 있으면 연출 없이 끝 상태를 바로 보인다.</summary>
        public void Init(GameSettings gameSettings)
        {
            settings = gameSettings;
        }

        private void Awake()
        {
            if (resultPanel != null)
            {
                resultRect = resultPanel.transform as RectTransform;
                if (resultRect != null) resultHome = resultRect.anchoredPosition;
            }
            if (starRow != null) starRow.SetActive(false);
            Bind(undoButton, () => { if (UndoClicked != null) UndoClicked(); });
            Bind(restartButton, () => { if (RestartClicked != null) RestartClicked(); });
            Bind(nextButton, () => { if (NextClicked != null) NextClicked(); });
            Bind(levelSelectButton, () => { if (LevelSelectClicked != null) LevelSelectClicked(); });
            Bind(hintButton, () => { if (HintClicked != null) HintClicked(); });
            if (skipText != null) skipText.Clicked += () => { if (SkipClicked != null) SkipClicked(); };
            ShowHelp(false, false);
            Bind(levelSelectCloseButton, CloseLevelSelect);
            if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (levelButtonTemplate != null) levelButtonTemplate.gameObject.SetActive(false);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        /// <summary>게임 화면 UI 전체를 보이거나 숨긴다. 메뉴가 열려 있는 동안 숨긴다.</summary>
        public void SetVisible(bool visible)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas.enabled = visible;
        }

        // ── 내용 ──

        /// <summary>스테이지 번호(1부터)와 이름, 설명.</summary>
        public void ShowStage(int number, string stageName, string description)
        {
            if (titleText != null) titleText.text = number + ". " + stageName;
            if (descriptionText != null) descriptionText.text = description ?? "";
        }

        /// <summary>starTarget은 지금 노릴 수 있는 별 기준의 남은 턴이다(`LevelRules.StarTargetTurnsLeft`). `남은 턴/기준`으로 쓴다.</summary>
        public void ShowCounts(int turnsLeft, int starTarget, int sheepOnGrass, int targetSheep, bool cannotWin)
        {
            if (turnsText != null) turnsText.text = Mathf.Max(0, turnsLeft) + "/" + starTarget;
            if (sheepText != null) sheepText.text = sheepOnGrass + "/" + targetSheep;
            if (cannotWinIcon != null) cannotWinIcon.SetActive(cannotWin);
        }

        /// <summary>힌트 버튼과 건너뛰기 글자를 보이거나 숨긴다.</summary>
        public void ShowHelp(bool hint, bool skip)
        {
            if (hintButton != null)
            {
                hintButton.gameObject.SetActive(hint);
                if (!hint) { hintPopTime = -1f; hintButton.transform.localScale = Vector3.one; }
            }
            if (skipText != null) skipText.gameObject.SetActive(skip);
        }

        /// <summary>힌트 버튼이 처음 나타날 때: 커졌다가 제 크기로 돌아온다.</summary>
        public void PopHint()
        {
            if (hintButton == null || settings == null) return;
            hintPopTime = 0f;
            ApplyHintPop();
        }

        /// <summary>힌트가 풀이에서 벗어났다고 알릴 때 Undo 버튼을 깜빡인다.</summary>
        public void SetUndoHint(bool on)
        {
            undoHint = on;
            if (!on && !pulse) SetScale(1f);
        }

        /// <summary>
        /// 승패 문구와 다음 스테이지 버튼. 진행 중이면 모두 숨긴다.
        /// stars는 승리했을 때의 별 수(1–3)다.
        /// </summary>
        public void ShowResult(GameResult result, int stars, bool hasNextStage)
        {
            bool over = result != GameResult.InProgress;
            bool won = result == GameResult.Won;
            // 별 이미지가 없는 예전 프리팹은 별 수를 글자로 쓴다.
            bool starIcons = starRow != null && starImages != null && starImages.Length > 0;
            if (resultPanel != null) resultPanel.SetActive(over);
            if (resultText != null && over)
            {
                resultText.text = won
                    ? (starIcons ? wonText : wonText + "\n" + stars + (stars == 1 ? " STAR" : " STARS"))
                    : lostText + "\n" + lostHint;
            }
            if (nextButton != null) nextButton.gameObject.SetActive(won && hasNextStage);
            pulse = result == GameResult.Lost;
            if (!pulse) SetScale(1f);

            // 별: 새로 클리어했을 때만 처음부터 연출한다. 같은 결과로 다시 불리면 하던 연출을 이어 간다.
            if (starRow != null) starRow.SetActive(won && starIcons);
            if (won && starIcons && (shownResult != GameResult.Won || stars != earnedStars))
            {
                earnedStars = stars;
                landedStars = 0;
                starTime = 0f;
                starStartFrame = Time.frameCount;
                if (settings == null) starTime = -1f;
                ApplyStars();
            }
            else if (!won)
            {
                StopStars();
            }
            shownResult = result;
        }

        // ── 클리어 별 연출 ──

        /// <summary>남은 별 연출을 건너뛰고 끝 상태로.</summary>
        public void SkipStars()
        {
            if (starTime < 0f) return;
            starTime = -1f;
            // 건너뛰면 남은 별의 소리는 내지 않는다.
            landedStars = earnedStars;
            ApplyStars();
        }

        private void StopStars()
        {
            starTime = -1f;
            if (resultRect != null) resultRect.anchoredPosition = resultHome;
        }

        private void UpdateStars()
        {
            if (starTime < 0f) return;
            // 연출을 시작한 프레임의 클릭(재생 빨리 감기)으로는 건너뛰지 않는다.
            var pointer = Pointer.current;
            if (Time.frameCount != starStartFrame && pointer != null && pointer.press.wasPressedThisFrame)
            {
                SkipStars();
                return;
            }
            starTime += Time.unscaledDeltaTime;
            ApplyStars();
        }

        /// <summary>
        /// starTime의 모습을 그린다. starTime &lt; 0이면 끝 상태(얻은 별은 채운 별, 나머지는 빈 별, 흔들림 없음).
        /// 얻은 별 i는 첫 지연 + i × 간격에 starDropScale 배로 나타나 줄어들며 박히고, 박히는 순간 판이 흔들린다.
        /// </summary>
        private void ApplyStars()
        {
            var s = settings;
            bool done = starTime < 0f || s == null;
            float shake = 0f;
            float lastEnd = 0f;
            for (int i = 0; i < starImages.Length; i++)
            {
                var image = starImages[i];
                if (image == null) continue;
                float scale = 1f, alpha = 1f;
                bool filled = i < earnedStars;
                if (!done && filled)
                {
                    float start = s.starFirstDelaySeconds + i * s.starIntervalSeconds;
                    float impact = start + s.starDropSeconds;
                    float t = starTime - start;
                    lastEnd = Mathf.Max(lastEnd, impact + Mathf.Max(s.starSettleSeconds, s.starShakeSeconds));
                    if (t < 0f)
                    {
                        // 아직 차례가 아니다: 빈 별로 둔다.
                        filled = false;
                    }
                    else if (t < s.starDropSeconds)
                    {
                        // 크게 나타나 점점 빠르게 줄어든다.
                        float k = t / s.starDropSeconds;
                        scale = Mathf.Lerp(s.starDropScale, 1f, k * k);
                        alpha = Mathf.Clamp01(k * 3f);
                    }
                    else
                    {
                        float after = starTime - impact;
                        // 박힌 순간: 별마다 한 번, 뒤의 별일수록 높은 피치로.
                        if (i >= landedStars)
                        {
                            landedStars = i + 1;
                            SoundManager.PlayStar(i);
                        }
                        if (after < s.starSettleSeconds)
                            scale = 1f - s.starSquash * Mathf.Sin(after / s.starSettleSeconds * Mathf.PI);
                        if (after < s.starShakeSeconds)
                            shake = Mathf.Max(shake, s.starShakePixels * (1f - after / s.starShakeSeconds));
                    }
                }

                image.sprite = filled ? starSprite : starEmptySprite;
                image.rectTransform.localScale = new Vector3(scale, scale, 1f);
                var color = image.color;
                color.a = alpha;
                image.color = color;
            }

            if (!done && starTime > lastEnd) { starTime = -1f; done = true; }

            if (resultRect != null)
            {
                // 도트가 흐려지지 않게 흔들림은 정수 픽셀로.
                Vector2 offset = shake > 0f
                    ? new Vector2(Mathf.Round(UnityEngine.Random.Range(-shake, shake)), Mathf.Round(UnityEngine.Random.Range(-shake, shake)))
                    : Vector2.zero;
                resultRect.anchoredPosition = resultHome + offset;
            }
        }

        // ── 레벨 선택 ──

        public void OpenLevelSelect(int stageCount, int current, ICollection<int> cleared)
        {
            if (levelSelectPanel == null || levelButtonTemplate == null) return;

            while (levelButtons.Count < stageCount)
            {
                int index = levelButtons.Count;
                var button = Instantiate(levelButtonTemplate, levelGrid != null ? levelGrid : levelButtonTemplate.transform.parent);
                button.name = "Level " + (index + 1);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = (index + 1).ToString();
                button.onClick.AddListener(() =>
                {
                    CloseLevelSelect();
                    if (LevelChosen != null) LevelChosen(index);
                });
                levelButtons.Add(button);
            }

            for (int i = 0; i < levelButtons.Count; i++)
            {
                levelButtons[i].gameObject.SetActive(i < stageCount);
                var image = levelButtons[i].targetGraphic;
                if (image != null) image.color = cleared.Contains(i) ? clearedLevelColor : i == current ? currentLevelColor : levelColor;
            }
            levelSelectPanel.SetActive(true);
        }

        public void CloseLevelSelect()
        {
            if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        }

        // ── 패배했을 때 undo와 재시작을, 힌트가 벗어났을 때 undo를 눈에 띄게 ──

        private void Update()
        {
            UpdateStars();
            float beat = 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            if (pulse) SetScale(beat);
            else if (undoHint && undoButton != null) undoButton.transform.localScale = new Vector3(beat, beat, 1f);
            if (hintPopTime >= 0f)
            {
                hintPopTime += Time.unscaledDeltaTime;
                ApplyHintPop();
            }
        }

        private void SetScale(float scale)
        {
            var s = new Vector3(scale, scale, 1f);
            if (undoButton != null) undoButton.transform.localScale = s;
            if (restartButton != null) restartButton.transform.localScale = s;
        }

        /// <summary>
        /// 앞의 40%는 0에서 hintPopScale까지 커지고, 나머지는 1로 줄어든다.
        /// 도트가 고르게 보이게 배율은 0.5 단위로 끊는다(아이콘이 16×16의 두 배라서 0.5배 = 원래 픽셀 1배).
        /// </summary>
        private void ApplyHintPop()
        {
            float k = hintPopTime / settings.hintPopSeconds;
            float scale;
            if (k >= 1f) { scale = 1f; hintPopTime = -1f; }
            else if (k < 0.4f) scale = Mathf.Lerp(0f, settings.hintPopScale, k / 0.4f);
            else scale = Mathf.Lerp(settings.hintPopScale, 1f, (k - 0.4f) / 0.6f);
            scale = Mathf.Round(scale * 2f) / 2f;
            hintButton.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
