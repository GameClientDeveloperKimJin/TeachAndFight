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
            HitsLanded = 2, // 교착 상태(WasStalemate) 오판 방지 - 이 헬퍼는 "제대로 붙어서 짐" 시나리오
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

        // 승리 -> 다음 상대 진행 배선이 원래 통째로 빠져있었음(OpponentIndex를 어디서도 증가
        // 안 시켜서 이겨도 계속 같은 상대만 재대결됨). 이 3개 테스트가 그 회귀를 잡는다.
        private static GameSession WonSession(int opponentIndex) => new GameSession
        {
            OpponentIndex = opponentIndex,
            DiscipleRuleSet = Rules(),
            LastMatch = new MatchResult { Won = true, SelfHpPct = 60, EnemyHpPct = 0, EventLog = new List<MatchEvent>() },
        };

        [Test]
        public void HasNextOpponent_TrueWhenWonAndBelowMaxIndex()
        {
            var p = new LockerRoomPresenter(WonSession(1), new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));
            Assert.IsTrue(p.HasNextOpponent);
            Assert.IsFalse(p.IsGameCleared);
        }

        [Test]
        public void HasNextOpponent_FalseWhenLost()
        {
            var p = new LockerRoomPresenter(Session(), new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}"))); // Session()은 Won=false
            Assert.IsFalse(p.HasNextOpponent);
        }

        [Test]
        public void IsGameCleared_TrueWhenWonAtMaxIndex()
        {
            var session = WonSession(OpponentRuleSetLoader.MaxIndex);
            var p = new LockerRoomPresenter(session, new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));

            Assert.IsTrue(p.IsGameCleared);
            Assert.IsFalse(p.HasNextOpponent);
            Assert.AreEqual("우승! 모든 상대를 이겼습니다", p.ResultBanner);
        }

        [Test]
        public void AdvanceToNextOpponent_IncrementsIndex_AndClearsStaleOpponent()
        {
            var session = WonSession(1);
            session.CurrentOpponent = Rules(); // 이전 상대 정보(더미) - 넘어가면 비워져야 함
            var p = new LockerRoomPresenter(session, new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));

            p.AdvanceToNextOpponent();

            Assert.AreEqual(2, session.OpponentIndex);
            Assert.IsNull(session.CurrentOpponent);
        }

        [Test]
        public void AdvanceToNextOpponent_NoOpWhenNoNextOpponent()
        {
            var session = WonSession(OpponentRuleSetLoader.MaxIndex);
            var p = new LockerRoomPresenter(session, new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));

            p.AdvanceToNextOpponent();

            Assert.AreEqual(OpponentRuleSetLoader.MaxIndex, session.OpponentIndex, "마지막 상대에서는 더 진행하면 안 됨");
        }

        // 유저 피드백: 도망형 상대한테 접근만 시켜서 안 부딪히고 지면 그냥 "패배..."로만 뜨는 게
        // 아니라 원인이 뭔지(교착 상태) 구분해서 알려줘야 함. 이 4개가 그 회귀를 잡는다.
        [Test]
        public void WasStalemate_TrueWhenLostWithNoEngagement()
        {
            var session = new GameSession
            {
                OpponentIndex = 2,
                DiscipleRuleSet = Rules(),
                LastMatch = new MatchResult { Won = false, SelfHpPct = 100, EnemyHpPct = 100, HitsLanded = 0, EventLog = new List<MatchEvent>() },
            };
            var p = new LockerRoomPresenter(session, new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));

            Assert.IsTrue(p.WasStalemate);
            Assert.AreEqual("교착 상태로 조기 종료 - 패배 처리됐어요", p.ResultBanner);
        }

        [Test]
        public void WasStalemate_FalseWhenLostButActuallyEngaged()
        {
            // Match() 헬퍼는 HitsLanded=2(제대로 붙어서 짐) - 기존 "패배..." 배너 그대로 나와야 함.
            var p = new LockerRoomPresenter(Session(), new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));

            Assert.IsFalse(p.WasStalemate);
            Assert.AreEqual("패배...", p.ResultBanner);
        }

        [Test]
        public void WasStalemate_FalseWhenWon_EvenWithLowHitCount()
        {
            // 견제/시간끌기로 이기는 것도 정식 공략(05장) - 이겼으면 HitsLanded 적어도 교착 취급 안 함.
            var session = WonSession(1);
            session.LastMatch.HitsLanded = 0;
            var p = new LockerRoomPresenter(session, new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));

            Assert.IsFalse(p.WasStalemate);
        }

        // 유저 요구사항: 교착 원인을 규칙셋 기반으로 구체적으로 짚어줌(사범전 dash-only 폴백 누락이
        // 실제 계기였음). 3가지 규칙셋 패턴별로 다른 메시지가 나와야 한다.
        private static GameSession StalemateSession(List<Rule> rules) => new GameSession
        {
            OpponentIndex = 5,
            DiscipleRuleSet = new RuleSet { Version = 1, FighterName = "제자", MaxSlots = 9, Rules = rules },
            LastMatch = new MatchResult { Won = false, SelfHpPct = 100, EnemyHpPct = 100, HitsLanded = 0, EventLog = new List<MatchEvent>() },
        };

        [Test]
        public void StalemateFeedback_Null_WhenNotStalemate()
        {
            var p = new LockerRoomPresenter(Session(), new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));
            Assert.IsNull(p.StalemateFeedback);
        }

        [Test]
        public void StalemateFeedback_NoApproachAtAll_SaysMissingClosingRule()
        {
            var rules = new List<Rule>
            {
                new Rule { Id = "r1", Label = "쉬기", Priority = 5, When = new List<Condition>(), Do = new RuleAction { Action = "idle" } },
            };
            var p = new LockerRoomPresenter(StalemateSession(rules), new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));

            StringAssert.Contains("다가가는 규칙이 아예 없어요", p.StalemateFeedback);
        }

        [Test]
        public void StalemateFeedback_DashOnlyNoWalkFallback_SaysDashCooldownGap()
        {
            // 사범전 실제 상황 재현: dash(toward)만 있고 approach 폴백이 없음.
            var rules = new List<Rule>
            {
                new Rule
                {
                    Id = "r1", Label = "대시로 접근", Priority = 6, When = new List<Condition>(),
                    Do = new RuleAction { Action = "dash", Params = new Dictionary<string, object> { { "direction", "toward" } } },
                },
            };
            var p = new LockerRoomPresenter(StalemateSession(rules), new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));

            StringAssert.Contains("대시로 다가가는 규칙만 있어요", p.StalemateFeedback);
        }

        [Test]
        public void StalemateFeedback_DashAwayOnly_CountsAsNoClosingRule()
        {
            // dash(away)는 회피용이라 접근 수단으로 안 침 - "다가가는 규칙 없음" 취급돼야 함.
            var rules = new List<Rule>
            {
                new Rule
                {
                    Id = "r1", Label = "궁 회피", Priority = 9, When = new List<Condition>(),
                    Do = new RuleAction { Action = "dash", Params = new Dictionary<string, object> { { "direction", "away" } } },
                },
            };
            var p = new LockerRoomPresenter(StalemateSession(rules), new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));

            StringAssert.Contains("다가가는 규칙이 아예 없어요", p.StalemateFeedback);
        }

        [Test]
        public void StalemateFeedback_HasApproach_SaysOpponentMightBeFleeing()
        {
            var rules = new List<Rule>
            {
                new Rule { Id = "r1", Label = "접근", Priority = 5, When = new List<Condition>(), Do = new RuleAction { Action = "approach" } },
            };
            var p = new LockerRoomPresenter(StalemateSession(rules), new FakeLLMClient(LLMResult.Ok("{\"recap\":\"x\"}")));

            StringAssert.Contains("도망 다니는 타입일 수 있어요", p.StalemateFeedback);
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
            Assert.Greater(session.LastMatch.HitsLanded, 1, "데모 데이터는 실제로 크게 맞고 진 시나리오 - 교착 상태로 오판되면 안 됨");

            // 이미 결과가 있으면 덮어쓰지 않음
            var existing = Match();
            var session2 = new GameSession { OpponentIndex = 1, DiscipleRuleSet = Rules(), LastMatch = existing };
            SampleMatch.EnsureForStandalone(session2);
            Assert.AreSame(existing, session2.LastMatch);
        }
    }
}
