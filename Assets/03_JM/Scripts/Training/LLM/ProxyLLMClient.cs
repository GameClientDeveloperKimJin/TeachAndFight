using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace TeachAndFight.Training.LLM
{
    // 배포용 LLM 클라이언트. Anthropic을 직접 호출하지 않고 팀 소유 프록시 서버(LLMSettings.ProxyEndpoint)만 호출한다.
    // 클라이언트 빌드에는 API 키가 전혀 포함되지 않는다 - 키는 프록시 서버의 환경변수에만 존재.
    // 프록시는 Anthropic 원본 응답 JSON을 그대로 돌려주므로 파싱은 AnthropicLLMClient와 동일하게 LLMResponseParser 재사용.
    public sealed class ProxyLLMClient : ILLMClient
    {
        private readonly string _endpoint;

        // 기본 생성자: LLMSettings.ProxyEndpoint 사용.
        public ProxyLLMClient() : this(LLMSettings.ProxyEndpoint)
        {
        }

        // 테스트/명시 주입용.
        public ProxyLLMClient(string endpoint)
        {
            _endpoint = endpoint;
        }

        // 자리표시자가 남아있으면(미배포) 호출 자체를 하지 않고 폴백 처리한다.
        public bool IsConfigured => LLMSettings.IsProxyEndpoint(_endpoint);

        // 프록시로 보내는 요청 본문. 키/모델/토큰은 서버가 결정하므로 여기선 프롬프트만 전달한다.
        [Serializable]
        private class ProxyRequest
        {
            [JsonProperty("system")] public string System;
            [JsonProperty("user")] public string User;
        }

        public async UniTask<LLMResult> CompleteAsync(
            string systemPrompt,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            // 프록시 미설정 -> 재시도 없이 즉시 폴백. 배포 전이거나 URL 미교체 상태.
            if (!IsConfigured)
            {
                Debug.LogWarning("[LLM] ProxyEndpoint 미설정(자리표시자) - 폴백 대사로 응답합니다.");
                return LLMResult.Fail(LLMFailureReason.MissingApiKey, "proxy endpoint not configured");
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

                if (attempt < LLMSettings.MaxAttempts)
                    Debug.LogWarning($"[LLM] 프록시 호출 실패({reason}) - 재시도 {attempt + 1}/{LLMSettings.MaxAttempts}. {detail}");
            }

            Debug.LogWarning($"[LLM] 프록시 최종 실패({lastReason}) - 폴백 대사로 응답합니다. {lastDetail}");
            return LLMResult.Fail(lastReason, lastDetail);
        }

        private static byte[] BuildRequestBody(string systemPrompt, string userMessage)
        {
            var request = new ProxyRequest { System = systemPrompt, User = userMessage };
            var json = JsonConvert.SerializeObject(request);
            return Encoding.UTF8.GetBytes(json);
        }

        // 단일 HTTP 시도. UnityWebRequest는 반드시 Dispose. 응답은 Anthropic 원본 JSON 전제.
        private async UniTask<(LLMResult result, LLMFailureReason reason, string detail)> SendOnceAsync(
            byte[] bodyBytes, CancellationToken cancellationToken)
        {
            using (var req = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = LLMSettings.TimeoutSeconds;
                req.SetRequestHeader("content-type", "application/json");
                // 주의: 여기에는 x-api-key 등 어떤 인증 키도 등장하지 않는다.

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
