using System.Collections.Generic;
using System.Linq;
using TeachAndFight.Combat;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;

namespace TeachAndFight.LockerRoom
{
    // 규칙 발동 통계 한 항목: label(×count). LockerRoom UI + 회고 프롬프트가 공유.
    public readonly struct FiredRule
    {
        public readonly string RuleId;
        public readonly string Label;
        public readonly int Count;

        public FiredRule(string ruleId, string label, int count)
        {
            RuleId = ruleId;
            Label = label;
            Count = count;
        }
    }

    // EventLog(#8) → 락커룸 표시/회고용 요약. 순수 함수 — 테스트 가능.
    public static class MatchStats
    {
        // rule_fired 빈도 상위 N개(내림차순). label은 규칙셋 id→label에서, 없으면 id 그대로.
        public static List<FiredRule> TopFiredRules(MatchResult result, RuleSet ruleSet, int top = 3)
        {
            var labels = LabelMap(ruleSet);
            return Events(result)
                .Where(e => e != null && e.Type == "rule_fired" && !string.IsNullOrEmpty(e.RuleId))
                .GroupBy(e => e.RuleId)
                .Select(g => new FiredRule(g.Key, labels.TryGetValue(g.Key, out var l) ? l : g.Key, g.Count()))
                .OrderByDescending(r => r.Count)
                .Take(top)
                .ToList();
        }

        // 최근 피격 이벤트 N건(시간 내림차순). "피격 직전 상태" 요약에 사용.
        public static List<MatchEvent> RecentHits(MatchResult result, int count = 2)
        {
            return Events(result)
                .Where(e => e != null && e.Type == "hit")
                .OrderByDescending(e => e.Time)
                .Take(count)
                .ToList();
        }

        public static Dictionary<string, string> LabelMap(RuleSet ruleSet)
        {
            var map = new Dictionary<string, string>();
            if (ruleSet?.Rules == null)
                return map;
            foreach (var r in ruleSet.Rules)
                if (r != null && !string.IsNullOrEmpty(r.Id))
                    map[r.Id] = r.Label;
            return map;
        }

        private static IEnumerable<MatchEvent> Events(MatchResult result)
            => result?.EventLog ?? Enumerable.Empty<MatchEvent>();
    }
}
