using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TeachAndFight.Combat;
using TeachAndFight.Core.Rules;

namespace TeachAndFight.Core.Tests
{
    // #12 완료기준: 상대 5종 JSON 전부 RuleValidator 통과 + 백지 규칙셋으로 1차전(러쉬) 반드시 패배.
    public class OpponentRuleSetTests
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
        public void Load_AllFiveOpponents_PassValidator()
        {
            for (int i = OpponentRuleSetLoader.MinIndex; i <= OpponentRuleSetLoader.MaxIndex; i++)
            {
                var ruleSet = OpponentRuleSetLoader.Load(i);
                var errors = RuleValidator.ValidateRuleSet(ruleSet);

                CollectionAssert.IsEmpty(errors, $"opponent_{i:00}({ruleSet.FighterName}) 검증 실패: {string.Join(" / ", errors)}");
            }
        }

        [Test]
        public void BlankDiscipleRuleSet_LosesToRush_WithinMatchDuration()
        {
            var blankDisciple = new RuleSet
            {
                Version = 1,
                FighterName = "제자",
                MaxSlots = 8,
                Rules = new List<Rule>(),
            };
            var rush = OpponentRuleSetLoader.Load(1);

            float half = config.Arena.StartDistance * 0.5f;
            self.Init(config, enemy, -half);
            enemy.Init(config, self, half);

            var selfEvaluator = new RuleEvaluator(blankDisciple);
            var enemyEvaluator = new RuleEvaluator(rush);

            const float dt = 0.1f;
            const float durationSec = 60f;
            float matchTime = 0f;

            while (matchTime < durationSec && self.State != FighterState.Down && enemy.State != FighterState.Down)
            {
                if (self.State == FighterState.Idle || self.State == FighterState.Move)
                {
                    var cmd = selfEvaluator.Evaluate(self, enemy, timeLeft: durationSec - matchTime, matchTime: matchTime);
                    if (!self.TryPerform(cmd))
                        self.TryPerform(ActionCommand.Idle());
                }

                if (enemy.State == FighterState.Idle || enemy.State == FighterState.Move)
                {
                    var cmd = enemyEvaluator.Evaluate(enemy, self, timeLeft: durationSec - matchTime, matchTime: matchTime);
                    if (!enemy.TryPerform(cmd))
                        enemy.TryPerform(ActionCommand.Idle());
                }

                self.Tick(dt);
                enemy.Tick(dt);
                matchTime += dt;
            }

            Assert.AreEqual(FighterState.Down, self.State,
                $"백지 규칙셋 제자가 {durationSec}s 안에 러쉬에게 KO당해야 함(#12 완료기준) - 종료 시점 제자 HP {self.HpPct:0}%, 러쉬 HP {enemy.HpPct:0}%, 경과 {matchTime:0.0}s");
        }
    }
}
