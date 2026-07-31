using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TeachAndFight.Core.Rules;

namespace TeachAndFight.Combat.EditorTools
{
    // #12 완료기준 수동 검증: opponent_01~05.json 5종 전부 RuleValidator 통과 + 백지 규칙셋(제자)이
    // 1차전 상대(러쉬, opponent_01)에게 반드시 패배(KO)하는지 눈으로 확인.
    public static class OpponentRuleSetVerificationMenu
    {
        [MenuItem("TeachAndFight/Combat/상대 5종 검증 - JSON 로드 + 백지 1차전")]
        public static void RunVerification()
        {
            bool allValid = true;
            for (int i = OpponentRuleSetLoader.MinIndex; i <= OpponentRuleSetLoader.MaxIndex; i++)
            {
                RuleSet ruleSet;
                try
                {
                    ruleSet = OpponentRuleSetLoader.Load(i);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[상대5종 검증] opponent_{i:00} 로드 실패: {e.Message}");
                    allValid = false;
                    continue;
                }

                var errors = RuleValidator.ValidateRuleSet(ruleSet);
                if (errors.Count > 0)
                {
                    Debug.LogError($"[상대5종 검증] opponent_{i:00}({ruleSet.FighterName}) 검증 실패: {string.Join(" / ", errors)}");
                    allValid = false;
                }
                else
                {
                    Debug.Log($"[상대5종 검증] opponent_{i:00}({ruleSet.FighterName}) 통과 - 규칙 {ruleSet.Rules.Count}개");
                }
            }

            if (!allValid)
            {
                Debug.LogError("[상대5종 검증] 일부 JSON이 검증 실패 - 1차전 시뮬레이션 생략");
                return;
            }

            RunBlankVsRush();
        }

        private static void RunBlankVsRush()
        {
            var blankDisciple = new RuleSet
            {
                Version = 1,
                FighterName = "제자",
                MaxSlots = 8,
                Rules = new List<Rule>(),
            };
            var rush = OpponentRuleSetLoader.Load(1);

            var config = CombatConfigLoader.Load();
            var self = new GameObject("검증_제자_백지").AddComponent<FighterController>();
            var enemy = new GameObject("검증_러쉬").AddComponent<FighterController>();

            try
            {
                float half = config.Arena.StartDistance * 0.5f;
                self.Init(config, enemy, -half);
                enemy.Init(config, self, half);

                var eventLog = new EventLog();
                var selfEvaluator = new RuleEvaluator(blankDisciple, eventLog);
                var enemyEvaluator = new RuleEvaluator(rush, eventLog);

                const float dt = 0.1f;
                const float durationSec = 60f;
                float matchTime = 0f;

                while (matchTime < durationSec && self.State != FighterState.Down && enemy.State != FighterState.Down)
                {
                    if (self.State == FighterState.Idle || self.State == FighterState.Move)
                    {
                        var cmd = selfEvaluator.Evaluate(self, enemy, timeLeft: durationSec - matchTime, matchTime: matchTime);
                        if (!self.TryPerform(cmd))
                            self.TryPerform(ActionCommand.Idle());
                    }

                    if (enemy.State == FighterState.Idle || enemy.State == FighterState.Move)
                    {
                        var cmd = enemyEvaluator.Evaluate(enemy, self, timeLeft: durationSec - matchTime, matchTime: matchTime);
                        if (!enemy.TryPerform(cmd))
                            enemy.TryPerform(ActionCommand.Idle());
                    }

                    self.Tick(dt);
                    enemy.Tick(dt);
                    matchTime += dt;
                }

                Debug.Log($"[상대5종 검증] 백지 1차전 종료(경과 {matchTime:0.0}s) - 제자 HP {self.HpPct:0}% / 러쉬 HP {enemy.HpPct:0}%");

                if (self.State == FighterState.Down)
                    Debug.Log("[상대5종 검증] ✅ 백지 규칙셋 제자가 러쉬에게 KO로 패배 - #12 완료기준 충족");
                else
                    Debug.LogError("[상대5종 검증] ⚠ 백지 규칙셋 제자가 60초 안에 KO당하지 않음 - 러쉬 규칙셋 보강 필요");
            }
            finally
            {
                Object.DestroyImmediate(self.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }
    }
}
