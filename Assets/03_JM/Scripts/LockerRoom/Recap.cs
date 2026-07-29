using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;
using TeachAndFight.Training.LLM;

namespace TeachAndFight.LockerRoom
{
    // 회고 호출 결과. 실패해도 락커룸이 죽지 않도록 항상 폴백 대사를 채운다.
    public readonly struct RecapResult
    {
        public readonly bool Success;
        public readonly string Text;

        private RecapResult(bool success, string text)
        {
            Success = success;
            Text = text;
        }

        // 03장: 실패 시 제자 톤 유지 폴백.
        public const string FallbackReply = "헥헥... 죄송해요, 지금은 말이 잘 안 나오네요. 다음 경기 때 다시 말씀드릴게요.";

        public static RecapResult Ok(string text) => new RecapResult(true, text);
        public static RecapResult Fail() => new RecapResult(false, FallbackReply);
    }

    // LLM 응답 {"recap":"..."} → 회고 텍스트. 순수 함수(네트워크 없이 테스트 가능).
    public static class RecapParser
    {
        private sealed class RecapDto
        {
            [JsonProperty("recap")] public string Recap;
        }

        public static bool TryParse(string text, out string recap)
        {
            recap = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // AnthropicLLMClient가 이미 펜스를 제거하지만, 폴백/직접입력 대비 한 번 더 방어.
            var cleaned = LLMResponseParser.StripCodeFences(text);
            try
            {
                var dto = JsonConvert.DeserializeObject<RecapDto>(cleaned);
                if (dto != null && !string.IsNullOrWhiteSpace(dto.Recap))
                {
                    recap = dto.Recap.Trim();
                    return true;
                }
            }
            catch (JsonException)
            {
            }
            return false;
        }
    }

    // 03장 호출2 실행: 프롬프트 구성 → LLM 호출 → 파싱 → 실패 시 폴백. LLM은 여기서만(전투 루프 밖).
    public static class RecapService
    {
        public static async UniTask<RecapResult> GetRecapAsync(
            ILLMClient client, MatchResult result, RuleSet ruleSet, CancellationToken cancellationToken = default)
        {
            var userMessage = RecapPromptBuilder.BuildUserMessage(result, ruleSet);
            var llm = await client.CompleteAsync(RecapPromptBuilder.SystemPrompt, userMessage, cancellationToken);

            if (!llm.Success)
                return RecapResult.Fail();

            return RecapParser.TryParse(llm.Text, out var recap)
                ? RecapResult.Ok(recap)
                : RecapResult.Fail();
        }
    }
}
