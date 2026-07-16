using System;
using UnityEngine;

namespace TeachAndFight.Combat
{
    // 02장 FighterController FSM. 상태: Idle/Move/Dash/AttackStartup/AttackActive/Recovery/HitStun/Down
    // 행동 커밋 원칙: AttackStartup 진입 후 취소 불가. 새 의사결정은 Idle/Move 상태에서만 수용.
    public class FighterController : MonoBehaviour
    {
        [SerializeField] private string fighterName = "Fighter";

        public string FighterName => fighterName;
        public FighterState State { get; private set; } = FighterState.Idle;
        public float Hp { get; private set; }
        public float Stamina { get; private set; }
        public float UltGauge { get; private set; }
        public bool FacingRight { get; private set; } = true;

        public FighterController Opponent { get; private set; }
        public CombatConfig Config { get; private set; }

        public float MaxHp => Config.Stats.MaxHp;
        public float MaxStamina => Config.Stats.MaxStamina;
        public float MaxUltGauge => Config.Stats.MaxUltGauge;
        public float HpPct => Hp / MaxHp * 100f;
        public float StaminaPct => Stamina / MaxStamina * 100f;
        public float UltGaugePct => UltGauge / MaxUltGauge * 100f;

        public float Distance => Opponent == null
            ? 0f
            : Mathf.Abs(transform.position.x - Opponent.transform.position.x);

        public bool IsDashInvulnerable =>
            State == FighterState.Dash &&
            (Config.Dash.Duration - stateTimer) < Config.Dash.InvulnerableDuration;

        // (공격자, 피해량, heavy/ultimate 여부)
        public event Action<FighterController, float, bool> OnHitLanded;
        public event Action<FighterController, float, bool> OnHitTaken;
        public event Action<FighterController> OnWhiff;
        public event Action<FighterController> OnDown;

        private float stateTimer;
        private float dashCooldownTimer;
        private float arenaHalfWidth;
        private ActionType committedAction;
        private ActionType activeMoveAction;
        private DashDirection dashDirection;
        private float moveDir;
        private float keepDistanceRange;

        public void Init(CombatConfig config, FighterController opponent, float startX)
        {
            Config = config;
            Opponent = opponent;
            Hp = config.Stats.MaxHp;
            Stamina = config.Stats.MaxStamina;
            UltGauge = 0f;
            State = FighterState.Idle;
            stateTimer = 0f;
            dashCooldownTimer = 0f;
            moveDir = 0f;
            activeMoveAction = ActionType.Idle;
            arenaHalfWidth = config.Arena.Width * 0.5f;

            var pos = transform.position;
            pos.x = startX;
            transform.position = pos;
        }

        // 스태미나/쿨다운/게이지 부족 시 false - 호출측(RuleEvaluator)이 다음 규칙으로 폴백
        public bool CanPerform(ActionType action)
        {
            if (State != FighterState.Idle && State != FighterState.Move)
                return false;

            switch (action)
            {
                case ActionType.Dash:
                    return Stamina >= Config.Skills.Dash.Stamina && dashCooldownTimer <= 0f;
                case ActionType.LightAttack:
                    return Stamina >= Config.Skills.LightAttack.Stamina;
                case ActionType.HeavyAttack:
                    return Stamina >= Config.Skills.HeavyAttack.Stamina;
                case ActionType.Ultimate:
                    return UltGauge >= MaxUltGauge;
                default:
                    return true; // Idle/Approach/Retreat/KeepDistance는 자원 소모 없음
            }
        }

        public bool TryPerform(ActionCommand cmd)
        {
            if (!CanPerform(cmd.Action))
                return false;

            switch (cmd.Action)
            {
                case ActionType.Idle:
                    State = FighterState.Idle;
                    activeMoveAction = ActionType.Idle;
                    moveDir = 0f;
                    break;

                case ActionType.Approach:
                case ActionType.Retreat:
                    State = FighterState.Move;
                    activeMoveAction = cmd.Action;
                    break;

                case ActionType.KeepDistance:
                    State = FighterState.Move;
                    activeMoveAction = ActionType.KeepDistance;
                    keepDistanceRange = cmd.KeepDistanceRange;
                    break;

                case ActionType.Dash:
                    Stamina -= Config.Skills.Dash.Stamina;
                    dashDirection = cmd.DashDirection;
                    dashCooldownTimer = Config.Skills.Dash.Cooldown;
                    State = FighterState.Dash;
                    stateTimer = Config.Dash.Duration;
                    break;

                case ActionType.LightAttack:
                    BeginAttackStartup(ActionType.LightAttack, Config.Skills.LightAttack);
                    break;

                case ActionType.HeavyAttack:
                    BeginAttackStartup(ActionType.HeavyAttack, Config.Skills.HeavyAttack);
                    break;

                case ActionType.Ultimate:
                    UltGauge = 0f;
                    BeginAttackStartup(ActionType.Ultimate, Config.Skills.Ultimate);
                    break;
            }

            return true;
        }

        private void BeginAttackStartup(ActionType action, SkillConfig skill)
        {
            if (action != ActionType.Ultimate)
                Stamina -= skill.Stamina;

            committedAction = action;
            State = FighterState.AttackStartup;
            stateTimer = skill.Startup;
        }

        public void Tick(float dt)
        {
            if (State == FighterState.Down)
                return;

            RegenStamina(dt);

            if (dashCooldownTimer > 0f)
                dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - dt);

            switch (State)
            {
                case FighterState.Move:
                    UpdateMoveDirection();
                    MoveByDir(moveDir, dt, Config.Stats.MoveSpeed);
                    break;

                case FighterState.Dash:
                    TickDash(dt);
                    break;

                case FighterState.AttackStartup:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                        ResolveAttackActive();
                    break;

                case FighterState.Recovery:
                case FighterState.HitStun:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                        State = FighterState.Idle;
                    break;
            }
        }

        private void TickDash(float dt)
        {
            stateTimer -= dt;
            float diff = Opponent.transform.position.x - transform.position.x;
            float towardSign = Mathf.Approximately(diff, 0f) ? 1f : Mathf.Sign(diff);
            float dashSign = dashDirection == DashDirection.Toward ? towardSign : -towardSign;
            float dashSpeed = Config.Dash.MoveDistance / Config.Dash.Duration;
            MoveByDir(dashSign, dt, dashSpeed);

            if (stateTimer <= 0f)
                State = FighterState.Idle;
        }

        private void ResolveAttackActive()
        {
            State = FighterState.AttackActive;
            var skill = GetSkillConfig(committedAction);
            bool isHeavyOrUlt = committedAction == ActionType.HeavyAttack || committedAction == ActionType.Ultimate;

            bool hit = Opponent.State != FighterState.Down && Distance <= skill.Range;

            if (hit)
            {
                UltGauge = Mathf.Min(MaxUltGauge, UltGauge + skill.Damage * Config.Stats.UltGaugePerDamageDealt);
                OnHitLanded?.Invoke(this, skill.Damage, isHeavyOrUlt);
                Opponent.ApplyHit(skill.Damage, isHeavyOrUlt, this);
                State = FighterState.Recovery;
                stateTimer = skill.Recovery;
            }
            else
            {
                OnWhiff?.Invoke(this);
                State = FighterState.Recovery;
                stateTimer = skill.WhiffRecovery;
            }
        }

        public void ApplyHit(float damage, bool isHeavyOrUltimate, FighterController attacker)
        {
            if (State == FighterState.Down)
                return;

            if (IsDashInvulnerable)
                return; // 대시 무적 프레임 - 판정 통과

            Hp = Mathf.Max(0f, Hp - damage);
            UltGauge = Mathf.Min(MaxUltGauge, UltGauge + damage * Config.Stats.UltGaugePerDamageTaken);

            float attackerX = attacker != null ? attacker.transform.position.x : transform.position.x;
            float awaySign = transform.position.x >= attackerX ? 1f : -1f;
            var pos = transform.position;
            pos.x = Mathf.Clamp(pos.x + awaySign * Config.HitReaction.Knockback, -arenaHalfWidth, arenaHalfWidth);
            transform.position = pos;

            OnHitTaken?.Invoke(this, damage, isHeavyOrUltimate);

            if (Hp <= 0f)
            {
                State = FighterState.Down;
                OnDown?.Invoke(this);
                return;
            }

            State = FighterState.HitStun;
            stateTimer = isHeavyOrUltimate ? Config.HitReaction.HeavyHitStunDuration : Config.HitReaction.HitStunDuration;
        }

        private SkillConfig GetSkillConfig(ActionType action)
        {
            switch (action)
            {
                case ActionType.LightAttack: return Config.Skills.LightAttack;
                case ActionType.HeavyAttack: return Config.Skills.HeavyAttack;
                case ActionType.Ultimate: return Config.Skills.Ultimate;
                default: return Config.Skills.LightAttack;
            }
        }

        private void UpdateMoveDirection()
        {
            if (Opponent == null)
            {
                moveDir = 0f;
                return;
            }

            float diff = Opponent.transform.position.x - transform.position.x;

            switch (activeMoveAction)
            {
                case ActionType.Approach:
                    moveDir = Mathf.Approximately(diff, 0f) ? 0f : Mathf.Sign(diff);
                    break;

                case ActionType.Retreat:
                    moveDir = Mathf.Approximately(diff, 0f) ? 0f : -Mathf.Sign(diff);
                    break;

                case ActionType.KeepDistance:
                {
                    float dist = Mathf.Abs(diff);
                    const float tolerance = 0.05f;
                    if (dist < keepDistanceRange - tolerance)
                        moveDir = Mathf.Approximately(diff, 0f) ? 0f : -Mathf.Sign(diff);
                    else if (dist > keepDistanceRange + tolerance)
                        moveDir = Mathf.Approximately(diff, 0f) ? 0f : Mathf.Sign(diff);
                    else
                        moveDir = 0f;
                    break;
                }

                default:
                    moveDir = 0f;
                    break;
            }
        }

        private void MoveByDir(float dir, float dt, float speed)
        {
            if (Mathf.Approximately(dir, 0f))
                return;

            var pos = transform.position;
            pos.x = Mathf.Clamp(pos.x + dir * speed * dt, -arenaHalfWidth, arenaHalfWidth);
            transform.position = pos;
            FacingRight = Opponent != null && Opponent.transform.position.x > pos.x;
        }

        private void RegenStamina(float dt)
        {
            float rate = State == FighterState.Idle
                ? Config.Stats.StaminaRegenIdlePerSec
                : Config.Stats.StaminaRegenPerSec;
            Stamina = Mathf.Min(MaxStamina, Stamina + rate * dt);
        }
    }
}
