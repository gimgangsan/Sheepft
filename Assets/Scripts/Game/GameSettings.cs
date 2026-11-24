using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 화면 연출의 조정값. `Assets/Data/GameSettings.asset`을 Inspector에서 고친다.
    /// 에셋이 없으면 메뉴 Sheepft > Setup Game Scene이 기본값으로 만든다. 이미 있으면 덮어쓰지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Sheepft/Game Settings")]
    public sealed class GameSettings : ScriptableObject
    {
        [Header("소리")]
        [Tooltip("음악과 효과음 목록(Assets/Data/SoundLibrary.asset). 비어 있으면 소리를 내지 않는다.")]
        public SoundLibrary sounds;

        [Header("이벤트 재생")]
        [Tooltip("오브젝트가 한 칸 움직이는 데 걸리는 시간(초).")]
        [Min(0.01f)] public float moveSeconds = 0.18f;

        [Tooltip("이동이 아닌 변화(상태, 지형, 사라짐)를 보여 준 뒤 쉬는 시간(초).")]
        [Min(0f)] public float stepPauseSeconds = 0.10f;

        [Tooltip("이동의 가속·감속 곡선. 가로는 진행(0–1), 세로는 위치(0–1).")]
        public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("염소 박치기")]
        [Tooltip("공격 방향으로 튀어 나가는 거리(칸).")]
        [Min(0f)] public float goatLungeDistance = 0.3f;

        [Tooltip("튀어 나가는 데 걸리는 시간(초). 끝날 때 부딪히며 이펙트가 나오고 대상이 밀린다.")]
        [Min(0.01f)] public float goatLungeSeconds = 0.09f;

        [Tooltip("제자리로 돌아오는 데 걸리는 시간(초).")]
        [Min(0.01f)] public float goatReturnSeconds = 0.06f;

        [Tooltip("한 방향 공격이 끝나고 다음 방향을 공격하기 전에 쉬는 시간(초).")]
        [Min(0f)] public float goatAttackGapSeconds = 0.05f;

        [Header("클리어 별 연출")]
        [Tooltip("CLEAR! 판이 뜬 뒤 첫 별이 떨어지기 시작할 때까지(초).")]
        [Min(0f)] public float starFirstDelaySeconds = 0.15f;

        [Tooltip("얻은 별이 하나씩 떨어지는 간격(초).")]
        [Min(0f)] public float starIntervalSeconds = 0.25f;

        [Tooltip("별이 크게 나타나 제 크기로 줄어들며 박히기까지(초). 끝나는 순간 판이 흔들린다.")]
        [Min(0.01f)] public float starDropSeconds = 0.12f;

        [Tooltip("별이 처음 나타날 때의 크기(제 크기의 배수).")]
        [Min(1f)] public float starDropScale = 2.5f;

        [Tooltip("박힌 뒤 납작해졌다 돌아오는 시간(초).")]
        [Min(0f)] public float starSettleSeconds = 0.12f;

        [Tooltip("박힐 때 납작해지는 정도. 0.15면 85%까지 줄었다가 돌아온다.")]
        [Range(0f, 0.5f)] public float starSquash = 0.15f;

        [Tooltip("별이 박힐 때 결과 판이 흔들리는 폭(기준 해상도의 픽셀). 0이면 흔들지 않는다.")]
        [Min(0f)] public float starShakePixels = 3f;

        [Tooltip("결과 판이 흔들리는 시간(초).")]
        [Min(0f)] public float starShakeSeconds = 0.15f;

        [Header("근육질 양이 되는 순간 (생강꽃을 먹음)")]
        [Tooltip("근육질 양이 처음 나타날 때의 크기(제 크기의 배수). 0.4면 보통 양 크기쯤에서 커진다.")]
        [Min(0.01f)] public float buffPopStartScale = 0.4f;

        [Tooltip("커지다가 넘치는 크기(제 크기의 배수). 그다음 제 크기로 돌아온다.")]
        [Min(1f)] public float buffPopOvershoot = 1.15f;

        [Tooltip("커졌다가 돌아오기까지(초).")]
        [Min(0.01f)] public float buffPopSeconds = 0.3f;

        [Header("엔딩 컷씬 (근육질 양이 늑대를 쓰러뜨림)")]
        [Tooltip("컷씬 전체 시간의 배율. 아래의 모든 시간과 스프라이트 프레임 길이에 곱한다. " +
                 "1이면 그대로, 1.5면 1.5배 느리게, 0.5면 두 배 빠르게. 늑대가 날아가는 거리는 그대로다.")]
        [Min(0.1f)] public float endingTimeScale = 1f;

        [Tooltip("양과 늑대만 남기고 화면이 검게 가려지는 시간(초). 그동안 늑대는 양 앞으로 물러선다.")]
        [Min(0.01f)] public float endingDarkenSeconds = 0.6f;

        [Tooltip("양의 발밑 가운데에서 늑대 발밑 가운데까지의 거리(픽셀). 늑대가 양과 마주 서는 자리다.")]
        public float endingWolfFrontPixels = 44f;

        [Tooltip("7·8프레임을 반복하며 힘을 모으는 시간(초). 5·6프레임은 이 앞에 한 번 나온다.")]
        [Min(0f)] public float endingChargeSeconds = 1.5f;

        [Tooltip("힘을 모으는 동안 양과 늑대 쪽으로 다가가는 배율. 2면 화면이 두 배로 커진다.")]
        [Min(1f)] public float endingFocusZoom = 2f;

        [Tooltip("다가간 화면의 가운데 높이(픽셀). 양과 늑대 발밑에서 잰다.")]
        public float endingFocusHeightPixels = 40f;

        [Tooltip("힘을 모으는 동안 화면이 떨리는 폭(픽셀, 32px = 1칸).")]
        [Min(0f)] public float endingChargeShakePixels = 1f;

        [Tooltip("buff_sheep 7·8프레임에서 power_charge를 놓는 왼쪽 주먹(캔버스 왼쪽 위 기준 픽셀). 양이 오른쪽을 볼 때 기준이다.")]
        public Vector2 endingChargeFist = new Vector2(17f, 66f);

        [Tooltip("buff_sheep 10프레임에서 uppercut_impact를 놓는 치켜든 주먹(캔버스 왼쪽 위 기준 픽셀).")]
        public Vector2 endingHitFist = new Vector2(69f, 11f);

        [Tooltip("10프레임에서 시간이 멈춰 있는 시간(초). 그동안 uppercut_impact는 3프레임까지 재생하고 멈춘다.")]
        [Min(0f)] public float endingHitstopSeconds = 0.35f;

        [Tooltip("맞는 순간 늑대 몸 가운데를 주먹에서 얼마나 옮겨 놓을지(픽셀). 늑대가 양 오른쪽에 있을 때 기준이다.")]
        public Vector2 endingWolfHitOffsetPixels = new Vector2(6f, 6f);

        [Tooltip("맞는 순간 늑대가 뒤로 젖혀지는 각도(도).")]
        public float endingWolfHitTilt = 25f;

        [Tooltip("늑대가 날아가는 속도(칸/초).")]
        [Min(0f)] public float endingWolfFlySpeed = 16f;

        [Tooltip("늑대가 날아가는 방향. 늑대가 양 오른쪽에 있을 때 기준이고, 왼쪽이면 x를 뒤집는다.")]
        public Vector2 endingWolfFlyDirection = new Vector2(0.5f, 1f);

        [Tooltip("늑대가 날아가며 도는 속도(도/초). 0이면 돌지 않는다.")]
        public float endingWolfSpinPerSecond = 1080f;

        [Tooltip("늑대가 날아가는 시간(초). 지나면 숨긴다.")]
        [Min(0f)] public float endingWolfFlySeconds = 0.9f;

        [Tooltip("시간이 다시 흐른 뒤 원래 화면 크기로 물러나는 시간(초).")]
        [Min(0.01f)] public float endingZoomOutSeconds = 0.25f;

        [Tooltip("타격 뒤 화면이 크게 떨리는 폭(픽셀). 시간이 지나며 줄어든다.")]
        [Min(0f)] public float endingBigShakePixels = 8f;

        [Tooltip("타격 뒤 화면이 떨리는 시간(초).")]
        [Min(0f)] public float endingBigShakeSeconds = 0.6f;

        [Tooltip("화면이 하얗게 덮이는 시간(초).")]
        [Min(0.01f)] public float endingWhiteSeconds = 1.5f;

        [Tooltip("하얗게 덮인 채 기다리는 시간(초).")]
        [Min(0f)] public float endingWhiteHoldSeconds = 0.3f;

        [Tooltip("하얀 막이 걷히며 원래 게임 화면과 결과 창이 나타나는 시간(초).")]
        [Min(0.01f)] public float endingRevealSeconds = 0.6f;

        [Tooltip("화면을 덮는 색.")]
        public Color endingFadeColor = Color.white;

        [Tooltip("컷씬이 시작된 뒤 이 시간이 지나야 클릭이나 키로 건너뛸 수 있다(초).")]
        [Min(0f)] public float endingSkipDelaySeconds = 0.5f;

        [Header("구름 화면 전환 (transition_cloud)")]
        [Tooltip("다 덮인 뒤 걷어 내기 전까지 기다리는 시간(초).")]
        [Min(0f)] public float transitionHoldSeconds = 0.1f;

        [Tooltip("프레임 길이(.aseprite에 적힌 값)에 곱하는 배수. 1이면 그대로, 0.5면 두 배 빠르게.")]
        [Min(0.1f)] public float transitionFrameScale = 1f;

        [Header("도착 칸 표시 (shift 화살표에 마우스를 올렸을 때)")]
        [Tooltip("동물 그림의 크기(원래 크기의 배수).")]
        [Min(0.1f)] public float arrivalIconScale = 0.5f;

        [Tooltip("아래 방향 화살표의 크기(원래 크기의 배수).")]
        [Min(0.1f)] public float arrivalArrowScale = 0.5f;

        [Tooltip("동물 그림 가운데의 높이(픽셀, 32px = 1칸). 칸 바닥에서 잰다.")]
        public float arrivalIconHeightPixels = 29f;

        [Tooltip("화살표 가운데의 높이(픽셀). 칸 바닥에서 잰다.")]
        public float arrivalArrowHeightPixels = 16f;

        [Tooltip("같은 칸에 여럿이 올 때 표시 사이의 가로 간격(픽셀).")]
        public float arrivalSpacingPixels = 16f;

        [Header("배치")]
        [Tooltip("같은 칸에 겹친 오브젝트를 옆으로 어긋나게 놓는 간격(픽셀, 32px = 1칸).")]
        public float stackSpacingPixels = 6f;

        [Tooltip("늑대 무리에서 뒤에 겹쳐 그리는 늑대의 간격(픽셀). 한 마리마다 이만큼씩 밀린다.")]
        public Vector2 packOffsetPixels = new Vector2(-4f, 3f);

        [Tooltip("말의 등에 탄 오브젝트를 말 그림에서 얼마나 옮겨 놓을지(픽셀). 말이 오른쪽을 볼 때 기준이고, 왼쪽을 보면 x를 뒤집는다.")]
        public Vector2 riderOffsetPixels = new Vector2(-5f, 13f);

        [Tooltip("상태 아이콘의 높이(픽셀). 칸 바닥에서 잰다.")]
        public float statusIconHeightPixels = 26f;

        [Tooltip("화면 가장자리와 맵·UI 사이의 여백(칸).")]
        public float cameraMargin = 0.25f;

        [Header("메뉴 배치 (메뉴 그림 1920×360의 픽셀, 왼쪽 위가 0, 칸은 그 칸의 왼쪽 위)")]
        [Tooltip("로고 캔버스의 왼쪽 위.")]
        public Vector2 menuLogoTopLeft = new Vector2(800f, 40f);

        [Tooltip("title_logo.aseprite의 캔버스 크기.")]
        public Vector2 menuLogoCanvasSize = new Vector2(320f, 96f);

        [Tooltip("타이틀에서 양이 서 있는 땅 칸. 왼쪽 칸에 ▶(아래 Play), 오른쪽 칸에 ◀(아래 Setting).")]
        public Vector2 menuTitleCell = new Vector2(944f, 200f);

        [Tooltip("설정 화면에서 양이 내려앉는 땅 칸. 왼쪽 칸에 ▶(아래 Back). 타이틀 칸과 같은 높이로 둔다.")]
        public Vector2 menuSettingsCell = new Vector2(576f, 200f);

        [Tooltip("설정 화면의 첫 설정 줄(음악)의 맨 왼쪽 칸. 줄은 6칸이고 양 끝 바깥 칸에 화살표가 있다. 이름과 값은 줄 바로 위에 적는다.")]
        public Vector2 menuSettingsRowTopLeft = new Vector2(96f, 56f);

        [Tooltip("설정 줄 사이의 세로 간격(픽셀). 음악, 효과음, 이동 속도 순으로 아래로 쌓인다.")]
        public float menuSettingsRowSpacing = 56f;

        [Tooltip("스테이지 선택에서 고른 줄의 땅 칸. 풀은 그 오른쪽 칸이고, 다른 스테이지 줄은 위아래로 32px씩 쌓인다. " +
                 "타이틀 칸과 같은 높이로 두어 양이 빈칸을 건너듯 같은 줄로 넘어가게 한다. 위아래 2줄이면 y는 96–232여야 화면에 들어간다.")]
        public Vector2 menuStageCell = new Vector2(1400f, 200f);

        [Tooltip("고른 줄의 위와 아래에 각각 보여 줄 스테이지 줄 수.")]
        [Min(0)] public int menuStageRowsAround = 2;

        [Header("메뉴 배치: 꿈 풍선 (안의 위치는 풍선 캔버스의 왼쪽 위 기준 픽셀)")]
        [Tooltip("dream_bubble 캔버스의 왼쪽 위(메뉴 그림 픽셀).")]
        public Vector2 menuBubbleTopLeft = new Vector2(1548f, 4f);

        [Tooltip("dream_bubble.aseprite의 캔버스 크기.")]
        public Vector2 menuBubbleCanvasSize = new Vector2(365f, 269f);

        [Tooltip("꿈 풍선 판의 불투명도. 1이면 불투명, 0이면 보이지 않는다. 안의 글자·별·맵에는 적용하지 않는다.")]
        [Range(0f, 1f)] public float menuBubbleAlpha = 1f;

        [Tooltip("스테이지 이름 줄의 가운데 위.")]
        public Vector2 menuBubbleNameTop = new Vector2(182f, 30f);

        [Tooltip("얻은 별(별 아이콘 3개) 줄의 가운데 위.")]
        public Vector2 menuBubbleStarsTop = new Vector2(182f, 50f);

        [Tooltip("3별 조건 줄(턴 아이콘 + 글자)의 가운데 위.")]
        public Vector2 menuBubbleConditionTop = new Vector2(182f, 70f);

        [Tooltip("맵 미리보기의 가운데. 칸 하나는 16px이고, 가장 큰 맵(8×6)은 128×96이다.")]
        public Vector2 menuBubbleMapCenter = new Vector2(182f, 146f);

        [Tooltip("꿈 풍선 안 글자 크기(월드 공간 TextMeshPro).")]
        [Min(0.1f)] public float menuBubbleFontSize = 4f;

        [Header("메뉴 글자와 시간")]
        [Tooltip("메뉴 글자 크기(월드 공간 TextMeshPro).")]
        [Min(0.1f)] public float menuLabelFontSize = 4f;

        [Tooltip("옆 화면으로 넘어가는 시간(초). 양은 이 동안 빈칸을 건너뛴다.")]
        [Min(0.01f)] public float menuPanSeconds = 0.6f;

        [Tooltip("양이 풀에 들어간 뒤 스테이지를 열기까지 기다리는 시간(초).")]
        [Min(0f)] public float menuEnterDelaySeconds = 0.5f;

        [Header("색")]
        public Color backgroundColor = new Color32(0x2b, 0x24, 0x33, 0xff);

        [Tooltip("맵 안의 구멍(x 칸) 색. 구멍 스프라이트가 생기면 쓰지 않는다.")]
        public Color holeColor = new Color32(0x1a, 0x15, 0x20, 0xff);
    }
}
