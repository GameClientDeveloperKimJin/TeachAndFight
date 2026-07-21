using System;
using System.Collections.Generic;
using System.Linq;
using TeachAndFight.Core.Rules;

namespace TeachAndFight.Combat
{
    // 02장 RuleEvaluator - 0.1s 틱마다 RuleSet 평가해서 ActionCommand 결정.
    // priority 내림차순(동률=배열순) 정렬 후 when 전부 만족 + CanPerform() true인 첫 규칙 실행.
    // 자원 부족으로 스킵된 규칙은 EventLog에 stamina_out 기록 후 다음 규칙으로 폴백.
    public class RuleEvaluator
    {
        private readonly List<Rule> sortedRules;
        private readonly EventLog eventLog;

        // 실행된 규칙 id - 머리 위 라벨 UI 갱신용 훅
        public event Action<string> OnRuleFired;

        public RuleEvaluator(RuleSet ruleSet, EventLog eventLog = null)
        {
            sortedRules = ruleSet.Rules.OrderByDescending(r => r.Priority).ToList();
            this.eventLog = eventLog;
        }

        public ActionCommand Evaluate(FighterController self, FighterController enemy, float timeLeft, float matchTime)
        {
            var facts = FactSnapshot.Capture(self, enemy, timeLeft);

            foreach (var rule in sortedRules)
            {
                if (!MatchesConditions(rule.When, facts))
                    continue;

                if (!TryBuildCommand(rule.Do, out var cmd))
                    continue;

                if (!self.CanPerform(cmd.Action))
                {
                    eventLog?.Record(matchTime, "self", "stamina_out", rule.Id);
                    continue;
                }

                eventLog?.Record(matchTime, "self", "rule_fired", rule.Id);
                OnRuleFired?.Invoke(rule.Id);
                return cmd;
            }

            return ActionCommand.Idle();
        }

        private static bool MatchesConditions(List<Condition> conditions, FactSnapshot facts)
        {
            foreach (var cond in conditions)
                if (!MatchesCondition(cond, facts))
                    return false;
            return true;
        }

        private static bool MatchesCondition(Condition cond, FactSnapshot facts)
        {
            if (Vocabulary.StringFacts.Contains(cond.Fact))
            {
                string actual = facts.GetStringFact(cond.Fact);
                string expected = cond.Value?.ToString();
                return cond.Op == "==" ? actual == expected : actual != expected;
            }

            double actual2 = facts.GetNumberFact(cond.Fact);
            double expected2 = ToDouble(cond.Value);

            switch (cond.Op)
            {
                case "==": return actual2 == expected2;
                case "!=": return actual2 != expected2;
                case ">": return actual2 > expected2;
                case "<": return actual2 < expected2;
                case ">=": return actual2 >= expected2;
                case "<=": return actual2 <= expected2;
                default: return false;
            }
        }

        private static bool TryBuildCommand(RuleAction action, out ActionCommand cmd)
        {
            switch (action?.Action)
            {
                case "idle":
                    cmd = ActionCommand.Idle();
                    return true;
                case "approach":
                    cmd = ActionCommand.Approach();
                    return true;
                case "retreat":
                    cmd = ActionCommand.Retreat();
                    return true;
                case "keep_distance":
                    cmd = ActionCommand.KeepDistanceAt((float)GetParamNumber(action.Params, "range", 2f));
                    return true;
                case "dash":
                    var dir = GetParamString(action.Params, "direction", "toward") == "away"
                        ? DashDirection.Away
                        : DashDirection.Toward;
                    cmd = ActionCommand.DashTo(dir);
                    return true;
                case "light_attack":
                    cmd = ActionCommand.LightAttack();
                    return true;
                case "heavy_attack":
                    cmd = ActionCommand.HeavyAttack();
                    return true;
                case "ultimate":
                    cmd = ActionCommand.Ultimate();
                    return true;
                default:
                    cmd = default;
                    return false;
            }
        }

        private static double GetParamNumber(Dictionary<string, object> parameters, string key, double fallback)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value))
                return fallback;
            return ToDouble(value);
        }

        private static string GetParamString(Dictionary<string, object> parameters, string key, string fallback)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
                return fallback;
            return value.ToString();
        }

        private static double ToDouble(object value)
        {
            switch (value)
            {
                case null: return 0;
                case double d: return d;
                case float f: return f;
                case int i: return i;
                case long l: return l;
                default:
                    double.TryParse(value.ToString(), out var parsed);
                    return parsed;
            }
        }
    }
}
