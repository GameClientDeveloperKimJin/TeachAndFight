using System;
using System.Collections.Generic;
using System.Linq;
using TeachAndFight.Combat;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;

namespace TeachAndFight.Match
{
    // #13 Match.unity 코어 로직(뷰 비의존, 테스트 가능).
    // 제자(self)는 session.DiscipleRuleSet, 상대(enemy)는 session.CurrentOpponent로 각각 RuleEvaluator 구동.
    // 종료 시 MatchResult를 만들어 session.LastMatch에 담는다 — LockerRoom(#15, B)이 그대로 소비.
    public sealed class MatchPresenter
    {
        private const float DecisionInterval = 0.1f; // 02장 RuleEvaluator 틱 주기
        private const float BigHitHpPctThreshold = 30f; // 04장 연출: 결정타(HP 30%↑ 또는 궁) 슬로모 기준

        private readonly GameSession session;
        private readonly CombatConfig config;
        private readonly FighterController self;
        private readonly FighterController enemy;
        private readonly EventLog selfEventLog = new EventLog();
        private readonly RuleEvaluator selfEvaluator;
        private readonly RuleEvaluator enemyEvaluator;
        private readonly Dictionary<string, string> selfLabels;
        private readonly Dictionary<string, string> enemyLabels;

        private float matchTimer;
        private float decisionTimer;

        // (발동한 파이터, ruleId, label) - 머리 위 라벨 UI 갱신용.
        public event Action<FighterController, string, string> OnRuleFired;

        // (피격자, 데미지 %) - 결정타 슬로모 연출 트리거용.
        public event Action<FighterController, float> OnBigHit;

        // (헛친 파이터) - 공격이 사거리 밖이라 안 맞았을 때 UI 피드백용("헛침!").
        public event Action<FighterController> OnAttackWhiff;

        public event Action<MatchResult> OnConcluded;

        public FighterController Self => self;
        public FighterController Enemy => enemy;
        public float TimeLeft => matchTimer < 0f ? 0f : matchTimer;
        public bool Concluded { get; private set; }
        public MatchResult Result { get; private set; }

        // 발동 라벨 이벤트(OnRuleFired)가 실제 RuleEvaluator 기록과 일치하는지 검증용으로 노출.
        public IReadOnlyList<MatchEvent> SelfEventLog => selfEventLog.Events;

        public MatchPresenter(GameSession session, CombatConfig config, FighterController self, FighterController enemy)
        {
            this.session = session;
            this.config = config;
            this.self = self;
            this.enemy = enemy;

            selfLabels = LabelMap(session.DiscipleRuleSet);
            enemyLabels = LabelMap(session.CurrentOpponent);

            selfEvaluator = new RuleEvaluator(session.DiscipleRuleSet, selfEventLog);
            enemyEvaluator = new RuleEvaluator(session.CurrentOpponent);
            selfEvaluator.OnRuleFired += id => OnRuleFired?.Invoke(self, id, LabelOf(selfLabels, id));
            enemyEvaluator.OnRuleFired += id => OnRuleFired?.Invoke(enemy, id, LabelOf(enemyLabels, id));

            matchTimer = config.Match.DurationSec;

            self.OnHitTaken += HandleSelfHitTaken;
            self.OnHitTaken += (attacker, dmg, heavy) => CheckBigHit(self, dmg);
            enemy.OnHitTaken += (attacker, dmg, heavy) => CheckBigHit(enemy, dmg);
            self.OnWhiff += who => OnAttackWhiff?.Invoke(who);
            enemy.OnWhiff += who => OnAttackWhiff?.Invoke(who);
            self.OnDown += _ => Conclude();
            enemy.OnDown += _ => Conclude();
        }

        public void Step(float dt)
        {
            if (Concluded)
                return;

            matchTimer -= dt;
            decisionTimer -= dt;

            if (decisionTimer <= 0f)
            {
                decisionTimer += DecisionInterval;
                Decide(self, enemy, selfEvaluator);
                Decide(enemy, self, enemyEvaluator);
            }

            self.Tick(dt);
            enemy.Tick(dt);

            if (!Concluded && matchTimer <= 0f)
                Conclude();
        }

        private void Decide(FighterController actor, FighterController opponent, RuleEvaluator evaluator)
        {
            if (Concluded)
                return;
            if (actor.State != FighterState.Idle && actor.State != FighterState.Move)
                return;

            var cmd = evaluator.Evaluate(actor, opponent, TimeLeft, config.Match.DurationSec - TimeLeft);
            if (!actor.TryPerform(cmd))
                actor.TryPerform(ActionCommand.Idle());
        }

        private void HandleSelfHitTaken(FighterController attacker, float damage, bool isHeavyOrUltimate)
        {
            selfEventLog.Record(config.Match.DurationSec - TimeLeft, "self", "hit",
                detail: new Dictionary<string, object> { { "by", attacker.ActionStateLabel }, { "dmg", damage } });
        }

        private void CheckBigHit(FighterController victim, float damage)
        {
            float pct = damage / victim.MaxHp * 100f;
            if (pct >= BigHitHpPctThreshold)
                OnBigHit?.Invoke(victim, pct);
        }

        private void Conclude()
        {
            if (Concluded)
                return;
            Concluded = true;

            selfEventLog.Record(config.Match.DurationSec - TimeLeft, "self", "match_end");

            bool selfDown = self.State == FighterState.Down;
            bool enemyDown = enemy.State == FighterState.Down;
            bool won = !selfDown && (enemyDown || (!enemyDown && self.HpPct > enemy.HpPct));

            Result = new MatchResult
            {
                Won = won,
                SelfHpPct = self.HpPct,
                EnemyHpPct = enemy.HpPct,
                EventLog = selfEventLog.Events.ToList(),
            };
            session.LastMatch = Result;

            OnConcluded?.Invoke(Result);
        }

        private static Dictionary<string, string> LabelMap(RuleSet ruleSet)
        {
            var map = new Dictionary<string, string>();
            if (ruleSet?.Rules == null)
                return map;
            foreach (var r in ruleSet.Rules)
                if (r != null && !string.IsNullOrEmpty(r.Id))
                    map[r.Id] = r.Label;
            return map;
        }

        private static string LabelOf(Dictionary<string, string> labels, string ruleId)
            => labels.TryGetValue(ruleId, out var label) ? label : ruleId;
    }
}
