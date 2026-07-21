using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TeachAndFight.Combat;
using TeachAndFight.Core.Rules;

namespace TeachAndFight.Core.Tests
{
    public class RuleEvaluatorTests
    {
        private FighterController self;
        private FighterController enemy;
        private CombatConfig config;

        [SetUp]
        public void SetUp()
        {
            config = MakeConfig();
            self = new GameObject("Self").AddComponent<FighterController>();
            enemy = new GameObject("Enemy").AddComponent<FighterController>();
            self.Init(config, enemy, -0.5f);
            enemy.Init(config, self, 0.5f); // distance 1.0, LightAttack.Range(1.2) 안쪽에서 시작
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(self.gameObject);
            Object.DestroyImmediate(enemy.gameObject);
        }

        private static CombatConfig MakeConfig()
        {
            return new CombatConfig
            {
                Stats = new StatsConfig
                {
                    MaxHp = 100, MaxStamina = 100, StaminaRegenPerSec = 15, StaminaRegenIdlePerSec = 30,
                    MoveSpeed = 3.5f, UltGaugePerDamageDealt = 1.5f, UltGaugePerDamageTaken = 1.0f, MaxUltGauge = 100,
                },
                Arena = new ArenaConfig { Width = 12, StartDistance = 6 },
                Match = new MatchConfig { DurationSec = 60 },
                Dash = new DashConfig { Duration = 0.2f, InvulnerableDuration = 0.15f, MoveDistance = 2.5f },
                HitReaction = new HitReactionConfig { HitStunDuration = 0.3f, HeavyHitStunDuration = 0.5f, Knockback = 0.5f },
                Skills = new SkillsConfig
                {
                    LightAttack = new SkillConfig { Damage = 8, Range = 1.2f, Startup = 0.15f, Recovery = 0.25f, WhiffRecovery = 0.35f, Stamina = 10, Cooldown = 0 },
                    HeavyAttack = new SkillConfig { Damage = 20, Range = 1.5f, Startup = 0.45f, Recovery = 0.50f, WhiffRecovery = 0.90f, Stamina = 25, Cooldown = 0 },
                    Dash = new SkillConfig { Damage = 0, Range = 0, Startup = 0.05f, Recovery = 0.15f, WhiffRecovery = 0, Stamina = 20, Cooldown = 1.0f },
                    Ultimate = new SkillConfig { Damage = 35, Range = 2.0f, Startup = 0.60f, Recovery = 0.70f, WhiffRecovery = 1.20f, Stamina = 0, Cooldown = 0 },
                },
            };
        }

        private static Rule AttackRule(string id, int priority, string action = "heavy_attack") => new Rule
        {
            Id = id,
            When = new List<Condition> { new Condition { Fact = "distance", Op = "<=", Value = 12 } },
            Do = new RuleAction { Action = action },
            Priority = priority,
        };

        [Test]
        public void Evaluate_PicksHighestPriorityMatchingRule()
        {
            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule> { AttackRule("low", 3, "light_attack"), AttackRule("high", 8, "heavy_attack") },
            };
            var evaluator = new RuleEvaluator(ruleSet);

            var cmd = evaluator.Evaluate(self, enemy, timeLeft: 60f, matchTime: 0f);

            Assert.AreEqual(ActionType.HeavyAttack, cmd.Action);
        }

        [Test]
        public void Evaluate_TieBreak_UsesArrayOrder()
        {
            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule> { AttackRule("first", 5, "heavy_attack"), AttackRule("second", 5, "light_attack") },
            };
            var evaluator = new RuleEvaluator(ruleSet);

            var cmd = evaluator.Evaluate(self, enemy, timeLeft: 60f, matchTime: 0f);

            Assert.AreEqual(ActionType.HeavyAttack, cmd.Action, "동률 priority는 배열 순서(먼저 오는 규칙)를 따라야 함");
        }

        [Test]
        public void Evaluate_NoMatchingRule_ReturnsIdle()
        {
            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "never",
                        When = new List<Condition> { new Condition { Fact = "distance", Op = ">", Value = 999 } },
                        Do = new RuleAction { Action = "heavy_attack" },
                        Priority = 5,
                    },
                },
            };
            var evaluator = new RuleEvaluator(ruleSet);

            var cmd = evaluator.Evaluate(self, enemy, timeLeft: 60f, matchTime: 0f);

            Assert.AreEqual(ActionType.Idle, cmd.Action);
        }

        [Test]
        public void Evaluate_FallsBackToNextRule_WhenStaminaInsufficient()
        {
            // 회복 0으로 고정한 config에서 히비(25) 3회 + 라이트(10) 1회 소모 -> 15 남음
            // (히비 25 미만이라 막히지만 라이트 10 이상이라 가능한 구간)
            var noRegenConfig = MakeConfig();
            noRegenConfig.Stats.StaminaRegenPerSec = 0f;
            noRegenConfig.Stats.StaminaRegenIdlePerSec = 0f;

            var x = new GameObject("X").AddComponent<FighterController>();
            var y = new GameObject("Y").AddComponent<FighterController>();
            x.Init(noRegenConfig, y, -3f);
            y.Init(noRegenConfig, x, 3f);

            PerformAndSettle(x, ActionCommand.HeavyAttack());
            PerformAndSettle(x, ActionCommand.HeavyAttack());
            PerformAndSettle(x, ActionCommand.HeavyAttack());
            PerformAndSettle(x, ActionCommand.LightAttack());

            Assert.Less(x.Stamina, noRegenConfig.Skills.HeavyAttack.Stamina);
            Assert.GreaterOrEqual(x.Stamina, noRegenConfig.Skills.LightAttack.Stamina);

            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "want_heavy",
                        When = new List<Condition> { new Condition { Fact = "self_stamina_pct", Op = "<", Value = 999 } },
                        Do = new RuleAction { Action = "heavy_attack" },
                        Priority = 9,
                    },
                    new Rule
                    {
                        Id = "fallback_light",
                        When = new List<Condition> { new Condition { Fact = "self_stamina_pct", Op = "<", Value = 999 } },
                        Do = new RuleAction { Action = "light_attack" },
                        Priority = 5,
                    },
                },
            };

            var eventLog = new EventLog();
            var evaluator = new RuleEvaluator(ruleSet, eventLog);

            var cmd = evaluator.Evaluate(x, y, timeLeft: 60f, matchTime: 12.3f);

            Assert.AreEqual(ActionType.LightAttack, cmd.Action, "스태미나 부족한 첫 규칙은 스킵하고 다음 규칙으로 폴백해야 함");
            Assert.IsTrue(eventLog.Events.Any(e => e.Type == "stamina_out" && e.RuleId == "want_heavy"));
            Assert.IsTrue(eventLog.Events.Any(e => e.Type == "rule_fired" && e.RuleId == "fallback_light"));

            Object.DestroyImmediate(x.gameObject);
            Object.DestroyImmediate(y.gameObject);
        }

        // Startup/Recovery를 큰 dt로 강제 통과시켜 Idle까지 되돌림 (회복 0 config라 dt 크기는 스태미나에 영향 없음)
        private static void PerformAndSettle(FighterController fighter, ActionCommand cmd)
        {
            Assert.IsTrue(fighter.TryPerform(cmd));
            fighter.Tick(10f); // AttackStartup -> Recovery
            fighter.Tick(10f); // Recovery -> Idle
        }

        [Test]
        public void Evaluate_OnRuleFired_InvokedWithRuleId()
        {
            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule> { AttackRule("only", 5, "light_attack") },
            };
            var evaluator = new RuleEvaluator(ruleSet);
            string firedId = null;
            evaluator.OnRuleFired += id => firedId = id;

            evaluator.Evaluate(self, enemy, timeLeft: 60f, matchTime: 0f);

            Assert.AreEqual("only", firedId);
        }
    }
}
