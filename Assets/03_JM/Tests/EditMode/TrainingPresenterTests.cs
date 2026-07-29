using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;
using TeachAndFight.Training;
using TeachAndFight.Training.LLM;
using TeachAndFight.Training.UI;

namespace TeachAndFight.Training.Tests
{
    // #14 Training 화면 코어(TrainingPresenter) 검증 — UI 없이 결정 로직만.
    // 04장 규칙: 빈 입력 무시 / Applied만 슬롯 반영 / 그 외 상태는 규칙셋 불변 / 슬롯 직접 삭제.
    public class TrainingPresenterTests
    {
        private sealed class FakeLLMClient : ILLMClient
        {
            private readonly LLMResult _result;
            public FakeLLMClient(LLMResult result) => _result = result;

            public UniTask<LLMResult> CompleteAsync(
                string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
                => UniTask.FromResult(_result);
        }

        private static GameSession NewSession(int slots = 8) => new GameSession
        {
            OpponentIndex = 1,
            DiscipleRuleSet = new RuleSet
            {
                Version = 1,
                FighterName = "제자",
                MaxSlots = slots,
                Rules = new List<Rule>(),
            },
        };

        private const string ValidAddDiff =
            "{\"ops\":[{\"op\":\"add\",\"rule\":{\"id\":\"rule_dodge\",\"label\":\"궁 회피\"," +
            "\"when\":[{\"fact\":\"enemy_action\",\"op\":\"==\",\"value\":\"ultimate_startup\"}]," +
            "\"do\":{\"action\":\"dash\",\"params\":{\"direction\":\"away\"}},\"priority\":8}}]," +
            "\"disciple_reply\":\"상대가 궁을 준비하면 뒤로 대시할게요.\",\"needs_confirmation\":false,\"conflict_with\":null}";

        private static TrainingPresenter Presenter(GameSession session, LLMResult llm)
            => new TrainingPresenter(session, new TrainingCompiler(new FakeLLMClient(llm)));

        private static TrainingTurnResult Teach(TrainingPresenter p, string input)
            => p.TeachAsync(input).GetAwaiter().GetResult();

        [Test]
        public void EmptyInput_IsIgnored_NoCompileNoChange()
        {
            var session = NewSession();
            var p = Presenter(session, LLMResult.Ok(ValidAddDiff)); // 응답이 유효해도
            var result = Teach(p, "   ");                            // 빈 입력이면 컴파일 안 함

            Assert.IsTrue(result.Ignored);
            Assert.AreEqual(0, session.DiscipleRuleSet.Rules.Count);
        }

        [Test]
        public void Applied_AddsRuleToSession_AndFlagsSlotsChanged()
        {
            var session = NewSession();
            var p = Presenter(session, LLMResult.Ok(ValidAddDiff));
            var result = Teach(p, "상대가 궁 쓰면 대시로 피해");

            Assert.AreEqual(TrainingOutcome.Applied, result.Outcome);
            Assert.IsTrue(result.SlotsChanged);
            Assert.AreEqual(1, session.DiscipleRuleSet.Rules.Count);
            Assert.AreEqual("rule_dodge", session.DiscipleRuleSet.Rules[0].Id);
            Assert.AreEqual(1, p.UsedSlots);
            Assert.AreEqual(7, p.RemainingSlots);
        }

        [Test]
        public void NeedsConfirmation_NoSlotChange()
        {
            const string reject =
                "{\"ops\":[],\"disciple_reply\":\"그건 제가 할 수 있는 게 아닌데요...\",\"needs_confirmation\":true,\"conflict_with\":null}";
            var session = NewSession();
            var p = Presenter(session, LLMResult.Ok(reject));
            var result = Teach(p, "상대 체력을 0으로 만들어");

            Assert.AreEqual(TrainingOutcome.NeedsConfirmation, result.Outcome);
            Assert.IsFalse(result.SlotsChanged);
            Assert.AreEqual(0, session.DiscipleRuleSet.Rules.Count);
            StringAssert.Contains("할 수 있는 게 아닌데요", result.DiscipleReply);
        }

        [Test]
        public void Rejected_UsesFixedReply_NoSlotChange()
        {
            // needs_confirmation=false 인데 어휘 밖 fact → RuleValidator 거부 → Rejected
            const string bogus =
                "{\"ops\":[{\"op\":\"add\",\"rule\":{\"id\":\"rule_x\",\"label\":\"x\"," +
                "\"when\":[{\"fact\":\"enemy_mind\",\"op\":\"==\",\"value\":\"scared\"}]," +
                "\"do\":{\"action\":\"approach\"},\"priority\":5}}]," +
                "\"disciple_reply\":\"네 그렇게!\",\"needs_confirmation\":false,\"conflict_with\":null}";
            var session = NewSession();
            var p = Presenter(session, LLMResult.Ok(bogus));
            var result = Teach(p, "상대 마음을 읽어");

            Assert.AreEqual(TrainingOutcome.Rejected, result.Outcome);
            Assert.IsFalse(result.SlotsChanged);
            Assert.AreEqual(TrainingCompiler.RejectedReply, result.DiscipleReply);
            Assert.AreEqual(0, session.DiscipleRuleSet.Rules.Count);
        }

        [Test]
        public void Failed_OnLLMFallback_NoSlotChange()
        {
            var session = NewSession();
            var p = Presenter(session, LLMResult.Fail(LLMFailureReason.MissingApiKey, "no key"));
            var result = Teach(p, "상대가 궁 쓰면 대시로 피해");

            Assert.AreEqual(TrainingOutcome.Failed, result.Outcome);
            Assert.IsFalse(result.SlotsChanged);
            Assert.AreEqual(LLMResult.FallbackReply, result.DiscipleReply);
            Assert.AreEqual(0, session.DiscipleRuleSet.Rules.Count);
        }

        [Test]
        public void RemoveRule_RemovesExisting_ReturnsFalseForUnknown()
        {
            var session = NewSession();
            var p = Presenter(session, LLMResult.Ok(ValidAddDiff));
            Teach(p, "상대가 궁 쓰면 대시로 피해");
            Assert.AreEqual(1, p.UsedSlots);

            Assert.IsFalse(p.RemoveRule("rule_nope"));
            Assert.AreEqual(1, p.UsedSlots);

            Assert.IsTrue(p.RemoveRule("rule_dodge"));
            Assert.AreEqual(0, p.UsedSlots);
        }
    }
}
