using System;
using System.Collections.Generic;
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

        // #22 애니메이션 브릿지용: 현재 커밋된 행동(공격 종류 구분). 시각 전용 읽기 노출(최소 변경).
        public ActionType CommittedAction => committedAction;

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

        // #26 Tier1: 등 뒤 벽까지 거리(코너 압박 판정용)
        public float WallDistance => arenaHalfWidth - Mathf.Abs(transform.position.x);

        // #26 Tier1: 현재 State 진입 후 경과 시간(초) - EnterState()에서 리셋
        public float StateElapsed => stateElapsed;

        // #26 Tier1: 최근 WhiffMemorySec초 내 헛스윙 횟수(롤링 윈도우)
        public int RecentWhiffCount => recentWhiffTimers.Count;

        public bool IsDashInvulnerable =>
            State == FighterState.Dash &&
            (Config.Dash.Duration - stateTimer) < Config.Dash.InvulnerableDuration;

        // 01장 어휘 사전 ActionStateValues 중 하나로 매핑 - RuleEvaluator의 self_action/enemy_action fact 소스
        public string ActionStateLabel
        {
            get
            {
                switch (State)
                {
                    case FighterState.Idle:
                        return "idle";
                    case FighterState.Move:
                        return MoveActionLabel();
                    case FighterState.Dash:
                        return "dash";
                    case FighterState.AttackStartup:
                    case FighterState.AttackActive:
                        return AttackActionLabel();
                    case FighterState.Recovery:
                        return "whiff_recovery";
                    case FighterState.HitStun:
                    case FighterState.Down:
                        return "hit_stun";
                    default:
                        return "idle";
                }
            }
        }

        private string MoveActionLabel()
        {
            if (activeMoveAction == ActionType.Approach)
                return "approach";
            if (activeMoveAction == ActionType.Retreat)
                return "retreat";

            if (activeMoveAction == ActionType.KeepDistance)
            {
                if (Mathf.Approximately(moveDir, 0f))
                    return "idle";

                float diff = Opponent.transform.position.x - transform.position.x;
                bool approaching = !Mathf.Approximately(diff, 0f) && Mathf.Sign(moveDir) == Mathf.Sign(diff);
                return approaching ? "approach" : "retreat";
            }

            return "idle";
        }

        private string AttackActionLabel()
        {
            switch (committedAction)
            {
                case ActionType.LightAttack: return "light_startup";
                case ActionType.HeavyAttack: return "heavy_startup";
                case ActionType.Ultimate: return "ultimate_startup";
                // #26 Tier2: counter_attack/feint는 light_attack과 구분 안 되게 위장(카운터/페인트 판별 불가가 의도)
                case ActionType.CounterAttack: return "light_startup";
                case ActionType.Feint: return "light_startup";
                default: return "idle";
            }
        }

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

        // #26 Tier1
        private float stateElapsed;
        private readonly List<float> recentWhiffTimers = new List<float>();
        private const float WhiffMemorySec = 5f;

        // #26 Tier2: counter_attack 커밋 시점에 상대가 startup 중이었는지(데미지 보너스 적용 여부)
        private bool counterBonusActive;

        public void Init(CombatConfig config, FighterController opponent, float startX)
        {
            Config = config;
            Opponent = opponent;
            Hp = config.Stats.MaxHp;
            Stamina = config.Stats.MaxStamina;
            UltGauge = 0f;
            stateTimer = 0f;
            dashCooldownTimer = 0f;
            moveDir = 0f;
            activeMoveAction = ActionType.Idle;
            arenaHalfWidth = config.Arena.Width * 0.5f;
            recentWhiffTimers.Clear();

            var pos = transform.position;
            pos.x = startX;
            transform.position = pos;

            EnterState(FighterState.Idle);
        }

        // #26 Tier1: State 전이는 항상 이 메서드로 - stateElapsed 리셋을 한 곳에서 보장
        private void EnterState(FighterState next)
        {
            State = next;
            stateElapsed = 0f;
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
                case ActionType.CounterAttack:
                    return Stamina >= Config.Skills.CounterAttack.Stamina;
                case ActionType.Feint:
                    return Stamina >= Config.Skills.Feint.Stamina;
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
                    EnterState(FighterState.Idle);
                    activeMoveAction = ActionType.Idle;
                    moveDir = 0f;
                    break;

                case ActionType.Approach:
                case ActionType.Retreat:
                    EnterState(FighterState.Move);
                    activeMoveAction = cmd.Action;
                    break;

                case ActionType.KeepDistance:
                    EnterState(FighterState.Move);
                    activeMoveAction = ActionType.KeepDistance;
                    keepDistanceRange = cmd.KeepDistanceRange;
                    break;

                case ActionType.Dash:
                    Stamina -= Config.Skills.Dash.Stamina;
                    dashDirection = cmd.DashDirection;
                    dashCooldownTimer = Config.Skills.Dash.Cooldown;
                    EnterState(FighterState.Dash);
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

                case ActionType.CounterAttack:
                    // 커밋 시점에 상대가 startup 중이었으면 보너스 데미지(#26 Tier2). 애니는 light 재사용하되
                    // 판정(발동속도/사거리)은 전용 설정 - light_attack 그대로 쓰면 반격 타이밍/사거리가 안 나옴(밸런스 패치).
                    counterBonusActive = Opponent != null && Opponent.State == FighterState.AttackStartup;
                    BeginAttackStartup(ActionType.CounterAttack, Config.Skills.CounterAttack);
                    break;

                case ActionType.Feint:
                    Stamina -= Config.Skills.Feint.Stamina;
                    committedAction = ActionType.Feint;
                    EnterState(FighterState.AttackStartup);
                    stateTimer = Config.Skills.Feint.Startup;
                    break;
            }

            return true;
        }

        private void BeginAttackStartup(ActionType action, SkillConfig skill)
        {
            if (action != ActionType.Ultimate)
                Stamina -= skill.Stamina;

            committedAction = action;
            EnterState(FighterState.AttackStartup);
            stateTimer = skill.Startup;
        }

        public void Tick(float dt)
        {
            if (State == FighterState.Down)
                return;

            stateElapsed += dt;
            TickRecentWhiffs(dt);
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
                    {
                        if (committedAction == ActionType.Feint)
                            ResolveFeint();
                        else
                            ResolveAttackActive();
                    }
                    break;

                case FighterState.Recovery:
                case FighterState.HitStun:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                        EnterState(FighterState.Idle);
                    break;
            }
        }

        // #26 Tier1: 롤링 윈도우 - WhiffMemorySec 지난 항목 제거
        private void TickRecentWhiffs(float dt)
        {
            for (int i = recentWhiffTimers.Count - 1; i >= 0; i--)
            {
                recentWhiffTimers[i] -= dt;
                if (recentWhiffTimers[i] <= 0f)
                    recentWhiffTimers.RemoveAt(i);
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
                EnterState(FighterState.Idle);
        }

        // #26 Tier2: feint - 데미지 판정 없이 짧은 recovery로 빠져나감(상대 판단 유도용 페이크)
        private void ResolveFeint()
        {
            EnterState(FighterState.Recovery);
            stateTimer = Config.Skills.Feint.Recovery;
        }

        private void ResolveAttackActive()
        {
            EnterState(FighterState.AttackActive);
            var skill = GetSkillConfig(committedAction);
            bool isHeavyOrUlt = committedAction == ActionType.HeavyAttack || committedAction == ActionType.Ultimate;

            float damage = skill.Damage;
            if (committedAction == ActionType.CounterAttack && counterBonusActive)
                damage *= Config.Counter.DamageMultiplier;
            counterBonusActive = false;

            bool hit = Opponent.State != FighterState.Down && Distance <= skill.Range;

            if (hit)
            {
                UltGauge = Mathf.Min(MaxUltGauge, UltGauge + damage * Config.Stats.UltGaugePerDamageDealt);
                OnHitLanded?.Invoke(this, damage, isHeavyOrUlt);
                Opponent.ApplyHit(damage, isHeavyOrUlt, this);
                EnterState(FighterState.Recovery);
                stateTimer = skill.Recovery;
            }
            else
            {
                recentWhiffTimers.Add(WhiffMemorySec);
                OnWhiff?.Invoke(this);
                EnterState(FighterState.Recovery);
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
                EnterState(FighterState.Down);
                OnDown?.Invoke(this);
                return;
            }

            EnterState(FighterState.HitStun);
            stateTimer = isHeavyOrUltimate ? Config.HitReaction.HeavyHitStunDuration : Config.HitReaction.HitStunDuration;
        }

        private SkillConfig GetSkillConfig(ActionType action)
        {
            switch (action)
            {
                case ActionType.LightAttack: return Config.Skills.LightAttack;
                case ActionType.HeavyAttack: return Config.Skills.HeavyAttack;
                case ActionType.Ultimate: return Config.Skills.Ultimate;
                case ActionType.CounterAttack: return Config.Skills.CounterAttack;
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
