using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json;
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
                    Feint = new SkillConfig { Damage = 0, Range = 0, Startup = 0.15f, Recovery = 0.15f, WhiffRecovery = 0, Stamina = 5, Cooldown = 0 },
                    CounterAttack = new SkillConfig { Damage = 8, Range = 1.5f, Startup = 0.08f, Recovery = 0.25f, WhiffRecovery = 0.35f, Stamina = 15, Cooldown = 0 },
                },
                Counter = new CounterConfig { DamageMultiplier = 1.5f },
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

        [Test]
        public void Evaluate_StaminaExactlyZero_SkipsAllPaidRules_FallsBackToFreeAction()
        {
            // 완료기준 명시 케이스: 스태미나 0에서 공격 규칙(유료) 전부 스킵 -> 무료 행동(approach)으로 폴백
            var noRegenConfig = MakeConfig();
            noRegenConfig.Stats.StaminaRegenPerSec = 0f;
            noRegenConfig.Stats.StaminaRegenIdlePerSec = 0f;

            var x = new GameObject("X").AddComponent<FighterController>();
            var y = new GameObject("Y").AddComponent<FighterController>();
            x.Init(noRegenConfig, y, -3f);
            y.Init(noRegenConfig, x, 3f);

            PerformAndSettle(x, ActionCommand.HeavyAttack()); // 100 -> 75
            PerformAndSettle(x, ActionCommand.HeavyAttack()); // 75 -> 50
            PerformAndSettle(x, ActionCommand.HeavyAttack()); // 50 -> 25
            PerformAndSettle(x, ActionCommand.HeavyAttack()); // 25 -> 0

            Assert.AreEqual(0f, x.Stamina);

            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "want_heavy",
                        When = new List<Condition> { new Condition { Fact = "self_stamina_pct", Op = "<=", Value = 100 } },
                        Do = new RuleAction { Action = "heavy_attack" },
                        Priority = 9,
                    },
                    new Rule
                    {
                        Id = "want_light",
                        When = new List<Condition> { new Condition { Fact = "self_stamina_pct", Op = "<=", Value = 100 } },
                        Do = new RuleAction { Action = "light_attack" },
                        Priority = 7,
                    },
                    new Rule
                    {
                        Id = "fallback_approach",
                        When = new List<Condition> { new Condition { Fact = "self_stamina_pct", Op = "<=", Value = 100 } },
                        Do = new RuleAction { Action = "approach" },
                        Priority = 5,
                    },
                },
            };

            var eventLog = new EventLog();
            var evaluator = new RuleEvaluator(ruleSet, eventLog);

            var cmd = evaluator.Evaluate(x, y, timeLeft: 60f, matchTime: 30f);

            Assert.AreEqual(ActionType.Approach, cmd.Action, "유료 행동 2개 모두 막히면 자원 소모 없는 규칙까지 폴백해야 함");
            Assert.IsTrue(eventLog.Events.Any(e => e.Type == "stamina_out" && e.RuleId == "want_heavy"));
            Assert.IsTrue(eventLog.Events.Any(e => e.Type == "stamina_out" && e.RuleId == "want_light"));
            Assert.IsTrue(eventLog.Events.Any(e => e.Type == "rule_fired" && e.RuleId == "fallback_approach"));

            Object.DestroyImmediate(x.gameObject);
            Object.DestroyImmediate(y.gameObject);
        }

        [Test]
        public void Evaluate_JsonLoadedRuleSet_DrivesRealCombatOverTicks()
        {
            // 완료기준: "JSON 규칙셋 로드 후 실제 전투에 반영되어 동작" - 파일이 아닌 JSON 텍스트에서 역직렬화한
            // RuleSet을 그대로 RuleEvaluator에 태우고, Evaluate -> TryPerform -> Tick 루프를 여러 틱 돌려
            // 실제 FighterController 상태(피해)에 반영되는지 확인한다.
            const string json = @"{
              ""version"": 1,
              ""fighter_name"": ""제자"",
              ""max_slots"": 5,
              ""rules"": [
                { ""id"": ""close_in"", ""when"": [ { ""fact"": ""distance"", ""op"": "">"", ""value"": 1.2 } ], ""do"": { ""action"": ""approach"" }, ""priority"": 5 },
                { ""id"": ""poke"", ""when"": [ { ""fact"": ""distance"", ""op"": ""<="", ""value"": 1.2 } ], ""do"": { ""action"": ""light_attack"" }, ""priority"": 8 }
              ]
            }";

            var ruleSet = JsonConvert.DeserializeObject<RuleSet>(json);
            CollectionAssert.IsEmpty(RuleValidator.ValidateRuleSet(ruleSet), "샘플 JSON은 어휘사전/스키마를 통과해야 함");

            var x = new GameObject("X").AddComponent<FighterController>();
            var y = new GameObject("Y").AddComponent<FighterController>();
            x.Init(config, y, -3f);
            y.Init(config, x, 3f); // distance 6 - close_in 규칙으로 접근부터 시작해야 함

            var eventLog = new EventLog();
            var evaluator = new RuleEvaluator(ruleSet, eventLog);

            const float dt = 0.1f;
            float matchTime = 0f;
            for (int i = 0; i < 150; i++)
            {
                if (x.State == FighterState.Idle || x.State == FighterState.Move)
                {
                    var cmd = evaluator.Evaluate(x, y, timeLeft: 60f - matchTime, matchTime: matchTime);
                    if (!x.TryPerform(cmd))
                        x.TryPerform(ActionCommand.Idle());
                }

                x.Tick(dt);
                y.Tick(dt);
                matchTime += dt;
            }

            Assert.Less(y.Hp, y.MaxHp, "JSON 규칙셋의 poke 규칙이 실제로 실행되어 피해를 입혀야 함");
            Assert.IsTrue(eventLog.Events.Any(e => e.Type == "rule_fired" && e.RuleId == "close_in"));
            Assert.IsTrue(eventLog.Events.Any(e => e.Type == "rule_fired" && e.RuleId == "poke"));

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

        // #26 Tier2: RuleEvaluator가 신규 action을 ActionCommand로 정확히 빌드하는지
        [TestCase("counter_attack", ActionType.CounterAttack)]
        [TestCase("feint", ActionType.Feint)]
        public void Evaluate_Tier2Action_BuildsExpectedCommand(string action, ActionType expected)
        {
            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule> { AttackRule("only", 5, action) },
            };
            var evaluator = new RuleEvaluator(ruleSet);

            var cmd = evaluator.Evaluate(self, enemy, timeLeft: 60f, matchTime: 0f);

            Assert.AreEqual(expected, cmd.Action);
        }

        // #26 Tier1: 신규 fact가 FactSnapshot을 거쳐 조건 평가에 실제로 반영되는지
        [Test]
        public void Evaluate_EnemyWhiffCountFact_MatchesAfterEnemyWhiffs()
        {
            // enemy(light_attack)가 self 사거리 밖(distance 1.0 < range 1.2라 사실 안 맞음) -> 강제로 멀리 재배치해 헛스윙 유도
            enemy.TryPerform(ActionCommand.Retreat());
            for (int i = 0; i < 50; i++) enemy.Tick(0.1f); // self와 거리 벌리기
            enemy.TryPerform(ActionCommand.Idle());

            Assert.IsTrue(enemy.TryPerform(ActionCommand.LightAttack()));
            enemy.Tick(config.Skills.LightAttack.Startup + 0.001f); // 사거리 밖 -> 헛스윙

            Assert.AreEqual(1, enemy.RecentWhiffCount);

            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "punish_whiff",
                        When = new List<Condition> { new Condition { Fact = "enemy_whiff_count", Op = ">=", Value = 1 } },
                        Do = new RuleAction { Action = "heavy_attack" },
                        Priority = 5,
                    },
                },
            };
            var evaluator = new RuleEvaluator(ruleSet);

            var cmd = evaluator.Evaluate(self, enemy, timeLeft: 60f, matchTime: 0f);

            Assert.AreEqual(ActionType.HeavyAttack, cmd.Action, "enemy_whiff_count fact가 실제 헛스윙 횟수를 반영해야 함");
        }

        // "공격 준비하면"처럼 light_startup/heavy_startup을 하나로 묶어 쓰기 위한 attack_startup -
        // 실제 상태값이 아니라 RuleEvaluator의 특수 매칭 값(유저 QA 피드백으로 추가).
        [Test]
        public void Evaluate_AttackStartupValue_MatchesLightStartup()
        {
            Assert.IsTrue(enemy.TryPerform(ActionCommand.LightAttack()));

            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "counter",
                        When = new List<Condition> { new Condition { Fact = "enemy_action", Op = "==", Value = "attack_startup" } },
                        Do = new RuleAction { Action = "counter_attack" },
                        Priority = 5,
                    },
                },
            };
            var evaluator = new RuleEvaluator(ruleSet);

            var cmd = evaluator.Evaluate(self, enemy, timeLeft: 60f, matchTime: 0f);

            Assert.AreEqual(ActionType.CounterAttack, cmd.Action);
        }

        [Test]
        public void Evaluate_AttackStartupValue_MatchesHeavyStartup()
        {
            Assert.IsTrue(enemy.TryPerform(ActionCommand.HeavyAttack()));

            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "counter",
                        When = new List<Condition> { new Condition { Fact = "enemy_action", Op = "==", Value = "attack_startup" } },
                        Do = new RuleAction { Action = "counter_attack" },
                        Priority = 5,
                    },
                },
            };
            var evaluator = new RuleEvaluator(ruleSet);

            var cmd = evaluator.Evaluate(self, enemy, timeLeft: 60f, matchTime: 0f);

            Assert.AreEqual(ActionType.CounterAttack, cmd.Action);
        }

        [Test]
        public void Evaluate_AttackStartupValue_DoesNotMatchIdle()
        {
            // enemy는 SetUp에서 Idle 상태 - attack_startup 조건은 안 걸리고 폴백(approach)으로 가야 함
            var ruleSet = new RuleSet
            {
                MaxSlots = 5,
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "counter",
                        When = new List<Condition> { new Condition { Fact = "enemy_action", Op = "==", Value = "attack_startup" } },
                        Do = new RuleAction { Action = "counter_attack" },
                        Priority = 8,
                    },
                    new Rule
                    {
                        Id = "fallback",
                        When = new List<Condition> { new Condition { Fact = "distance", Op = "<=", Value = 12 } },
                        Do = new RuleAction { Action = "approach" },
                        Priority = 3,
                    },
                },
            };
            var evaluator = new RuleEvaluator(ruleSet);

            var cmd = evaluator.Evaluate(self, enemy, timeLeft: 60f, matchTime: 0f);

            Assert.AreEqual(ActionType.Approach, cmd.Action, "enemy가 idle이면 attack_startup 조건은 안 걸려야 함");
        }
    }
}
