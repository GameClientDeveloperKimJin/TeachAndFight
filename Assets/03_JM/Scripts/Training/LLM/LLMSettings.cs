namespace TeachAndFight.Training.LLM
{
    // LLM 호출 설정. 모델/타임아웃 등은 여기서만 바꾼다 (03장 공통).
    public static class LLMSettings
    {
        public const string Endpoint = "https://api.anthropic.com/v1/messages";
        public const string ApiVersion = "2023-06-01";       // anthropic-version 헤더
        public const string ApiKeyEnvVar = "ANTHROPIC_API_KEY";

        // Haiku 계열 확정 (03장). 모델 교체는 이 상수만 수정.
        public const string Model = "claude-haiku-4-5";

        public const int MaxTokens = 1024;
        public const int TimeoutSeconds = 10;   // 03장: 타임아웃 10s
        public const int MaxAttempts = 2;       // 최초 1회 + 재시도 1회
    }
}
