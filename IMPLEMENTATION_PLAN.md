# Sheepft 구현 계획

게임 규칙은 [README.md](README.md)를 따른다. 이 문서는 그 규칙을 코드로 옮기는 구조와 순서를 정한다. README와 다르면 README가 맞고, 이 문서를 고친다.

- 작성: 2026-11-13 (같은 날 README 1·3·6·10절 수정 반영: 빈칸 두 종류, shift 포인트, 염소 비순환, 늑대 합류·기절)
- 2026-11-14: 데스크톱에서 만든 30개 스테이지(`Assets/Levels/stages.txt`)와 맞췄다. 레벨 파서에 `@` 줄을, `LevelRules`에 설명과 별 기준을 넣고, 9단계를 "스테이지 읽어 들이기"로 바꿨다.
- 마감: GitHub Game Off 2026 (11월 말). 남은 기간이 2주 남짓이므로 **일찍 플레이 가능한 상태를 만들고 오브젝트를 하나씩 더하는** 순서로 간다.

---

## 1. 제안된 구조 검토

### 제안

- `TileEntity` 부모 아래 `ObjectEntity`, `TerrainEntity`
- `TileEntity`의 virtual 3개: `AfterShift`(종료 후 반응), `WhenOverlapedWith(TileEntity)`(겹침 상호작용), `CheckEverytime()`(자동 반응)
- `Tile`(순수 C#)이 그 칸의 `TileEntity`를 모두 저장
- 맵은 `Tile`의 2차원 배열

### 그대로 가져갈 점

- **공통 부모 + 반응 3종**: README가 지형을 "칸마다 하나씩 고정된 오브젝트"로 보고 모든 규칙을 종료 후 반응 / 겹침 상호작용 / 자동 반응으로 나눈다. 클래스 구조가 문서 구조와 1:1로 맞으므로 규칙을 찾고 고치기 쉽다.
- **`Tile`을 순수 C#으로, 맵을 2차원 배열로**: undo를 위해 상태를 통째로 복제해야 하고, 규칙을 Unity 없이 테스트할 수 있어야 한다. 맞는 방향이다.

### 고쳐야 할 점

| # | 문제 | 고친 방향 |
| --- | --- | --- |
| 1 | **순서를 엔티티가 정할 수 없다.** 종료 후 반응은 종류별 차례(염소→말→소→늑대→들불), 그 안에서는 차례 시작 때 위치의 행 우선 순서이고, 한 마리가 끝날 때마다 정리 절차를 돈다. 자동 반응도 종류 순서가 있다. | 엔티티의 virtual은 **"무엇을 하는가"** 만 맡는다. **"언제, 어떤 순서로"** 는 `TurnRunner`(턴 진행)와 `Settler`(정리 절차)가 맡는다. |
| 2 | **`WhenOverlapedWith(TileEntity other)`는 둘씩만 본다.** 늑대 합류는 여러 무리를 한 번에 합치고, 들불 칸에서는 물 찬 양동이를 먼저 처리해야 하며, A→B와 B→A가 둘 다 불려 같은 일이 두 번 일어날 수 있다. | `OnOverlap(Board, Tile)`로 바꿔 **칸 전체**를 받는다. 그리고 README 6절 표의 "겹침 상호작용" 칸에 적힌 쪽(지형, 늑대, 소)만 처리한다. 상대 쪽은 아무것도 하지 않으므로 중복이 없다. |
| 3 | **들불 확산은 칸 하나의 행동이 아니다.** "확산이 시작될 때 있던 들불만" 한꺼번에 번지고, 정리 절차도 확산 전체가 끝난 뒤 한 번 돈다. 칸마다 `AfterShift`를 부르면 사이사이 정리가 끼어 결과가 달라진다. | 확산은 `TurnRunner`의 마지막 단계로 한꺼번에 처리한다(`FireSpread`). |
| 4 | **지형과 오브젝트를 한 목록에 넣으면** 지형 찾기, shift 이동, 지형 교체(풀→들불→재)마다 걸러내야 한다. 지형은 칸마다 정확히 하나이고 움직이지 않는다. | `Tile { Kind; TerrainEntity Terrain; List<ObjectEntity> Objects; }`로 나눈다. 공통 부모는 그대로 둔다. |
| 5 | **엔티티가 MonoBehaviour이면** undo 복제와 테스트가 어렵다. | 엔티티까지 모두 순수 C#. 화면은 별도 View가 엔티티 `Id`로 따라간다(5절). |
| 6 | **반응이 일어났는지 알 수 없다.** 정리 절차는 "바뀌는 것이 없을 때까지" 반복한다. | 겹침·자동 반응 함수는 상태를 바꿨으면 `true`를 돌려준다. |
| 7 | **이름** `CheckEverytime`은 "매 프레임 검사"처럼 읽힌다. 말에게는 자동 반응이 둘(먹기, 발견)이고 종류 순서가 있다. | `TryAutoReact(Board, AutoReaction kind)`. `kind`는 늑대 잡기 / 말 먹기 / 말 발견 / 소 냄새. 오타 `Overlaped`도 정리한다. |

### 저장하지 않고 계산하는 것

README가 "따로 기록하지 않음"으로 정한 것은 필드로 두지 않는다. 저장하면 실제 위치와 어긋날 수 있다.

- **탑승**: 같은 칸에 발견 상태(목표 있음, 배부르지 않음)인 말이 있는, 말이 아닌 동물
- **행동 불가**: 화상, 기절, 식사, 탑승 중 하나
- **늑대의 잡는 범위**: 항상 마리 수 − 1. 기절·식사·화상 중에는 잡지 않을 뿐 범위 값을 따로 두지 않는다.
- **양 무리**: 같은 칸의 양은 shift·밀치기·운반·소의 밀기에서 항상 함께 움직이고 양은 걷지 않으므로, 한 번 겹치면 절대 갈라지지 않는다. 그래서 **양은 한 마리 = 엔티티 하나**로 두고 합치지 않는다. 같은 칸의 양이 곧 무리다. 마리마다 젖음·화상이 다를 수 있다는 규칙도 이렇게 해야 표현된다.
- 늑대는 혼자 걸어 나가므로 반대로 **무리 = 엔티티 하나 + 마리 수**로 둔다.

---

## 2. 클래스 구조

```csharp
// ── 위치와 방향 ──
public readonly struct Pos { public readonly int Row, Col; }   // (행, 열), (0,0)이 왼쪽 위
public enum Dir { Up, Right, Down, Left }                     // 동점 처리 순서(상>우>하>좌)와 같음
public enum Axis { Row, Col }

// ── 칸 ──
public enum TileKind { Normal, OuterBlank, InnerBlank }       // 일반 칸 / `-` 외곽 빈칸 / `x` 중간 빈칸

public sealed class Tile {
    public Pos Pos;
    public TileKind Kind;
    public bool IsBlank => Kind != TileKind.Normal;
    public TerrainEntity Terrain;              // 빈칸이면 null
    public List<ObjectEntity> Objects;         // 빈칸이면 항상 비어 있음. Id 순 정렬
}

// ── shift 포인트 ──
// README 3절: 포인트 하나 = 행이나 열 하나 + 방향 하나. `R1:RL`은 포인트 2개로 읽는다.
// 플레이어의 수와 풀이 수순도 같은 타입을 쓴다.
public readonly struct ShiftPoint {
    public readonly Axis Axis;                 // Row면 Dir은 Right/Left, Col이면 Up/Down만
    public readonly int Index;
    public readonly Dir Dir;
}

public sealed class LevelRules {
    public IReadOnlyList<ShiftPoint> ShiftPoints;
    public int TurnLimit;
    public int TargetSheep;                    // 생략하면 레벨의 양 수
    public IReadOnlyList<ShiftPoint> Solution; // 선택. 테스트용
    public string Description;                 // `@desc`. 플레이어용 설명
    public int Star3Turns, Star2Turns;         // `@stars`. 별 3개, 별 2개 기준 턴. 별 1개는 제한 턴 이내
}

// ── 맵 ──
public sealed class Board {
    Tile[,] tiles;                             // null 없음. 빈칸도 Tile
    public Tile At(Pos p);                     // 격자 바깥이면 null
    public bool IsBlank(Pos p);                // 빈칸이거나 격자 바깥이면 true
    public bool BlocksShift(Tile t);           // shift용: 지형이 장애물 (빈칸은 줄에 들어오지 않음)
    public bool BlocksAnimal(Pos p);           // 동물용: 빈칸, 격자 바깥, 장애물 지형
    public bool IsWalkable(Pos p);             // 걷기용: !BlocksAnimal && 물·들불이 아님
    public IReadOnlyList<Tile> Line(Axis axis, int index); // 그 줄의 실제 칸 목록 (3절)
    public void Move(ObjectEntity o, Pos to, MoveCause cause);
    public void Remove(ObjectEntity o, RemoveCause cause);
    public void SetTerrain(Pos p, TerrainType type);
    public LevelRules Rules;
    public int TurnsUsed;
    public GameResult Result;                  // 진행 중 / 승리 / 패배
    public List<GameEvent> Events;             // 화면 재생용 기록 (5절)
    public Board Clone();                      // undo용 깊은 복사. Id는 그대로 유지
}

// ── 엔티티 ──
public abstract class TileEntity {
    public int Id;                                          // 레벨 파일을 읽은 순서. 같은 칸·같은 종류의 처리 순서로도 쓴다
    public virtual void AfterShift(Board b) { }             // 종료 후 반응
    public virtual bool OnOverlap(Board b, Tile t) => false; // 겹침 상호작용 (그 칸 전체)
    public virtual bool TryAutoReact(Board b, AutoReaction k) => false; // 자동 반응
}

public abstract class TerrainEntity : TileEntity {
    public abstract TerrainType Type { get; }
    public virtual bool IsObstacle => false;
    public virtual bool IsWooden => false;
    // OnOverlap 기본 구현: 물 찬 양동이 → 나머지 순으로, 이번 턴에 이 지형 종류를
    // 아직 적용받지 않은 오브젝트에만 ApplyTo를 부르고 기록한다.
    public virtual void ApplyTo(Board b, Tile t, ObjectEntity o) { }
}
// Ground, Stone, Fence, Tree, Water, Fire, Grass, Ash
// 지형이 바뀌면 인스턴스를 새로 만들어 교체한다. 빈칸은 지형이 아니다.

public abstract class ObjectEntity : TileEntity {
    public Pos Pos;
    public bool Wet;
    public bool Removed;                                    // 먹힘, 탐, 합류로 사라짐
    public HashSet<TerrainType> AppliedThisTurn;            // 지형 상호작용 턴당 1회 기록
    public virtual bool IsAnimal => false;
    public virtual bool IsWooden => false;
}
public abstract class Animal : ObjectEntity {
    public bool Burned;
    public Dir? LastWalkDir;                                // 늑대·말·소만 사용
    public bool IsRiding(Board b);
    public bool IsIncapacitated(Board b);
}
// Sheep
// Wolf(Count, StunLeft, MealLeft)   잡는 범위 = Count − 1, 잡는 조건 = !Burned && StunLeft == 0 && MealLeft == 0
// Goat, Horse(Target, Full), Cow(Target, Full)
// FruitBasket, Bucket(Filled)
```

### 빈칸과 장애물 판정

README 1절: 빈칸과 장애물 지형은 **플레이어의 shift에서만** 다르다. 판정을 둘로 나누고, 규칙마다 어느 쪽을 쓰는지 고정한다.

| 판정 | 빈칸·격자 바깥 | 장애물 지형 | 쓰는 규칙 |
| --- | --- | --- | --- |
| shift용 (`Line` + `BlocksShift`) | 줄에서 빠짐 (뛰어넘음) | 막음 | 플레이어의 shift |
| 동물용 (`BlocksAnimal`) | 막음 | 막음 | 걷기(`IsWalkable`), 염소 대상 찾기·밀치기, 말의 시야, 소의 밀기, 말 먹기의 "붙은 칸" |
| 범위 (장애물 무시) | 칸이 없으므로 대상 없음 | 무시 | 늑대 잡기 범위, 소의 냄새, 들불 확산 8칸 |

- 빈칸에는 지형도 오브젝트도 없다. 레벨 파서가 빈칸에 오브젝트가 적혀 있으면 오류로 거부한다.
- `-`와 `x`는 규칙상 같고 화면에서만 다르다(`TileKind`를 나눈 이유).

### 반응을 누가 맡나

| 반응 | 맡는 곳 |
| --- | --- |
| 지형 → 오브젝트 (물, 들불, 양동이) | `TerrainEntity.OnOverlap` / `ApplyTo` |
| 늑대 합류 | `Wolf.OnOverlap`. 그 칸에서 Id가 가장 작은 화상 아닌 늑대가 나머지를 흡수 |
| 소의 바구니 먹기 | `Cow.OnOverlap` |
| 늑대 잡기, 말 먹기·발견, 소 냄새 | 각자의 `TryAutoReact` |
| 염소 공격, 말·소·늑대 걷기 | 각자의 `AfterShift` |
| 들불 확산 | `FireSpread` (턴 단계) |
| 탑승, 행동 불가 | 계산 (`Animal.IsRiding`, `IsIncapacitated`) |

### 늑대 합류 (README 6절)

흡수하는 무리 하나에 같은 칸의 화상 아닌 늑대를 모두 합친다.

| 값 | 합친 결과 |
| --- | --- |
| 마리 수 | 합 |
| 기절 | 새로 걸림 (남은 기간을 처음부터) |
| 젖음 | 하나라도 젖었으면 젖음 |
| 식사 | 하나라도 식사 중이면 유지. 남은 기간은 가장 긴 것 |
| 마지막 이동 방향 | 초기화 |
| 이번 턴 지형 적용 기록 | 합집합 (9절 확인 항목) |

### 공용 규칙 모듈

| 모듈 | 내용 |
| --- | --- |
| `LineShifter` | 플레이어의 shift. 3절 알고리즘 |
| `TilePusher` | 칸 하나의 오브젝트를 한 방향으로 1칸 민다. 순환하지 않고, 다음 칸이 `BlocksAnimal`이면 제자리. 염소 밀치기와 소의 밀기가 같이 쓴다 |
| `Pathfinder` | 걷기. 목표 칸들에서 시작하는 BFS 거리표 → 거리가 가장 줄어드는 이웃, 동점이면 마지막 걸은 방향, 그다음 상>우>하>좌. `IsWalkable`인 칸만 지나가고 순환하지 않는다. 늑대는 갈 수 있는 모든 양 칸을 목표로 한 번에 계산한다 |
| `Sight` | 말의 시야, 염소의 대상 찾기. `BlocksAnimal`인 칸에서 멈추고 순환하지 않는다 |

---

## 3. shift 줄과 순환

- `Board.Line(axis, index)`는 그 행(왼→오)이나 열(위→아래)에서 **빈칸을 뺀 실제 칸**을 순서대로 담은 목록이다. 빈칸은 레벨 중에 생기거나 사라지지 않으므로 레벨을 읽을 때 한 번 만들어 둔다. 장애물 지형 칸은 실제 칸이므로 목록에 들어간다.
- shift 알고리즘 (줄 길이 n, 오른쪽·아래는 +1, 왼쪽·위는 −1):
  1. 줄의 각 칸 i에 대해 도착 칸 = `line[(i ± 1 + n) % n]`.
  2. 도착 칸이 `BlocksShift`면 그 칸의 오브젝트는 제자리, 아니면 도착 칸으로 간다.
  3. 모든 도착지를 먼저 계산한 뒤 한꺼번에 옮긴다(앞 칸이 비었는지와 상관없음).
  4. n = 1이면 도착 칸이 자기 자신이므로 아무것도 움직이지 않는다.
- 예 (README 3절): 행 `- .S . x x . -`의 실제 칸은 1·2·5열 세 칸이다. 오른쪽 shift를 반복하면 양이 1 → 2 → 5 → 1열로 간다.
- `PlayTurn`은 받은 수가 `Rules.ShiftPoints`에 있는지 먼저 확인하고, 없으면 거부한다(턴 소모 없음).

---

## 4. 턴 진행

```
PlayTurn(move: ShiftPoint):
    move가 ShiftPoints에 없으면 거부
    snapshot을 undo 스택에 넣는다
    모든 오브젝트의 AppliedThisTurn을 비운다
    LineShifter.Shift(move)
    Settle()                         ; 패배면 끝
    승리 판정                          ; 승리면 끝
    for 종류 in [염소, 말, 소, 늑대]:
        actors = 그 종류 전부를 지금 위치 기준 행 우선(같은 칸이면 Id) 정렬
        for a in actors:
            if a.Removed or a.IsIncapacitated: continue
            a.AfterShift(board)
            Settle()                 ; 패배면 끝
    FireSpread()
    Settle()                         ; 패배면 끝
    승리 판정                          ; 승리면 끝
    기절·식사 남은 기간 -1, 0이 된 것을 모두 해제
    해제가 있었으면 Settle()           ; 패배면 끝 (기절·식사가 끝난 늑대가 여기서 잡음)
    TurnsUsed == 제한 턴이면 패배
    남은 풀이 0이면 "승리 불가" 표시

Settle():
    loop (안전장치: 1000회 넘으면 오류 로그):
        for 칸 in 행 우선 (빈칸 제외):
            칸.Terrain.OnOverlap(칸)            ; 지형 먼저
            for o in 칸.Objects: o.OnOverlap(칸) ; 그다음 오브젝트끼리
        changed = 위에서 바뀐 것이 있었나
        for k in [늑대 잡기, 말 먹기, 말 발견, 소 냄새]:
            그 종류를 지금 위치 기준 행 우선으로 정렬해 TryAutoReact(k)
            (Removed는 건너뜀, 패배가 되면 바로 끝)
        if 자동 반응에서 바뀐 것이 없고 changed도 없으면 끝
```

- 패배(남은 양 < 목표)는 양이 사라지는 순간 `Board.Result`에 기록하고, `Settle`과 `PlayTurn`이 매 단계 확인해 바로 빠져나온다.
- 겹침 단계는 칸 순서대로 한 번 훑어도 칸 안의 순서(지형 → 물 찬 양동이 우선 → 오브젝트끼리)가 정해져 있으므로 충분하다. 바뀐 것이 있으면 바깥 루프가 다시 돈다.

---

## 5. 로직과 화면 분리

- 로직(`Sheepft.Core`)은 한 턴을 **즉시** 끝까지 계산하고, 일어난 일을 `Board.Events`에 순서대로 남긴다.
  - `Moved(id, from, to, cause)` cause: Shift / Push / Walk / Carry / CowPush / HorseEat. Shift는 빈칸을 뛰어넘거나 순환해 멀리 갈 수 있으므로, 화면은 `from`과 `to`가 이웃이 아니면 "줄 끝으로 빠져나가 반대편에서 나오는" 식으로 연출한다.
  - `Removed(id, cause)` cause: Eaten / Burned / Merged / Destroyed
  - `TerrainChanged(pos, from, to)`, `StatusChanged(id, status, on)`, `CountChanged(id, n)`, `TargetChanged(id, targetId)`
  - `StepEnd` — shift, 한 마리의 행동, 확산, 정리 한 바퀴마다. 화면이 여기서 잠깐 쉬며 연쇄 반응을 읽을 수 있게 한다.
- 화면(`Sheepft.Game`)은 이벤트를 차례로 재생한다. 재생 중 입력은 막고, 클릭하면 빨리 감는다.
- **undo**: 턴마다 `Board.Clone()`을 스택에 쌓는다. 맵이 작아 통째 복사가 가장 단순하고 안전하다. undo·재시작 때 화면은 애니메이션 없이 스냅샷으로 다시 그린다. (게임 빌드에는 솔버를 넣지 않는다. 스테이지 검증용 solver는 에디터 전용 `Assets/Scripts/Tools/`에 있다.)

### 칸과 화살표 그리기

- 일반 칸은 지형 스프라이트, `x`는 구멍 모습, `-`는 아무것도 그리지 않는다(배경).
- **shift 화살표**: shift 포인트 하나마다 화살표 하나. **미는 쪽의 격자 가장자리 바깥**에 진행 방향을 가리키게 둔다. 빈칸과 상관없이 격자 끝에 두므로 화살표가 한 줄로 가지런히 선다.

| 포인트 | 화살표 위치 | 가리키는 방향 |
| --- | --- | --- |
| `Rr:R` | (r, −1) — 왼쪽 가장자리 바깥 | 오른쪽 |
| `Rr:L` | (r, 너비) — 오른쪽 가장자리 바깥 | 왼쪽 |
| `Cc:D` | (−1, c) — 위쪽 가장자리 바깥 | 아래 |
| `Cc:U` | (높이, c) — 아래쪽 가장자리 바깥 | 위 |

- 화살표는 **마우스를 올린 끝에만** 보인다. 한꺼번에 떠 있으면 화면이 어지럽기 때문이다. 재생 중이나 승패가 난 뒤에는 뜨지 않는다.
- 모든 줄의 양 끝 가운데 **shift 포인트가 없는 끝에는 X자**(`blocked` 태그)를 늘 보인다. X자는 돌리지 않고 입력도 받지 않는다. 한 방향만 허용된 줄은 한쪽 끝이 화살표 자리, 반대쪽 끝이 X자다.
- `shift_arrow.aseprite`(SPRITES.md)는 오른쪽만 그리고 회전해서 쓴다. 보일 때는 `hover` 프레임을 쓴다.

---

## 6. 폴더와 어셈블리

| 위치 | asmdef | 내용 |
| --- | --- | --- |
| `Assets/Scripts/Core/` | `Sheepft.Core` (`noEngineReferences: true`) | 맵, 엔티티, 규칙, 레벨 파서. UnityEngine을 쓰지 않는다 |
| `Assets/Scripts/Game/` | `Sheepft.Game` | View, 입력, HUD, 씬 진행 |
| `Assets/Scripts/Tools/` | `Sheepft.Tools` (Editor 전용, `Core`·`Game` 참조) | 스테이지 solver·검증기·prune, 검증 창(Sheepft > Stage Validator), 스테이지 에디터(Sheepft > Stage Editor). 규칙은 `Core`를 그대로 쓴다. [STAGE_TOOLS_PLAN.md](STAGE_TOOLS_PLAN.md) |
| `Assets/Tests/EditMode/` | `Sheepft.Tests` (Editor 전용) | Core·Tools 단위 테스트 |
| `Assets/Levels/` | | 레벨 텍스트 파일 (`TextAsset`) |

`Core` 안은 `Board/`, `Entities/`, `Rules/`, `Level/`, `Events/`로 나눈다.

---

## 7. 레벨 파일과 테스트

### 레벨 파서 (`LevelParser`)

README 10절 표기법을 읽는다.

- **헤더**: `# 이름  shift: R1:RL, C2:U  제한 4턴  목표 양 2`
  - shift 포인트는 `([RC])(\d+):([RLUD]+)`. 방향 글자마다 포인트 하나를 만든다(`R1:RL` → `R1:R`, `R1:L`).
  - 오류로 거부: 행에 `U`/`D`나 열에 `R`/`L`, 같은 포인트 중복, 번호가 격자 밖, 실제 칸이 하나도 없는 줄.
  - `목표 양`이 없으면 레벨의 양 수. 양 수보다 크면 오류.
- **풀이 줄**(선택): 헤더 다음 줄 `# 풀이: R1:R, C2:U`. 수마다 방향이 정확히 하나여야 하고, `ShiftPoints`에 있는 포인트여야 한다.
- **`@` 줄**(선택): 풀이 줄 다음에 온다. `Assets/Levels/stages.txt`가 쓰는 표시다.
  - `@desc 문장`: 플레이어용 설명 → `LevelRules.Description`. 여러 줄이면 이어 붙인다.
  - `@stars 3 4`: 별 3개, 별 2개 기준 턴 → `Star3Turns`, `Star2Turns`.
  - `@intent`, `@optimal`: 스테이지 검증용 기믹 이름(`Mechanic`) → `Intent`, `OptimalIntent`. 게임 진행에는 쓰지 않는다. 모르는 이름은 오류.
  - 그 밖의 `@` 줄은 오류.
- `//`로 시작하는 줄과 빈 줄은 건너뛴다. 한 파일에 레벨을 여러 개 이어 적고, 헤더(`#`으로 시작하고 `shift`가 있는 줄)가 레벨을 나눈다. 돌(`#`)로 시작하는 맵 줄과는 `shift`와 `풀이`라는 말로 구분한다.
- **격자**: 공백으로 칸을 나눈다. 모든 행의 칸 수가 같아야 한다.
  - 칸 = 지형 한 글자 + 오브젝트들. 오브젝트 = 대문자 + 선택 숫자 + 선택 `(w)`/`(b)`.
  - `-`, `x`는 기호 하나로만 쓴다. 뒤에 오브젝트가 붙으면 오류.
  - 한 칸에 상태가 다른 양이 섞이면 `S2S(w)`(마른 양 2, 젖은 양 1)처럼 적는다. 기존 규칙("같은 오브젝트 여럿은 숫자", "상태는 괄호")을 이어 붙인 것이다.
- `LevelWriter`는 반대로 맵을 같은 표기로 쓴다. 테스트는 **"레벨 문자열 + 수 → 기대하는 맵 문자열"** 로 적는다.

```csharp
AssertAfter(
    level:    "- .S . x x . -",
    moves:    "R0:R, R0:R",
    expected: "- .  . x x .S -");
```

- 기절·식사 남은 기간 등 표기법에 없는 값은 테스트에서 속성으로 직접 확인한다.
- `Assets/Levels/`의 모든 레벨에 대해, 풀이 줄이 있으면 그 수순으로 클리어되는지 확인하는 테스트를 하나 둔다.

### 꼭 넣을 테스트

| 영역 | 테스트 |
| --- | --- |
| shift 빈칸 | 행 `- .S . x x . -`를 R로 3번: 1열 → 2열 → 5열 → 1열 |
| shift 빈칸 (열) | ㄷ자 맵에서 중간에 `-`가 낀 열을 D로 밀면 `-`를 뛰어넘어 아래 실제 칸으로 감. U로 밀면 반대 |
| shift 막힘 | 빈칸 너머 칸이 장애물이면 제자리. 예: `.S x # .` R → 양 그대로. 순환 도착 칸이 장애물일 때도 제자리 |
| shift 기타 | 실제 칸이 하나뿐인 줄은 아무것도 안 움직임. 허용되지 않은 방향의 수는 거부되고 턴이 줄지 않음 |
| 염소 비순환 | 2칸 거리의 오브젝트가 격자 끝에 있으면 밀리지 않고 제자리(반대편으로 나오지 않음). 밀릴 칸이 `-`나 `x`여도 제자리. 1칸 거리가 빈칸이면 2칸 거리는 공격하지 않음 |
| 늑대 합류 | 젖은 늑대 + 마른 늑대 → 젖은 무리. 식사 중(남은 1) + 식사 중(남은 2) → 식사 남은 2. 식사 중 + 식사 안 함 → 식사 유지. 합류 직후 기절하고 기절 중에는 범위 안 양을 잡지 않음. 기절이 풀리는 턴 끝 정리에서 새 범위(n − 1)로 잡음 |
| 파싱 | `R1:RL` → 포인트 2개, `C2:U` → 1개. `R1:U`·중복·범위 밖·빈 줄은 오류. 풀이 줄의 `R1:RL`(방향 둘)이나 목록에 없는 포인트는 오류. `-`/`x` 칸 종류 구분, `x`에 오브젝트가 붙으면 오류 |
| 왕복 | 읽고 다시 쓰면 같은 문자열 (`-`, `x`, `S2S(w)` 포함) |

README의 예시 문장(예: "양 3마리가 겹친 칸을 2마리 무리가 잡으면 1마리가 남는다")도 하나씩 테스트로 옮긴다.

---

## 8. 작업 순서

각 단계는 테스트가 통과해야 끝난다. 3단계에서 처음으로 플레이할 수 있고, 이후로는 오브젝트를 하나 더할 때마다 화면과 스프라이트까지 붙인다.

| 단계 | 내용 | 끝난 상태 |
| --- | --- | --- |
| 1. 뼈대 | 폴더·asmdef, `Pos`/`Dir`/`Tile`(빈칸 종류)/`Board`, `ShiftPoint`/`LevelRules`, 지형 8종, 양, `LevelParser`/`LevelWriter` | 파싱·왕복 테스트 통과 |
| 2. shift와 지형 | `Line`, `LineShifter`, `Settler`, 물·들불 상호작용, 턴당 1회 기록, 양동이, 승리·패배·승리 불가, `TurnRunner` 골격 | shift 테스트 통과. 양·양동이만 있는 레벨을 로직으로 끝까지 풀 수 있음 |
| 3. 화면 | `BoardView`(칸마다 SpriteRenderer, `-`/`x` 구분), `EntityView`, shift 포인트 화살표 입력, 이벤트 재생(순환 연출 포함), undo·재시작, 남은 턴·양 수 HUD | 2단계 레벨을 Unity에서 플레이 |
| 4. 늑대 | 잡기(범위, n마리, 젖거나 탄 양 먼저, 기절·식사·화상 중 안 잡음), 합류(2절 표), 걷기(`Pathfinder`), 기절·식사 기간 | 늑대 합류 테스트와 README 늑대 예시 통과 |
| 5. 들불 확산 | `FireSpread`, 바구니 없이 양동이·풀로 확인 | 설계 메모의 들불 예시 통과 |
| 6. 염소 | 대상 찾기(`Sight`), `TilePusher`, 4방향 밀치기 순서 | 염소 비순환 테스트 통과 |
| 7. 말 | 발견, 먹기(옮기기 포함), 운반, 탑승, 목표 상실 | |
| 8. 소 | 냄새, 전진 밀기(`TilePusher`), 겹침 먹기 | |
| 9. 레벨 | `Assets/Levels/stages.txt`의 30개 스테이지를 읽어 들인다. 스테이지마다 solver가 찾은 최적해가 풀이 줄에 적혀 있다. 별 기준과 설명을 HUD에 붙인다 | 30개 모두 풀이 테스트 통과 (solver와 게임 구현이 같은 결과를 내는지 확인하는 테스트이기도 하다) |
| 10. 마무리 | 메뉴 화면(11절), 연출(이펙트), 사운드, WebGL 빌드 | 제출 |

---

## 9. 정한 것 (2026-11-13)

README에 빠져 있거나 해석이 갈리던 것을 사용자와 정했다. 규칙에 해당하는 것은 README에 반영되어 있다.

- 같은 칸에서는 지형 상호작용이 오브젝트끼리의 상호작용보다 먼저다.
- 자동 반응은 늑대 잡기 → 말 먹기 → 말 발견 → 소 냄새 순서로 한 바퀴 돈 뒤, 바뀐 것이 있으면 겹침부터 다시.
- 빈칸은 `-`(외곽)와 `x`(중간) 두 종류이고 규칙상 같다. shift에서만 뛰어넘고, 동물이 하는 모든 일에서는 장애물이다.
- shift 포인트 하나 = 줄 + 방향 하나. `R1:RL`은 포인트 2개. 풀이 수순은 레벨 파일의 선택 줄 `# 풀이:`에 적는다.
- 화살표는 미는 쪽 격자 가장자리 바깥에 둔다.
- 염소 밀치기는 순환하지 않는다.
- 늑대는 같은 칸의 젖거나 화상 입은 양을 먼저 먹는다. 기절·식사 중에는 잡지 않는다.
- 합류한 무리는 하나라도 젖었으면 젖고, 하나라도 식사 중이면 식사를 유지한다(남은 기간은 가장 긴 것).
- 들불 확산은 순환하지 않고, 물 칸은 들불이 되지 않는다.
- 게임 빌드에는 솔버를 넣지 않는다. 테스트는 `Assets/Tests/`에 둔다.
- 스테이지 검증용 solver는 에디터 전용 `Assets/Scripts/Tools/`에 있고 게임의 규칙 코드(`Core`)를 그대로 쓴다(2026-11-23, 예전 `Assets/Levels/Solver~/`의 별도 구현을 대체). 그래서 규칙을 바꾸면 `Core`만 고치고 검증 창으로 다시 검증한다.

## 10. 아직 확인할 것

구현하며 임시로 아래처럼 처리한다. 다르게 하고 싶으면 해당 단계 전에 정한다.

| 단계 | 질문 | 임시 처리 |
| --- | --- | --- |
| 3 | `x` 구멍 스프라이트가 SPRITES.md에 없다 | 스프라이트가 생길 때까지 단색 사각형으로 그림 |
| 3 | `shift_arrow`의 `disabled`는 SPRITES.md에 "shift할 수 없는 줄"로 적혀 있다. 이제 포인트가 없는 줄에는 화살표 자체가 없다 | 이벤트 재생 중 입력을 막을 때 씀. SPRITES.md 설명도 맞춰 고쳐야 함 |
| 4 | 합류한 늑대의 "이번 턴 지형 적용 기록" | 두 기록의 합집합 |
| 4 | 기절과 식사가 같은 턴 끝에 여럿 풀리면 정리를 몇 번 도는가 | 모두 한꺼번에 푼 뒤 한 번 |
| 2 | 같은 칸·같은 종류 오브젝트(예: 한 칸의 말 둘)의 처리 순서 | 레벨 파일을 읽은 순서(Id) |
| 8 | 화상 입은 소도 냄새를 맡는가 | 맡는다. 행동 불가는 종료 후 반응만 막는다고 본다 |

---

## 11. 메뉴 화면 (2026-11-16 정함)

타이틀, 스테이지 선택, 설정을 **shift로 오간다.** 씬을 새로 불러오거나 화면을 켜고 끄지 않고, 양이 줄을 따라 밀려 가는 동안 카메라가 옆 화면으로 움직인다. 그림과 화면별 좌표는 SPRITES.md 6절(헛간 1920×360, 화면 3개)에 있다.

### 규칙을 빌리는 방식

- 메뉴의 줄은 Core 규칙(`TurnRunner`)으로 돌리지 않는다. 메뉴 전용 코드가 **이동 연출만** 빌린다(`moveCurve`, 프리팹, `shift_arrow`, `line_highlight`).
- 그래도 보이는 움직임은 게임 규칙과 어긋나지 않게 맞춘다.
  - **화면 사이는 빈칸이다.** shift가 빈칸을 뛰어넘듯이 양이 옆 화면의 같은 높이 칸으로 건너간다. 그래서 화면을 넘나드는 칸은 모두 **타이틀 줄과 같은 높이(y 284)** 에 둔다.
  - 끝을 돌로 막은 줄은 끝에서 멈추고, 막지 않은 줄은 순환한다.

### 화면

| 화면 | 줄 | 조작 |
| --- | --- | --- |
| 타이틀 (가운데) | 로고 아래, 문 앞 바닥(y 284)에 `▶ [땅+양] ◀` 한 줄 | ▶를 누르면 양이 오른쪽으로 밀려 스테이지 선택으로, ◀를 누르면 왼쪽으로 밀려 설정으로 간다 |
| 스테이지 선택 (오른쪽) | 열린 스테이지마다 `[땅][풀]` 두 칸 줄. 줄들이 세로로 쌓이고 땅 칸이 한 열을 이룬다 | 땅 열을 위아래로 밀어 스테이지를 고른다. 고른 줄을 오른쪽으로 밀어 양이 풀에 들어가면 시작(`happy`), 왼쪽으로 밀면 빈칸을 건너 타이틀로 돌아간다 |
| 설정 (왼쪽) | 6칸짜리 설정 줄 3개, 그 아래 y 284에 양이 내려앉는 돌아가는 칸 | 설정 줄을 좌우로 밀어 값을 바꾼다. 돌아가는 칸의 양을 오른쪽으로 밀면 타이틀로 돌아간다 |

- **화살표는 게임과 같이 마우스를 올려야 보인다.** 타이틀의 두 화살표 아래에는 `Play`, `Setting` 글자를 둔다. 글자에 마우스를 올리면 그 화살표와 줄 표시가 나타난다. 플레이어는 글자에 마우스를 올렸다가 나타난 화살표를 눌러 넘어간다.
- **스테이지 선택에서는 카메라와 배경이 그대로이고 줄들만 위아래로 흐른다.** 고른 줄(양이 있는 줄)은 늘 y 284에 온다. 그래서 고른 줄을 왼쪽으로 밀면 같은 높이의 타이틀 줄로 건너간다. 땅 열은 순환한다(첫 스테이지 ↔ 마지막으로 열린 스테이지).
- 고른 스테이지의 정보는 오른쪽 아래 자는 양의 꿈 풍선에 나온다: 이름, 얻은 별, 별 조건 턴 수(`@stars`), 맵 미리보기(`minimap_tiles`).

### 설정

| 줄 | 칸 | 값 | 끝 |
| --- | --- | --- | --- |
| 음악 | 6칸 | 오브젝트(`setting_props`의 `music`)가 있는 칸 = 크기 0–5 | 순환. 실수로 넘겨도 반대로 밀면 된다 |
| 효과음 | 6칸 | 오브젝트(`sound`)가 있는 칸 = 크기 0–5 | 순환 |
| 이동 속도 | `# 매우 느림, 느림, 보통, 빠름 #` | 기본값 보통 | 양 끝이 돌이라 끝에서 멈춘다. 막힘 규칙을 그대로 보여 준다 |

- 이동 속도는 `GameSettings`의 `moveSeconds`, `stepPauseSeconds`, 염소 박치기 시간에 배율을 곱한다.

### 진행과 저장

- 처음에는 1번만 열려 있고, 스테이지를 하나 깰 때마다 다음 하나가 열린다.
- 메뉴에 들어올 때 양을 **마지막으로 한 스테이지의 줄**에 놓는다.
- 저장 내용: 열린 스테이지 수, 스테이지별 최고 별 수, 마지막으로 한 스테이지, 설정 3개, `stages.txt` 지문.

#### 저장 파일 구조 (2026-11-17)

- **파일 하나**: 모든 값을 `SaveFile` 하나에 담아 JSON 한 덩어리로 키 `sheepft.gameoff2026.save`에 쓴다. `version`이 다른 파일이나 깨진 파일은 버리고 새로 시작한다. 형식을 바꾸면 `SaveFile.CurrentVersion`을 올린다.
- **저장소**
  - WebGL 빌드: 브라우저 `localStorage`(`Assets/Scripts/Game/SaveStorage.jslib`). Unity의 PlayerPrefs는 WebGL에서 페이지 주소로 저장 경로를 정하는데, itch.io는 빌드를 다시 올릴 때마다 주소가 바뀌어 기록이 사라진다. `localStorage`는 출처가 같으면 남는다. 그 출처(`html-classic.itch.zone`)를 다른 게임과 함께 쓰므로 키에 게임 이름을 넣었다. 사생활 보호 모드처럼 저장소가 막히면 조용히 저장 없이 진행한다.
  - 에디터와 그 밖의 플랫폼: PlayerPrefs의 같은 키.
- **스테이지가 바뀌면 진행 기록 초기화**: 별과 열림은 스테이지 번호로 저장한다. 그래서 `stages.txt`의 지문(줄 끝 차이를 무시한 FNV-1a 해시)이 저장된 것과 다르면 진행 기록(열림, 별, 마지막 스테이지)을 지운다. 설정은 남긴다. 스테이지 에디터로 구성을 바꿔도 별이 엉뚱한 스테이지에 붙지 않는다.
- **개발용 메뉴**: Sheepft > Reset Save Data(진행과 설정 모두 지움), Sheepft > Unlock All Stages.
- 테스트는 `SaveFile`만 본다(`SaveFileTests`). 실제 저장소는 건드리지 않는다.

### 키보드

- 게임 중: `Z` undo, `R` 재시작, `Esc` 메뉴(스테이지 선택 화면, 양은 하던 스테이지 줄).
- 메뉴에는 키보드 조작을 넣지 않는다. 개발용 `[`, `]`는 그대로 둔다.

### 구현 순서

| 단계 | 내용 |
| --- | --- |
| 11-1. 저장 | `SaveData`와 `SaveFile`(위 "저장 파일 구조"). 클리어하면 별과 열린 스테이지를 기록. 지금의 `cleared`(실행 중에만 남음)를 대신한다 |
| 11-2. 타이틀·스테이지 선택 | `MenuController`(지금 화면, 카메라 이동), 메뉴 줄 하나를 밀고 그리는 `MenuRow`, 글자 hover로 화살표 보이기, 스테이지 줄 흐르기, 풀에 들어가면 스테이지 시작, `Esc`·메뉴 버튼으로 돌아오기. 꿈 풍선은 이름과 별만 |
| 11-3. 꿈 풍선 (2026-11-17 구현) | 고른 스테이지의 이름, 얻은 별(`hud_icons`의 `star`/`star_empty` 3개), 3별 조건(턴 아이콘 + `3 stars in N turns`), `minimap_tiles`(16×16)로 그린 맵. 잠긴 스테이지는 번호와 `Locked`만. 위치는 `GameSettings`의 "메뉴 배치: 꿈 풍선"(`menuBubbleTopLeft`, 그리고 풍선 캔버스 기준의 `menuBubbleNameTop`·`StarsTop`·`ConditionTop`·`MapCenter`, `menuBubbleFontSize`) |
| 11-4. 설정 (2026-11-16 구현) | 설정 줄 3개, 값 저장, 이동 속도 배율. 줄 위치는 `menuSettingsRowTopLeft`(첫 줄 맨 왼쪽 칸, 기본 (96, 56))와 `menuSettingsRowSpacing`(기본 56). 오브젝트는 음악 `setting_props:music`, 효과음 `setting_props:sound`, 이동 속도는 전용 그림이 없어 임시로 `horse:idle`(`MenuController`의 `speedProp`). 음악·효과음 값은 저장만 하고, 소리를 붙이는 11-5에서 쓴다 |
| 11-5. 다듬기 | 화면 전환 타이밍, 풀에 들어갈 때 `happy`, 소리 |

- 메뉴가 다 될 때까지 지금의 Canvas 레벨 선택 화면을 남겨 둔다.
- 화면 확인은 PLAYTEST.md에 항목으로 남긴다.

### 더 정한 것 (2026-11-16)

- 잠긴 스테이지: 다음 하나만 풀 대신 돌이 있는 줄로 보인다(들어갈 수 없음). 그 뒤는 보이지 않는다.
- 메뉴와 게임판은 한 씬에 둔다. 메뉴는 게임판과 떨어진 자리에 있고, 스테이지를 시작하면 카메라가 게임판으로 옮겨 간다.
- 음악·효과음 기본값은 3(0–5 가운데). 이동 속도 배율은 매우 느림 ×2, 느림 ×1.5, 보통 ×1, 빠름 ×0.6.
- 게임 중 레벨 선택 버튼(`button_level_select`)은 `Esc`와 같이 메뉴의 스테이지 선택 화면으로 간다.
- 설정 화면의 돌아가는 칸: 설정 줄 3개 아래 y 284에 땅 한 칸.
- 화면 크기: 배경은 1920×360, 게임 해상도는 640×360이다. 메뉴 카메라는 높이 360px(orthographic size 5.625)을 그대로 보여 주고 640px씩 옆으로 움직인다.
- 꿈 풍선(이름, 별, 별 조건, 미리보기)은 11-3으로 미룬다. 11-2에서는 고른 줄에 스테이지 번호만 적는다.

### 메뉴 배치 (메뉴 1920×360 기준 픽셀, 칸의 왼쪽 위)

배치는 `Assets/Data/GameSettings.asset`의 **메뉴 배치** 항목에서 고친다. Play 중에 고쳐도 남고, 메뉴를 다시 열 때(게임 중 `Esc`) 새 값으로 다시 그린다. 화살표·글자·줄 표시는 아래 칸 기준으로 따라 움직인다.

| 항목 | 기본값 | 따라오는 것 |
| --- | --- | --- |
| `menuLogoTopLeft` / `menuLogoCanvasSize` | (800, 40) / (320, 96) | 로고 |
| `menuTitleCell` | (944, 200) | 왼쪽 칸 ▶와 그 아래 `Play`, 오른쪽 칸 ◀와 그 아래 `Setting` |
| `menuSettingsCell` | (576, 200) | 왼쪽 칸 ▶와 그 아래 `Back` |
| `menuStageCell` | (1400, 200) | 고른 줄의 땅. 오른쪽 칸이 풀(잠기면 돌)이고 그 위에 번호(깼으면 노란색, 아니면 하얀색), 왼쪽 칸 ▶, 풀 오른쪽 칸 ◀. 다른 스테이지 줄은 위(다음 스테이지)·아래로 32px씩 |
| `menuStageRowsAround` | 2 | 고른 줄 위아래로 보이는 줄 수. 땅 열의 ▼는 맨 위 줄 위 칸, ▲는 맨 아래 줄 아래 칸 |

- 고른 줄은 보이는 줄들의 가운데다. 위아래 2줄이면 화살표까지 7칸(224px)이 필요해서, 고른 줄 y는 96–232 사이여야 화면에 들어간다.
- 화면을 넘나드는 칸(타이틀, 고른 스테이지 줄, 설정의 돌아가는 칸)은 모두 **같은 높이**에 둔다. 양은 같은 높이의 줄을 따라 빈칸을 건너뛰듯 넘어간다. 위아래 2줄이 들어가려면 y는 96–232여야 하고, 지금은 Inspector에서 맞춘 **y 200**이다(2026-11-16). 배경은 이 높이에 맞춰 그린다(SPRITES.md 6절).
