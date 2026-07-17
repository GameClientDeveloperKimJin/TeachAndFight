using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace TeachAndFight.Training.LLM
{
    // Anthropic Claude(Haiku) 구현. UnityWebRequest POST + UniTask.
    // API 키는 환경변수 ANTHROPIC_API_KEY로만 주입 - 코드/저장소에 하드코딩 금지.
    public sealed class AnthropicLLMClient : ILLMClient
    {
        private readonly string _apiKey;

        // 기본 생성자: 환경변수에서 키를 읽는다.
        public AnthropicLLMClient()
            : this(Environment.GetEnvironmentVariable(LLMSettings.ApiKeyEnvVar))
        {
        }

        // 테스트/명시 주입용.
        public AnthropicLLMClient(string apiKey)
        {
            _apiKey = apiKey;
        }

        public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

        public async UniTask<LLMResult> CompleteAsync(
            string systemPrompt,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            // 키 없음 -> 재시도 없이 즉시 폴백. 게임이 죽지 않게 안내 대사로만 응답.
            if (!HasApiKey)
            {
                Debug.LogWarning($"[LLM] {LLMSettings.ApiKeyEnvVar} 미설정 - 폴백 대사로 응답합니다.");
                return LLMResult.Fail(LLMFailureReason.MissingApiKey, "API key not set");
            }

            var bodyBytes = BuildRequestBody(systemPrompt, userMessage);

            LLMFailureReason lastReason = LLMFailureReason.None;
            string lastDetail = null;

            for (int attempt = 1; attempt <= LLMSettings.MaxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (result, reason, detail) = await SendOnceAsync(bodyBytes, cancellationToken);
                if (result.Success)
                    return result;

                lastReason = reason;
                lastDetail = detail;

                // 재시도해도 의미 없는 실패(빈 응답 제외 파싱류는 재시도)에도 1회는 더 시도한다.
                if (attempt < LLMSettings.MaxAttempts)
                    Debug.LogWarning($"[LLM] 호출 실패({reason}) - 재시도 {attempt + 1}/{LLMSettings.MaxAttempts}. {detail}");
            }

            Debug.LogWarning($"[LLM] 최종 실패({lastReason}) - 폴백 대사로 응답합니다. {lastDetail}");
            return LLMResult.Fail(lastReason, lastDetail);
        }

        private static byte[] BuildRequestBody(string systemPrompt, string userMessage)
        {
            var request = new AnthropicRequest
            {
                Model = LLMSettings.Model,
                MaxTokens = LLMSettings.MaxTokens,
                System = systemPrompt,
            };
            request.Messages.Add(new AnthropicMessage { Role = "user", Content = userMessage });

            var json = JsonConvert.SerializeObject(request);
            return Encoding.UTF8.GetBytes(json);
        }

        // 단일 HTTP 시도. UnityWebRequest는 반드시 Dispose.
        private async UniTask<(LLMResult result, LLMFailureReason reason, string detail)> SendOnceAsync(
            byte[] bodyBytes, CancellationToken cancellationToken)
        {
            using (var req = new UnityWebRequest(LLMSettings.Endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = LLMSettings.TimeoutSeconds;
                req.SetRequestHeader("content-type", "application/json");
                req.SetRequestHeader("x-api-key", _apiKey);
                req.SetRequestHeader("anthropic-version", LLMSettings.ApiVersion);

                try
                {
                    await req.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw; // 취소는 호출자에게 전파
                }
                catch (Exception e)
                {
                    // UnityWebRequestException 등 - 연결 실패/타임아웃
                    var reason = ClassifyNetworkFailure(req);
                    return (LLMResult.Fail(reason, e.Message), reason, e.Message);
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    var reason = ClassifyNetworkFailure(req);
                    var detail = $"HTTP {req.responseCode}: {req.error}";
                    return (LLMResult.Fail(reason, detail), reason, detail);
                }

                var responseText = req.downloadHandler.text;
                if (LLMResponseParser.TryExtractText(responseText, out var text, out var parseReason))
                    return (LLMResult.Ok(text), LLMFailureReason.None, null);

                var parseDetail = $"parse failed ({parseReason}) body: {Truncate(responseText, 300)}";
                return (LLMResult.Fail(parseReason, parseDetail), parseReason, parseDetail);
            }
        }

        private static LLMFailureReason ClassifyNetworkFailure(UnityWebRequest req)
        {
            if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                // 타임아웃도 ConnectionError로 잡힌다.
                if (!string.IsNullOrEmpty(req.error) &&
                    req.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                    return LLMFailureReason.Timeout;
                return LLMFailureReason.HttpError;
            }
            return LLMFailureReason.HttpError;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s;
            return s.Substring(0, max) + "...";
        }
    }
}
