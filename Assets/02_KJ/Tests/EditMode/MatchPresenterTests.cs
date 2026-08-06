using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TeachAndFight.Combat;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;
using TeachAndFight.Match;

namespace TeachAndFight.Core.Tests
{
    // #13 완료기준: 발동 규칙 라벨(OnRuleFired)이 실제 RuleEvaluator 로그(EventLog rule_fired)와 일치.
    public class MatchPresenterTests
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

        [Test]
        public void OnRuleFired_LabelAndEventLog_MatchDiscipleRuleSet()
        {
            var disciple = new RuleSet
            {
                Version = 1,
                FighterName = "제자",
                MaxSlots = 8,
                Rules = new List<Rule>
                {
                    new Rule
                    {
                        Id = "rule_a",
                        Label = "항상 접근",
                        When = new List<Condition> { new Condition { Fact = "distance", Op = ">=", Value = 0 } },
                        Do = new RuleAction { Action = "approach" },
                        Priority = 5,
                    },
                },
            };
            var opponent = new RuleSet { Version = 1, FighterName = "상대", MaxSlots = 8, Rules = new List<Rule>() };
            var session = new GameSession { DiscipleRuleSet = disciple, CurrentOpponent = opponent, OpponentIndex = 1 };

            float half = config.Arena.StartDistance * 0.5f;
            self.Init(config, enemy, -half);
            enemy.Init(config, self, half);

            var presenter = new MatchPresenter(session, config, self, enemy);
            var fired = new List<(FighterController who, string ruleId, string label)>();
            presenter.OnRuleFired += (who, ruleId, label) => fired.Add((who, ruleId, label));

            for (int i = 0; i < 5; i++)
                presenter.Step(0.1f);

            Assert.IsTrue(fired.Any(f => f.who == self && f.ruleId == "rule_a" && f.label == "항상 접근"),
                "OnRuleFired가 self/rule_a/라벨로 발동돼야 함");

            var loggedFired = presenter.SelfEventLog.Where(e => e.Type == "rule_fired").ToList();
            CollectionAssert.IsNotEmpty(loggedFired, "EventLog에 rule_fired가 기록돼야 함");
            Assert.IsTrue(loggedFired.All(e => e.RuleId == "rule_a"),
                "EventLog에 기록된 rule_fired의 ruleId가 실제 발동시킨 규칙(rule_a)과 일치해야 함");
        }

        [Test]
        public void BlankDisciple_LosesToRush_MatchResultReflectsLoss()
        {
            var disciple = new RuleSet { Version = 1, FighterName = "제자", MaxSlots = 8, Rules = new List<Rule>() };
            var rush = OpponentRuleSetLoader.Load(1);
            var session = new GameSession { DiscipleRuleSet = disciple, CurrentOpponent = rush, OpponentIndex = 1 };

            float half = config.Arena.StartDistance * 0.5f;
            self.Init(config, enemy, -half);
            enemy.Init(config, self, half);

            var presenter = new MatchPresenter(session, config, self, enemy);

            const float dt = 0.1f;
            int maxSteps = Mathf.CeilToInt(config.Match.DurationSec / dt) + 10;
            for (int i = 0; i < maxSteps && !presenter.Concluded; i++)
                presenter.Step(dt);

            Assert.IsTrue(presenter.Concluded, "백지 제자 vs 러쉬는 매치 시간 안에 결판나야 함");
            Assert.IsFalse(presenter.Result.Won, "백지 규칙셋 제자는 러쉬에게 패배해야 함(#12 완료기준과 동일 전제)");
            Assert.AreSame(presenter.Result, session.LastMatch, "종료 시 session.LastMatch에 결과가 담겨야 함(LockerRoom 접점 계약)");
        }

        // 유저 요구사항: 교착 상태면 60초 다 안 기다리고 조기 종료(패배 처리)돼야 함.
        [Test]
        public void Stalemate_NoHitsForThreshold_ConcludesEarlyAsLoss()
        {
            // 둘 다 규칙 없음(Idle만) - 영원히 안 부딪히는 순수 교착 시나리오.
            var disciple = new RuleSet { Version = 1, FighterName = "제자", MaxSlots = 8, Rules = new List<Rule>() };
            var opponent = new RuleSet { Version = 1, FighterName = "상대", MaxSlots = 8, Rules = new List<Rule>() };
            var session = new GameSession { DiscipleRuleSet = disciple, CurrentOpponent = opponent, OpponentIndex = 1 };

            float half = config.Arena.StartDistance * 0.5f;
            self.Init(config, enemy, -half);
            enemy.Init(config, self, half);

            var presenter = new MatchPresenter(session, config, self, enemy);

            const float dt = 0.1f;
            int steps = 0;
            while (!presenter.Concluded && steps < 200) // 20초치 - 15초 근방에서 끝나야 함
            {
                presenter.Step(dt);
                steps++;
            }

            float elapsed = steps * dt;
            Assert.IsTrue(presenter.Concluded, "교착이면 60초 끝까지 안 가고 조기 종료돼야 함");
            Assert.Less(elapsed, 20f, "15초 임계값보다 한참 늦게 끝나면 조기종료가 동작 안 한 것");
            Assert.GreaterOrEqual(elapsed, 15f, "15초보다 일찍 끝나면 임계값이 잘못 걸린 것");
            Assert.IsFalse(presenter.Result.Won, "HP 동률(100:100)이라 패배로 처리돼야 함");
            Assert.AreEqual(0, presenter.Result.HitsLanded);
        }
    }
}
