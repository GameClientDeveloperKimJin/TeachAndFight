using Newtonsoft.Json;

namespace TeachAndFight.Training.LLM
{
    // 순수 파싱 유틸 - 네트워크 없이 EditMode 테스트 가능하도록 static 함수로 분리한다.
    public static class LLMResponseParser
    {
        // Anthropic 응답 JSON에서 첫 text 블록을 뽑아 마크다운 펜스를 제거해 반환한다.
        // 실패 시 text=null, reason에 원인을 담는다.
        public static bool TryExtractText(string responseJson, out string text, out LLMFailureReason reason)
        {
            text = null;
            reason = LLMFailureReason.None;

            if (string.IsNullOrWhiteSpace(responseJson))
            {
                reason = LLMFailureReason.EmptyResponse;
                return false;
            }

            AnthropicResponse parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<AnthropicResponse>(responseJson);
            }
            catch (JsonException)
            {
                reason = LLMFailureReason.ParseError;
                return false;
            }

            if (parsed?.Content == null || parsed.Content.Count == 0)
            {
                reason = LLMFailureReason.EmptyResponse;
                return false;
            }

            string raw = null;
            foreach (var block in parsed.Content)
            {
                if (block != null && block.Type == "text" && !string.IsNullOrEmpty(block.Text))
                {
                    raw = block.Text;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                reason = LLMFailureReason.EmptyResponse;
                return false;
            }

            text = StripCodeFences(raw);
            return true;
        }

        // ```json ... ``` 또는 ``` ... ``` 펜스를 제거하고 안쪽만 남긴다. 펜스가 없으면 trim만.
        public static string StripCodeFences(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            var trimmed = s.Trim();
            if (!trimmed.StartsWith("```"))
                return trimmed;

            // 첫 줄바꿈까지가 ```json 같은 펜스 헤더 - 제거
            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline < 0)
                return trimmed.Trim('`').Trim();

            var body = trimmed.Substring(firstNewline + 1);

            // 닫는 ``` 제거
            int closing = body.LastIndexOf("```");
            if (closing >= 0)
                body = body.Substring(0, closing);

            return body.Trim();
        }
    }
}
