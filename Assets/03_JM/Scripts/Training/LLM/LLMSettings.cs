namespace TeachAndFight.Training.LLM
{
    // LLM 호출 설정. 모델/타임아웃 등은 여기서만 바꾼다 (03장 공통).
    public static class LLMSettings
    {
        public const string Endpoint = "https://api.anthropic.com/v1/messages";
        public const string ApiVersion = "2023-06-01";       // anthropic-version 헤더
        public const string ApiKeyEnvVar = "ANTHROPIC_API_KEY";

        // 프로젝트 전용 폴백: OS 환경변수 대신 이 프로젝트에서만 키를 쓰고 싶을 때.
        // 프로젝트 루트(Assets 상위) 기준 경로, .gitignore에 등록되어 있어 커밋되지 않는다.
        public const string LocalKeyFilePath = ".secrets/anthropic_api_key.txt";

        // Haiku 계열 확정 (03장). 모델 교체는 이 상수만 수정.
        public const string Model = "claude-haiku-4-5";

        public const int MaxTokens = 1024;
        public const int TimeoutSeconds = 10;   // 03장: 타임아웃 10s
        public const int MaxAttempts = 2;       // 최초 1회 + 재시도 1회
    }
}
