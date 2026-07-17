using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeachAndFight.Training.LLM
{
    // Anthropic Messages API 요청/응답 DTO. https://api.anthropic.com/v1/messages
    // 필드명은 API 스펙과 철자 동일해야 하므로 JsonProperty로 고정한다.

    public class AnthropicRequest
    {
        [JsonProperty("model")] public string Model;
        [JsonProperty("max_tokens")] public int MaxTokens;
        [JsonProperty("system")] public string System;
        [JsonProperty("messages")] public List<AnthropicMessage> Messages = new List<AnthropicMessage>();
    }

    public class AnthropicMessage
    {
        [JsonProperty("role")] public string Role;      // "user" | "assistant"
        [JsonProperty("content")] public string Content;
    }

    // 응답: { "content": [ { "type": "text", "text": "..." } ], "stop_reason": "...", ... }
    public class AnthropicResponse
    {
        [JsonProperty("content")] public List<AnthropicContentBlock> Content;
        [JsonProperty("stop_reason")] public string StopReason;
        [JsonProperty("type")] public string Type; // 오류 시 "error"
    }

    public class AnthropicContentBlock
    {
        [JsonProperty("type")] public string Type; // "text"
        [JsonProperty("text")] public string Text;
    }
}
