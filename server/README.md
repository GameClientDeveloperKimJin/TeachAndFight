# TEACH & FIGHT — LLM 프록시 서버

Unity 클라이언트가 Anthropic을 직접 호출하지 않도록, API 키를 대신 보관·호출하는 최소 서버리스 함수입니다.
클라이언트 빌드에는 키가 전혀 들어가지 않고, 이 서버의 **환경변수**에만 키가 존재합니다.

```
Unity(클라) ──{system,user}──▶ /api/compile ──x-api-key──▶ Anthropic
            ◀──원본 JSON 응답──            ◀────────────────
```

## 배포 (Vercel)

1. 이 `server/` 폴더를 Vercel에 배포합니다.
   - GitHub 연동: Vercel → New Project → 이 저장소 선택 → **Root Directory 를 `server` 로 지정**.
   - 또는 CLI: `npm i -g vercel && cd server && vercel`.
2. Vercel 프로젝트 → **Settings → Environment Variables** 에 아래를 등록합니다.
   - `LLM_API_KEY` = 실제 Anthropic 키 (`sk-ant-...`)
3. 배포하면 엔드포인트가 생깁니다: `https://<프로젝트>.vercel.app/api/compile`
4. Unity 쪽 `Assets/03_JM/Scripts/Training/LLM/LLMSettings.cs` 의 `ProxyEndpoint` 를 위 URL로 교체합니다.
   (교체 전에는 자리표시자로 인식되어 게임이 폴백 대사만 출력합니다.)

## 로컬 테스트

```bash
cd server
cp .env.example .env      # .env 에 실제 키 입력 (커밋되지 않음)
npx vercel dev            # http://localhost:3000/api/compile
```

```bash
curl -X POST http://localhost:3000/api/compile \
  -H "content-type: application/json" \
  -d '{"system":"너는 규칙 컴파일러다","user":"체력이 절반이면 방어해"}'
```

## 계약 (Unity ⇄ 서버)

- 요청: `POST { "system": string, "user": string }`
- 응답: Anthropic Messages API 원본 JSON 그대로 전달 → Unity `LLMResponseParser` 가 파싱.
- 모델/토큰(`claude-haiku-4-5`, 1024)은 **서버에서 확정** — 클라이언트가 바꿀 수 없습니다.

## 남용 방지

- 메서드 제한(POST만), 입력 길이 제한(system 8000자 / user 2000자).
- 동일 IP 분당 20회 인메모리 rate limit(시연용 최소 방어).
  - 서버리스 특성상 완벽하지 않음. 엄격히 하려면 Vercel KV / Upstash Redis 로 교체.

## 보안 체크리스트

- [ ] `LLM_API_KEY` 는 Vercel 환경변수에만 있고 코드/저장소에 없다.
- [ ] `.env` 는 커밋되지 않는다(.gitignore 등록됨).
- [ ] 저장소 과거 커밋 히스토리에도 키가 들어간 적이 없다.
