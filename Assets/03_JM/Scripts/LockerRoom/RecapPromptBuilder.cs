using System.Text;
using TeachAndFight.Combat;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;

namespace TeachAndFight.LockerRoom
{
    // 03장 "호출 2: 경기 회고" 프롬프트 구성. 순수 함수 — 테스트 가능.
    // ⚠ 시스템 프롬프트 원문은 docs/DEV_SPEC.md 03장이 원본. 여기서 임의 수정 금지(문서 먼저 고치고 반영).
    public static class RecapPromptBuilder
    {
        // 03장 원문 그대로.
        public const string SystemPrompt =
@"너는 방금 경기를 마친 격투 게임 캐릭터 '제자'다. 경기 로그를 보고 코치에게 소감을 말한다.
[규칙] 1. 3문장 이내, 존댓말. 2. 로그에 실제로 있었던 사건만 언급. 3. 패배 시 원인이 된 규칙(rule_id의 label)을 1개 짚고 개선 방향을 조심스럽게 제안. 승리 시 잘 작동한 가르침 1개를 기뻐하며 언급. 4. JSON: {""recap"":""...""}";

        // 유저 메시지: 결과(승/패, 잔여 HP) + EventLog 요약(rule_fired 상위3, 피격 직전 2건) + 규칙셋 id→label.
        public static string BuildUserMessage(MatchResult result, RuleSet ruleSet)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"[결과] {(result.Won ? "승리" : "패배")} · 내 HP {result.SelfHpPct:0}% · 상대 HP {result.EnemyHpPct:0}%");

            sb.AppendLine("[발동 규칙 상위]");
            var top = MatchStats.TopFiredRules(result, ruleSet, 3);
            if (top.Count == 0)
                sb.AppendLine("- (없음)");
            else
                foreach (var r in top)
                    sb.AppendLine($"- {r.Label} ({r.RuleId}) ×{r.Count}");

            sb.AppendLine("[피격 직전]");
            var hits = MatchStats.RecentHits(result, 2);
            if (hits.Count == 0)
                sb.AppendLine("- (없음)");
            else
                foreach (var h in hits)
                    sb.AppendLine($"- t={h.Time:0.0}s {DetailBrief(h)}".TrimEnd());

            sb.AppendLine("[규칙 id→label]");
            if (ruleSet?.Rules == null || ruleSet.Rules.Count == 0)
                sb.AppendLine("- (백지 규칙셋)");
            else
                foreach (var rule in ruleSet.Rules)
                    sb.AppendLine($"- {rule.Id}: {rule.Label}");

            return sb.ToString().TrimEnd();
        }

        private static string DetailBrief(MatchEvent e)
        {
            if (e.Detail == null || e.Detail.Count == 0)
                return string.Empty;
            var parts = new System.Collections.Generic.List<string>();
            foreach (var kv in e.Detail)
                parts.Add($"{kv.Key}={kv.Value}");
            return string.Join(" ", parts);
        }
    }
}
