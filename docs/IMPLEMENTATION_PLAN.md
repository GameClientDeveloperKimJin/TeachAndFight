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

## AI 세션 시작 프로토콜 — 진행 상태 자동 점검 & 마일스톤 게이트

> Claude Code가 이 프로젝트에서 새로 시작될 때마다(개발자 A/B 누구든, 본인이든), 사용자가 다시 설명해줄 필요 없이 아래 순서로 현재 진행 상태를 스스로 파악하고 세션 시작 시 먼저 보고한다.

### 1. 상태 조회

- `gh issue list --state all` (또는 `--json number,state,title`) — #3~#20 각각 open/closed 확인
- `git log --all --oneline -20`, `git branch -a` — dev/각 feat 브랜치 최신 커밋, dev 머지 여부 확인
- 이슈 ↔ 마일스톤(W1~W4) ↔ 담당(개발자A/개발자B/공용) 매핑은 **DEV_SPEC.md 맨 아래 "이슈 매핑" 표가 원본(source of truth)** — 여기 별도로 복제하지 않고 항상 그 표를 조회해서 판단한다.

### 2. 현재 Wn 판정

- 어떤 Wn에 속한 **이슈 전부(개발자A 담당 + 개발자B 담당 + 공용)가 closed**일 때만 그 Wn을 "완료"로 간주한다.
- 그 중 하나라도 open이면 해당 Wn은 "진행중" — 담당자가 나 자신(A)이든 팀원(B)이든 구분 없이 동일하게 적용한다.
- 세션 시작 시 다음 형식으로 사용자에게 보고: `현재 W{n} — 완료: [closed 이슈], 진행중: [open 이슈 + 담당자]`

### 3. 마일스톤 게이트 (하드 제한 — 예외 없음)

- **Wn+1의 작업(코드 작성/이슈 착수)은 Wn에 속한 이슈가 개발자A/개발자B/공용 구분 없이 전부 closed일 때만 시작한다.**
- 사용자가 "W{n+1} 시작하자"라고 말해도, Wn에 열린 이슈가 하나라도 있으면(내 담당이 아니라 팀원 담당이라도) **절대 다음 단계 코드를 작성하지 않는다.** 대신 무엇이 막고 있는지(이슈 번호, 담당자, 근거 장)를 명시하고 작업을 중단한다. 이 제한은 A/B/공용 담당 전부에게 동일하게 적용된다.
- **같은 Wn 안에서 A/B 병렬 작업은 게이트 대상이 아니다.** 각 주차 표(Day 단위 A/B 분담)대로 병렬 진행은 자유 — 게이트는 오직 Wn → Wn+1 "주차 전환" 시점에만 걸린다.
- 이슈가 closed라는 신호는 1차 신호일 뿐, 완료 기준(✅)의 수동 검증 항목(플레이테스트, 10회 중 9회 등)까지 실제로 통과했는지는 보장하지 않는다. 의심되면 해당 이슈의 완료 기준을 다시 열어 확인하고, 애매하면 진행하지 말고 사용자에게 확인을 요청한다.

### 4. 보고 예시

```
현재 W1 완료: #3,#4,#5,#6 전부 closed (공용/A만 있는 주차)
→ W2 진입 조건(A: RuleValidator 테스트/FSM/데모, B: LLMClient/프롬프트 검증) 표까지 확인 후 다음 단계 진행 가능.
```

```
현재 W2 진행중: A #7,#8 closed / B #10 open, #11 open
→ "W3 시작하자"에도 B의 #10/#11이 열려있어 W3 진입 불가. #10/#11 완료 여부 팀원(B)에게 먼저 확인 필요 — 내가 A 담당이라도 예외 없음.
```

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

**W2 진입 조건 (아래 다 충족해야 W2 시작)**

| 담당 | 산출물 | 확인 방법 |
|---|---|---|
| A | #4 RuleValidator 단위테스트 3종(정상/어휘위반/슬롯초과) 통과, 스키마 필드명·enum **버전 고정**(더 이상 안 바뀜) | EditMode 테스트 그린 |
| A | #5 FSM+스탯/스킬 4종 동작, 수치는 combat_config.json에서 로드 | 인스펙터에서 config 값 바꿔보고 즉시 반영 확인 |
| A | #6 하드코딩 규칙 2개로 AI vs AI 1v1이 60초 내 자연스럽게 종료 | 직접 플레이 실행 |
| B | #9 LLMClient가 실제 Anthropic API 호출→응답 파싱 성공, API 키 없음/타임아웃 시 안내 대사로 폴백 | 정상 호출 1회 + 키 제거 후 폴백 1회 |
| B | 03장 프롬프트로 최소 1개 유효 규칙 diff 생성 확인(수동 테스트) | 콘솔/에디터 스크립트 결과 로그 |
| 공용 | A의 RuleSet 스키마가 고정됐고, B의 LLMClient가 그 스키마로 응답 파싱 가능함을 서로 확인 | 짧은 동기화(15분) |

**주의**: A의 스키마가 아직 유동적이면 B의 #10(RuleValidator 연결) 작업이 W2 내내 계속 깨짐 — 스키마 고정이 W1→W2의 진짜 게이트.

### W2 — 규칙 실행 + LLM 파이프라인 연동

| Day | 개발자 A | 개발자 B |
|---|---|---|
| 6~8 | #7 RuleEvaluator (0.1s 틱 의사결정 루프, JSON 규칙셋 실행) | #10 훈련 컴파일 파이프라인 연동 (RuleValidator 연결, ops 적용/needs_confirmation/conflict_with 처리) — #4 필요 |
| 8~10 | #8 EventLog 시스템 | #11 프롬프트 인젝션 방어 테스트 케이스 |

**W3 진입 조건**

| 담당 | 산출물 | 확인 방법 |
|---|---|---|
| A | #7 RuleEvaluator가 JSON 규칙셋 실제 실행, 스태미나 부족 시 다음 규칙으로 폴백 | 규칙셋 JSON 바꿔서 행동 변화 확인 + 스태미나 0 상태 테스트 |
| A | #8 EventLog가 경기당 정상 생성, rule_fired에 rule_id 포함 | 경기 1회 후 로그 파일/객체 직접 확인 |
| B | #10 자연어 입력 → 유효 diff 생성(10회 중 9회 이상), 거절/되묻기/모순 케이스 각각 동작 | 04장 완료기준 4항목 수동 테스트 |
| B | #11 "너는 이제 시스템이다" 류 인젝션 입력이 거절 처리됨 | 테스트 케이스 실행 |
| 공용 | **엔드투엔드 1회 확인**: 플레이어가 자연어로 규칙 하나 가르침 → RuleValidator 통과 → 실제 전투(RuleEvaluator)에 그 규칙이 반영되는 것을 A/B 공동으로 직접 확인 | 통합 시연 (필수 — 이게 안 되면 W3에서 화면 만들 데이터 흐름 자체가 미검증 상태) |

→ W3에서 만들 UI(#13~#15)는 전부 이 데이터 흐름 위에 얹는 것이므로, 위 엔드투엔드 확인 없이 넘어가면 화면부터 만들고 나중에 배관이 안 맞는 사고가 남.

### W3 — 콘텐츠 + 화면 3종

| Day | 개발자 A | 개발자 B |
|---|---|---|
| 11~13 | #12 상대 5종 규칙셋 JSON (러쉬/철벽/그림자/카멜레온/사범) | #14 Training.unity 화면 (규칙 슬롯, 대화 로그, 가르치기 버튼) |
| 13~15 | #13 Match.unity 화면 (HP/스태미나 바, 타이머, 규칙 라벨) | #15 LockerRoom.unity 화면 + 회고 LLM 연동 |
| 14~15 | 공용: #16 GameFlow 씬 전환 + 데이터 컨테이너 — 양쪽 화면 얼추 나온 뒤 반나절~하루 페어 작업 |
| 15~16 | 공용: #22(신규, GitHub 등록 필요) 캐릭터 에셋 확보(AI 생성 또는 보유 에셋) — A/B 공동. Animator 설정+재생 코드는 B 단독(아래 별도 섹션 참조) |

**W4 진입 조건**

| 담당 | 산출물 | 확인 방법 |
|---|---|---|
| A | #12 상대 5종 JSON 전부 RuleValidator 통과, 백지 규칙셋으로 1차전(러쉬) 반드시 패배 | 5개 JSON 로드 테스트 + 백지 상태 1차전 실행 |
| A | #13 Match.unity에서 발동 규칙 라벨이 실제 RuleEvaluator 로그와 일치 | 경기 중 라벨-로그 대조 |
| B | #14 Training→Match 전환 시 방금 추가한 규칙이 즉시 반영 | 규칙 추가 직후 경기 시작해서 확인 |
| B | #15 LockerRoom 회고 LLM이 정상 호출되고 규칙 발동 통계가 실제 EventLog와 일치 | 경기 1회 후 락커룸 진입해서 확인 |
| B | #22 기본 애니메이션(Idle/Move/Dash/Attack 3종/HitStun/Down) 최소 1세트가 Match.unity에서 재생 확인 | 경기 1회 플레이하며 상태 전환마다 애니메이션 반영 확인 |
| 공용 | #16 GameFlow로 Training⇄Match→LockerRoom→Training 3씬 순환이 규칙셋/EventLog 유실 없이 동작 | 최소 2바퀴 순환 플레이 |
| 공용 | #22 에셋 확보(캐릭터 스프라이트/클립 소스) 완료 | 8종 클립 원본 소스 확보 확인 |

**주의**: #16 순환이 안 맞으면 W4에서 아무리 연출/밸런싱을 다듬어도 의미 없음 — 이게 W3→W4의 하드 게이트. #22은 하드 게이트는 아니지만(순수 비주얼, 데이터 흐름과 무관), #17(연출 폴리싱: 슬로모/스케일펀치)이 애니메이션 위에 얹는 작업이라 W4 시작 전까지 최소한의 클립은 있어야 #17이 헛돌지 않음 — 최대한 W3 안에서 끝낼 것.

---

### W3 공용 추가 작업 — 캐릭터 비주얼(스프라이트 + 애니메이터) 적용 (#22, 신규)

> #8까지 완료 시점 기준 워크로드: A(#4,5,6,7,8,12,13,17,18=9개) vs B(#9,10,11,14,15,19=6개) — A가 이슈 수 더 많음. 이 작업은 두 몫으로 쪼개서 워크로드를 맞춘다.
> - **에셋 확보**(AI 생성 또는 팀 보유 에셋 활용): A+B 공동, 병렬로 소스 구하고 취합만 함께.
> - **Animator 설정 + 재생 코드 구현**: **개발자 B 단독 담당** (이슈 수 6개 < A 9개, 워크로드 격차 보정).
>
> GitHub 이슈 #22로 등록 완료(2026-07-29). start.sh W3 이슈 목록에도 추가됨 — 이제 세션 시작 게이트 자동점검에 포함됨.

**목적**: 지금까지 코드 FSM(`FighterController`)만으로 굴러가던 전투에 실제 캐릭터 스프라이트 + 애니메이션을 입혀서, W4 연출 폴리싱(#17)과 밸런싱 플레이테스트(#18)가 비주얼 피드백 위에서 진행되게 한다.

**필요 에셋 (A+B 공동)**

| 항목 | 옵션 | 비고 |
|---|---|---|
| 캐릭터 스프라이트 | (A) 에셋스토어 2D 파이터 스프라이트팩(예: 픽셀/카툰 계열 액션 캐릭터, 좌우 대칭 사용 가능한 것) 구매/무료 팩 | 라이선스 상업적 이용 가능 여부 확인 |
| | (B) AI 이미지 생성(스프라이트시트 형태로 프롬프트) 후 Sprite Editor로 그리드 슬라이스 | 프레임 일관성(같은 캐릭터/같은 화풍) 확보가 관건 — 여러 장 생성 후 수작업 보정 필요할 수 있음 |
| 최소 클립 세트 | Idle(loop), Move(loop), Dash(non-loop), Attack_Light(non-loop), Attack_Heavy(non-loop), Attack_Ultimate(non-loop), HitStun(non-loop), Down/KO(non-loop, 마지막 프레임 홀드) | 8종. Recovery/Whiff는 별도 클립 없이 Attack 클립 꼬리 재사용하거나 Idle로 바로 전환해도 무방(완료기준엔 없음) |
| 프레임 수 권장 | Idle 4~6f, Move 6~8f, Dash 3~4f, Attack류 4~6f(startup+active 합쳐서 1클립), HitStun 2~3f, Down 3~4f | 낮은 우선순위 항목이므로 과도한 프레임 수 투자 지양 |

**적용 스크립트 / 코드 위치 (담당: 개발자 B 단독)**

| 파일 | 변경 내용 |
|---|---|
| `Assets/02_KJ/Scripts/Combat/FighterController.cs` | `State` setter가 여러 지점에 산재(`TryPerform`, `Tick`, `ResolveAttackActive`, `ApplyHit` 등)되어 있어, 상태 변경을 한 곳으로 모으는 `private void SetState(FighterState s)` 헬퍼로 리팩터링하고 `public event Action<FighterState> OnStateChanged` 추가해 변경 시점마다 발행. `committedAction`(현재 private 필드)을 `public ActionType CommittedAction => committedAction;` 으로 공개 — Attack계열 진입 시 Light/Heavy/Ultimate 구분에 필요. |
| `Assets/02_KJ/Scripts/Combat/FighterAnimatorBridge.cs` (신규) | `FighterController` + `Animator`를 같은 GameObject에서 참조하는 새 MonoBehaviour. `OnStateChanged`, `OnHitTaken`, `OnWhiff`, `OnDown` 구독해 Animator 파라미터 갱신. `FacingRight`(기존 공개 프로퍼티) 값으로 매 프레임 `SpriteRenderer.flipX` 갱신(좌우 반전은 이 스크립트에서 처리 — `FighterController` 쪽은 위치 계산만 하고 시각 반전은 알지 못함). |
| Prefab (Fighter) | `SpriteRenderer` + `Animator` 컴포넌트 추가, 위 브릿지 스크립트 부착, Animator Controller 연결 |

**Animator 파라미터**

| 이름 | 타입 | 설명 |
|---|---|---|
| `State` | Int | `FighterState` enum과 동일 순서(0 Idle ~ 7 Down)로 매핑 |
| `AttackKind` | Int | 0 None / 1 Light / 2 Heavy / 3 Ultimate — `AttackStartup`/`AttackActive` 진입 시 `CommittedAction` 값으로 세팅 |

**전이 구조 — AnyState만 사용**

- 클립 간 직접 연결(Idle→Move, Move→Attack 등 개별 전이)은 만들지 않는다. 모든 클립은 `Any State → 클립` 전이 하나씩만 갖는다(Attack류는 `State == AttackStartup/Active && AttackKind == N` 조건 추가).
- 이유: 상태 전이 권한이 이미 코드(`FighterController.Tick`/`stateTimer`)에 있음 — Animator가 자체적으로 "이 상태에서 저 상태로만 갈 수 있다"는 그래프를 또 만들면 코드 FSM과 이중 관리가 되고, 어느 한쪽만 수정해도 어긋난다. Any State 단일 계층이면 파라미터 값만 맞으면 항상 원하는 클립으로 전이 가능 — 코드 쪽 상태 변경 로직을 그대로 신뢰.
- 전이 설정: `Exit Time` 체크 해제(즉시 전이), `Transition Duration` 0.05~0.1초 정도의 짧은 크로스페이드만(끊김 방지용, 게임플레이 타이밍에는 영향 없음 — 타이밍은 여전히 `stateTimer`가 결정).
- `Has Exit Time` / 조건 없는 자동 루프백 전이 금지 — 파라미터가 안 바뀌면 같은 클립에 계속 머무르게(각 클립을 Loop 여부에 맞게 Loop Time 체크).
- HitStun/Down처럼 즉시 끊겨도 어색하지 않은 상태는 Interruption Source를 `Current State`로 둬서 재히트 시 애니메이션이 처음부터 다시 재생되게 한다.

**완료 기준(안)**

- 8종 클립 전부 Match.unity 데모 경기 중 최소 1회씩 재생 확인(Idle/Move/Dash/Attack 3종/HitStun/Down)
- 좌우 반전이 `FacingRight`와 항상 일치
- 히트/다운 시 애니메이션이 실제 `OnHitTaken`/`OnDown` 이벤트 타이밍과 눈으로 봤을 때 어긋나지 않음

### W4 — 폴리싱 + 밸런싱 + 데모

| Day | 개발자 A | 개발자 B |
|---|---|---|
| 16~17 | #17 연출 폴리싱 (슬로모, 스케일 펀치, 배속/일시정지) | #19 제자 톤 다듬기 + LLM 프롬프트 튜닝 |
| 17~19 | #18 밸런싱 플레이테스트 (백지 1차전 패배 보장, 보스 재도전 3회 목표) | #19 계속 + A와 교차 플레이테스트 |
| 19~20 | 공용: #20 데모 빌드 + 최종 QA (00~05장 전체 완료 기준 재확인) | 공용: 동일 |

**완료(데모 제출) 조건**

| 담당 | 산출물 | 확인 방법 |
|---|---|---|
| A | #17 결정타 슬로모/라벨 스케일펀치 연출, 배속 0.5x/1x/2x·일시정지 정상 동작 | 직접 조작 확인 |
| A | #18 백지 규칙셋 1차전 반드시 패배, 보스전 평균 재도전 3회 목표 근접 | 최소 3회 이상 반복 플레이테스트 기록 |
| B | #19 disciple_reply/회고 대사가 톤 가이드(존댓말/재해석/거절 톤) 부합 | 샘플 다회 검수 |
| 공용 | #20 데모 빌드 정상 실행 + DEV_SPEC.md 01~05장 전체 ✅ 완료 기준 체크리스트 전항목 통과 | 체크리스트 하나씩 대조 |

**주의**: 이 게이트를 대충 넘기면 제출 직전에 결함 발견 → 4주 일정 전체가 흔들림. #20은 개인 작업이 아니라 A/B 공동 체크리스트 대조로 마무리할 것.

**개발자A(KJ) 추가 작업 — 되묻기(needs_confirmation) UX 개선**

(2026-07-29, Match.unity 플레이테스트 중 A가 발견. #19(B 담당)에 맡기지 않고 **A가 직접 작업**하기로 함 — 별도 GitHub 이슈로 등록 필요([A] 태그, 아직 미등록).
⚠ `TrainingPromptBuilder.SystemPrompt`/`TrainingScreenController`는 원래 B 담당 파일 영역이라 작업 전 JM에게 공유. ⚠ 지금 W3 진행중 — 이 작업은 W4 폴리싱 성격이라 **마일스톤 게이트상 W3의 개발자A+개발자B+공용 이슈가 전부 close될 때까지 착수 금지**(담당자를 A로 바꿔도 게이트는 그대로 적용됨).

`TrainingPromptBuilder.SystemPrompt` 규칙4("가르침이 모호하면 무조건 needs_confirmation")가 실제로는 플레이어에게 매우 혼란스러움:

1. 되묻는 disciple_reply가 질문형이 아니라 서술형("제가 ~하도록 하겠습니다")으로 나올 때가 있어, 거절당한 건지 적용된 건지 UI만 봐선 구분이 안 됨 → 슬롯 변화 여부로만 판단 가능한데 플레이어는 그 규칙을 모름.
2. 되묻는 이유(예: "사거리 안이면"처럼 숫자가 없어서 모호함)가 disciple_reply에 명시되지 않아, 플레이어가 뭘 고쳐 말해야 하는지 알 수 없음.
3. "사거리"처럼 실제 스킬 사거리(약공 1.2 / 강공 1.5)와 연동되는 개념은, 숫자를 안 줬을 때 임의로 추정값(예: 2)을 만들어 확인받기보다, 차라리 명확한 질문("몇 정도 거리를 말씀하시는 건가요?")으로 유도하거나 대표값 제안을 대사에 포함시키는 게 나음.

개선 방향 제안(확정 아님, A가 착수 시 최종 판단):
- needs_confirmation 응답은 disciple_reply를 항상 명확한 질문형으로 강제(시스템 프롬프트 규칙4/5 문구 보강).
- 모호했던 이유(숫자 없음/충돌 규칙 id 등)를 disciple_reply 안에서 구체적으로 언급하도록 유도.
- UI 쪽(TrainingScreenController)도 needs_confirmation일 때 말풍선에 "(아직 적용 안 됨, 다시 말해주세요)" 같은 보조 표시를 추가하는 것도 고려.

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
