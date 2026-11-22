# 스테이지 도구 구현 계획

목표: 사용자가 스테이지를 마음대로 고치고 더할 수 있는 도구를 만든다. 세 가지 일로 나눈다.

1. **solver 대체** — `Assets/Levels/Solver~/`를 지우고, 게임이 실제로 쓰는 규칙 코드(`Assets/Scripts/Core/`) 위에서 돌아가는 solver와 검증기를 만든다. 버튼 하나로 검증 결과를 본다. ← **이 문서의 범위**
2. stages.txt 에디터 — 나중에 정한다.
3. 스테이지를 고쳤을 때 오류가 나지 않는지 검증 — 나중에 정한다. 1번의 검증기가 바탕이 된다.

- 작성: 2026-11-23
- 규칙의 기준은 [README.md](README.md), 게임 코드 구조는 [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)다.

---

## 1. 지금 상태와 문제

| | `Solver~` (지금) | 게임 (`Scripts/Core`) |
| --- | --- | --- |
| 규칙 구현 | `Simulator.cs` (996줄, 자체 상태 모델) | `TurnRunner`, 엔티티 클래스들 |
| 이벤트 | 의미 단위 플래그 `Ev` 55종 (`SheepWrap`, `StunSpared` …) | 화면 재생용 저수준 이벤트 (`MovedEvent`, `RemovedEvent` …) |
| `@intent`, `@optimal` | 읽어서 씀 | 읽고 버림 |
| 실행 | 명령줄 `dotnet run … check` (.NET SDK 10 필요) | — |

- **규칙 구현이 두 벌이다.** 한쪽만 고치면 solver가 "깰 수 있다"고 한 스테이지가 게임에서는 안 깨질 수 있다. 그런데 이걸 잡는 테스트가 없다. `StagesFileTests`는 풀이 줄을 파싱만 하고 실제로 두어 보지 않는다.
- 검증하려면 터미널에서 .NET 명령을 쳐야 한다.

### 기준값 (2026-11-23, 지금 `Solver~`의 `check` 결과)

새 solver가 같은 답을 내는지 맞춰 볼 기준이다. `Solver~`를 지우기 전에 이 표와 비교한다.
30개 모두 OK이고, 우회해는 모두 없음(`-`)이다. 실행 시간은 빌드를 포함해 26초다.

| # | 최적 | 수순 | 경로 | 의도해 | 제한 | 별3/별2 | | # | 최적 | 수순 | 경로 | 의도해 | 제한 | 별3/별2 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 3 | 6 | 6 | 3 | 7 | 3/4 | | 16 | 7 | 3 | 3 | 7 | 10 | 7/9 |
| 2 | 3 | 2 | 2 | 3 | 7 | 3/4 | | 17 | 9 | 1 | 1 | 9 | 12 | 9/11 |
| 3 | 3 | 1 | 1 | 3 | 7 | 3/4 | | 18 | 6 | 6 | 6 | 6 | 9 | 6/8 |
| 4 | 3 | 1 | 1 | 3 | 7 | 3/4 | | 19 | 6 | 360 | 20 | 6 | 9 | 6/8 |
| 5 | 4 | 12 | 12 | 4 | 8 | 4/5 | | 20 | 6 | 1 | 1 | 6 | 9 | 6/8 |
| 6 | 5 | 4 | 2 | 5 | 9 | 5/7 | | 21 | 6 | 2 | 2 | 6 | 8 | 6/8 |
| 7 | 7 | 24 | 24 | 7 | 11 | 7/9 | | 22 | 5 | 1 | 1 | 5 | 7 | 5/7 |
| 8 | 3 | 2 | 2 | 3 | 7 | 3/4 | | 23 | 6 | 1 | 1 | 6 | 8 | 6/8 |
| 9 | 6 | 60 | 4 | 6 | 10 | 6/8 | | 24 | 5 | 36 | 4 | 5 | 7 | 5/7 |
| 10 | 5 | 1 | 1 | 5 | 9 | 5/7 | | 25 | 7 | 2 | 2 | 7 | 9 | 7/9 |
| 11 | 8 | 1 | 1 | 8 | 12 | 8/10 | | 26 | 5 | 1 | 1 | 5 | 7 | 5/7 |
| 12 | 4 | 2 | 2 | 4 | 8 | 4/5 | | 27 | 6 | 2 | 2 | 6 | 8 | 6/8 |
| 13 | 5 | 12 | 12 | 5 | 8 | 5/7 | | 28 | 7 | 27 | 2 | 7 | 8 | 7/8 |
| 14 | 7 | 5 | 5 | 7 | 10 | 7/9 | | 29 | 10 | 22 | 22 | 10 | 12 | 10/12 |
| 15 | 6 | 129 | 14 | 6 | 9 | 6/8 | | 30 | 11 | 40 | 40 | 11 | 13 | 11/13 |

---

## 2. 정할 것

| # | 질문 | 제안 |
| --- | --- | --- |
| A | solver와 검증 창을 어디에 두나 | **새 폴더 `Assets/Scripts/Tools/`**, asmdef `Sheepft.Tools`(Editor 전용, `Sheepft.Core` 참조). 빌드에 들어가지 않으므로 IMPLEMENTATION_PLAN 9절의 "게임 코드에는 솔버를 넣지 않는다"는 원칙도 지킨다. CLAUDE.md 폴더 표에 추가한다. 2번 에디터를 게임 안(런타임)에 만들게 되면 그때 solver를 `Core/`로 옮긴다 |
| B | 기믹 이름(`@intent`)을 바꾸나 | **그대로 둔다.** `Ev`의 55개 이름을 게임 쪽 enum에 그대로 옮긴다. `stages.txt`는 고치지 않는다 |
| C | 상태가 많은 스테이지의 시간 | 게임 코드는 객체 위주라 `Solver~`보다 느릴 것이다. 30개 전체 검증이 **1분 안**이면 그대로 두고, 넘으면 5단계에서 최적화한다 |

---

## 3. 구조

```
Assets/Scripts/Core/                  (게임과 함께 빌드됨)
  Events/Mechanic.cs        새 파일. [Flags] enum Mechanic : ulong — Ev의 55개 이름 그대로
  Board/Board.cs            + Mechanics (이번 턴에 일어난 기믹), Mark(Mechanic)
  Board/LevelRules.cs       + Intent, OptimalIntent
  Level/LevelParser.cs      @intent/@optimal을 읽어 LevelRules에 넣음. 모르는 이름은 오류
  Entities/FruitBasket.cs   + Carried (말이 옮긴 바구니. HorseLure·CowLure 판정용, 규칙에는 영향 없음)
  Rules/*, Entities/*       기믹이 일어나는 자리마다 board.Mark(...) 한 줄

Assets/Scripts/Tools/                 (Editor 전용, 빌드에 안 들어감)
  Sheepft.Tools.asmdef
  Solver/BoardCodec.cs      Board ↔ byte[]. 탐색 상태의 키이자 보관 형식
  Solver/Solver.cs          너비 우선 탐색. Solver~/Core/Solver.cs를 옮김
  Solver/Pruner.cs          shift 포인트 정하기. Solver~/Cli/Pruner.cs를 옮김
  Solver/StageChecker.cs    check 명령의 판정 규칙 + 풀이 줄 재생
  StageValidatorWindow.cs   메뉴 Sheepft > Stage Validator

Assets/Tests/EditMode/
  MechanicTests.cs          기믹 표시가 제대로 켜지는지
  BoardCodecTests.cs        인코딩 왕복, 복원한 맵이 원본과 똑같이 진행되는지
  SolverTests.cs            Solver~/Tests/SolverTests.cs 이식
  StagesFileTests.cs        + 모든 스테이지의 풀이 줄을 실제로 두어 승리하는지 (빠름)
  StageCheckTests.cs        스테이지마다 전체 검증 (느림, Category "StageCheck")
```

### 3.1 기믹 표시 (`Mechanic`)

`@intent`는 "양이 순환했다", "기절한 무리 범위에 양이 있었지만 잡히지 않았다" 같은 **의미 단위**를 본다. 게임 이벤트에는 이런 정보가 없다. 특히 `StunSpared`, `MealSpared`, `GoatSightBlocked`, `HorseNoPath`, `RideHeld`처럼 "일어나지 않은 일"은 이벤트 목록에서 거꾸로 알아낼 수도 없다.

그래서 규칙 코드가 그 판단을 내리는 자리에서 직접 표시한다.

```csharp
// Wolf.cs, 기절 중이라 잡지 않는 자리
if (StunLeft > 0) { if (InRange(sheep)) board.Mark(Mechanic.StunSpared); return; }
```

- `Board.Mechanics`는 턴을 시작할 때 비운다(`Events`와 같다). 비트 OR 한 번이라 게임에 부담이 없고, 화면은 읽지 않는다.
- 표시할 자리는 `Solver~/Core/Simulator.cs`에서 `Ev.`가 나오는 58곳을 하나씩 대응시킨다. 대응표를 PR 설명에 남긴다.
- 55종 모두 옮긴다. 테스트는 `stages.txt`가 쓰는 33종을 먼저 하나씩 넣는다.

### 3.2 탐색 상태 (`BoardCodec`)

탐색은 상태를 수백만 개 보관한다. `Board.Clone()`을 그대로 쌓으면 메모리가 모자라므로 맵을 바이트 배열로 줄인다.

- 담는 것: 칸마다 지형 종류, 오브젝트마다 종류·Id·위치와 모든 상태 값(젖음, 화상, 물 참, 무리 수, 기절·식사 남은 기간, 목표 바구니, 탄 것, 배부름, 마지막 걸은 방향).
- 담지 않는 것: 빈칸 배치와 규칙(레벨마다 같음), 턴 수(탐색 깊이로 앎), `AppliedThisTurn`(턴 시작마다 비움), 이벤트.
- 같은 바이트면 같은 상태다. 이 배열을 그대로 사전(Dictionary)의 키로 쓴다.
- `Solver~`는 "같은 칸의 같은 상태 오브젝트는 서로 바꿔도 같은 상태"로 보았다. 게임 코드에서는 같은 칸 안의 행동 순서를 Id가 정하므로, **처음에는 Id까지 키에 넣는다.** 안전한 대신 상태가 더 많아진다. 시간이 문제될 때만 다시 본다.
  - 이 차이 때문에 결과 중 "경로"(상태 경로 수)와 탐색 상태 수는 기준값보다 커질 수 있다. **최적 턴, 수순 수, 의도해, 우회해, 판정은 기준값과 같아야 한다.**

### 3.3 Solver와 검증기

`Solver~`의 알고리즘을 그대로 옮기고, 상태 한 수 진행만 `TurnRunner.PlayTurn`으로 바꾼다.

- 너비 우선 탐색. 대기 수(아무것도 안 움직이는 shift)도 수로 센다.
- 결과: 최적 턴, 최적 수순 수와 예시, 의도해, 우회해(`@intent` 기믹마다 그 기믹이 일어나는 수를 금지하고 다시 탐색), `@optimal` 확인, 탐색 상태 수, 한도 초과 여부.
- 패배한 상태와 승리 불가 상태(`Board.CannotWin`, 풀이 모두 탐)는 더 펼치지 않는다.
- `StageChecker`는 `check` 명령의 판정을 그대로 옮긴다. 해 없음, 탐색 한도, `@intent` 없음, 의도해 ≠ 최적, 우회해 있음, `@optimal` 위반, 별 기준(별3 = 최적, 별3 < 별2 ≤ 제한), 맵 크기, 최적 20턴 초과, 풀이 줄이 실제로 승리하는지.
- 취소 토큰을 받아 탐색 중간에 멈출 수 있게 한다. Core에는 바뀌는 static 상태가 없으므로 백그라운드 스레드에서 돌려도 된다.

### 3.4 검증 창 (메뉴 **Sheepft > Stage Validator**)

```
┌ Stage Validator ─────────────────────────────────────────────┐
│ [Validate All] [Cancel]   상태 한도 [4000000]   12/30 진행 중… │
├──────────────────────────────────────────────────────────────┤
│ #  최적 수순 경로 의도해 우회해 제한 별3/별2 판정  제목    [Solve] [Prune] │
│ 1  3    6    6    3      -      7    3/4    OK   First Steps  │
│ 7  …                                   확인  ← 우회해 6턴 …   │  ← 빨간 줄
├ 7. Water for Burns ──────────────────────────────────────────┤
│ 맵 / 최적 수순 10개와 각 수순의 기믹 / 우회해 수순 / 시간       │
│ Prune 결과: shift: R0:RL, C1:UD …            [Copy]           │
└──────────────────────────────────────────────────────────────┘
```

- **Validate All**: 디스크의 `stages.txt`를 그 자리에서 읽어 30개를 차례로 검증한다. 파일을 고친 뒤 Unity가 다시 import하기를 기다리지 않아도 된다.
- 백그라운드에서 돌고, 스테이지 하나가 끝날 때마다 표에 줄이 채워진다. 에디터는 멈추지 않는다.
- 줄을 누르면 아래에 상세(`solve` 명령의 출력)가 나온다. **Solve**는 한 스테이지만 다시 검증하고, **Prune**은 shift 포인트 조합을 찾아 헤더에 붙일 표기를 보여 준다(복사 버튼).
- 파싱 오류가 나면 표 대신 줄 번호와 메시지를 보여 준다.
- 결과는 창을 닫으면 사라진다. 파일로 저장하지 않는다.

### 3.5 Claude가 검증할 때

CLAUDE.md 규칙상 Claude는 MCP로 EditMode 테스트까지만 돌린다. 그래서 같은 검증을 테스트로도 둔다.

- `StagesFileTests`에 풀이 줄 재생 테스트를 더한다. 빠르므로 항상 돈다.
- `StageCheckTests`는 스테이지마다 테스트 케이스 하나(`TestCaseSource`)로 `StageChecker`를 돌린다. 느리므로 `[Category("StageCheck")]`를 붙여 필요할 때 골라 돌린다. 실패 메시지는 검증 창의 "← …" 문구와 같다.

---

## 4. 작업 순서

각 단계는 EditMode 테스트가 통과해야 끝난다.

| 단계 | 내용 | 끝난 상태 |
| --- | --- | --- |
| 1. 기믹 표시 ✅ 2026-11-23 | `Mechanic` enum, `Board.Mark`, `LevelRules.Intent/OptimalIntent`, 파서. `Simulator.cs`의 58곳을 규칙 코드에 대응시킴. `LevelWriter`는 맵만 쓰므로 `@` 줄 쓰기는 2번 에디터 때 정한다 | `MechanicTests` 44개 통과. 기존 테스트 포함 338개 통과 |
| 2. 풀이 재생 테스트 ✅ | `StagesFileTests`에 30개 풀이 줄을 실제로 두어 보는 테스트 | 30개 모두 게임 규칙으로 승리, 길이 = 별3 |
| 3. 상태 인코딩 ✅ | `Sheepft.Tools` asmdef, `BoardCodec` | `BoardCodecTests` 4개 통과 |
| 4. Solver, 검증기 ✅ | `Solver`, `StageChecker`, `Pruner` 이식. `SolverTests` 이식 | `SolverTests` 12개 통과. 전체 355개 통과 |
| 5. 기준값 대조 ✅ | 새 검증기로 30개를 돌려 1절의 표와 비교. 시간을 잰다 | 30개 모두 최적·수순·**경로까지** 기준값과 같음, 우회해 없음, 전부 OK. 시간은 아래 |
| 6. 검증 창 ✅ (사람 확인 남음) | `StageValidatorWindow` | 컴파일·메뉴 등록 확인. 사람이 버튼을 눌러 보는 항목은 PLAYTEST.md "스테이지 검증 창" |
| 7. 스테이지 테스트 ✅ | `StageCheckTests` (`[Explicit]`, 카테고리 `StageCheck`) | MCP로 실행해 30개 통과 |
| 8. 테스트 점검 ✅ | `Solver~/Tests`의 규칙 테스트 81개(`SolverTests` 10개는 4단계에서 옮김)를 게임 테스트와 하나씩 대조 | 68개는 게임 테스트에 같은 경우가 있었다. 없던 13개를 `LegacyRuleTests`로 옮김 |
| 9. 정리 ✅ | `Solver~` 삭제. 문서 고치기 | CLAUDE.md, IMPLEMENTATION_PLAN.md, README.md, StageDesign.md, stages.txt 머리 주석 |

### 5단계 결과: 속도 (2026-11-23)

처음 이식했을 때는 한 턴이 약 200µs라 30개에 10분이 넘었다. 결과를 바꾸지 않는 선에서 아래를 고쳐 **30개 전체 약 2분**(1–29번 합쳐 약 20초, 30번 혼자 약 110초)이 되었다. 고칠 때마다 30개 결과가 이전과 같은지 대조했다.

| 고친 곳 | 내용 |
| --- | --- |
| `Board.Tiles` / `Board.Objects` (Core) | `yield` 열거 → 배열과 구조체 열거자. foreach가 메모리를 새로 잡지 않는다. 쓰는 쪽 코드(foreach, LINQ)는 그대로 |
| `Settler` (Core) | 칸마다 `ToArray` 하던 것을 작업용 목록 하나로. 겹침 상호작용은 늑대·말·소가 있는 칸만, 자동 반응은 늑대·말·소만 모아 한 번에 |
| `TurnRunner` (Core) | 동물이 아무것도 안 했거나(이벤트 없음) 확산이 없었으면 정리 절차를 다시 돌리지 않는다. 바로 앞의 정리 절차가 끝까지 돌았으므로 할 일이 없다 |
| `Wolf.TryAutoReact` (Core) | 범위에 양이 있을 때만 목록을 만든다 |
| `Solver` (Tools) | 아무것도 안 움직이는 수(빈 줄, 다 막힌 줄)는 부모마다 한 번만 두고 결과를 나눠 쓴다 |

- 병렬화(탐색을 여러 스레드로 나누기)도 해 봤지만 오히려 느려져서(30번 108초 → 146초) 뺐다.
- 30번이 느린 까닭: `@intent CowPushSheep`을 금지한 탐색이 제한 턴(13턴)까지 상태 90만 개를 모두 훑는다. 맵을 새로 만들 때 상태 수가 이 정도로 크면 오래 걸린다.
- 더 빠르게 해야 하면: 키에서 Id를 빼고 같은 오브젝트를 하나로 보기(3.2절). 말이 태울 것을 Id 순서로 고르는 등 Id가 결과를 바꾸는 경우가 있어 지금은 하지 않았다.

### 9단계에서 고칠 문서

- **CLAUDE.md**: 폴더 표의 `Solver~` 줄을 `Scripts/Tools/`로 바꿈. "`check` 명령으로 검증", "`prune` 명령" 문구를 검증 창과 `StageCheckTests`로 바꿈. `Solver~` 예외 설명(`~` 폴더) 삭제
- **IMPLEMENTATION_PLAN.md**: 6절 폴더 표에 `Tools/` 추가, 9절의 "규칙 구현이 둘" 문단 삭제
- **StageDesign.md**: `Solver~` 언급과 사용법을 검증 창으로 바꿈
- **stages.txt** 머리 주석, **LevelParser** 주석("solver 전용. 읽고 버린다")

---

## 5. 하지 않는 것

- 게임 규칙 자체는 바꾸지 않는다. 5단계에서 두 구현이 다르면, README와 다른 쪽을 고친다.
- `stages.txt`의 표기와 `@intent` 이름은 바꾸지 않는다.
- 명령줄 실행기는 만들지 않는다. 에디터가 닫혀 있을 때는 Unity 배치 모드(`-runTests -testCategory StageCheck`)로 같은 테스트를 돌린다.
