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

        // 유저 요구사항: 가르침 1번 = 규칙 1개. LLM이 OR 의미 문장("약공이나 강공 준비하면")을
        // add 2개로 쪼개서 보내는 경우가 실사용에서 관측됨 - 프롬프트로도 막았지만(2번 규칙) 최종
        // 저지선을 코드에도 둔다. 적용하지 않고 하나씩 나눠 가르쳐달라고 되묻어야 한다.
        [Test]
        public void MultipleOps_TreatedAsNeedsConfirmation_EvenIfLLMSaysApply()
        {
            const string twoAdds =
                "{\"ops\":[" +
                "{\"op\":\"add\",\"rule\":{\"id\":\"rule_a\",\"label\":\"약공 반격\"," +
                "\"when\":[{\"fact\":\"enemy_action\",\"op\":\"==\",\"value\":\"light_startup\"}]," +
                "\"do\":{\"action\":\"counter_attack\"},\"priority\":8}}," +
                "{\"op\":\"add\",\"rule\":{\"id\":\"rule_b\",\"label\":\"강공 반격\"," +
                "\"when\":[{\"fact\":\"enemy_action\",\"op\":\"==\",\"value\":\"heavy_startup\"}]," +
                "\"do\":{\"action\":\"counter_attack\"},\"priority\":8}}" +
                "],\"disciple_reply\":\"약공이나 강공 준비하면 반격기 쓸게요!\",\"needs_confirmation\":false,\"conflict_with\":null}";
            var current = EmptyRuleSet();
            var result = Run(LLMResult.Ok(twoAdds), current);

            Assert.AreEqual(TrainingOutcome.NeedsConfirmation, result.Outcome);
            Assert.AreEqual(0, result.ResultingRuleSet.Rules.Count, "규칙 2개가 한 번에 들어가면 안 됨");
            Assert.AreEqual(0, current.Rules.Count);
        }

        // 유저 요구사항: "A일 때 B하고 C일 때 D해줘"류 복합 문장은 첫 번째만 적용하고 두 번째는
        // 대사로 되물어야 함(10번 규칙) - ops 1개 + needs_confirmation=true 조합이 이 케이스.
        // "여러 상황" 가드(ops.Count>1)에 걸리지 않고 정상 적용돼야 한다.
        [Test]
        public void CompoundTeaching_AppliesFirstPart_AsksAboutSecond()
        {
            const string compound =
                "{\"ops\":[{\"op\":\"add\",\"rule\":{\"id\":\"rule_a\",\"label\":\"궁 회피\"," +
                "\"when\":[{\"fact\":\"enemy_action\",\"op\":\"==\",\"value\":\"ultimate_startup\"}]," +
                "\"do\":{\"action\":\"dash\",\"params\":{\"direction\":\"away\"}},\"priority\":8}}]," +
                "\"disciple_reply\":\"상대가 궁 쓰면 대시로 피하도록 배웠습니다! 헛치면 강공으로 처벌하면 되는 건가요?\"," +
                "\"needs_confirmation\":true,\"conflict_with\":null}";
            var current = EmptyRuleSet();
            var result = Run(LLMResult.Ok(compound), current);

            Assert.AreEqual(TrainingOutcome.Applied, result.Outcome, "첫 부분은 적용돼야 함");
            Assert.AreEqual(1, result.ResultingRuleSet.Rules.Count);
            Assert.AreEqual("rule_a", result.ResultingRuleSet.Rules[0].Id);
            StringAssert.Contains("배웠습니다", result.DiscipleReply);
            StringAssert.Contains("처벌하면 되는 건가요?", result.DiscipleReply, "두 번째 부분은 되묻는 대사로 남아있어야 함");
            Assert.AreEqual(0, current.Rules.Count, "원본은 불변");
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
