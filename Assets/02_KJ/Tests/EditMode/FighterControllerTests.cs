using NUnit.Framework;
using UnityEngine;
using TeachAndFight.Combat;

namespace TeachAndFight.Core.Tests
{
    public class FighterControllerTests
    {
        private FighterController a;
        private FighterController b;
        private CombatConfig config;

        [SetUp]
        public void SetUp()
        {
            config = MakeConfig();
            a = new GameObject("A").AddComponent<FighterController>();
            b = new GameObject("B").AddComponent<FighterController>();
            a.Init(config, b, -3f);
            b.Init(config, a, 3f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(a.gameObject);
            Object.DestroyImmediate(b.gameObject);
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

        [Test]
        public void CanPerform_HeavyAttack_FalseWhenStaminaBelowCost()
        {
            var lowStamina = MakeConfig();
            lowStamina.Skills.HeavyAttack.Stamina = 200; // 항상 부족하도록

            var x = new GameObject("X").AddComponent<FighterController>();
            var y = new GameObject("Y").AddComponent<FighterController>();
            x.Init(lowStamina, y, -3f);
            y.Init(lowStamina, x, 3f);

            Assert.IsFalse(x.CanPerform(ActionType.HeavyAttack));
            Assert.IsFalse(x.TryPerform(ActionCommand.HeavyAttack()));
            Assert.AreEqual(FighterState.Idle, x.State);

            Object.DestroyImmediate(x.gameObject);
            Object.DestroyImmediate(y.gameObject);
        }

        [Test]
        public void AttackStartup_BlocksNewCommand_UntilResolved()
        {
            Assert.IsTrue(a.TryPerform(ActionCommand.LightAttack()));
            Assert.AreEqual(FighterState.AttackStartup, a.State);

            // 커밋 원칙: startup 중 새 명령 거부
            Assert.IsFalse(a.TryPerform(ActionCommand.Approach()));

            a.Tick(config.Skills.LightAttack.Startup + 0.001f);

            Assert.AreEqual(FighterState.Recovery, a.State);
        }

        [Test]
        public void LightAttack_InRange_DealsConfiguredDamage()
        {
            // 시작거리 6m는 사거리 밖 -> 접근 후 공격
            a.TryPerform(ActionCommand.Approach());
            for (int i = 0; i < 200 && a.Distance > config.Skills.LightAttack.Range; i++)
                a.Tick(0.1f);

            Assert.LessOrEqual(a.Distance, config.Skills.LightAttack.Range);

            a.TryPerform(ActionCommand.Idle());
            Assert.IsTrue(a.TryPerform(ActionCommand.LightAttack()));
            a.Tick(config.Skills.LightAttack.Startup + 0.001f);

            Assert.AreEqual(100f - config.Skills.LightAttack.Damage, b.Hp);
            Assert.AreEqual(FighterState.HitStun, b.State);
        }

        [Test]
        public void Dash_DuringInvulnerableWindow_IgnoresHit()
        {
            a.TryPerform(ActionCommand.DashTo(DashDirection.Away));
            Assert.AreEqual(FighterState.Dash, a.State);
            Assert.IsTrue(a.IsDashInvulnerable);

            a.ApplyHit(20f, false, b);

            Assert.AreEqual(100f, a.Hp, "무적 프레임 중 피격은 무시되어야 함");
        }

        [Test]
        public void StaminaRegen_FasterWhileIdle()
        {
            var x = new GameObject("X").AddComponent<FighterController>();
            var y = new GameObject("Y").AddComponent<FighterController>();
            x.Init(config, y, -3f);
            y.Init(config, x, 3f);

            x.TryPerform(ActionCommand.HeavyAttack()); // 스태미나 25 소모, AttackStartup 진입
            x.Tick(config.Skills.HeavyAttack.Startup + 0.001f); // Recovery 진입 -> whiff (사거리 밖)
            x.Tick(config.Skills.HeavyAttack.WhiffRecovery + 0.001f); // Idle 복귀

            float idleStamina = x.Stamina;
            Assert.Less(idleStamina, config.Stats.MaxStamina, "천장(100) 근처면 회복량 검증이 클램핑에 가려짐");

            const float probeDt = 0.1f;
            x.Tick(probeDt); // Idle 상태: 30/s 회복

            Assert.AreEqual(idleStamina + config.Stats.StaminaRegenIdlePerSec * probeDt, x.Stamina, 0.01f);

            Object.DestroyImmediate(x.gameObject);
            Object.DestroyImmediate(y.gameObject);
        }

        // #26 Tier2: feint
        [Test]
        public void Feint_DisguisedAsLightStartup_DealsNoDamageAndRecoversFast()
        {
            float staminaBefore = a.Stamina;

            Assert.IsTrue(a.TryPerform(ActionCommand.Feint()));
            Assert.AreEqual(FighterState.AttackStartup, a.State);
            Assert.AreEqual("light_startup", a.ActionStateLabel, "feint는 light_attack과 구분 안 되게 위장해야 함");
            Assert.AreEqual(staminaBefore - config.Skills.Feint.Stamina, a.Stamina);

            a.Tick(config.Skills.Feint.Startup + 0.001f);

            Assert.AreEqual(FighterState.Recovery, a.State);
            Assert.AreEqual(100f, b.Hp, "feint는 데미지가 없어야 함");

            a.Tick(config.Skills.Feint.Recovery + 0.001f);
            Assert.AreEqual(FighterState.Idle, a.State);
        }

        // #26 Tier2: counter_attack
        [Test]
        public void CounterAttack_WhenOpponentInStartup_DealsBonusDamage()
        {
            MoveIntoLightRange();

            b.TryPerform(ActionCommand.LightAttack()); // b를 startup 상태로 - a의 카운터 보너스 조건
            Assert.AreEqual(FighterState.AttackStartup, b.State);

            Assert.IsTrue(a.TryPerform(ActionCommand.CounterAttack()));
            a.Tick(config.Skills.CounterAttack.Startup + 0.001f);

            float expected = 100f - config.Skills.CounterAttack.Damage * config.Counter.DamageMultiplier;
            Assert.AreEqual(expected, b.Hp, 0.01f);
        }

        [Test]
        public void CounterAttack_WhenOpponentNotInStartup_DealsNormalDamage()
        {
            MoveIntoLightRange();

            Assert.IsTrue(a.TryPerform(ActionCommand.CounterAttack())); // b는 Idle - 보너스 없음
            a.Tick(config.Skills.CounterAttack.Startup + 0.001f);

            Assert.AreEqual(100f - config.Skills.CounterAttack.Damage, b.Hp, 0.01f);
        }

        // light_attack 사거리(1.2)까지만 접근 - counter_attack 사거리(1.5)가 더 넓어 안에 포함됨
        private void MoveIntoLightRange()
        {
            a.TryPerform(ActionCommand.Approach());
            for (int i = 0; i < 200 && a.Distance > config.Skills.LightAttack.Range; i++)
                a.Tick(0.1f);
            a.TryPerform(ActionCommand.Idle());
        }
    }
}
