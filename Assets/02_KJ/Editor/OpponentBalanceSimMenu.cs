using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TeachAndFight.Core.Rules;

namespace TeachAndFight.Combat.EditorTools
{
    // #18 완료기준 검증: 각 상대의 "의도된 공략 가르침" 룰셋을 제자에게 태워 실제 승리 가능한지 시뮬레이션.
    // OpponentRuleSetVerificationMenu(#12)의 백지 패배 검증과 짝을 이루는 반대쪽 확인(가르치면 이긴다).
    public static class OpponentBalanceSimMenu
    {
        [MenuItem("TeachAndFight/Combat/밸런싱 - 공략 가르침으로 상대 5종 승리 시뮬레이션")]
        public static void RunAll()
        {
            for (int i = 1; i <= 5; i++)
                RunOne(i, TaughtRuleSet(i));
        }

        [MenuItem("TeachAndFight/Combat/밸런싱 - 상세 로그(opponent_01)")]
        public static void RunVerboseOpponent1()
        {
            RunOneVerbose(1, TaughtRuleSet(1));
        }

        [MenuItem("TeachAndFight/Combat/밸런싱 - 상세 로그(opponent_05)")]
        public static void RunVerboseOpponent5()
        {
            RunOneVerbose(5, TaughtRuleSet(5));
        }

        private static void RunOneVerbose(int opponentIndex, RuleSet taught)
        {
            var opponent = OpponentRuleSetLoader.Load(opponentIndex);
            var config = CombatConfigLoader.Load();
            var self = new GameObject($"검증_상세_{opponentIndex}").AddComponent<FighterController>();
            var enemy = new GameObject($"검증_상세상대_{opponentIndex}").AddComponent<FighterController>();

            try
            {
                float half = config.Arena.StartDistance * 0.5f;
                self.Init(config, enemy, -half);
                enemy.Init(config, self, half);

                self.OnHitLanded += (attacker, dmg, heavy) => Debug.Log($"[상세] t=? 제자→상대 hit dmg={dmg} heavy={heavy}");
                enemy.OnHitLanded += (attacker, dmg, heavy) => Debug.Log($"[상세] t=? 상대→제자 hit dmg={dmg} heavy={heavy}");
                self.OnWhiff += _ => Debug.Log("[상세] 제자 헛침");
                enemy.OnWhiff += _ => Debug.Log("[상세] 상대 헛침");

                var selfEvaluator = new RuleEvaluator(taught);
                var enemyEvaluator = new RuleEvaluator(opponent);

                const float dt = 0.1f;
                const float durationSec = 60f;
                float matchTime = 0f;
                int step = 0;

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
                    step++;

                    if (step % 5 == 0 || step < 20)
                        Debug.Log($"[상세] t={matchTime:0.0} dist={self.Distance:0.00} 제자[{self.State}/{self.HpPct:0}%hp/{self.StaminaPct:0}%st] 상대[{enemy.State}/{enemy.HpPct:0}%hp/{enemy.StaminaPct:0}%st]");
                }

                Debug.Log($"[상세] 종료 t={matchTime:0.0} 제자HP={self.HpPct:0}% 상대HP={enemy.HpPct:0}%");
            }
            finally
            {
                Object.DestroyImmediate(self.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        private static void RunOne(int opponentIndex, RuleSet taught)
        {
            var opponent = OpponentRuleSetLoader.Load(opponentIndex);
            var config = CombatConfigLoader.Load();
            var self = new GameObject($"검증_공략_{opponentIndex}").AddComponent<FighterController>();
            var enemy = new GameObject($"검증_상대_{opponentIndex}").AddComponent<FighterController>();

            try
            {
                float half = config.Arena.StartDistance * 0.5f;
                self.Init(config, enemy, -half);
                enemy.Init(config, self, half);

                var selfEvaluator = new RuleEvaluator(taught);
                var enemyEvaluator = new RuleEvaluator(opponent);

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

                bool selfDown = self.State == FighterState.Down;
                bool enemyDown = enemy.State == FighterState.Down;
                bool won = !selfDown && (enemyDown || (!enemyDown && self.HpPct > enemy.HpPct));

                string verdict = won ? "✅ 승리" : "❌ 패배/무승부";
                Debug.Log($"[밸런싱 검증] opponent_{opponentIndex:00}({opponent.FighterName}) {verdict} - 경과 {matchTime:0.0}s / 제자 HP {self.HpPct:0}% / 상대 HP {enemy.HpPct:0}% / 마진 {(self.HpPct - enemy.HpPct):+0;-0}%");
            }
            finally
            {
                Object.DestroyImmediate(self.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        // DEV_SPEC 05장 "공략(기대 가르침)" 힌트를 실제 RuleSet으로 구성 - 밸런싱 시뮬레이션 전용(훈련 슬롯 제한 미적용).
        private static RuleSet TaughtRuleSet(int opponentIndex)
        {
            List<Rule> rules;
            switch (opponentIndex)
            {
                case 1: // 러쉬: 상대 약공 낌새엔 물러나서 헛치게 만들고(회피), 헛친 직후(무방비)에 강공 처벌
                    rules = new List<Rule>
                    {
                        Rule("t1", "약공 낌새엔 회피", Cond("enemy_action", "==", "light_startup"), "retreat", 9),
                        Rule("t2", "헛치면 강공 처벌", Cond("enemy_action", "==", "whiff_recovery"), "heavy_attack", 8),
                        Rule("t3", "상대 지치면 강공", Cond("enemy_stamina_pct", "<", 30), "heavy_attack", 7),
                        Rule("t4", "거리 좁히기", Cond("distance", ">", 1.5), "approach", 3),
                    };
                    break;

                case 2: // 철벽: 대시로 빠르게 붙어 약공(헛치면 처벌 회피), 대시 쿨다운 중엔 접근
                    rules = new List<Rule>
                    {
                        Rule("t1", "근접 약공", Cond("distance", "<=", 1.2), "light_attack", 8),
                        Rule("t2", "대시 접근", Cond("distance", ">", 1.2), "dash", 6, "toward"),
                        Rule("t3", "걸어서 접근", Cond("distance", ">", 1.2), "approach", 3),
                    };
                    break;

                case 3: // 그림자: 강공/궁 지양(회피대시 유발 방지), 약공 위주로 압박
                    rules = new List<Rule>
                    {
                        Rule("t1", "근접 약공", Cond("distance", "<=", 1.2), "light_attack", 8),
                        Rule("t2", "대시 접근", Cond("distance", ">", 1.2), "dash", 6, "toward"),
                        Rule("t3", "걸어서 접근", Cond("distance", ">", 1.2), "approach", 3),
                    };
                    break;

                case 4: // 카멜레온: 전 구간 공용 압박 + 헛치면 강공 처벌 + 궁 즉발
                    rules = new List<Rule>
                    {
                        Rule("t0", "궁 즉발", Cond("self_ult_gauge", ">=", 100), "ultimate", 10),
                        Rule("t1", "헛치면 강공 처벌", CondAnd(("enemy_action", "==", "whiff_recovery"), ("distance", "<=", 1.5)), "heavy_attack", 9),
                        Rule("t2", "근접 약공", Cond("distance", "<=", 1.2), "light_attack", 7),
                        Rule("t3", "대시 접근", Cond("distance", ">", 1.2), "dash", 6, "toward"),
                        Rule("t4", "걸어서 접근", Cond("distance", ">", 1.2), "approach", 3),
                    };
                    break;

                case 5: // 사범(보스): 상대 공격 낌새(약공/강공)엔 물러나서 헛치게 하고, 헛친 직후 강공 처벌.
                        // 강공/도보접근은 상대 반응규칙(회피대시·견제)을 유발하므로 우리 쪽 평시 압박엔 안 씀.
                default:
                    rules = new List<Rule>
                    {
                        Rule("t0", "궁 즉발", Cond("self_ult_gauge", ">=", 100), "ultimate", 10),
                        Rule("t1", "약공 낌새엔 회피", Cond("enemy_action", "==", "light_startup"), "retreat", 9),
                        Rule("t1b", "강공 낌새엔 회피", Cond("enemy_action", "==", "heavy_startup"), "retreat", 9),
                        // 보스는 "우리 강공 낌새"에 회피 대시로 반응(rule_02) - 강공으로 처벌하면 항상 헛침.
                        // 약공은 그 반응을 안 유발하니 약공으로 처벌.
                        Rule("t2", "헛치면 약공 처벌", Cond("enemy_action", "==", "whiff_recovery"), "light_attack", 8),
                        Rule("t5", "저스태미나 휴식", Cond("self_stamina_pct", "<", 30), "idle", 7),
                        Rule("t3", "근접 약공", Cond("distance", "<=", 1.2), "light_attack", 5),
                        // "approach"(걷기)는 보스 rule_04(견제) 유발 - 대시 쿨다운 중엔 접근 대신 대기(휴식)
                        Rule("t4", "대시 접근", Cond("distance", ">", 1.2), "dash", 4, "toward"),
                        Rule("t4b", "대시 쿨다운 중 대기", Cond("distance", ">", 1.2), "idle", 3),
                    };
                    break;
            }

            return new RuleSet { Version = 1, FighterName = "제자", MaxSlots = 10, Rules = rules };
        }

        private static List<Condition> Cond(string fact, string op, object value)
            => new List<Condition> { new Condition { Fact = fact, Op = op, Value = value } };

        private static List<Condition> CondAnd(params (string fact, string op, object value)[] conds)
        {
            var list = new List<Condition>();
            foreach (var c in conds)
                list.Add(new Condition { Fact = c.fact, Op = c.op, Value = c.value });
            return list;
        }

        private static Rule Rule(string id, string label, List<Condition> when, string action, int priority, string direction = null)
        {
            var ruleAction = new RuleAction { Action = action };
            if (direction != null)
                ruleAction.Params = new Dictionary<string, object> { { "direction", direction } };

            return new Rule { Id = id, Label = label, When = when, Do = ruleAction, Priority = priority };
        }

        private static Rule RuleKeepDistance(string id, string label, List<Condition> when, float range, int priority)
        {
            var ruleAction = new RuleAction
            {
                Action = "keep_distance",
                Params = new Dictionary<string, object> { { "range", range } },
            };
            return new Rule { Id = id, Label = label, When = when, Do = ruleAction, Priority = priority };
        }
    }
}
