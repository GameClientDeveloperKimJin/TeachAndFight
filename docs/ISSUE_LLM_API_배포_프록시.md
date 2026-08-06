# [배포][공용] LLM API 키 배포 문제 — 프록시 서버 전환

> GitHub 이슈로 붙여넣기용. (자동 등록은 현재 연동 권한 부족(403)으로 막혀 수동 등록 필요)
> 제목: `[배포][공용] LLM API 키 배포 문제 — 프록시 서버 전환`

## 배경
첨부 문서(TEACH & FIGHT — LLM API 키 배포 문제 해결 문서) 참고. 심사/시연을 위한 웹(WebGL)·앱 빌드 배포 시 발생하는 배포 블로커. #9(LLMClient provider 추상화)는 "환경변수/로컬 파일에 키가 존재한다"는 전제라 **배포 환경(키 없음)은 커버하지 못함** — 이 이슈에서 해결한다.

## 문제 정의
개발자는 각자 로컬(.env / 환경변수)에 API 키를 두고 개발해 왔다. 이 상태로 빌드해 배포하면 심사위원/유저 환경에는 키가 없어 LLM 규칙 컴파일이 동작하지 않는다. 빌드는 소스+에셋을 바이너리로 변환할 뿐 개발자 로컬 환경변수를 자동 포함하지 않기 때문.

두 가지 실패 양상 모두 제출 불가:
- ① 키 미포함 빌드 → 인증 실패, 규칙 컴파일·사제 대화 등 핵심 게임플레이 전면 중단.
- ② 키를 클라이언트에 하드코딩 → F12/디컴파일로 키 노출 → 탈취·과금·키 정지 위험.

## 해결책 — 백엔드 프록시 서버
API 키를 클라이언트(Unity 빌드) 밖으로 완전히 분리한다. 팀 소유 서버(Vercel 서버리스)가 키를 환경변수로 보관하고 LLM 호출을 대행한다. 게임은 팀 서버 엔드포인트 하나만 바라본다.

```
변경 전: Unity ──x-api-key──▶ Anthropic         (키 없으면 실패 / 넣으면 유출)
변경 후: Unity ──{system,user}──▶ 프록시(팀 소유) ──키(서버 환경변수)──▶ Anthropic
```

## 구현 (코드 완료됨 — 이 커밋 범위)
- [x] `server/` 서버리스 함수(`api/compile.js`) — `{system,user}` 수신 → `process.env.LLM_API_KEY`로 Anthropic 호출 → 원본 JSON 반환. 메서드/입력길이/IP rate limit 방어 포함. (`package.json`, `vercel.json`, `.env.example`, `.gitignore`, `README.md` 동봉)
- [x] Unity `ProxyLLMClient : ILLMClient` 신규 — 키 없이 프록시만 호출, 기존 `LLMResponseParser` 재사용.
- [x] `LLMClientFactory.CreateDefault()` — 빌드=프록시(키 미포함) / 에디터=로컬 키 직접 호출 자동 선택. 런타임 생성부(`TrainingScreenController`, `LockerRoomController`)를 팩토리로 교체. 기존 `AnthropicLLMClient`·테스트는 유지.
- [x] `LLMSettings.ProxyEndpoint` 추가(자리표시자 → 미배포로 간주해 폴백).

## 남은 배포 작업 (사람이 해야 함)
- [ ] `server/`를 Vercel에 배포(Root Directory=`server`), 대시보드에 `LLM_API_KEY` 환경변수 등록.
- [ ] 발급된 `https://<프로젝트>.vercel.app/api/compile` URL을 `LLMSettings.ProxyEndpoint`에 반영.
- [ ] 실제 배포 빌드(WebGL/앱) + 시연 네트워크에서 종단 테스트.

## 완료 기준 (최종 체크리스트)
- [ ] 로컬 키가 Unity 코드/빌드 어디에도 하드코딩되어 있지 않다.
- [ ] API 키는 프록시 서버 환경변수에만 등록되어 있다.
- [ ] Unity 클라이언트는 Anthropic 주소가 아닌 프록시 주소만 호출한다.
- [ ] 프록시에 입력 길이 제한·요청 빈도 제한이 걸려 있다.
- [ ] 실제 배포 빌드에서 종단 테스트 완료.
- [ ] 네트워크 불안정 대비 폴백 동작 확인(핵심 로직 아님, 시연 안정성용).
- [ ] 저장소 과거 커밋 히스토리에 키가 커밋된 적 없는지 확인.

담당: 개발자 A/B 공동 (배포·환경변수 등록은 저장소 소유자)
관련: #9
