# CLAUDE.md

Claude Code가 이 프로젝트에서 작업할 때 따르는 지침이다.
노트북과 데스크톱을 git으로 오가며 작업하므로, **기기에 따로 저장되는 메모리 대신 이 파일에 규칙을 적는다.** 규칙이 바뀌면 이 파일을 고치고 커밋한다.

## 프로젝트

- **Sheepft**: 타일 줄을 shift해서 양을 풀로 데려가는 턴제 퍼즐 게임 (GitHub Game Off 2026)
- 게임 규칙의 기준 문서는 [README.md](README.md)다. 게임 로직을 만들거나 고칠 때는 README를 먼저 읽고, 다르면 README를 따른다.
- 코드 구조와 작업 순서는 [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)를 따른다.
- 엔진: Unity 6000.5.0f1, URP, Input System
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
| `Assets/Prefabs/` | 프리팹 |
| `Assets/Scenes/` | 씬 |
| `Assets/Levels/` | 레벨 데이터 (README 10절 표기법) |
| `Assets/Settings/` | URP 등 렌더링 설정. 요청이 없으면 건드리지 않는다 |

- 게임에 들어가지 않는 파일(미리보기 이미지, 임시 스크립트, 테스트 출력)은 `Assets/` 안에 두지 않는다. `Assets/` 안의 파일은 Unity가 모두 불러온다.
- 문서는 저장소 루트에 둔다.

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

MCP가 없는 세션에서는 Aseprite 배치 모드(`Aseprite.exe -b --script <lua>`)로 같은 작업을 할 수 있다.
