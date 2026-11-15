# CLAUDE.md

Claude Code가 이 프로젝트에서 작업할 때 따르는 지침이다.
노트북과 데스크톱을 git으로 오가며 작업하므로, **기기에 따로 저장되는 메모리 대신 이 파일에 규칙을 적는다.** 규칙이 바뀌면 이 파일을 고치고 커밋한다.

## 프로젝트

- **Sheepft**: 타일 줄을 shift해서 양을 풀로 데려가는 턴제 퍼즐 게임 (GitHub Game Off 2026)
- 게임 규칙의 기준 문서는 [README.md](README.md)다. 게임 로직을 만들거나 고칠 때는 README를 먼저 읽고, 다르면 README를 따른다.
- 코드 구조와 작업 순서는 [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)를 따른다.
- 엔진: Unity 6000.5.0f1, URP, Input System
- **인게임 텍스트는 모두 영어로 쓴다** (스테이지 이름·설명, 버튼, 승패 문구). 문서와 코드 주석은 한국어다.
- UI 기준 해상도는 640×360이다.
- 2D 도트 그래픽. 스프라이트는 Aseprite로 만든다. 만들 스프라이트 목록과 파일별 사양은 [SPRITES.md](SPRITES.md)에 있다.

## 폴더 구조

저장소 루트는 `Sheepft/`이고 Unity 에셋은 `Assets/` 아래에 있다. 새 파일은 아래 위치에 만든다. 여기 없는 종류의 폴더가 필요하면 먼저 사용자에게 묻고, 정해지면 이 표에 추가한다.

| 위치 | 넣는 것 |
| --- | --- |
| `Assets/Sprites/Animals/` | 동물: 양, 늑대, 염소, 말, 소 |
| `Assets/Sprites/Objects/` | 물건: 과일 바구니, 나무 양동이 |
| `Assets/Sprites/Terrain/` | 지형 타일: 땅, 돌, 울타리, 나무, 물, 들불, 풀, 재 |
| `Assets/Sprites/UI/` | 버튼, 아이콘, 표시 등 UI 그래픽 |
| `Assets/Sprites/Effects/` | 반응 연출 이펙트: 물보라, 수증기, 연기, 충격파 등 |
| `Assets/Scripts/` | C# 스크립트. `Core/`(게임 규칙, 순수 C#)와 `Game/`(화면·입력)으로 나눈다 |
| `Assets/Tests/` | 단위 테스트 (Unity Test Framework, `EditMode/`) |
| `Assets/Prefabs/` | 프리팹. `GameRoot.prefab`(목록과 진행), `Entities/`(오브젝트), `Terrain/`(지형), `Effects/`(이펙트), `UI/`(Canvas UI, 화살표, 줄 표시) |
| `Assets/Scenes/` | 씬 |
| `Assets/Levels/` | 레벨 데이터 `stages.txt` (README 10절 표기법), 스테이지 설계 문서 `StageDesign.md` |
| `Assets/Levels/Solver~/` | 시뮬레이터, solver, 단위 테스트 (C#, .NET SDK 필요). 사용법은 그 폴더의 `README.md` |
| `Assets/Settings/` | URP 등 렌더링 설정. 요청이 없으면 건드리지 않는다 |
| `Assets/Fonts/` | 폰트. 인게임 글자와 숫자는 모두 Agrave Pro(`AgravePro-Regular.ttf`, `AgravePro-Bold.ttf`, SIL OFL)를 쓴다. 라이선스 `AgravePro-OFL.txt`를 함께 둔다 |
| `Assets/Data/` | 조정값 ScriptableObject (이동 시간, 간격 등) |
| `Assets/TextMesh Pro/` | TMP 필수 리소스. Unity가 만든다. 직접 고치지 않는다 |

- 게임에 들어가지 않는 파일(미리보기 이미지, 임시 스크립트, 테스트 출력)은 `Assets/` 안에 두지 않는다. `Assets/` 안의 파일은 Unity가 모두 불러온다.
  - 예외: 이름이 `~`로 끝나는 폴더는 Unity가 불러오지 않는다. `Solver~`가 그렇다. 이 폴더에는 `.meta`가 생기지 않는다.
- 문서는 저장소 루트에 둔다. 스테이지 설계 문서만 레벨 데이터 옆에 둔다.
- 레벨을 만들거나 고치면 `Solver~`의 `check` 명령으로 검증한다.
- 레벨의 shift 포인트는 최소한으로 줄이지 않는다. 모든 줄을 양방향으로 연 뒤, 의도와 다른 해를 만드는 포인트만 `prune` 명령으로 찾아 뺀다. 여러 개를 빼야 하면 포인트 대신 맵의 지형을 고친다.
  - 예외: 21–30번은 지형을 고치는 대신 `prune`이 알려 주는 포인트를 여러 개 빼도 된다. "우회해 없음" 기준은 그대로다. 뺀 포인트와 이유는 `StageDesign.md`에 적는다.

## 스프라이트 규칙

- **`.aseprite` 파일 하나만 저장한다. PNG로 내보내지 않는다.** Unity의 2D Aseprite Importer(`com.unity.2d.aseprite`)가 `.aseprite`를 바로 불러온다. PNG가 꼭 필요하면 `Assets/` 밖에 저장한다.
- 파일 이름은 소문자 snake_case다. 예: `sheep.aseprite`, `fruit_basket.aseprite`, `wild_fire.aseprite`
- 오브젝트 하나에 파일 하나를 쓴다. 애니메이션은 같은 파일 안에 프레임과 태그(`idle`, `walk` 등)로 넣는다.
- 기본 크기는 32×32다. 동물과 물건은 발이 아래쪽에 오도록 그리고 1–2px 여백을 둔다.
- Unity Import 설정: Pixels Per Unit 32, Filter Mode Point (no filter), Compression None
- 빛은 왼쪽 위에서 온다. 바깥선은 1px로 그리고 색은 `#2b2433`이다.

### 팔레트 (양 기준)

새 스프라이트도 가능하면 이 색을 재사용한다. 색을 추가하면 여기에 적는다.

| 용도 | 색 |
| --- | --- |
| 바깥선, 발굽 | `#2b2433` |
| 눈 | `#1a1520` |
| 양털 밝음 / 중간 / 그림자 | `#f4f0e8` / `#d6cfc4` / `#b0a698` |
| 양 얼굴·다리 / 얼굴 밝음 / 먼 다리 | `#6e5c5f` / `#8c787a` / `#4f4044` |
| 귀 안쪽 | `#e89aa6` |
| 땅 바탕 / 밝은 점 / 어두운 점 / 자갈 그늘·풀 그림자 | `#c8a46e` / `#d8b985` / `#b38f5d` / `#9b7a4e` |
| 풀 바탕 / 밝음 / 어두움 / 테두리 | `#5f9d3c` / `#7fbb4b` / `#477e2f` / `#33602a` |
| 물 바탕 / 물결 밝음 / 물가 그늘 / 물거품 / 젖은 물가 흙 | `#3b79c3` / `#66a3df` / `#2c5b9b` / `#d3ebf6` / `#8a6943` |
| 재 바탕 / 밝음 / 어두움 / 숯 | `#6c6671` / `#868090` / `#56505c` / `#3d3743` |
| 들불 바닥 / 바닥 그늘 / 불씨 금 | `#4e2620` / `#3b1d1c` / `#a33a1f` |
| 불꽃 빨강 / 주황 / 노랑 / 심지 | `#d23c1e` / `#f2812a` / `#fcc94b` / `#fff2b3` |
| 돌 하이라이트 / 윗면 / 앞면 / 그늘 / 금 | `#d3d6de` / `#b4b8c4` / `#8b8f9e` / `#666a79` / `#4a4c5a` |
| 나무 재질(울타리·줄기 등) 밝음 / 중간 / 어두움 | `#c88f52` / `#a2683b` / `#774a2b` |
| 나뭇잎 밝음 / 중간 / 어두움 / 가장 어두움 | `#76b04f` / `#4f8f3e` / `#3a6f33` / `#2a5129` |
| 늑대 털 밝음 / 등 / 그늘 / 먼 다리 / 배·주둥이 | `#a9adb8` / `#868a98` / `#646877` / `#4f5261` / `#d6d8de` |
| 염소 털 밝음 / 중간 / 그늘 / 먼 다리 | `#b98a63` / `#9b6b4a` / `#764f37` / `#5a3a2a` |
| 염소·말 주둥이, 소 뿔 (크림) | `#e3d2b4` |
| 말 털 밝음 / 중간 / 그늘 / 먼 다리 / 갈기·꼬리 | `#d07a45` / `#b4592f` / `#87411f` / `#66301a` / `#3d2a2e` |
| 말 안장 깔개 / 그늘 / 테두리 | `#c0483e` / `#8e3029` / `#e0c068` |
| 소 무늬 (재의 숯 색과 같음) / 소 먼 다리 그늘 / 콧구멍 | `#3d3743` / `#8c8377` / `#c97a88` |
| 이빨 (양털 밝음과 같음) | `#f4f0e8` |
| 나무 재질 가장 어두움 (양동이 안쪽 바닥) | `#5a3622` |
| 빨간 사과 바탕 / 밝음 / 그늘 | `#d8453a` / `#ef7a62` / `#a52f2c` |
| 오렌지 그늘 (바탕·밝음은 불꽃 주황·노랑) | `#c8601f` |
| 초록 사과 밝음 (바탕·그늘은 풀 밝음·어두움) | `#a7d46a` |
| 양동이 쇠테 (돌 앞면·그늘과 같음) | `#8b8f9e` / `#666a79` |
| UI 버튼 판 보통 / 그늘 / 아랫단 (양털 색과 같음) | `#f4f0e8` / `#d6cfc4` / `#b0a698` |
| UI 버튼 hover 판 / 그늘 / 아랫단 (불꽃 심지·노랑 + 새 색) | `#fff2b3` / `#fcc94b` / `#d9a441` |
| UI 버튼 아이콘 (양 먼 다리와 같음) | `#4f4044` |
| shift 화살표 hover 하이라이트 | `#ffffff` |
| 승리 불가 표시 빨강 / 깜빡일 때 밝은 빨강 | `#d23c1e` / `#ef4a2a` |
| 모래시계 모래 (안장 테두리와 같음) | `#e0c068` |

## Unity 파일 다루기

- `.meta` 파일은 Unity가 만든다. 직접 만들거나 고치지 않는다.
- 이미 `.meta`가 있는 에셋을 Unity 밖에서 옮기거나 이름을 바꾸면 `.meta`도 똑같이 옮긴다. 그래야 참조가 끊어지지 않는다.
- 에셋과 `.meta`는 함께 커밋한다.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`는 기기마다 따로 생기는 폴더라서 git에 올리지 않는다.

## 두 기기에서 작업하기

- 작업을 시작하기 전에 `git pull`, 기기를 바꾸기 전에 커밋하고 push한다. 커밋과 push는 사용자가 요청할 때만 한다.
- Aseprite MCP는 기기마다 설치한다. 저장소에는 MCP 설정 파일을 두지 않는다.

### Aseprite MCP 설치 (기기마다 한 번)

1. 서버 받기: `git clone https://github.com/diivi/aseprite-mcp.git C:/tools/aseprite-mcp`
2. `C:/tools/aseprite-mcp/.env`에 Aseprite 경로 적기. Steam 설치는 보통 아래 경로다.
   `ASEPRITE_PATH=C:/Program Files (x86)/Steam/steamapps/common/Aseprite/Aseprite.exe`
3. 라이브러리 설치: `uv --directory C:/tools/aseprite-mcp sync`
4. Claude Code에 등록: `claude mcp add aseprite -s user -- uv --directory C:/tools/aseprite-mcp run -m aseprite_mcp`
   - 데스크톱 앱만 쓰면 `claude`가 PATH에 없다. 앱에 들어 있는 CLI를 쓴다: `%LOCALAPPDATA%\Claude-Data\claude_*\claude-code\<버전>\<id>\claude.exe`
5. 새 세션을 열면 Aseprite 도구가 연결된다.

### Unity MCP 설치 (기기마다 한 번)

Unity 쪽 패키지(`com.coplaydev.unity-mcp`, v10.3.0)는 `Packages/manifest.json`에 들어 있어 git으로 따라온다. 서버만 기기마다 설치한다.

1. 서버 설치: `python -m venv C:/tools/unity-mcp` 다음 `C:/tools/unity-mcp/Scripts/python.exe -m pip install mcpforunityserver==10.3.0`
2. Claude Code에 등록: `claude mcp add unity -s user -e DISABLE_TELEMETRY=true -e UNITY_MCP_DISABLE_TELEMETRY=true -- C:/tools/unity-mcp/Scripts/mcp-for-unity.exe --transport stdio`
3. Unity 에디터에서 프로젝트를 열고 Window > MCP for Unity에서 연결 상태를 확인한다. 새 세션을 열면 Unity 도구가 연결된다.

- 에디터가 프로젝트를 열고 있는 동안에는 Unity 배치 모드(`-batchmode -runTests`)를 쓸 수 없다. 그때는 MCP로 테스트를 돌린다.

## 게임 씬 설정

화면은 프리팹과 씬으로 이루어져 있고 에디터에서 직접 고친다. 맵 배치만 `stages.txt`가 정한다(Play할 때 프리팹을 찍어 낸다).

| 고칠 것 | 어디서 |
| --- | --- |
| 오브젝트의 그림 위치, 아이콘 높이, 붙일 컴포넌트 | `Assets/Prefabs/Entities/*.prefab` (뿌리에 `EntityView`, 자식 `Body`와 `Status`) |
| 지형, 이펙트 | `Assets/Prefabs/Terrain/`, `Assets/Prefabs/Effects/` |
| 애니메이션 | `.aseprite`의 태그. Aseprite Importer가 태그마다 AnimationClip과 Animator Controller를 만든다. 상태 이름이 태그 이름이다 |
| UI 배치, 글자 크기·색, 문구 | `Assets/Prefabs/UI/GameUI.prefab` (Canvas, 기준 640×360, `GameUI` 컴포넌트) |
| 이동 시간, 간격, 배경색 | `Assets/Data/GameSettings.asset` |
| 어떤 프리팹을 쓸지 | `GameRoot.prefab`의 `PrefabCatalog` |

- 스프라이트를 추가하거나 `.aseprite`의 프레임·태그를 바꾸면 Unity 메뉴 **Sheepft > Setup Game Scene**을 다시 돌린다. 배치 모드로는 `-executeMethod Sheepft.Game.GameSceneSetup.Setup`이다.
- 이 메뉴는 손으로 고친 것을 덮어쓰지 않는다. Import 설정(Pixels Per Unit 32)을 맞추고, `GameRoot.prefab`의 **스프라이트 목록만** 다시 채운다. 프리팹, 폰트 에셋, UI, 씬의 `GameUI`·`EventSystem`, `GameSettings.asset`은 **없을 때만** 만든다. 처음부터 다시 만들고 싶으면 그 에셋을 지우고 메뉴를 돌린다.
- 프리팹의 스프라이트 목록(`SpriteCatalog.sheets`)은 손으로 고치지 않는다.
- 물·풀·울타리 프리팹에는 Animator를 붙이지 않는다. 이웃에 따라 `BoardView`가 프레임을 골라 넣는다.
- Play하지 않고 스테이지를 보려면 메뉴 **Sheepft > Preview Stage**를 쓴다. 씬에 저장되지 않는다.
- 에디터 전용 코드는 `Assets/Scripts/Game/` 안에서 `#if UNITY_EDITOR`로 감싼다.

## 테스트

- 규칙은 단위 테스트(`Assets/Tests/EditMode`)로 확인한다. Unity MCP로는 컴파일과 EditMode 테스트 실행까지만 한다.
- **인게임(Play 모드) 확인은 Claude가 하지 않는다.** 토큰을 많이 쓰기 때문이다. 눈으로 봐야 하는 것은 [PLAYTEST.md](PLAYTEST.md)에 항목으로 남기고, 사람이 Play해서 확인한다.

MCP가 없는 세션에서는 Aseprite 배치 모드(`Aseprite.exe -b --script <lua>`)로 같은 작업을 할 수 있다.
