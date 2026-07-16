using Newtonsoft.Json;

namespace TeachAndFight.Combat
{
    // 02장 "모든 수치는 combat_config.json에서 로드" - 코드에 하드코딩 금지
    public class StatsConfig
    {
        [JsonProperty("maxHp")] public float MaxHp;
        [JsonProperty("maxStamina")] public float MaxStamina;
        [JsonProperty("staminaRegenPerSec")] public float StaminaRegenPerSec;
        [JsonProperty("staminaRegenIdlePerSec")] public float StaminaRegenIdlePerSec;
        [JsonProperty("moveSpeed")] public float MoveSpeed;
        [JsonProperty("ultGaugePerDamageDealt")] public float UltGaugePerDamageDealt;
        [JsonProperty("ultGaugePerDamageTaken")] public float UltGaugePerDamageTaken;
        [JsonProperty("maxUltGauge")] public float MaxUltGauge;
    }

    public class ArenaConfig
    {
        [JsonProperty("width")] public float Width;
        [JsonProperty("startDistance")] public float StartDistance;
    }

    public class MatchConfig
    {
        [JsonProperty("durationSec")] public float DurationSec;
    }

    public class DashConfig
    {
        [JsonProperty("duration")] public float Duration;
        [JsonProperty("invulnerableDuration")] public float InvulnerableDuration;
        [JsonProperty("moveDistance")] public float MoveDistance;
    }

    public class HitReactionConfig
    {
        [JsonProperty("hitStunDuration")] public float HitStunDuration;
        [JsonProperty("heavyHitStunDuration")] public float HeavyHitStunDuration;
        [JsonProperty("knockback")] public float Knockback;
    }

    public class SkillConfig
    {
        [JsonProperty("damage")] public float Damage;
        [JsonProperty("range")] public float Range;
        [JsonProperty("startup")] public float Startup;
        [JsonProperty("recovery")] public float Recovery;
        [JsonProperty("whiffRecovery")] public float WhiffRecovery;
        [JsonProperty("stamina")] public float Stamina;
        [JsonProperty("cooldown")] public float Cooldown;
    }

    public class SkillsConfig
    {
        [JsonProperty("light_attack")] public SkillConfig LightAttack;
        [JsonProperty("heavy_attack")] public SkillConfig HeavyAttack;
        [JsonProperty("dash")] public SkillConfig Dash;
        [JsonProperty("ultimate")] public SkillConfig Ultimate;
    }

    public class CombatConfig
    {
        [JsonProperty("stats")] public StatsConfig Stats;
        [JsonProperty("arena")] public ArenaConfig Arena;
        [JsonProperty("match")] public MatchConfig Match;
        [JsonProperty("dash")] public DashConfig Dash;
        [JsonProperty("hitReaction")] public HitReactionConfig HitReaction;
        [JsonProperty("skills")] public SkillsConfig Skills;
    }
}
