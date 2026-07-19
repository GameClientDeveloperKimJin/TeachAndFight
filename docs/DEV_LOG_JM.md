# 개발 로그 — 개발자 B (JM)

> AI 레이어(LLM 파이프라인 / Training·LockerRoom) 담당. 근거 문서: `docs/DEV_SPEC.md` 03·04장, `docs/IMPLEMENTATION_PLAN.md`.

---

## #9 LLMClient (W2 · 근거 03장)

### 목표

이 게임은 전투에 직접 개입하지 않고 **자연어로 제자를 가르치는** 구조라, 플레이어의 한국어 지시를 규칙 JSON으로 바꿔줄 LLM 호출이 필요하다. #9는 그 호출의 **기반(transport) 계층** — "어떻게 안전하게 부르고, 실패해도 게임이 죽지 않게 하느냐"를 담당한다.

- LLM은 전투 루프에 절대 들어가지 않는다. 호출 지점은 훈련 컴파일 / 경기 회고 두 곳뿐.
- 실제 프롬프트를 규칙에 반영·검증(RuleValidator 연결)하는 파이프라인은 **#10**에서 이어서 한다.

### 결정 사항

| 항목 | 값 | 근거 |
|---|---|---|
| 모델 | `claude-haiku-4-5` (상수로 고정, 교체 쉬움) | 03장 "Haiku 계열 확정" |
| 호출 | `UnityWebRequest` POST → `api.anthropic.com/v1/messages`, UniTask async | 03장 공통 |
| API 키 | 환경변수 `ANTHROPIC_API_KEY`로만 주입, 하드코딩 금지 | 계획서 사전준비 3 |
| 실패 처리 | 타임아웃 10s · 1회 재시도 → 폴백 대사 | 03장 |
| 응답 | JSON only, 마크다운 펜스 제거 후 파싱 | 03장 |

### 만든 파일 (`Assets/03_JM/`)

| 파일 | 역할 |
|---|---|
| `Scripts/Training/LLM/ILLMClient.cs` | provider 교체 가능한 인터페이스 + 결과/실패사유 타입 |
| `Scripts/Training/LLM/AnthropicLLMClient.cs` | Anthropic Haiku 구현 — 호출/타임아웃/재시도/폴백 |
| `Scripts/Training/LLM/LLMResponseParser.cs` | 응답 텍스트 추출 + 마크다운 펜스 제거 (순수 함수) |
| `Scripts/Training/LLM/TrainingPromptBuilder.cs` | 03장 원문 시스템 프롬프트 + 유저 메시지 조립 |
| `Scripts/Training/LLM/LLMSettings.cs` | 모델·타임아웃·엔드포인트 상수 |
| `Scripts/Training/LLM/AnthropicDto.cs` | Messages API 요청/응답 데이터 모델 |
| `Editor/LLMVerificationMenu.cs` | 키 준비 시 메뉴에서 샘플 호출 품질 확인 |
| `Tests/EditMode/*Tests.cs` | 아래 검증 |
| `*.asmdef` ×3 | Training(런타임) / Editor / Tests.Editor 어셈블리 |

### EditMode 테스트 (통과 확인)

Unity Test Runner(EditMode) Run All → 통과. 콘솔의 폴백 경고 2개는 "키 없음" 테스트가 실제로 폴백을 탄 정상 신호.

검증 항목:
1. 정상 Anthropic 응답 파싱
2. 마크다운 ` ```json ` 펜스 제거
3. 잘못된 JSON / 빈 응답 → 실패 사유 반환
4. **키 없을 때 게임 안 죽고 폴백 대사** (03장 완료기준)
5. 시스템 프롬프트에 어휘사전·diff 형식 마커 유지
6. 인젝션 입력이 `[코치의 말]` 필드 안에 갇힘

### 남은 것 (#9 완료 처리 조건)

- [ ] 실제 API 키로 정상 호출 1회 → 응답 파싱 성공 확인 (03장 완료기준)
- [ ] 키 제거 후 폴백 1회 확인 (자동 테스트로 이미 커버, 실호출 환경에서 재확인)

### AI 활용 방식 (해커톤 기록용)

- **스펙 우선**: 코드 전에 03장 원문·완료기준을 컨텍스트로 확정하고 착수 (재작업 방지).
- **컨벤션 잠금**: 기존 KJ 코드(namespace `TeachAndFight.*`, `[JsonProperty]` snake_case, feature별 asmdef) 스타일을 먼저 추출해 동일하게 생성.
- **셀프 리뷰**: 생성 직후 P0(프레임 의존)~P3(매직넘버) 체크리스트로 자체 검증 — 상태 누수/키 하드코딩 없음 확인.
- **순수 함수 분리**: 파싱·프롬프트 조립을 네트워크와 분리해 EditMode에서 자동 검증 가능하게 설계.

### 커밋

```
[training] LLMClient(ILLMClient/Anthropic Haiku) + 03장 프롬프트 빌더 + 폴백/파싱 테스트 (#9)
```

---

## #10 훈련 컴파일 파이프라인 (W2 · 근거 03·01장)

### 목표

#9(LLMClient)와 KJ의 #4(RuleValidator)를 잇는 **오케스트레이션**. 자연어 한 마디를 검증된 규칙 변경으로 바꿔 규칙셋에 반영한다. 03장 "게임 측 처리 흐름"을 구현.

```
코치 발화 + 현재 규칙셋 + 남은 슬롯
  → TrainingPromptBuilder(프롬프트)
  → ILLMClient.CompleteAsync
  → RuleDiffParser(응답 JSON → RuleDiff)
  → needs_confirmation/conflict 분기
  → RuleValidator.ApplyOps(최종 저지선)
  → TrainingCompileResult
```

### 결과 상태 4가지 (03장 완료기준 4케이스와 1:1)

| Outcome | 조건 | 처리 |
|---|---|---|
| `Applied` | needs_confirmation=false + ApplyOps 성공 | 규칙셋 갱신 + disciple_reply |
| `NeedsConfirmation` | needs_confirmation=true | 적용 안 함 — 거절/되묻기/모순 전부 여기(모순은 conflict_with 세팅) |
| `Rejected` | needs_confirmation=false 인데 ApplyOps 실패 | 적용 안 함, 고정 거절 대사(LLM이 무효 규칙을 냈을 때 최종 저지선) |
| `Failed` | LLM 호출/파싱 실패 | 폴백 대사, 적용 안 함 |

- 거절/되묻기/모순은 LLM이 전부 `needs_confirmation=true`로 돌려주므로 한 분기로 처리, 차이는 대사 톤과 conflict_with.
- **Rejected 대사**: `"무슨 말인지 모르겠어요. 다시 알려주시겠어요?"` (상수, 존댓말 톤). LLM이 붙인 대사는 무효 규칙과 함께 온 거라 신뢰하지 않음.
- 대화 맥락 2턴 전달은 지금 미배선(단일 턴만) — #14 UI에서 연결 예정.

### 만든 파일 (`Assets/03_JM/Scripts/Training/`)

| 파일 | 역할 |
|---|---|
| `TrainingCompileResult.cs` | Outcome enum + 결과 타입(갱신 규칙셋/대사/conflict/오류) |
| `RuleDiffParser.cs` | 응답 텍스트 → `RuleDiff` 파싱(펜스 재제거 방어), 순수 함수 |
| `TrainingCompiler.cs` | 파이프라인 오케스트레이션 |
| `Tests/EditMode/TrainingCompilerTests.cs` | 가짜 ILLMClient로 4케이스 검증 |
| `Tests/EditMode/RuleDiffParserTests.cs` | 파싱/펜스/오류 |

### 완료 기준 대비

- [x] 정상 add diff 생성·반영 (Applied) — 자동 테스트
- [x] 거절 케이스 동작 (NeedsConfirmation)
- [x] 되묻기 케이스 동작 (NeedsConfirmation)
- [x] 모순 케이스 conflict_with 지정 (NeedsConfirmation)
- [x] 무효 규칙 최종 저지(Rejected) + LLM 실패 폴백(Failed)
- [ ] **"10회 중 9회 이상 유효 diff"는 실제 API 키로 수동 측정 필요** (LLM 응답 특성상 자동화 범위 밖 — 03장/계획서 명시)

### AI 활용 방식 (해커톤 기록용)

- **스펙 우선(game-spec-first)**: 코딩 전 결과 상태 4가지·엣지케이스를 표로 확정하고 착수 → 재작업 0.
- **기존 자산 재사용**: KJ의 `RuleValidator.ApplyOps`(깊은 복사 롤백)를 그대로 최종 저지선으로 사용, 중복 구현 안 함.
- **순수 함수 + 의존성 주입**: `ILLMClient`를 주입받아 가짜 클라이언트로 네트워크 없이 4케이스 자동 검증.

### 커밋

```
[training] 훈련 컴파일 파이프라인 TrainingCompiler + RuleDiff 파서 + 4케이스 테스트 (#10)
```

---

## 실제 LLM 검증 & 디버깅 기록 (#9·#10 B 항목 클리어)

### 환경 세팅

- `ANTHROPIC_API_KEY` 환경변수 등록 → **Unity Hub 트레이까지 완전 종료 후 재시작**해야 인식됨(프로세스가 시작 시 환경변수를 읽기 때문). 재시작 전엔 `키 감지: False`.
- 크레딧 없으면 실호출이 HTTP 400 `credit balance too low` → 폴백. Plans & Billing에서 크레딧 충전 후 해결.

### 버그: 자동 테스트는 통과했는데 실제 LLM은 Rejected

`훈련 컴파일 파이프라인 검증` 첫 실행 결과 `Outcome=Rejected / errors: rule 누락`.

원인 = **프롬프트가 규칙 JSON 구조를 안 가르쳐줌.** 실제 LLM 응답과 우리 스키마(01장) 불일치:

| 항목 | LLM 출력 | 우리 스키마 |
|---|---|---|
| 규칙 위치 | op에 평평하게 | op 안 `rule` 객체로 중첩 |
| 식별자 | `rule_id` | `id` |
| 행동 필드 | `then` | `do` |

→ 파서가 `op.rule`을 못 찾아 null → RuleValidator "rule 누락" → **최종 저지선이 정상적으로 걸러냄**. 스키마(KJ 공용 계약)는 못 바꾸므로 **프롬프트를 스키마에 맞게 수정**.

### 수정

- `docs/DEV_SPEC.md` 03장 `[diff JSON 형식]`을 **완전한 rule 객체 예시 + 필드명 명시**(`id`/`do`/중첩 `rule`)로 교체 → `TrainingPromptBuilder.SystemPrompt`에 동일 반영. (문서=원본 규칙 준수)

### 재검증 결과 (통과)

```
Outcome=Applied / 규칙수 1
추가된 규칙: id=rule_01 label=상대 궁극기 시작 시 대시로 회피 priority=9
            when=enemy_action == ultimate_startup do=dash
```

자연어 → 실제 LLM → 올바른 diff → 검증 통과 → 규칙 반영까지 라이브 확인.

### W2 진입조건 B·공용 최종 상태

- [x] #9 실제 Anthropic 호출→응답 파싱 성공 (라이브)
- [x] 03장 프롬프트로 유효 규칙 diff 1개 생성 (라이브, Applied)
- [x] #9 키없음/타임아웃 폴백 (자동 테스트 + 라이브 400 폴백 확인)
- [x] 공용: B의 diff가 KJ RuleSet 스키마로 파싱·적용됨 (라이브 Applied로 증명, 팀 15분 싱크만 남음)

### 교훈 (해커톤 디버깅 포인트)

**자동 테스트(손으로 만든 완벽한 JSON)만으론 부족하다.** 실제 LLM은 프롬프트가 덜 구체적이면 그럴듯하지만 계약과 다른 구조를 낸다. 수동 실호출 검증이 프롬프트 갭을 드러냈고, "스키마는 고정 계약이라 프롬프트를 맞춘다"가 정답. 검증기(최종 저지선)가 있어 잘못된 구조가 게임에 반영되진 않았다.

### 커밋(예정)

```
[training] 03장 diff 프롬프트에 rule 객체 스키마 명시 + 파이프라인 실호출 검증 메뉴 (#10)
```
