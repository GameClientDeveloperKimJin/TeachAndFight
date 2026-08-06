using System.Collections.Generic;
using NUnit.Framework;
using TeachAndFight.Core.Rules;

namespace TeachAndFight.Core.Tests
{
    public class RuleValidatorTests
    {
        private static RuleSet MakeRuleSet(int maxSlots, params Rule[] rules)
        {
            return new RuleSet
            {
                Version = 1,
                FighterName = "제자",
                MaxSlots = maxSlots,
                Rules = new List<Rule>(rules),
            };
        }

        private static Rule MakeValidRule(string id, int priority = 5)
        {
            return new Rule
            {
                Id = id,
                Label = "궁 회피",
                When = new List<Condition>
                {
                    new Condition { Fact = "enemy_action", Op = "==", Value = "ultimate_startup" },
                    new Condition { Fact = "self_stamina_pct", Op = ">=", Value = 20 },
                },
                Do = new RuleAction { Action = "dash", Params = new Dictionary<string, object> { { "direction", "away" } } },
                Priority = priority,
                SourceUtterance = "상대가 궁 쓰면 일단 대시로 빠져",
            };
        }

        [Test]
        public void ApplyOps_ValidAddRule_Succeeds()
        {
            var current = MakeRuleSet(5);
            var op = new RuleOp { Op = "add", Rule = MakeValidRule("rule_01") };

            var result = RuleValidator.ApplyOps(current, new List<RuleOp> { op });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.UpdatedRuleSet.Rules.Count);
            Assert.AreEqual(0, current.Rules.Count, "원본 RuleSet은 변경되지 않아야 함");
        }

        [Test]
        public void ApplyOps_UnknownFact_FailsAndRollsBack()
        {
            var current = MakeRuleSet(5);
            var badRule = MakeValidRule("rule_01");
            badRule.When[0].Fact = "enemy_mood"; // 어휘 사전에 없는 fact
            var op = new RuleOp { Op = "add", Rule = badRule };

            var result = RuleValidator.ApplyOps(current, new List<RuleOp> { op });

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.UpdatedRuleSet);
            Assert.AreEqual("그건 제가 할 수 있는 게 아닌데요?", result.DiscipleReply);
            Assert.IsTrue(result.Errors.Exists(e => e.Contains("enemy_mood")));
        }

        [Test]
        public void ApplyOps_SlotOverflow_FailsAndRollsBack()
        {
            var current = MakeRuleSet(1, MakeValidRule("rule_01"));
            var op = new RuleOp { Op = "add", Rule = MakeValidRule("rule_02") };

            var result = RuleValidator.ApplyOps(current, new List<RuleOp> { op });

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.UpdatedRuleSet);
            Assert.AreEqual(1, current.Rules.Count, "원본 RuleSet은 변경되지 않아야 함");
            Assert.IsTrue(result.Errors.Exists(e => e.Contains("슬롯 초과")));
        }

        [Test]
        public void ApplyOps_PriorityOutOfRange_Fails()
        {
            var current = MakeRuleSet(5);
            var op = new RuleOp { Op = "add", Rule = MakeValidRule("rule_01", priority: 11) };

            var result = RuleValidator.ApplyOps(current, new List<RuleOp> { op });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ApplyOps_StringFactWithNumericOp_Fails()
        {
            var current = MakeRuleSet(5);
            var badRule = MakeValidRule("rule_01");
            badRule.When[0] = new Condition { Fact = "enemy_action", Op = ">", Value = "ultimate_startup" };
            var op = new RuleOp { Op = "add", Rule = badRule };

            var result = RuleValidator.ApplyOps(current, new List<RuleOp> { op });

            Assert.IsFalse(result.Success);
        }

        // #26 Tier1: 신규 fact 4종 정상 케이스
        [TestCase("self_wall_dist", 3.0)]
        [TestCase("self_action_duration", 2.5)]
        [TestCase("enemy_action_duration", 0.0)]
        [TestCase("enemy_whiff_count", 2.0)]
        public void ApplyOps_Tier1Fact_Valid_Succeeds(string fact, double value)
        {
            var current = MakeRuleSet(5);
            var rule = MakeValidRule("rule_01");
            rule.When[0] = new Condition { Fact = fact, Op = ">=", Value = value };
            var op = new RuleOp { Op = "add", Rule = rule };

            var result = RuleValidator.ApplyOps(current, new List<RuleOp> { op });

            Assert.IsTrue(result.Success, string.Join(", ", result.Errors));
        }

        [Test]
        public void ApplyOps_Tier1Fact_OutOfRange_Fails()
        {
            var current = MakeRuleSet(5);
            var rule = MakeValidRule("rule_01");
            rule.When[0] = new Condition { Fact = "self_wall_dist", Op = ">=", Value = 999.0 };
            var op = new RuleOp { Op = "add", Rule = rule };

            var result = RuleValidator.ApplyOps(current, new List<RuleOp> { op });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Errors.Exists(e => e.Contains("범위 초과")));
        }

        // "공격 준비하면" 등 OR 뜻 문장을 규칙 1개로 표현하게 해주는 포괄 값(유저 QA 피드백)
        [Test]
        public void ApplyOps_AttackStartupValue_Valid_Succeeds()
        {
            var current = MakeRuleSet(5);
            var rule = MakeValidRule("rule_01");
            rule.When[0] = new Condition { Fact = "enemy_action", Op = "==", Value = "attack_startup" };
            var op = new RuleOp { Op = "add", Rule = rule };

            var result = RuleValidator.ApplyOps(current, new List<RuleOp> { op });

            Assert.IsTrue(result.Success, string.Join(", ", result.Errors));
        }

        // #26 Tier2: 신규 action 2종 정상 케이스
        [TestCase("counter_attack")]
        [TestCase("feint")]
        public void ApplyOps_Tier2Action_Valid_Succeeds(string action)
        {
            var current = MakeRuleSet(5);
            var rule = MakeValidRule("rule_01");
            rule.Do = new RuleAction { Action = action };
            var op = new RuleOp { Op = "add", Rule = rule };

            var result = RuleValidator.ApplyOps(current, new List<RuleOp> { op });

            Assert.IsTrue(result.Success, string.Join(", ", result.Errors));
        }
    }
}
