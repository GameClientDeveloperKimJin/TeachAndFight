using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TeachAndFight.Core.Rules;
using TeachAndFight.Training;
using TeachAndFight.Training.LLM;

namespace TeachAndFight.Training.Tests
{
    public class TrainingCompilerTests
    {
        // 네트워크 없이 파이프라인만 검증하기 위한 가짜 LLM.
        private sealed class FakeLLMClient : ILLMClient
        {
            private readonly LLMResult _result;
            public FakeLLMClient(LLMResult result) => _result = result;

            public UniTask<LLMResult> CompleteAsync(
                string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
                => UniTask.FromResult(_result);
        }

        private static RuleSet EmptyRuleSet(int slots = 8) => new RuleSet
        {
            Version = 1,
            FighterName = "제자",
            MaxSlots = slots,
            Rules = new List<Rule>(),
        };

        // 유효한 add diff (enemy_action == ultimate_startup -> dash away)
        private const string ValidAddDiff =
            "{\"ops\":[{\"op\":\"add\",\"rule\":{\"id\":\"rule_dodge\",\"label\":\"궁 회피\"," +
            "\"when\":[{\"fact\":\"enemy_action\",\"op\":\"==\",\"value\":\"ultimate_startup\"}]," +
            "\"do\":{\"action\":\"dash\",\"params\":{\"direction\":\"away\"}},\"priority\":8}}]," +
            "\"disciple_reply\":\"상대가 궁을 준비하면 뒤로 대시할게요.\",\"needs_confirmation\":false,\"conflict_with\":null}";

        private static TrainingCompileResult Run(LLMResult llm, RuleSet rs)
        {
            var compiler = new TrainingCompiler(new FakeLLMClient(llm));
            return compiler.CompileAsync(rs, "상대가 궁 쓰면 대시로 피해").GetAwaiter().GetResult();
        }

        [Test]
        public void Applied_ValidDiff_AddsRuleAndKeepsOriginalImmutable()
        {
            var current = EmptyRuleSet();
            var result = Run(LLMResult.Ok(ValidAddDiff), current);

            Assert.AreEqual(TrainingOutcome.Applied, result.Outcome);
            Assert.AreEqual(1, result.ResultingRuleSet.Rules.Count);
            Assert.AreEqual("rule_dodge", result.ResultingRuleSet.Rules[0].Id);
            StringAssert.Contains("대시", result.DiscipleReply);
            // 원본 불변(ApplyOps는 깊은 복사)
            Assert.AreEqual(0, current.Rules.Count);
        }

        [Test]
        public void NeedsConfirmation_Rejection_DoesNotApply()
        {
            const string reject =
                "{\"ops\":[],\"disciple_reply\":\"그건 제가 할 수 있는 게 아닌데요...\",\"needs_confirmation\":true,\"conflict_with\":null}";
            var current = EmptyRuleSet();
            var result = Run(LLMResult.Ok(reject), current);

            Assert.AreEqual(TrainingOutcome.NeedsConfirmation, result.Outcome);
            Assert.AreEqual(0, result.ResultingRuleSet.Rules.Count);
            StringAssert.Contains("할 수 있는 게 아닌데요", result.DiscipleReply);
        }

        [Test]
        public void NeedsConfirmation_Ambiguous_AsksBack()
        {
            const string ambiguous =
                "{\"ops\":[],\"disciple_reply\":\"어떤 상황에서 그렇게 할까요?\",\"needs_confirmation\":true}";
            var result = Run(LLMResult.Ok(ambiguous), EmptyRuleSet());

            Assert.AreEqual(TrainingOutcome.NeedsConfirmation, result.Outcome);
            StringAssert.Contains("어떤 상황", result.DiscipleReply);
        }

        [Test]
        public void NeedsConfirmation_Conflict_SurfacesConflictId()
        {
            const string conflict =
                "{\"ops\":[],\"disciple_reply\":\"이건 기존 가르침과 부딪혀요. 어느 쪽을 우선할까요?\"," +
                "\"needs_confirmation\":true,\"conflict_with\":\"rule_dodge\"}";
            var result = Run(LLMResult.Ok(conflict), EmptyRuleSet());

            Assert.AreEqual(TrainingOutcome.NeedsConfirmation, result.Outcome);
            Assert.AreEqual("rule_dodge", result.ConflictWith);
        }

        [Test]
        public void Rejected_LLMSaysApplyButValidatorRefuses()
        {
            // needs_confirmation=false 인데 어휘 사전에 없는 fact(enemy_mind) -> RuleValidator 거부
            const string bogus =
                "{\"ops\":[{\"op\":\"add\",\"rule\":{\"id\":\"rule_x\",\"label\":\"x\"," +
                "\"when\":[{\"fact\":\"enemy_mind\",\"op\":\"==\",\"value\":\"scared\"}]," +
                "\"do\":{\"action\":\"approach\"},\"priority\":5}}]," +
                "\"disciple_reply\":\"네 그렇게 할게요!\",\"needs_confirmation\":false,\"conflict_with\":null}";
            var current = EmptyRuleSet();
            var result = Run(LLMResult.Ok(bogus), current);

            Assert.AreEqual(TrainingOutcome.Rejected, result.Outcome);
            Assert.AreEqual(TrainingCompiler.RejectedReply, result.DiscipleReply);
            Assert.AreEqual(0, current.Rules.Count);
            Assert.Greater(result.Errors.Count, 0);
        }

        [Test]
        public void Failed_LLMFallback_MapsToFailed()
        {
            var result = Run(LLMResult.Fail(LLMFailureReason.MissingApiKey, "no key"), EmptyRuleSet());

            Assert.AreEqual(TrainingOutcome.Failed, result.Outcome);
            Assert.AreEqual(LLMResult.FallbackReply, result.DiscipleReply);
        }

        [Test]
        public void Failed_UnparsableJson_MapsToFailed()
        {
            var result = Run(LLMResult.Ok("이건 JSON이 아니에요"), EmptyRuleSet());

            Assert.AreEqual(TrainingOutcome.Failed, result.Outcome);
            Assert.AreEqual(0, result.ResultingRuleSet.Rules.Count);
        }
    }
}
