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

        [Header("배치")]
        [Tooltip("같은 칸에 겹친 오브젝트를 옆으로 어긋나게 놓는 간격(픽셀, 32px = 1칸).")]
        public float stackSpacingPixels = 6f;

        [Tooltip("늑대 무리에서 뒤에 겹쳐 그리는 늑대의 간격(픽셀). 한 마리마다 이만큼씩 밀린다.")]
        public Vector2 packOffsetPixels = new Vector2(-4f, 3f);

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
