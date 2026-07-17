using NUnit.Framework;
using TeachAndFight.Training.LLM;

namespace TeachAndFight.Training.Tests
{
    public class AnthropicLLMClientTests
    {
        // 03장 완료기준: API 키 없음 시 게임이 죽지 않고 안내 대사 출력.
        [Test]
        public void CompleteAsync_NoApiKey_ReturnsFallbackWithoutThrow()
        {
            var client = new AnthropicLLMClient(apiKey: null);
            Assert.IsFalse(client.HasApiKey);

            // 키 없음 경로는 네트워크 없이 동기 완료된다.
            var result = client.CompleteAsync("system", "[코치의 말] 테스트")
                .GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(LLMFailureReason.MissingApiKey, result.Failure);
            Assert.AreEqual(LLMResult.FallbackReply, result.Text);
        }

        [Test]
        public void CompleteAsync_EmptyApiKey_TreatedAsMissing()
        {
            var client = new AnthropicLLMClient(apiKey: "   ");
            Assert.IsFalse(client.HasApiKey);

            var result = client.CompleteAsync("system", "msg").GetAwaiter().GetResult();

            Assert.AreEqual(LLMFailureReason.MissingApiKey, result.Failure);
        }
    }
}
