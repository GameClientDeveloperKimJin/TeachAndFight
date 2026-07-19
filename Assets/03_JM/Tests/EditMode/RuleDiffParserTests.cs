using NUnit.Framework;
using TeachAndFight.Training;

namespace TeachAndFight.Training.Tests
{
    public class RuleDiffParserTests
    {
        [Test]
        public void TryParse_FencedJson_ParsesAfterStrip()
        {
            // 모델이 ```json 펜스로 감싸 보내도 파싱돼야 한다.
            const string fenced =
                "```json\n{\"ops\":[],\"disciple_reply\":\"네\",\"needs_confirmation\":true}\n```";

            bool ok = RuleDiffParser.TryParse(fenced, out var diff, out var error);

            Assert.IsTrue(ok, error);
            Assert.IsTrue(diff.NeedsConfirmation);
            Assert.IsNotNull(diff.Ops);        // null -> 빈 리스트로 정규화
            Assert.AreEqual(0, diff.Ops.Count);
        }

        [Test]
        public void TryParse_Empty_Fails()
        {
            Assert.IsFalse(RuleDiffParser.TryParse("  ", out _, out _));
        }

        [Test]
        public void TryParse_Malformed_ReturnsError()
        {
            bool ok = RuleDiffParser.TryParse("{ nope", out var diff, out var error);
            Assert.IsFalse(ok);
            Assert.IsNull(diff);
            Assert.IsNotEmpty(error);
        }
    }
}
