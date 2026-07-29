using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TeachAndFight.Combat;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;
using TeachAndFight.Match;

namespace TeachAndFight.Match.EditorTools
{
    // #13 완료기준 수동 검증: 발동 규칙 라벨(OnRuleFired, HUD가 구독하는 이벤트)이
    // 실제 RuleEvaluator EventLog(rule_fired)와 눈으로 봐도 일치하는지 콘솔 대조.
    public static class MatchVerificationMenu
    {
        [MenuItem("TeachAndFight/Match/라벨-로그 대조 검증 - 백지 vs 러쉬")]
        public static void RunLabelLogComparison()
        {
            var disciple = new RuleSet { Version = 1, FighterName = "제자", MaxSlots = 8, Rules = new List<Rule>() };
            var rush = OpponentRuleSetLoader.Load(1);
            var session = new GameSession { DiscipleRuleSet = disciple, CurrentOpponent = rush, OpponentIndex = 1 };

            var config = CombatConfigLoader.Load();
            var self = new GameObject("검증_제자").AddComponent<FighterController>();
            var enemy = new GameObject("검증_러쉬").AddComponent<FighterController>();

            try
            {
                float half = config.Arena.StartDistance * 0.5f;
                self.Init(config, enemy, -half);
                enemy.Init(config, self, half);

                var presenter = new MatchPresenter(session, config, self, enemy);
                var uiEvents = new List<string>();
                presenter.OnRuleFired += (who, ruleId, label) =>
                    uiEvents.Add($"{(who == self ? "제자" : "상대")}:{ruleId}={label}");

                const float dt = 0.1f;
                int maxSteps = Mathf.CeilToInt(config.Match.DurationSec / dt) + 10;
                for (int i = 0; i < maxSteps && !presenter.Concluded; i++)
                    presenter.Step(dt);

                Debug.Log($"[Match 검증] 종료 - 제자 HP {self.HpPct:0}% / 러쉬 HP {enemy.HpPct:0}% / 승패: {(presenter.Result.Won ? "승리" : "패배")}");
                Debug.Log($"[Match 검증] OnRuleFired(HUD 라벨) {uiEvents.Count}건 - {string.Join(", ", uiEvents.Take(10))}{(uiEvents.Count > 10 ? " ..." : "")}");

                var loggedFired = presenter.SelfEventLog.Where(e => e.Type == "rule_fired").ToList();
                Debug.Log($"[Match 검증] EventLog(제자쪽) rule_fired {loggedFired.Count}건 - {string.Join(", ", loggedFired.Take(10).Select(e => e.RuleId))}");

                if (loggedFired.Count == 0)
                    Debug.Log("[Match 검증] 백지 제자는 규칙이 없어 EventLog rule_fired 0건이 정상(항상 Idle) - HUD 라벨도 제자쪽은 안 뜨는 게 맞음");
                else
                    Debug.LogWarning("[Match 검증] 백지 규칙셋인데 제자 rule_fired가 기록됨 - 확인 필요");
            }
            finally
            {
                Object.DestroyImmediate(self.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }
    }
}
