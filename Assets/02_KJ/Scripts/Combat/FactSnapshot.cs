using System;

namespace TeachAndFight.Combat
{
    // 02장 RuleEvaluator 입력 - 01장 Vocabulary.Facts 9종을 매 틱 스냅샷으로 캡처
    public readonly struct FactSnapshot
    {
        public readonly float SelfHpPct;
        public readonly float SelfStaminaPct;
        public readonly float SelfUltGauge;
        public readonly float EnemyHpPct;
        public readonly float EnemyStaminaPct;
        public readonly float Distance;
        public readonly string EnemyAction;
        public readonly float TimeLeft;
        public readonly string SelfAction;

        // #26 Tier1
        public readonly float SelfWallDist;
        public readonly float SelfActionDuration;
        public readonly float EnemyActionDuration;
        public readonly float EnemyWhiffCount;

        public FactSnapshot(float selfHpPct, float selfStaminaPct, float selfUltGauge,
            float enemyHpPct, float enemyStaminaPct, float distance,
            string enemyAction, float timeLeft, string selfAction,
            float selfWallDist, float selfActionDuration, float enemyActionDuration, float enemyWhiffCount)
        {
            SelfHpPct = selfHpPct;
            SelfStaminaPct = selfStaminaPct;
            SelfUltGauge = selfUltGauge;
            EnemyHpPct = enemyHpPct;
            EnemyStaminaPct = enemyStaminaPct;
            Distance = distance;
            EnemyAction = enemyAction;
            TimeLeft = timeLeft;
            SelfAction = selfAction;
            SelfWallDist = selfWallDist;
            SelfActionDuration = selfActionDuration;
            EnemyActionDuration = enemyActionDuration;
            EnemyWhiffCount = enemyWhiffCount;
        }

        public static FactSnapshot Capture(FighterController self, FighterController enemy, float timeLeft)
        {
            return new FactSnapshot(
                self.HpPct, self.StaminaPct, self.UltGaugePct,
                enemy.HpPct, enemy.StaminaPct, self.Distance,
                enemy.ActionStateLabel, timeLeft, self.ActionStateLabel,
                self.WallDistance, self.StateElapsed, enemy.StateElapsed, enemy.RecentWhiffCount);
        }

        public double GetNumberFact(string fact)
        {
            switch (fact)
            {
                case "self_hp_pct": return SelfHpPct;
                case "self_stamina_pct": return SelfStaminaPct;
                case "self_ult_gauge": return SelfUltGauge;
                case "enemy_hp_pct": return EnemyHpPct;
                case "enemy_stamina_pct": return EnemyStaminaPct;
                case "distance": return Distance;
                case "time_left": return TimeLeft;
                case "self_wall_dist": return SelfWallDist;
                case "self_action_duration": return SelfActionDuration;
                case "enemy_action_duration": return EnemyActionDuration;
                case "enemy_whiff_count": return EnemyWhiffCount;
                default:
                    throw new ArgumentException($"숫자 fact 아님: {fact}");
            }
        }

        public string GetStringFact(string fact)
        {
            switch (fact)
            {
                case "enemy_action": return EnemyAction;
                case "self_action": return SelfAction;
                default:
                    throw new ArgumentException($"문자열 fact 아님: {fact}");
            }
        }
    }
}
