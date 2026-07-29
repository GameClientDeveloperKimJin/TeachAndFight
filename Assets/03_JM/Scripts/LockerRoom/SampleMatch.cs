using System.Collections.Generic;
using TeachAndFight.Combat;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;

namespace TeachAndFight.LockerRoom
{
    // #13 Match가 아직 없어서, 락커룸을 단독으로 돌려볼 때 쓰는 데모용 경기 결과.
    // 실제 경기(#13)가 GameSession.LastMatch를 채우면 이 주입은 건너뛴다.
    public static class SampleMatch
    {
        // 세션에 경기 기록이 없을 때만 샘플을 주입(실제 경기 결과를 덮어쓰지 않음).
        public static void EnsureForStandalone(GameSession session)
        {
            if (session == null || session.LastMatch != null)
                return;

            // Training을 거치지 않고 락커룸만 열면 규칙셋이 비어 있어 label이 안 나오므로 샘플 규칙셋도 채운다.
            if (session.DiscipleRuleSet == null || session.DiscipleRuleSet.Rules == null || session.DiscipleRuleSet.Rules.Count == 0)
                session.DiscipleRuleSet = SampleRuleSet();

            session.LastMatch = SampleResult();
        }

        private static RuleSet SampleRuleSet() => new RuleSet
        {
            Version = 1,
            FighterName = "제자",
            MaxSlots = 8,
            Rules = new List<Rule>
            {
                new Rule { Id = "rule_dodge", Label = "상대 궁극기 피하기", Priority = 8, When = new List<Condition>(), Do = new RuleAction { Action = "dash" } },
                new Rule { Id = "rule_close", Label = "멀면 접근", Priority = 5, When = new List<Condition>(), Do = new RuleAction { Action = "approach" } },
            },
        };

        // 데모: 아깝게 패배(내 HP 0, 상대 45%). 회고가 "패배 원인 규칙 1개 + 개선 제안"을 낼 소재를 담는다.
        private static MatchResult SampleResult() => new MatchResult
        {
            Won = false,
            SelfHpPct = 0f,
            EnemyHpPct = 45f,
            EventLog = new List<MatchEvent>
            {
                new MatchEvent { Time = 2.1f,  Actor = "self", Type = "rule_fired", RuleId = "rule_close" },
                new MatchEvent { Time = 5.4f,  Actor = "self", Type = "rule_fired", RuleId = "rule_dodge" },
                new MatchEvent { Time = 9.8f,  Actor = "self", Type = "rule_fired", RuleId = "rule_dodge" },
                new MatchEvent { Time = 12.0f, Actor = "self", Type = "hit", Detail = new Dictionary<string, object> { { "by", "heavy_attack" }, { "dmg", 20 } } },
                new MatchEvent { Time = 14.3f, Actor = "self", Type = "rule_fired", RuleId = "rule_close" },
                new MatchEvent { Time = 18.6f, Actor = "self", Type = "hit", Detail = new Dictionary<string, object> { { "by", "ultimate" }, { "dmg", 35 } } },
                new MatchEvent { Time = 18.9f, Actor = "self", Type = "match_end" },
            },
        };
    }
}
