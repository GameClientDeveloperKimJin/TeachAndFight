using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using TeachAndFight.Core.Rules;

namespace TeachAndFight.Combat.EditorTools
{
    // #7/#8 수동 검증 하네스. 실제 Resources/combat_config.json + 샘플 JSON 규칙셋으로 매치를 시뮬레이션해
    // 콘솔에서 눈으로 확인한다: JSON 규칙셋이 실전투에 반영되는지, 스태미나 부족 시 폴백되는지,
    // EventLog에 rule_fired(rule_id 포함)가 정상 기록되는지.
    public static class RuleEvaluatorVerificationMenu
    {
        // 궁 우선(10) > 히비 견제(8, 스태미나 25 이상일 때만) > 라이트 견제(6, 히비 못 쓸 때 폴백) > 접근(5)
        private const string SampleRuleSetJson = @"{
          ""version"": 1,
          ""fighter_name"": ""제자"",
          ""max_slots"": 5,
          ""rules"": [
            { ""id"": ""ult_when_ready"", ""label"": ""궁 준비되면 즉시"", ""when"": [ { ""fact"": ""self_ult_gauge"", ""op"": "">="", ""value"": 100 } ], ""do"": { ""action"": ""ultimate"" }, ""priority"": 10 },
            { ""id"": ""heavy_poke"", ""label"": ""스태미나 넉넉하면 강공"", ""when"": [ { ""fact"": ""distance"", ""op"": ""<="", ""value"": 1.2 }, { ""fact"": ""self_stamina_pct"", ""op"": "">="", ""value"": 25 } ], ""do"": { ""action"": ""heavy_attack"" }, ""priority"": 8 },
            { ""id"": ""light_when_low_stamina"", ""label"": ""스태미나 부족하면 약공으로 폴백"", ""when"": [ { ""fact"": ""distance"", ""op"": ""<="", ""value"": 1.2 } ], ""do"": { ""action"": ""light_attack"" }, ""priority"": 6 },
            { ""id"": ""close_in"", ""label"": ""사거리 밖이면 접근"", ""when"": [ { ""fact"": ""distance"", ""op"": "">"", ""value"": 1.2 } ], ""do"": { ""action"": ""approach"" }, ""priority"": 5 }
          ]
        }";

        [MenuItem("TeachAndFight/Combat/RuleEvaluator 검증 - JSON 규칙셋 매치 시뮬레이션")]
        public static void RunSampleMatch()
        {
            var ruleSet = JsonConvert.DeserializeObject<RuleSet>(SampleRuleSetJson);
            var validationErrors = RuleValidator.ValidateRuleSet(ruleSet);
            if (validationErrors.Count > 0)
            {
                Debug.LogError($"[RuleEvaluator 검증] 샘플 규칙셋 검증 실패: {string.Join(" / ", validationErrors)}");
                return;
            }

            var config = CombatConfigLoader.Load(); // Resources/Data/Config/combat_config.json 실제 로드
            var self = new GameObject("검증_제자").AddComponent<FighterController>();
            var enemy = new GameObject("검증_상대").AddComponent<FighterController>();
            float half = config.Arena.StartDistance * 0.5f;
            self.Init(config, enemy, -half);
            enemy.Init(config, self, half);

            var eventLog = new EventLog();
            var evaluator = new RuleEvaluator(ruleSet, eventLog);
            evaluator.OnRuleFired += id => Debug.Log($"[RuleEvaluator 검증] 규칙 발동: {id}");

            const float dt = 0.1f;
            const float durationSec = 20f;
            float matchTime = 0f;

            while (matchTime < durationSec && self.State != FighterState.Down && enemy.State != FighterState.Down)
            {
                if (self.State == FighterState.Idle || self.State == FighterState.Move)
                {
                    var cmd = evaluator.Evaluate(self, enemy, timeLeft: durationSec - matchTime, matchTime: matchTime);
                    if (!self.TryPerform(cmd))
                        self.TryPerform(ActionCommand.Idle());
                }

                self.Tick(dt);
                enemy.Tick(dt);
                matchTime += dt;
            }

            Debug.Log($"[RuleEvaluator 검증] 종료(경과 {matchTime:0.0}s) - 제자 HP {self.HpPct:0}% 스태미나 {self.StaminaPct:0}% 궁게이지 {self.UltGaugePct:0}% / 상대 HP {enemy.HpPct:0}%");

            var fired = eventLog.Events.Where(e => e.Type == "rule_fired").ToList();
            var staminaOut = eventLog.Events.Where(e => e.Type == "stamina_out").ToList();

            Debug.Log($"[RuleEvaluator 검증] rule_fired {fired.Count}건 - {string.Join(", ", fired.GroupBy(e => e.RuleId).Select(g => $"{g.Key}x{g.Count()}"))}");

            if (staminaOut.Count > 0)
                Debug.Log($"[RuleEvaluator 검증] ✅ stamina_out(폴백) {staminaOut.Count}건 - heavy_poke가 스태미나 부족으로 스킵되고 light_when_low_stamina로 폴백된 사례 확인됨");
            else
                Debug.LogWarning("[RuleEvaluator 검증] stamina_out 0건 - 이번 실행에선 스태미나 폴백이 관측되지 않음(우연히 안 밀린 경우, 재실행해서 확인)");

            var json = eventLog.ToJson();
            Debug.Log($"[RuleEvaluator 검증] EventLog 총 {eventLog.Events.Count}건 기록됨. JSON 앞부분: {json.Substring(0, Mathf.Min(300, json.Length))}...");

            Object.DestroyImmediate(self.gameObject);
            Object.DestroyImmediate(enemy.gameObject);
        }
    }
}
