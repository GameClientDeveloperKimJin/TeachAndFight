using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TeachAndFight.Combat;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;
using TeachAndFight.LockerRoom;
using TeachAndFight.Training.LLM;

namespace TeachAndFight.LockerRoom.Tests
{
    // #15 LockerRoom 회고 파이프라인/통계/프레젠터 검증 — UI 없이 결정 로직만.
    public class LockerRoomTests
    {
        private sealed class FakeLLMClient : ILLMClient
        {
            private readonly LLMResult _result;
            public FakeLLMClient(LLMResult result) => _result = result;

            public UniTask<LLMResult> CompleteAsync(
                string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
                => UniTask.FromResult(_result);
        }

        private static RuleSet Rules() => new RuleSet
        {
            Version = 1,
            FighterName = "제자",
            MaxSlots = 8,
            Rules = new List<Rule>
            {
                new Rule { Id = "rule_dodge", Label = "궁 회피", Priority = 8, When = new List<Condition>(), Do = new RuleAction { Action = "dash" } },
                new Rule { Id = "rule_close", Label = "접근", Priority = 5, When = new List<Condition>(), Do = new RuleAction { Action = "approach" } },
            },
        };

        private static MatchResult Match() => new MatchResult
        {
            Won = false,
            SelfHpPct = 0,
            EnemyHpPct = 40,
            EventLog = new List<MatchEvent>
            {
                new MatchEvent { Time = 1, Type = "rule_fired", RuleId = "rule_dodge" },
                new MatchEvent { Time = 2, Type = "rule_fired", RuleId = "rule_dodge" },
                new MatchEvent { Time = 3, Type = "rule_fired", RuleId = "rule_close" },
                new MatchEvent { Time = 4, Type = "hit" },
            },
        };

        private static GameSession Session() => new GameSession
        {
            OpponentIndex = 1,
            DiscipleRuleSet = Rules(),
            LastMatch = Match(),
        };

        [Test]
        public void TopFiredRules_CountsAndOrdersDesc()
        {
            var top = MatchStats.TopFiredRules(Match(), Rules(), 3);
            Assert.AreEqual(2, top.Count);
            Assert.AreEqual("rule_dodge", top[0].RuleId);
            Assert.AreEqual(2, top[0].Count);
            Assert.AreEqual("궁 회피", top[0].Label);
            Assert.AreEqual(1, top[1].Count);
        }

        [Test]
        public void PromptBuilder_IncludesResult_Rules_LabelMap()
        {
            var msg = RecapPromptBuilder.BuildUserMessage(Match(), Rules());
            StringAssert.Contains("패배", msg);
            StringAssert.Contains("궁 회피", msg);
            StringAssert.Contains("rule_dodge", msg);
            StringAssert.Contains("rule_close: 접근", msg);
        }

        [Test]
        public void RecapParser_ParsesRecap_AndFences_RejectsGarbageAndEmpty()
        {
            Assert.IsTrue(RecapParser.TryParse("{\"recap\":\"잘 싸웠어요.\"}", out var a));
            Assert.AreEqual("잘 싸웠어요.", a);

            Assert.IsTrue(RecapParser.TryParse("```json\n{\"recap\":\"펜스도 OK\"}\n```", out var b));
            Assert.AreEqual("펜스도 OK", b);

            Assert.IsFalse(RecapParser.TryParse("그냥 텍스트", out _));
            Assert.IsFalse(RecapParser.TryParse("{\"recap\":\"\"}", out _));
        }

        [Test]
        public void Presenter_Recap_Ok_OnValidResponse()
        {
            var p = new LockerRoomPresenter(Session(), new FakeLLMClient(LLMResult.Ok("{\"recap\":\"수고하셨어요.\"}")));
            var res = p.RecapAsync().GetAwaiter().GetResult();

            Assert.IsTrue(res.Success);
            Assert.AreEqual("수고하셨어요.", res.Text);
        }

        [Test]
        public void Presenter_Recap_Fallback_OnLLMFail()
        {
            var p = new LockerRoomPresenter(Session(), new FakeLLMClient(LLMResult.Fail(LLMFailureReason.Timeout, "t")));
            var res = p.RecapAsync().GetAwaiter().GetResult();

            Assert.IsFalse(res.Success);
            Assert.AreEqual(RecapResult.FallbackReply, res.Text);
        }

        [Test]
        public void Presenter_Recap_Fallback_OnUnparsable()
        {
            var p = new LockerRoomPresenter(Session(), new FakeLLMClient(LLMResult.Ok("회고가 JSON이 아니에요")));
            var res = p.RecapAsync().GetAwaiter().GetResult();

            Assert.IsFalse(res.Success);
            Assert.AreEqual(RecapResult.FallbackReply, res.Text);
        }

        [Test]
        public void Presenter_ResultBanner_And_HasMatch()
        {
            var p = new LockerRoomPresenter(Session(), new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));
            Assert.IsTrue(p.HasMatch);
            Assert.AreEqual("패배...", p.ResultBanner);
            Assert.AreEqual("rule_dodge", p.TopFiredRules(3)[0].RuleId);
        }

        [Test]
        public void SampleMatch_InjectsOnlyWhenEmpty()
        {
            var session = new GameSession
            {
                OpponentIndex = 1,
                DiscipleRuleSet = new RuleSet { Version = 1, FighterName = "제자", MaxSlots = 8, Rules = new List<Rule>() },
                LastMatch = null,
            };
            SampleMatch.EnsureForStandalone(session);
            Assert.IsNotNull(session.LastMatch);
            Assert.Greater(session.DiscipleRuleSet.Rules.Count, 0);

            // 이미 결과가 있으면 덮어쓰지 않음
            var existing = Match();
            var session2 = new GameSession { OpponentIndex = 1, DiscipleRuleSet = Rules(), LastMatch = existing };
            SampleMatch.EnsureForStandalone(session2);
            Assert.AreSame(existing, session2.LastMatch);
        }
    }
}
