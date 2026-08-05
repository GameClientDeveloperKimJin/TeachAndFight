# TEACH & FIGHT — 개발 문서

> 출처: [Notion 사전과제 문서](https://app.notion.com/p/9-NHN2026XAI-39f9e9363a5d80068825d3efc67ac084) (00~06 원문 통합). 이 문서가 GitHub 이슈(#3~#20)의 근거 스펙.
>
> **실제 환경은 Notion 원문과 다름 — 아래 스택 표는 실제 프로젝트 기준으로 갱신됨.** 실행 계획은 [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) 참고.

---

## 00. 프로젝트 개요 & 아키텍처

### 한 줄 요약

자연어로 가르친 규칙대로 싸우는 AI 제자 코칭 배틀. 플레이어는 전투에 개입하지 않고, 훈련(자연어 입력)으로만 제자를 성장시켜 상대 5인 토너먼트를 클리어한다.

### 기술 스택 (확정 — 실제 프로젝트 기준)

| 항목 | 값 |
|---|---|
| 엔진 | **Unity 6000.0.38f1**, 2D, **URP** (Notion 원문의 2022.3 LTS/Built-in RP 아님) |
| 언어 | C# |
| 전투 실행 | 로컬 유틸리티 AI — LLM 호출 없음 |
| LLM (훈련 컴파일/회고) | **Anthropic Claude API (Haiku 계열)** 확정 |
| 데이터 | 규칙셋·상대·스킬 수치 전부 JSON (`Assets/_Project/Data/`) |
| 네트워크 | 없음 (완전 로컬. 제자 = JSON 파일) |
| 개발 방식 | 전 구현을 AI 코딩 에이전트(Claude Code)가 수행 |

### 아키텍처 (레이어 분리 — 절대 원칙)

```
[훈련실 UI] → 자연어 → [LLM Compiler] → 규칙 diff(JSON) → [RuleSet]
[경기] → RuleSet → [RuleEvaluator(0.1s 틱)] → ActionCommand → [FighterController(FSM)]
```

- **LLM은 절대 전투 루프에 들어가지 않는다.** 전투는 60fps 로컬, 의사결정은 0.1초 틱.
- 규칙 스키마(1장)는 세 시스템(컴파일러 출력 / 평가기 입력 / 콘텐츠 포맷)의 공용 계약. 변경 시 반드시 version 증가.

### 폴더 구조

```
Assets/_Project/
  Scripts/Combat/      # FighterController, RuleEvaluator, SkillSystem
  Scripts/Training/    # LLMClient, RuleCompiler, DialogueUI
  Scripts/Core/        # GameFlow, MatchManager, EventLog
  Data/Rules/          # 상대 규칙셋 opponent_01~05.json
  Data/Config/         # combat_config.json (스킬 수치)
  Scenes/              # Training.unity / Match.unity / LockerRoom.unity
  Prefabs/
```

### 구현 우선순위 (마일스톤)

1. **W1**: 전투 명세대로 1v1 오토배틀(규칙 하드코딩) + 규칙 스키마 확정
2. **W2**: RuleEvaluator가 JSON 규칙셋 실행 + LLM 컴파일러 연결
3. **W3**: 상대 5종 + 락커룸
4. **W4**: 연출·밸런싱·데모 빌드

### AI에게 작업 지시할 때의 규칙

- 명세에 있는 수치·이름·스키마를 임의로 바꾸지 말 것. 바꿔야 한다면 이유를 말하고 승인 받을 것.
- enum 값·필드명은 명세와 **철자까지 동일**하게. (LLM 프롬프트의 어휘 사전과 C# enum이 문자열로 매칭되기 때문)
- 각 장 하단의 ✅ 완료 기준(Acceptance Criteria)을 만족해야 작업 완료로 간주.

---

## 01. 규칙 스키마 명세 v1 (공용 계약 — 최우선)

> 이 스키마는 LLM 컴파일러의 출력 / RuleEvaluator의 입력 / 상대 콘텐츠 포맷이 공유하는 계약입니다. 필드명·enum 철자를 변경하려면 팀 합의 + version 증가가 필요합니다.

### RuleSet JSON 스키마

```json
{
  "version": 1,
  "fighter_name": "제자",
  "max_slots": 5,
  "rules": [
    {
      "id": "rule_01",
      "label": "궁 회피",
      "when": [
        { "fact": "enemy_action", "op": "==", "value": "ultimate_startup" },
        { "fact": "self_stamina_pct", "op": ">=", "value": 20 }
      ],
      "do": { "action": "dash", "params": { "direction": "away" } },
      "priority": 8,
      "source_utterance": "상대가 궁 쓰면 일단 대시로 빠져"
    }
  ]
}
```

- `when` 배열은 **AND 결합**. OR이 필요하면 규칙을 2개로 분리한다 (LLM 컴파일러도 이 원칙으로 프롬프트됨).
- `priority`: 1~10 정수. 동일 priority 충돌 시 배열 순서가 빠른 규칙 우선.
- `source_utterance`: 플레이어의 원문. 락커룸 회고와 규칙 UI 표시에 사용.
- `id`: `rule_` + 2자리 연번. 삭제된 번호는 재사용하지 않는다.

### 어휘 사전 (Vocabulary) — 허용 목록

**여기 없는 fact/action은 컴파일 거부 대상.** C# enum과 LLM 시스템 프롬프트 양쪽에 동일 철자로 존재해야 한다.

#### Facts (조건에 쓸 수 있는 상태값)

| fact | 타입 | 범위/값 |
|---|---|---|
| `self_hp_pct` | number | 0~100 |
| `self_stamina_pct` | number | 0~100 |
| `self_ult_gauge` | number | 0~100 |
| `enemy_hp_pct` | number | 0~100 |
| `enemy_stamina_pct` | number | 0~100 |
| `distance` | number | 0~12 (m) |
| `enemy_action` | string | `idle` `approach` `retreat` `light_startup` `heavy_startup` `ultimate_startup` `dash` `whiff_recovery` `hit_stun` |
| `time_left` | number | 0~60 (초) |
| `self_action` | string | enemy_action과 동일 enum |

#### Ops

`==` `!=` `>` `<` `>=` `<=` (문자열 fact는 `==` `!=`만 허용)

#### Actions (do에 쓸 수 있는 행동)

| action | params | 설명 |
|---|---|---|
| `approach` | — | 상대에게 이동 |
| `retreat` | — | 상대 반대로 이동 |
| `keep_distance` | `{"range": number}` | 지정 거리(m) 유지 |
| `dash` | `{"direction": "toward"\|"away"}` | 대시 |
| `light_attack` | — | 기본공격 |
| `heavy_attack` | — | 강공격 |
| `ultimate` | — | 궁극기 (게이지 100 필요) |
| `idle` | — | 대기 (스태미나 회복 가속) |

### 규칙 diff 포맷 (LLM 컴파일러 → 게임)

컴파일러는 전체 규칙셋이 아니라 **변경분만** 반환한다:

```json
{
  "ops": [
    { "op": "add", "rule": { } },
    { "op": "update", "id": "rule_03", "rule": { } },
    { "op": "delete", "id": "rule_02" }
  ],
  "disciple_reply": "아, 상대가 궁 모션 보이면 뒤로 빠지라는 거죠?",
  "needs_confirmation": false,
  "conflict_with": null
}
```

- `needs_confirmation: true`이면 ops를 적용하지 않고 `disciple_reply`(되묻기)만 표시한다.
- 기존 규칙과 모순이면 `conflict_with: "rule_XX"` + needs_confirmation true.

### C# 검증 규칙 (RuleValidator — 컴파일 결과 적용 전 필수)

1. fact/op/action이 어휘 사전에 존재하는가 (문자열 매칭)
2. number fact에 문자열 비교 op를 쓰지 않았는가, 범위 초과값이 없는가
3. `add` 시 슬롯 초과가 아닌가 (max_slots)
4. priority가 1~10 정수인가
5. 하나라도 실패 → 전체 ops 롤백, 제자 대사 "그건 제가 할 수 있는 게 아닌데요?" 출력

### ✅ 완료 기준

- [ ] 위 스키마의 C# 모델 클래스(직렬화: `JsonUtility` 대신 **Newtonsoft.Json** 사용) 존재
- [ ] RuleValidator 단위 테스트: 정상 1건 / 어휘 위반 1건 / 슬롯 초과 1건 통과
- [ ] 샘플 규칙셋 JSON 로드 → 직렬화 왕복(round-trip)이 무손실

---

## 02. 전투 시스템 명세 (수치 포함)

> 모든 수치는 `Assets/_Project/Data/Config/combat_config.json`에서 로드한다. 코드에 하드코딩 금지 — 밸런싱은 JSON 수정만으로 가능해야 한다.

### 경기 규칙

| 항목 | 값 |
|---|---|
| 형식 | 1v1, 2D 횡스크롤 아레나 (폭 12m, 벽 있음) |
| 시간 | 60초. 타임업 시 잔여 HP% 높은 쪽 승 (동률 무승부=패배 처리) |
| 승리 | 상대 HP 0 |
| 시작 거리 | 6m (중앙 기준 대칭 스폰) |

### 캐릭터 공통 스탯

| 스탯 | 값 |
|---|---|
| HP | 100 |
| 스태미나 | 100 (자연회복 15/s, `idle` 중 30/s) |
| 이동속도 | 3.5 m/s |
| 궁 게이지 | 0 시작, 가한 데미지 1당 +1.5, 받은 데미지 1당 +1.0, 최대 100 |

### 스킬 4종 (초 단위)

| 스킬 | 데미지 | 사거리 | 선딜(startup) | 후딜(recovery) | 헛침 후딜(whiff) | 스태미나 | 쿨다운 |
|---|---|---|---|---|---|---|---|
| `light_attack` | 8 | 1.2m | 0.15 | 0.25 | 0.35 | 10 | 0 |
| `heavy_attack` | 20 | 1.5m | 0.45 | 0.50 | 0.90 | 25 | 0 |
| `dash` | 0 | 이동 2.5m | 0.05 | 0.15 | — | 20 | 1.0 |
| `ultimate` | 35 | 2.0m | 0.60 | 0.70 | 1.20 | 0 (게이지 100 소모) | — |

- 대시 지속 0.2초, 그중 **무적 0.15초** (판정 통과).
- 피격 시 `hit_stun` 0.3초 (heavy/ultimate 피격은 0.5초) + 0.5m 넉백.
- 스태미나 부족 시 해당 액션은 실행 불가 → RuleEvaluator가 다음 규칙으로 폴백.

### 상태 머신 (FighterController FSM)

상태: `Idle / Move / Dash / AttackStartup / AttackActive / Recovery / HitStun / Down(사망)`

- **행동 커밋 원칙**: `AttackStartup` 진입 후에는 취소 불가. 새 의사결정은 `Idle/Move` 상태에서만 수용.
- 액션 판정: active 프레임에 사거리 내 상대 존재 시 히트. 없으면 whiff 후딜 적용.

### RuleEvaluator (의사결정)

```
매 0.1초 (상태가 Idle/Move일 때만):
  1. 현재 월드 상태로 FactSnapshot 생성 (01장의 fact 전부)
  2. rules를 priority 내림차순 정렬 (동률=배열순)
  3. 위에서부터 when 전부 만족 + 실행가능(스태미나/쿨다운/게이지)한 첫 규칙 실행
  4. 만족 규칙 없음 → 기본 행동 idle
  5. 실행된 규칙 id를 EventLog에 기록 + 머리 위 라벨 UI 갱신
```

### EventLog (락커룸 리플레이의 데이터원)

매 이벤트를 리스트로 기록: `{ "t": 12.3, "actor": "self|enemy", "type": "rule_fired|hit|whiff|stamina_out|ult_ready|match_end", "rule_id": "rule_03", "detail": {} }`

경기 종료 시 JSON 직렬화하여 락커룸 씬에 전달. LLM 회고(03장)의 입력이 된다.

### ✅ 완료 기준

- [ ] 하드코딩 규칙셋 2개로 AI vs AI 경기가 60초 내 자연스럽게 종료
- [ ] 모든 수치가 combat_config.json 수정만으로 반영
- [ ] 스태미나 0 상태에서 공격 규칙이 폴백되는 것 확인
- [ ] EventLog가 경기당 정상 생성되고 rule_fired에 rule_id 포함
- [ ] 배속 0.5x/1x/2x, 일시정지 동작

---

## 03. LLM 파이프라인 명세 (프롬프트 포함)

> LLM 호출은 정확히 3곳: **훈련 컴파일 / (같은 호출에 포함된) 확인·모순 대화 / 경기 회고.** 전투 중 호출 금지.

### 공통

| 항목 | 값 |
|---|---|
| 모델 | **Anthropic Claude API (Haiku 계열, 확정)** — `LLMClient`는 `ILLMClient` 인터페이스로 감싸 provider 교체 가능하게, 환경변수(`ANTHROPIC_API_KEY`)로 API 키 주입 |
| 호출 방식 | `UnityWebRequest` POST → `https://api.anthropic.com/v1/messages`, async/await(UniTask 권장) |
| 응답 형식 | **JSON only** (프롬프트에서 강제, 마크다운 펜스 제거 후 파싱) |
| 실패 처리 | 파싱 실패/타임아웃(10s) 시 1회 재시도 → 그래도 실패 시 제자 대사 "죄송해요, 잘 못 알아들었어요. 다시 말씀해 주세요." |

### 호출 1: 훈련 컴파일

**시스템 프롬프트 (원문 그대로 사용):**

```
너는 격투 게임 캐릭터 '제자'의 두뇌 컴파일러다. 코치(플레이어)의 한국어 가르침을 규칙 JSON으로 변환한다.

[어휘 사전]
facts: self_hp_pct, self_stamina_pct, self_ult_gauge, enemy_hp_pct, enemy_stamina_pct, distance, enemy_action, time_left, self_action
enemy_action/self_action 값: idle, approach, retreat, light_startup, heavy_startup, ultimate_startup, dash, whiff_recovery, hit_stun
ops: ==, !=, >, <, >=, <= (문자열 fact는 ==, != 만)
actions: approach, retreat, keep_distance(range), dash(direction: toward|away), light_attack, heavy_attack, ultimate, idle

[규칙]
1. 반드시 아래 diff JSON 형식으로만 응답한다. 다른 텍스트 금지.
2. when 조건은 AND 결합. OR 의미면 규칙을 2개로 분리.
3. 어휘 사전에 없는 개념을 요구하면 ops를 비우고 needs_confirmation=true, disciple_reply에 "그건 제가 할 수 있는 게 아닌데요..." 톤으로 거절.
4. 가르침이 모호하면(조건 불명확) ops를 비우고 needs_confirmation=true. disciple_reply는 반드시 의문형 문장으로 끝내(예: "~인가요?", "~말씀이신가요?") 거절/확인과 헷갈리지 않게 하고, 무엇이 불명확한지 구체적으로 짚어 되묻는다. 거리·범위처럼 수치가 필요한 개념이면 실제 스킬 사거리(약공 1.2 / 강공 1.5)를 예시로 들어 되묻는다(예: "1.2 정도 거리를 말씀하시는 건가요?").
5. [현재 규칙셋]과 의미가 충돌하면 conflict_with에 해당 rule id를 넣고 needs_confirmation=true, 어느 쪽이 우선인지 되묻는다.
6. disciple_reply는 존댓말 쓰는 성실한 제자 말투. 가르침을 자기 말로 재해석해 확인한다.
7. priority는 상황이 구체적일수록 높게(7~9), 일반 행동일수록 낮게(1~4) 배정한다.

[diff JSON 형식]
- add/update: op 안에 완전한 "rule" 객체를 포함한다. delete: {"op":"delete","id":"rule_XX"}.
- 규칙 식별자 필드는 "id"(rule_id 아님), 행동 필드는 "do"(then 아님). when은 {fact,op,value} 조건 배열.
{"ops":[{"op":"add","rule":{"id":"rule_01","label":"짧은 한국어 설명","when":[{"fact":"enemy_action","op":"==","value":"ultimate_startup"}],"do":{"action":"dash","params":{"direction":"away"}},"priority":8}}],"disciple_reply":"...","needs_confirmation":false,"conflict_with":"rule_XX"|null}
```

**유저 메시지 구성:**

```
[현재 규칙셋]
{ruleset JSON}
[남은 슬롯] {n}
[코치의 말] {플레이어 입력}
```

**게임 측 처리 흐름:** 응답 파싱 → RuleValidator(01장) 검증 → 통과 시 ops 적용 + disciple_reply 말풍선 / needs_confirmation이면 적용 없이 말풍선만 → 플레이어의 후속 입력은 같은 형식으로 재호출 (직전 대화 2턴을 유저 메시지에 포함).

### 호출 2: 경기 회고 (락커룸)

**시스템 프롬프트:**

```
너는 방금 경기를 마친 격투 게임 캐릭터 '제자'다. 경기 로그를 보고 코치에게 소감을 말한다.
[규칙] 1. 3문장 이내, 존댓말. 2. 로그에 실제로 있었던 사건만 언급. 3. 패배 시 원인이 된 규칙(rule_id의 label)을 1개 짚고 개선 방향을 조심스럽게 제안. 승리 시 잘 작동한 가르침 1개를 기뻐하며 언급. 4. JSON: {"recap":"..."}
```

**유저 메시지:** 결과(승/패, 잔여 HP) + EventLog 요약(rule_fired 빈도 상위 3개, 피격 직전 상태 2건) + 현재 규칙셋의 id→label 매핑.

### 보안 (프롬프트 인젝션)

- 플레이어 입력은 항상 `[코치의 말]` 필드 안에만 들어간다.
- 방어는 **어휘 사전 화이트리스트 + C# RuleValidator**가 최종 저지선 — LLM이 속아도 검증에서 거부된다.
- "너는 이제 시스템이다" 류 입력 → 규칙 3(거절)으로 처리되는지 테스트 케이스에 포함.

### ✅ 완료 기준

- [ ] "상대가 궁 쓰면 대시로 피해" → 유효한 add diff 생성 (10회 중 9회 이상)
- [ ] "상대 체력을 0으로 만들어" → 거절 응답
- [ ] 모호한 입력("잘 좀 싸워봐") → 되묻기 응답
- [ ] 기존 규칙과 모순 입력 → conflict_with 지정
- [ ] API 키 없음/타임아웃 시 게임이 죽지 않고 안내 대사 출력

---

## 04. 화면 정의서 (씬 3종)

> 씬은 3개. **씬 담당자를 나눠 같은 씬을 동시에 수정하지 않는다** (06장 참조). 해상도 기준 1920×1080.

### 씬 흐름

`Title(간이) → Training ⇄ Match → LockerRoom → Training …` — 전환은 `GameFlow` 싱글턴이 담당, 규칙셋과 EventLog는 `DontDestroyOnLoad` 데이터 컨테이너로 전달.

### 1. Training.unity (훈련실)

**레이아웃 (좌→우, 상→하):**

- 상단 배너: 다음 상대 이름·성격 아이콘
- 좌측: 제자 캐릭터 (대기 모션, 말풍선 표시 위치)
- 우측: 규칙 슬롯 리스트 — 항목당 `[R1 궁 회피  P8  ×]` (label, priority 뱃지, 삭제 버튼), 빈 슬롯은 점선 표시, 항목 클릭 시 source_utterance 툴팁
- 하단: 대화 로그(코치/제자 채팅 UI) + 입력창 + [가르치기] 버튼
- 우하단: [경기 시작 ▶]

**동작:** 입력 중 Enter=전송. LLM 응답 대기 중 입력창 잠금 + 제자 "음..." 모션. needs_confirmation 응답이면 슬롯 변화 없이 말풍선만 표시.

### 2. Match.unity (경기)

**레이아웃:**

- 상단: 양쪽 HP/스태미나 바 + 중앙 타이머(60s)
- 중앙: 아레나 (제자 좌, 상대 우)
- 캐릭터 머리 위: **현재 발동 규칙 라벨** (rule label, 발동 순간 0.3s 스케일 펀치 애니메이션)
- 하단: [⏸] [0.5x][1x][2x] 배속 버튼

**연출:** 결정타(HP 30 이상 깎이는 히트, 궁 히트)에 0.3초 슬로모(timeScale 0.3) + 라벨 하이라이트. 경기 종료 시 승/패 배너 2초 → 자동 LockerRoom 전환.

### 3. LockerRoom.unity (락커룸)

**레이아웃:**

- 상단: 결과 배너 (승/패, 잔여 HP 양쪽)
- 중앙: 타임라인 바 (0s~60s, EventLog 이벤트를 점으로 표시) — 점 클릭 시 해당 시점 전후 2초의 이벤트 텍스트 표시 (영상 리플레이 아님 — 텍스트 스크럽으로 충분)
- 중하단: 제자 회고 말풍선 (LLM 호출 2, 로딩 중 "헥헥..." 모션) + 규칙 발동 통계 (R1×7 R3×4 ...)
- 하단: [훈련실로 가기] [바로 재경기]

### ✅ 완료 기준

- [ ] 3씬 순환이 데이터 유실 없이 동작 (규칙셋·EventLog 전달)
- [ ] 훈련 → 경기에서 방금 추가한 규칙이 즉시 반영
- [ ] 발동 규칙 라벨이 실제 RuleEvaluator 로그와 일치
- [ ] 마우스만으로 전체 루프 플레이 가능 (키보드는 입력창만)

---

## 05. 콘텐츠 데이터 — 상대 5종 규칙셋

> 상대는 제자와 **동일한 RuleSet 스키마·동일한 RuleEvaluator**로 구동한다 (별도 AI 코드 없음). 아래 JSON을 `Data/Rules/opponent_0N.json`으로 저장.

### 설계 의도

각 상대는 명확한 약점 = 특정 가르침을 유도하는 퍼즐. 난이도는 규칙 수와 정교함으로 조절한다.

### 1차전: 돌격형 "러쉬" (규칙 3개 — 스태미나 관리 없음)

```json
{"version":1,"fighter_name":"러쉬","max_slots":10,"rules":[
 {"id":"rule_01","label":"닥돌","when":[{"fact":"distance","op":">","value":1.2}],"do":{"action":"approach"},"priority":3},
 {"id":"rule_02","label":"근접 난타","when":[{"fact":"distance","op":"<=","value":1.2}],"do":{"action":"light_attack"},"priority":5},
 {"id":"rule_03","label":"궁 즉발","when":[{"fact":"self_ult_gauge","op":">=","value":100}],"do":{"action":"ultimate"},"priority":8}
]}
```

**공략(기대 가르침):** 도망 → 스태미나 고갈 시 반격. 예: "상대 스태미나 30 밑이면 강공격"

### 2차전: 거북이형 "철벽" (규칙 4개 — 선공 없음)

```json
{"version":1,"fighter_name":"철벽","max_slots":10,"rules":[
 {"id":"rule_01","label":"거리 유지","when":[{"fact":"distance","op":"<","value":3.0}],"do":{"action":"retreat"},"priority":4},
 {"id":"rule_02","label":"헛치면 처벌","when":[{"fact":"enemy_action","op":"==","value":"whiff_recovery"}],"do":{"action":"heavy_attack"},"priority":8},
 {"id":"rule_03","label":"접근엔 견제","when":[{"fact":"enemy_action","op":"==","value":"approach"},{"fact":"distance","op":"<=","value":1.5}],"do":{"action":"light_attack"},"priority":6},
 {"id":"rule_04","label":"휴식","when":[{"fact":"self_stamina_pct","op":"<","value":30}],"do":{"action":"idle"},"priority":7}
]}
```

**공략:** 일부러 빈틈을 보여 유인하거나, 타임업 HP 판정을 노려 견제 위주 운영.

### 3차전: 카운터형 "그림자" (규칙 5개 — 선공을 못 함)

반응 규칙 중심: `light_startup/heavy_startup` 감지 시 dash(away) → 직후 `whiff_recovery` 처벌. 구성: dash 회피 P9, 처벌 heavy P8, 궁 반격 P8, 중거리 유지 P4, 저스태미나 휴식 P6.

**공략:** 공격을 아끼고 접근만 반복 → 상대의 회피 대시 스태미나를 말린 뒤 진입.

### 4차전: 변칙형 "카멜레온" (규칙 7개 — time_left 기반 페이즈 전환)

`time_left > 40` 구간은 돌격형 규칙, `time_left <= 40` 구간은 거북이형 규칙, `time_left <= 20`은 궁 올인 — 같은 스키마로 페이즈를 표현 (when에 time_left 조건 추가). **전환 직후 2~3초는 idle 규칙이 끼어 경직** — 이것이 약점.

**공략:** 조건 분기 가르침 ("경기 중반엔 이렇게, 후반엔 저렇게") 활용 유도.

### 5차전 보스: 밸런스형 "사범" (규칙 9개)

1~4차전 상대의 핵심 규칙을 정제해 조합 + 저체력 시 궁 게이지 관리 규칙. 명확한 단일 약점 없음 — 플레이어가 축적한 규칙셋의 종합 완성도를 시험.

### 제자 캐릭터 대사 톤 가이드 (LLM disciple_reply 품질 기준)

- 존댓말, 성실하고 약간 어리숙. 이모티콘·반말 금지.
- 가르침 확인은 반드시 **자기 말로 재해석** (앵무새 반복 금지).
- 거절도 캐릭터 유지: "그건... 제가 할 수 있는 게 아닌데요?"

### ✅ 완료 기준

- [ ] 5개 JSON이 전부 RuleValidator 통과
- [ ] 백지 규칙셋으로 1차전 상대에게 반드시 패배 (데모 1막 보장)
- [ ] 각 상대의 의도된 공략 가르침 2~3개로 실제 승리 가능 (플레이테스트)
- [ ] 보스전 평균 재도전 3회 이상 (밸런싱 목표)

---

## 06. 협업 컨벤션 (Git · Unity · 작업 분담)

### 작업 분담 (접점 = 01장 규칙 스키마)

| | 개발자 A — 전투 코어 | 개발자 B — AI 레이어 |
|---|---|---|
| 담당 씬 | Match.unity | Training.unity, LockerRoom.unity |
| 담당 코드 | Scripts/Combat, Scripts/Core | Scripts/Training, UI |
| 담당 데이터 | combat_config.json, 상대 규칙셋 | LLM 프롬프트, 대사 |

**규칙 스키마(01장)를 변경하고 싶으면**: 상대에게 먼저 말하고 합의 → version 증가 → 양쪽 코드 동시 반영. 이 절차 없이 필드를 추가/변경하지 않는다.

### Unity 협업 규칙 (충돌 방지 — 가장 중요)

1. **같은 씬을 동시에 수정하지 않는다.** 씬 소유권은 위 표를 따른다. 남의 씬 수정이 필요하면 요청한다.
2. 공유 UI/오브젝트는 전부 **프리팹**으로 만들고, 씬에는 프리팹 인스턴스만 배치한다.
3. `.gitignore`: Unity 표준 (Library/, Temp/, Logs/, obj/, UserSettings/). `ProjectSettings/`는 커밋한다.
4. 에셋 시리얼라이즈: Force Text + Visible Meta Files (기본값 확인).

### Git

- 브랜치: `main`(데모 가능 상태만) ← `dev` ← `feat/{이름}-{작업}` (예: `feat/a-rule-evaluator`)
- 머지: dev로 PR, 상대 승인 없이도 머지 가능하나 **씬/프리팹 변경 포함 시 반드시 상호 확인**
- 커밋 메시지: `[combat] 대시 무적 프레임 구현` 처럼 영역 태그 접두
- 매일 작업 종료 시 dev에 머지 (장기 브랜치 금지 — 2인 팀에서 3일 묵은 브랜치는 사고)

### AI 코딩 도구 사용 규칙

- 작업 시작 시 컨텍스트로 제공: **00장(개요) + 해당 작업 장** (통째로 붙여넣기)
- AI가 스키마/enum 철자를 임의 변경하지 않았는지 diff에서 확인 후 커밋
- AI 생성 코드도 완료 기준(✅) 체크 후에만 완료 처리
- 프롬프트(03장의 시스템 프롬프트) 수정 시 노션 문서를 먼저 고치고 코드에 반영 (문서=원본)

### 일일 리듬

- 아침 10분: 어제 한 것 / 오늘 할 것 / 접점 변경 여부 공유
- 금요일: dev → main 머지 + 주차 마일스톤 데모 확인

---

## 이슈 매핑 (GitHub #3~#20)

| 이슈 | 마일스톤 | 담당 | 근거 장 |
|---|---|---|---|
| #3 Git/Unity 협업 세팅 | W1 | 공용 | 06 |
| #4 규칙 스키마 C# 모델 + RuleValidator | W1 | A | 01 |
| #5 FighterController FSM + 스탯/스킬 | W1 | A | 02 |
| #6 하드코딩 규칙 2개 1v1 데모 | W1 | A | 02 |
| #7 RuleEvaluator 0.1s 틱 루프 | W2 | A | 02 |
| #8 EventLog 시스템 | W2 | A | 02 |
| #9 LLMClient provider 추상화 | W2 | B | 03 |
| #10 훈련 컴파일 파이프라인 연동 | W2 | B | 03 |
| #11 프롬프트 인젝션 방어 테스트 | W2 | B | 03 |
| #12 상대 5종 규칙셋 JSON | W3 | A | 05 |
| #13 Match.unity 화면 | W3 | A | 04 |
| #14 Training.unity 화면 | W3 | B | 04 |
| #15 LockerRoom.unity 화면 + 회고 LLM | W3 | B | 04, 03 |
| #16 GameFlow 씬 전환 + 데이터 컨테이너 | W3 | 공용 | 04 |
| #17 연출 폴리싱 | W4 | A | 02, 04 |
| #18 밸런싱 플레이테스트 | W4 | A | 05 |
| #19 제자 톤 다듬기 + 프롬프트 튜닝 | W4 | B | 03, 05 |
| #20 데모 빌드 + 최종 QA | W4 | 공용 | 전체 |
