# TEACH & FIGHT — 구현 계획서 (AI 실행 기준)

> 이 프로젝트는 **전체 구현을 AI 코딩 에이전트(Claude Code)가 수행**한다. 개발자 A/B는 각자 담당 이슈를 AI에게 지시·검토·커밋하는 역할. 스펙 원문은 [DEV_SPEC.md](./DEV_SPEC.md), 이슈는 GitHub #3~#20.

## AI 구현 착수 전 팀원 공지

AI로 구현을 맡기기 전에, 팀원(개발자 A/B) 모두 아래 3개 문서를 반드시 확인하고 시작해야 정확하고 안전하게 각자 역할을 완수할 수 있음. 아래 내용을 그대로 팀원에게 전달할 것.

---

**[공지] AI 구현 시작 전 필독**

이번 프로젝트는 전 구현을 Claude Code(AI 에이전트)로 진행합니다. 작업 시작 전 아래 3가지를 반드시 확인해주세요.

1. **개발 문서**: `docs/DEV_SPEC.md`
   Notion 기획 원문(00~06장) 전체 — 규칙 스키마, 전투 수치, LLM 프롬프트 원문, 화면 정의, 콘텐츠 데이터, 협업 컨벤션. **AI에게 작업 지시할 때 이 문서의 00장 + 본인 작업 해당 장을 통째로 컨텍스트로 붙여넣을 것.**
2. **구현 계획서**: `docs/IMPLEMENTATION_PLAN.md`
   확정된 실제 스택(Unity 6000.0.38f1/URP, Claude Haiku API), 이슈 의존관계, 4주 일정(Day 단위 A/B 분담), A↔B 접점 계약(GameSession/MatchResult 클래스 시그니처), 완료 기준 검증 방식, 커밋 규칙.
3. **GitHub 이슈**: https://github.com/GameClientDeveloperKimJin/TeachAndFight/issues
   작업 단위 18개(#3~#20)가 W1~W4로 등록되어 있음. 본인 담당(dev-A/dev-B/공용) 이슈만 순서대로 진행.

**AI에게 작업 지시하는 방법 (매 이슈 공통)**
- 컨텍스트로 줄 것: ① DEV_SPEC.md 00장 + 해당 이슈의 근거 장(예: #7이면 02장) ② 해당 GitHub 이슈 본문(목표/작업내용/완료기준) ③ IMPLEMENTATION_PLAN.md에서 그 이슈가 속한 주차 설명
- AI 결과물 커밋 전 확인: 01장 스키마의 필드명·enum 철자를 AI가 임의로 바꾸지 않았는지 diff 확인
- 이슈의 ✅ 완료 기준을 전부 통과해야 그 이슈를 완료 처리 (닫기)
- A↔B 접점(GameSession/MatchResult)이 바뀌어야 하는 상황이면, 코드 짜기 전에 상대에게 먼저 알릴 것

---

## 확정 스택

| 항목 | 값 |
|---|---|
| 엔진 | Unity 6000.0.38f1, 2D, URP |
| 렌더 파이프라인 | URP (`Assets/Settings/Renderer2D.asset`, `UniversalRP.asset`) — Notion 원문의 Built-in RP 무시 |
| 입력 | 새 Input System (`Assets/InputSystem_Actions.inputactions`) |
| 언어 | C# |
| 직렬화 | Newtonsoft.Json (스키마 계약, `01. 규칙 스키마` 참조) |
| 비동기 | UniTask (LLM 호출용) |
| LLM | Anthropic Claude API — Haiku 계열 모델, endpoint `https://api.anthropic.com/v1/messages` |
| 네트워크 | 없음 (완전 로컬) |

## 사전 준비 (구현 시작 전, #3 세팅 이슈에서 처리)

1. **패키지 추가** (`Packages/manifest.json`):
   - `com.unity.nuget.newtonsoft-json` (Unity Registry에 있음, Package Manager에서 바로 설치)
   - UniTask — Unity Registry에 없음. git URL로 추가: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`
2. **Unity 6 + 새 Input System 주의점**: uGUI 버튼 클릭이 동작하려면 `EventSystem`의 입력 모듈이 `InputSystemUIInputModule`이어야 함 (레거시 `StandaloneInputModule`이면 클릭 무반응). Training/LockerRoom UI 작업(#14, #15) 시작 전 확인.
3. **API 키 세팅**: `ANTHROPIC_API_KEY` 환경변수로 주입 (Windows: 시스템 환경 변수 등록, 또는 로컬 실행 시 `System.Environment.SetEnvironmentVariable`로 테스트). **코드/저장소에 키 하드코딩 금지, .gitignore에 로컬 키 파일 있으면 반드시 추가.**
4. **.gitignore / Force Text 확인**: Library/, Temp/, Logs/, obj/, UserSettings/ 제외, ProjectSettings/는 커밋. Edit > Project Settings > Editor에서 Asset Serialization = Force Text, Visible Meta Files 확인.

## 실행 순서 (이슈 의존관계 기준)

AI에게 각 단계 지시할 때 컨텍스트로 줄 것: **DEV_SPEC.md 00장 + 해당 장 전체**. 완료 기준(✅) 통과 확인 전에는 다음 단계로 넘어가지 않는다.

```
#3 (세팅) ─┬─▶ #4 (스키마+Validator) ─┬─▶ #7 (RuleEvaluator) ─▶ #8 (EventLog)
           │                          │
           └─▶ #5 (FSM+스탯/스킬) ─────┴─▶ #6 (하드코딩 1v1 데모)  [W1 끝]

#4 ─▶ #9 (LLMClient) ─▶ #10 (훈련 컴파일 연동) ─▶ #11 (인젝션 방어 테스트)  [W2 끝]

#8, #10 ─┬─▶ #12 (상대5종 JSON)
         ├─▶ #13 (Match.unity)
         ├─▶ #14 (Training.unity)
         ├─▶ #15 (LockerRoom.unity + 회고 LLM)
         └─▶ #16 (GameFlow 씬 전환)  [W3 끝]

#13, #17 ─▶ 연출 폴리싱 / #12, #18 ─▶ 밸런싱 / #19 ─▶ 톤·프롬프트 튜닝 ─▶ #20 (데모 빌드+QA)  [W4 끝]
```

- 개발자 A 계열(#5~#8, #12~#13, #17~#18)과 개발자 B 계열(#9~#11, #14~#15, #19)은 **#4 스키마 확정 후 병렬 진행 가능**.
- #16(GameFlow)은 A/B 접점 — 아래 인터페이스 계약을 먼저 합의하고 시작.

## 4주 일정 (개발자 A/B 주차별 분담)

전제: 주 5일 작업 기준 상대 일수(Day 1~20). 실제 캘린더 요일이 아니라 **작업 순서**이므로, 주 3~4일만 가능하면 그 비율로 늘려 잡으면 됨. 병렬성을 최대화하려고 A/B가 서로 안 막히는 순서로 배치함 — **매주 금요일(주 마지막 날) dev→main 머지 + 마일스톤 데모 확인** (06장 협업 컨벤션).

### W1 — 스키마 확정 + 전투 코어 기반

| Day | 개발자 A | 개발자 B |
|---|---|---|
| 1 | 공용: #3 세팅(.gitignore, 패키지 추가) + **GameSession/MatchResult 접점 계약**(아래 섹션) 합의 — B와 페어로 30분~1시간 | 공용: 동일 |
| 1~2 | #4 규칙 스키마 C# 모델 + RuleValidator | #9 LLMClient (Anthropic Haiku 연동, HTTP 호출/파싱/재시도) |
| 2~4 | #5 FighterController FSM + 스탯/스킬 4종 | (LLMClient 완료 후) 03장 프롬프트 초안을 실제 API로 수동 검증 — 에디터 스크립트/콘솔로 훈련 컴파일 프롬프트 응답 품질 확인, #10 사전 준비 |
| 4~5 | #6 하드코딩 규칙 2개로 1v1 데모 | 위 프롬프트 검증 계속 + #10 코드 스켈레톤 착수 |

→ W1 완료 기준: 하드코딩 규칙으로 1v1 데모 동작, 규칙 스키마 확정(버전 고정), LLMClient가 실제 Claude API 호출 성공.

### W2 — 규칙 실행 + LLM 파이프라인 연동

| Day | 개발자 A | 개발자 B |
|---|---|---|
| 6~8 | #7 RuleEvaluator (0.1s 틱 의사결정 루프, JSON 규칙셋 실행) | #10 훈련 컴파일 파이프라인 연동 (RuleValidator 연결, ops 적용/needs_confirmation/conflict_with 처리) — #4 필요 |
| 8~10 | #8 EventLog 시스템 | #11 프롬프트 인젝션 방어 테스트 케이스 |

→ W2 완료 기준: JSON 규칙셋이 실제 전투에 반영, 훈련 컴파일이 자연어→규칙 diff로 정상 동작.

### W3 — 콘텐츠 + 화면 3종

| Day | 개발자 A | 개발자 B |
|---|---|---|
| 11~13 | #12 상대 5종 규칙셋 JSON (러쉬/철벽/그림자/카멜레온/사범) | #14 Training.unity 화면 (규칙 슬롯, 대화 로그, 가르치기 버튼) |
| 13~15 | #13 Match.unity 화면 (HP/스태미나 바, 타이머, 규칙 라벨) | #15 LockerRoom.unity 화면 + 회고 LLM 연동 |
| 14~15 | 공용: #16 GameFlow 씬 전환 + 데이터 컨테이너 — 양쪽 화면 얼추 나온 뒤 반나절~하루 페어 작업 |

→ W3 완료 기준: 3씬(Training/Match/LockerRoom) 순환이 데이터 유실 없이 동작, 상대 1~5차전 콘텐츠 로드 확인.

### W4 — 폴리싱 + 밸런싱 + 데모

| Day | 개발자 A | 개발자 B |
|---|---|---|
| 16~17 | #17 연출 폴리싱 (슬로모, 스케일 펀치, 배속/일시정지) | #19 제자 톤 다듬기 + LLM 프롬프트 튜닝 |
| 17~19 | #18 밸런싱 플레이테스트 (백지 1차전 패배 보장, 보스 재도전 3회 목표) | #19 계속 + A와 교차 플레이테스트 |
| 19~20 | 공용: #20 데모 빌드 + 최종 QA (00~05장 전체 완료 기준 재확인) | 공용: 동일 |

→ W4 완료 기준: 데모 빌드 정상 실행, 전체 ✅ 완료 기준 체크리스트 통과.

## A↔B 접점 계약 (병렬 작업 시 충돌 방지)

`Scripts/Core/`에 아래 데이터 컨테이너를 **Day 1(W1 시작 시점)에 가장 먼저 확정**하고, 이후 A/B 각자 이 계약에 맞춰 독립 개발 (실제 구현은 #16에서 하되, 시그니처 합의는 그 전에 끝내야 병렬 작업이 막히지 않음):

```csharp
// GameFlow가 DontDestroyOnLoad로 들고 있는 세션 데이터
public class GameSession
{
    public RuleSet DiscipleRuleSet;   // 01장 스키마, Newtonsoft.Json 모델 (#4 산출물)
    public RuleSet CurrentOpponent;   // opponent_0N.json 로드 결과 (#12 산출물)
    public int OpponentIndex;         // 1~5
    public MatchResult LastMatch;     // 직전 경기 결과, LockerRoom 회고 입력
}

public class MatchResult
{
    public bool Won;
    public float SelfHpPct;
    public float EnemyHpPct;
    public List<EventLogEntry> EventLog;  // 02장 EventLog 포맷 (#8 산출물)
}
```

- `RuleSet`/`EventLogEntry` 필드명은 01·02장 스키마와 **철자 동일**해야 함.
- A는 `MatchResult` 생성(Match.unity 종료 시점)을, B는 `MatchResult` 소비(LockerRoom 회고 프롬프트 구성)를 담당 — 이 클래스 시그니처가 바뀌면 상대에게 먼저 알릴 것.

## 완료 기준 검증 방식

- 01, 02장의 단위 테스트류(RuleValidator, RuleEvaluator 폴백)는 Unity Test Framework **EditMode** 테스트로 작성 (`Assets/_Project/Tests/EditMode/`).
- 03장의 "10회 중 9회 이상" 같은 확률적 기준은 자동화하지 않고, 같은 입력 10회 수동 실행 후 통과/실패 수기 기록 (LLM 응답 특성상 완전 자동 CI 검증은 범위 밖).
- 04장의 "마우스만으로 플레이 가능"은 자동 테스트 대상 아님 — 플레이테스트로 확인.

## 커밋 규칙

- 이슈 하나 = 커밋 최소 1개 이상, 완료 기준 통과 후 커밋.
- 커밋 메시지: `[영역] 내용` (영역 = combat/training/core/data/docs), 이슈 번호 참조 (`#7` 형식으로 본문에 포함).
- AI가 생성한 diff에서 01장 스키마 필드명·enum 철자가 임의로 바뀌지 않았는지 커밋 전 확인.
