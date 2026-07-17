using NUnit.Framework;
using TeachAndFight.Training.LLM;

namespace TeachAndFight.Training.Tests
{
    public class LLMResponseParserTests
    {
        [Test]
        public void TryExtractText_ValidResponse_ReturnsText()
        {
            const string json =
                "{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"{\\\"ops\\\":[]}\"}],\"stop_reason\":\"end_turn\"}";

            bool ok = LLMResponseParser.TryExtractText(json, out var text, out var reason);

            Assert.IsTrue(ok);
            Assert.AreEqual(LLMFailureReason.None, reason);
            Assert.AreEqual("{\"ops\":[]}", text);
        }

        [Test]
        public void TryExtractText_MarkdownFenced_StripsFence()
        {
            // 모델이 ```json 펜스로 감싸 응답하는 흔한 케이스
            const string inner = "{\\\"ops\\\":[]}";
            const string json =
                "{\"content\":[{\"type\":\"text\",\"text\":\"```json\\n" + inner + "\\n```\"}]}";

            bool ok = LLMResponseParser.TryExtractText(json, out var text, out var reason);

            Assert.IsTrue(ok, "reason={0}", reason);
            Assert.AreEqual("{\"ops\":[]}", text);
        }

        [Test]
        public void TryExtractText_EmptyContent_Fails()
        {
            const string json = "{\"content\":[]}";

            bool ok = LLMResponseParser.TryExtractText(json, out _, out var reason);

            Assert.IsFalse(ok);
            Assert.AreEqual(LLMFailureReason.EmptyResponse, reason);
        }

        [Test]
        public void TryExtractText_MalformedJson_ReturnsParseError()
        {
            bool ok = LLMResponseParser.TryExtractText("{ not json ", out _, out var reason);

            Assert.IsFalse(ok);
            Assert.AreEqual(LLMFailureReason.ParseError, reason);
        }

        [Test]
        public void TryExtractText_NullOrEmpty_ReturnsEmptyResponse()
        {
            Assert.IsFalse(LLMResponseParser.TryExtractText(null, out _, out var r1));
            Assert.AreEqual(LLMFailureReason.EmptyResponse, r1);

            Assert.IsFalse(LLMResponseParser.TryExtractText("   ", out _, out var r2));
            Assert.AreEqual(LLMFailureReason.EmptyResponse, r2);
        }

        [Test]
        public void StripCodeFences_NoFence_TrimsOnly()
        {
            Assert.AreEqual("{\"a\":1}", LLMResponseParser.StripCodeFences("  {\"a\":1}  "));
        }

        [Test]
        public void StripCodeFences_PlainFence_RemovesBackticks()
        {
            Assert.AreEqual("hello", LLMResponseParser.StripCodeFences("```\nhello\n```"));
        }
    }
}
