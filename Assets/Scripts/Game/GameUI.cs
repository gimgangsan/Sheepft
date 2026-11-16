using System;
using System.Collections.Generic;
using Sheepft.Core;
using TMPro;
using UnityEngine;
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

        [Header("아래쪽 줄")]
        public Button levelSelectButton;
        public Button nextButton;
        public TMP_Text descriptionText;

        [Header("결과")]
        public GameObject resultPanel;
        public TMP_Text resultText;

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
        public event Action<int> LevelChosen;

        private readonly List<Button> levelButtons = new List<Button>();
        private bool pulse;

        public bool IsLevelSelectOpen => levelSelectPanel != null && levelSelectPanel.activeSelf;

        private void Awake()
        {
            Bind(undoButton, () => { if (UndoClicked != null) UndoClicked(); });
            Bind(restartButton, () => { if (RestartClicked != null) RestartClicked(); });
            Bind(nextButton, () => { if (NextClicked != null) NextClicked(); });
            Bind(levelSelectButton, () => { if (LevelSelectClicked != null) LevelSelectClicked(); });
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

        public void ShowCounts(int turnsLeft, int sheepOnGrass, int targetSheep, bool cannotWin)
        {
            if (turnsText != null) turnsText.text = Mathf.Max(0, turnsLeft).ToString();
            if (sheepText != null) sheepText.text = sheepOnGrass + "/" + targetSheep;
            if (cannotWinIcon != null) cannotWinIcon.SetActive(cannotWin);
        }

        /// <summary>
        /// 승패 문구와 다음 스테이지 버튼. 진행 중이면 모두 숨긴다.
        /// stars는 승리했을 때의 별 수(1–3)다.
        /// </summary>
        public void ShowResult(GameResult result, int stars, bool hasNextStage)
        {
            bool over = result != GameResult.InProgress;
            if (resultPanel != null) resultPanel.SetActive(over);
            if (resultText != null && over)
            {
                resultText.text = result == GameResult.Won
                    ? wonText + "\n" + stars + (stars == 1 ? " STAR" : " STARS")
                    : lostText + "\n" + lostHint;
            }
            if (nextButton != null) nextButton.gameObject.SetActive(result == GameResult.Won && hasNextStage);
            pulse = result == GameResult.Lost;
            if (!pulse) SetScale(1f);
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

        // ── 패배했을 때 undo와 재시작을 눈에 띄게 ──

        private void Update()
        {
            if (pulse) SetScale(1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)));
        }

        private void SetScale(float scale)
        {
            var s = new Vector3(scale, scale, 1f);
            if (undoButton != null) undoButton.transform.localScale = s;
            if (restartButton != null) restartButton.transform.localScale = s;
        }
    }
}
