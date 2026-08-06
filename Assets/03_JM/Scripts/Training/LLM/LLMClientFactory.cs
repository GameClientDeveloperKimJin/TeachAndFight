using UnityEngine;

namespace TeachAndFight.Training.LLM
{
    // ILLMClient 생성 단일 진입점. "빌드=프록시, 에디터=직접 호출(개발 편의)" 정책을 여기서만 관리한다.
    // 런타임 코드(TrainingScreenController, LockerRoomController 등)는 new AnthropicLLMClient() 대신 이 팩토리를 쓴다.
    //
    // 배포(빌드) 원칙: 클라이언트 바이너리에는 API 키가 절대 포함되지 않아야 하므로 항상 프록시 서버만 호출한다.
    public static class LLMClientFactory
    {
        public static ILLMClient CreateDefault()
        {
#if UNITY_EDITOR
            // 에디터: 로컬 키(.secrets 또는 환경변수)가 있으면 Anthropic 직접 호출로 빠르게 개발.
            var direct = new AnthropicLLMClient();
            if (direct.HasApiKey)
            {
                Debug.Log("[LLM] 에디터: 로컬 키로 Anthropic 직접 호출.");
                return direct;
            }

            // 로컬 키가 없으면 프록시가 설정돼 있을 때 프록시로.
            if (LLMSettings.ProxyConfigured)
            {
                Debug.Log("[LLM] 에디터: 로컬 키 없음 - 프록시 서버로 호출.");
                return new ProxyLLMClient();
            }

            // 둘 다 없으면 폴백 대사만 나온다(게임은 죽지 않음).
            Debug.LogWarning("[LLM] 에디터: 로컬 키/프록시 모두 미설정 - LLM 기능은 폴백 대사만 응답합니다.");
            return direct; // HasApiKey=false 이므로 즉시 폴백
#else
            // 배포 빌드: 키를 절대 포함하지 않는다 -> 무조건 프록시.
            if (!LLMSettings.ProxyConfigured)
                Debug.LogError("[LLM] 배포 빌드인데 LLMSettings.ProxyEndpoint가 미설정입니다. " +
                               "Vercel 배포 후 실제 URL로 교체하세요. 현재는 폴백 대사만 동작합니다.");
            return new ProxyLLMClient();
#endif
        }
    }
}
