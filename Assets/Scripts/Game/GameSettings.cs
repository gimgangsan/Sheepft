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

        [Header("색")]
        public Color backgroundColor = new Color32(0x2b, 0x24, 0x33, 0xff);

        [Tooltip("맵 안의 구멍(x 칸) 색. 구멍 스프라이트가 생기면 쓰지 않는다.")]
        public Color holeColor = new Color32(0x1a, 0x15, 0x20, 0xff);
    }
}
