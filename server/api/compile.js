// TEACH & FIGHT — LLM 프록시 서버리스 함수 (Vercel)
//
// 역할: Unity 클라이언트로부터 { system, user } 프롬프트만 받아 Anthropic Messages API를 대신 호출하고,
//       Anthropic 원본 응답 JSON을 그대로 클라이언트에 돌려준다.
// 핵심: API 키(LLM_API_KEY)는 이 서버의 "환경변수"에만 존재한다. 클라이언트 빌드에는 키가 절대 포함되지 않는다.
//
// 배포: Vercel 대시보드 → Settings → Environment Variables 에 LLM_API_KEY 등록 후 배포.
//       배포되면 엔드포인트는 https://<프로젝트>.vercel.app/api/compile 가 된다.
//       그 URL을 Unity 쪽 LLMSettings.ProxyEndpoint 에 넣으면 끝.

// Unity 쪽 LLMSettings 와 값 일치 (모델/토큰은 서버가 확정 — 클라가 임의 변경 불가하게).
const MODEL = "claude-haiku-4-5";
const MAX_TOKENS = 1024;
const ANTHROPIC_URL = "https://api.anthropic.com/v1/messages";
const ANTHROPIC_VERSION = "2023-06-01";

// 남용 방지 상수.
const MAX_SYSTEM_LEN = 8000; // 시스템 프롬프트 상한(자수)
const MAX_USER_LEN = 2000;   // 사용자 입력 상한(자수) — 규칙 텍스트 + 현재 규칙셋 포함
const RATE_WINDOW_MS = 60_000;
const RATE_MAX_PER_WINDOW = 20; // 동일 IP 분당 최대 요청 수

// 아주 단순한 인메모리 rate limit (해커톤 시연용 최소 방어).
// 주의: 서버리스는 인스턴스가 여러 개 뜨거나 콜드스타트로 초기화될 수 있어 완벽하지 않다.
// 엄격한 제한이 필요하면 Vercel KV / Upstash Redis 같은 외부 저장소로 교체할 것.
const hits = new Map(); // ip -> number[] (요청 타임스탬프)

function rateLimited(ip) {
  const now = Date.now();
  const arr = (hits.get(ip) || []).filter((t) => now - t < RATE_WINDOW_MS);
  arr.push(now);
  hits.set(ip, arr);
  return arr.length > RATE_MAX_PER_WINDOW;
}

export default async function handler(req, res) {
  // 1) 메서드 제한
  if (req.method !== "POST") {
    return res.status(405).json({ error: "POST만 허용됩니다." });
  }

  // 2) 서버 환경변수에 키가 있어야 동작
  const apiKey = process.env.LLM_API_KEY;
  if (!apiKey) {
    return res.status(500).json({ error: "서버에 LLM_API_KEY가 설정되지 않았습니다." });
  }

  // 3) rate limit
  const ip =
    (req.headers["x-forwarded-for"] || "").split(",")[0].trim() ||
    req.socket?.remoteAddress ||
    "unknown";
  if (rateLimited(ip)) {
    return res.status(429).json({ error: "요청이 너무 많습니다. 잠시 후 다시 시도하세요." });
  }

  // 4) 입력 파싱 + 길이 검증 (비용 폭탄 방지)
  let body = req.body;
  if (typeof body === "string") {
    try {
      body = JSON.parse(body);
    } catch {
      return res.status(400).json({ error: "JSON 파싱 실패." });
    }
  }
  const system = (body && body.system) || "";
  const user = body && body.user;

  if (!user || typeof user !== "string") {
    return res.status(400).json({ error: "user 필드가 필요합니다." });
  }
  if (user.length > MAX_USER_LEN || system.length > MAX_SYSTEM_LEN) {
    return res.status(400).json({ error: "입력이 너무 깁니다." });
  }

  // 5) Anthropic 호출 — 키는 서버 환경변수에서만.
  try {
    const upstream = await fetch(ANTHROPIC_URL, {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-api-key": apiKey,
        "anthropic-version": ANTHROPIC_VERSION,
      },
      body: JSON.stringify({
        model: MODEL,
        max_tokens: MAX_TOKENS,
        system,
        messages: [{ role: "user", content: user }],
      }),
    });

    // Anthropic 원본 응답을 그대로 전달 (Unity의 LLMResponseParser가 그대로 파싱).
    const data = await upstream.json();
    return res.status(upstream.status).json(data);
  } catch (e) {
    return res.status(502).json({ error: "LLM 호출 실패", detail: String(e) });
  }
}
