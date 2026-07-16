using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TeachAndFight.Core.Rules
{
    public class RuleApplyResult
    {
        public bool Success;
        public RuleSet UpdatedRuleSet; // 실패 시 null (원본 불변)
        public List<string> Errors = new List<string>();
        public string DiscipleReply; // 실패 시 거절 대사

        public static RuleApplyResult Ok(RuleSet updated) =>
            new RuleApplyResult { Success = true, UpdatedRuleSet = updated };

        public static RuleApplyResult Fail(List<string> errors) =>
            new RuleApplyResult
            {
                Success = false,
                Errors = errors,
                DiscipleReply = "그건 제가 할 수 있는 게 아닌데요?",
            };
    }

    // 01장 "C# 검증 규칙". LLM 컴파일 결과(ops)를 실제 RuleSet에 적용하기 전 마지막 저지선.
    public static class RuleValidator
    {
        // 규칙셋 전체(로드된 JSON 등) 검증. 하드코딩/콘텐츠 규칙셋, 라운드트립 검증에 사용.
        public static List<string> ValidateRuleSet(RuleSet ruleSet)
        {
            var errors = new List<string>();
            var seenIds = new HashSet<string>();

            foreach (var rule in ruleSet.Rules)
            {
                if (!seenIds.Add(rule.Id))
                    errors.Add($"[{rule.Id}] id 중복");

                ValidateRule(rule, errors);
            }

            if (ruleSet.Rules.Count > ruleSet.MaxSlots)
                errors.Add($"슬롯 초과: max_slots={ruleSet.MaxSlots}, rules={ruleSet.Rules.Count}");

            return errors;
        }

        // ops(add/update/delete) 적용. 하나라도 실패하면 전체 롤백(원본 RuleSet 불변).
        public static RuleApplyResult ApplyOps(RuleSet current, List<RuleOp> ops)
        {
            var errors = new List<string>();
            var scratch = JsonConvert.DeserializeObject<RuleSet>(JsonConvert.SerializeObject(current));

            foreach (var op in ops)
                ApplyOp(scratch, op, errors);

            if (scratch.Rules.Count > scratch.MaxSlots)
                errors.Add($"슬롯 초과: max_slots={scratch.MaxSlots}, rules={scratch.Rules.Count}");

            return errors.Count == 0 ? RuleApplyResult.Ok(scratch) : RuleApplyResult.Fail(errors);
        }

        private static void ApplyOp(RuleSet scratch, RuleOp op, List<string> errors)
        {
            switch (op.Op)
            {
                case "add":
                    ValidateRule(op.Rule, errors);
                    if (op.Rule != null && scratch.Rules.Any(r => r.Id == op.Rule.Id))
                        errors.Add($"[{op.Rule.Id}] id 중복(add)");
                    // 실패해도 scratch에 반영 - 오류 있으면 ApplyOps가 scratch 전체를 버림(롤백)
                    if (op.Rule != null)
                        scratch.Rules.Add(op.Rule);
                    break;

                case "update":
                {
                    var idx = scratch.Rules.FindIndex(r => r.Id == op.Id);
                    if (idx < 0)
                    {
                        errors.Add($"[{op.Id}] update 대상 없음");
                        break;
                    }

                    ValidateRule(op.Rule, errors);
                    scratch.Rules[idx] = op.Rule;
                    break;
                }

                case "delete":
                {
                    var removed = scratch.Rules.RemoveAll(r => r.Id == op.Id);
                    if (removed == 0)
                        errors.Add($"[{op.Id}] delete 대상 없음");
                    break;
                }

                default:
                    errors.Add($"알 수 없는 op: {op.Op}");
                    break;
            }
        }

        private static void ValidateRule(Rule rule, List<string> errors)
        {
            if (rule == null)
            {
                errors.Add("rule 누락");
                return;
            }

            if (rule.Priority < 1 || rule.Priority > 10)
                errors.Add($"[{rule.Id}] priority는 1~10 정수여야 함: {rule.Priority}");

            foreach (var cond in rule.When)
                ValidateCondition(rule.Id, cond, errors);

            ValidateAction(rule.Id, rule.Do, errors);
        }

        private static void ValidateCondition(string ruleId, Condition cond, List<string> errors)
        {
            if (cond == null || !Vocabulary.Facts.Contains(cond.Fact))
            {
                errors.Add($"[{ruleId}] 어휘 사전에 없는 fact: {cond?.Fact}");
                return;
            }

            if (!Vocabulary.Ops.Contains(cond.Op))
            {
                errors.Add($"[{ruleId}] 어휘 사전에 없는 op: {cond.Op}");
                return;
            }

            if (Vocabulary.StringFacts.Contains(cond.Fact))
            {
                if (!Vocabulary.StringOnlyOps.Contains(cond.Op))
                    errors.Add($"[{ruleId}] 문자열 fact({cond.Fact})에 숫자 비교 op 사용: {cond.Op}");

                var strValue = cond.Value?.ToString();
                if (!Vocabulary.ActionStateValues.Contains(strValue))
                    errors.Add($"[{ruleId}] {cond.Fact}에 허용되지 않는 값: {strValue}");
            }
            else
            {
                if (!TryToDouble(cond.Value, out var numValue))
                {
                    errors.Add($"[{ruleId}] number fact({cond.Fact})에 숫자가 아닌 값: {cond.Value}");
                    return;
                }

                if (Vocabulary.FactRanges.TryGetValue(cond.Fact, out var range) &&
                    (numValue < range.Min || numValue > range.Max))
                {
                    errors.Add($"[{ruleId}] {cond.Fact} 범위 초과({range.Min}~{range.Max}): {numValue}");
                }
            }
        }

        private static void ValidateAction(string ruleId, RuleAction action, List<string> errors)
        {
            if (action == null || !Vocabulary.Actions.Contains(action.Action))
                errors.Add($"[{ruleId}] 어휘 사전에 없는 action: {action?.Action}");
        }

        private static bool TryToDouble(object value, out double result)
        {
            switch (value)
            {
                case null:
                    result = 0;
                    return false;
                case double d:
                    result = d;
                    return true;
                case float f:
                    result = f;
                    return true;
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = l;
                    return true;
                default:
                    return double.TryParse(value.ToString(), out result);
            }
        }
    }
}
