using System.Threading;
using Cysharp.Threading.Tasks;

namespace TeachAndFight.Training.LLM
{
    // LLM provider 추상화 (03장). 실제 전투 루프에는 절대 들어가지 않는다 - 훈련 컴파일/회고 전용.
    // provider 교체 가능하게 인터페이스로 감싸고, AnthropicLLMClient가 기본 구현.
    public interface ILLMClient
    {
        // systemPrompt + userMessage로 1회 완성 응답을 받는다.
        // 실패(키없음/타임아웃/파싱) 시 예외를 던지지 않고 LLMResult.Success=false + 폴백 대사로 반환한다.
        UniTask<LLMResult> CompleteAsync(
            string systemPrompt,
            string userMessage,
            CancellationToken cancellationToken = default);
    }

    // LLM 호출 실패 원인 - 폴백 처리/로깅 구분용.
    public enum LLMFailureReason
    {
        None,
        MissingApiKey,
        Timeout,
        HttpError,
        EmptyResponse,
        ParseError
    }

    // LLM 호출 결과. 실패해도 게임이 죽지 않도록 항상 FallbackReply를 채워 반환한다.
    public readonly struct LLMResult
    {
        public readonly bool Success;
        public readonly string Text;              // 성공 시 모델 응답 본문(마크다운 펜스 제거 후)
        public readonly LLMFailureReason Failure; // 실패 시 원인
        public readonly string ErrorDetail;       // 로깅용 상세(사용자 노출 X)

        private LLMResult(bool success, string text, LLMFailureReason failure, string errorDetail)
        {
            Success = success;
            Text = text;
            Failure = failure;
            ErrorDetail = errorDetail;
        }

        // 03장: 파싱 실패/타임아웃 시 재시도 후에도 실패하면 제자 대사로 폴백.
        public const string FallbackReply = "죄송해요, 잘 못 알아들었어요. 다시 말씀해 주세요.";

        public static LLMResult Ok(string text)
            => new LLMResult(true, text, LLMFailureReason.None, null);

        public static LLMResult Fail(LLMFailureReason reason, string errorDetail)
            => new LLMResult(false, FallbackReply, reason, errorDetail);
    }
}
