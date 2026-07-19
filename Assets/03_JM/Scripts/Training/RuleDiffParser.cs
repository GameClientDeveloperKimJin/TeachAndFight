using Newtonsoft.Json;
using TeachAndFight.Core.Rules;
using TeachAndFight.Training.LLM;

namespace TeachAndFight.Training
{
    // LLM 응답 텍스트 -> RuleDiff. 순수 함수(네트워크 없이 테스트 가능).
    public static class RuleDiffParser
    {
        // 성공 시 diff 반환. 실패 시 diff=null, error에 사유.
        public static bool TryParse(string text, out RuleDiff diff, out string error)
        {
            diff = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "빈 응답";
                return false;
            }

            // AnthropicLLMClient가 이미 펜스를 제거하지만, FakeClient/직접 입력 대비 한 번 더 방어.
            var cleaned = LLMResponseParser.StripCodeFences(text);

            try
            {
                diff = JsonConvert.DeserializeObject<RuleDiff>(cleaned);
            }
            catch (JsonException e)
            {
                error = $"JSON 파싱 실패: {e.Message}";
                return false;
            }

            if (diff == null)
            {
                error = "diff가 null로 역직렬화됨";
                return false;
            }

            // ops 자체는 없을 수 있다(needs_confirmation 케이스). null이면 빈 리스트로 정규화.
            if (diff.Ops == null)
                diff.Ops = new System.Collections.Generic.List<RuleOp>();

            return true;
        }
    }
}
