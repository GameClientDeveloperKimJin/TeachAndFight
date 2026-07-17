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

### 커밋(예정)

```
[training] LLMClient(ILLMClient/Anthropic Haiku) + 03장 프롬프트 빌더 + 폴백/파싱 테스트 (#9)
```
