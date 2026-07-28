using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TeachAndFight.Combat;
using TeachAndFight.Core.Rules;
using TeachAndFight.Training;
using TeachAndFight.Training.LLM;
using UnityEditor;
using UnityEngine;

namespace TeachAndFight.Integration.EditorTools
{
    // W3 진입 게이트 "엔드투엔드 통합 시연" 임시 하네스 (#16 씬 전환 전에 데이터 흐름만 검증).
    //
    // 검증하는 것: 플레이어가 자연어로 규칙 하나를 가르치면
    //   TrainingCompiler(03장) → RuleValidator(01장) → 그 규칙셋으로 RuleEvaluator(02장) 전투
    // 까지 배관이 실제로 이어져, "가르친 그 규칙"이 경기에서 발동(rule_fired)되는지.
    //
    // API 키(ANTHROPIC_API_KEY)가 있으면 실제 LLM으로, 없으면 고정 diff 스텁으로 폴백해
    // 키 유무와 무관하게 흐름 자체를 돌려볼 수 있게 했다(수동 게이트 확인용, 자동 테스트 아님).
    public static class EndToEndVerificationMenu
    {
        // startDistance=6 상태에서 즉시 참이 되는 조건(거리>1.2 → 접근)이라, 정지한 상대에게도 반드시 발동한다.
        // 실제 LLM도 이 문장이면 distance 기반 approach 규칙을 내도록 유도됨.
        private const string CoachInput = "상대랑 거리가 멀면 다가가서 붙어";

        // 키 없을 때 폴백: 위 가르침을 그대로 반영한 유효 add diff(01장 어휘 화이트리스트 통과).
        private const string StubDiffJson =
            "{\"ops\":[{\"op\":\"add\",\"rule\":{\"id\":\"rule_close_in\",\"label\":\"멀면 접근\"," +
            "\"when\":[{\"fact\":\"distance\",\"op\":\">\",\"value\":1.2}]," +
            "\"do\":{\"action\":\"approach\"},\"priority\":5}}]," +
            "\"disciple_reply\":\"거리가 멀면 다가갈게요.\",\"needs_confirmation\":false,\"conflict_with\":null}";

        // 키 없을 때만 쓰는 스텁 LLM. 실제 파이프라인(파싱/검증/적용)은 그대로 타고, 응답만 고정한다.
        private sealed class StubLLMClient : ILLMClient
        {
            public UniTask<LLMResult> CompleteAsync(
                string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
                => UniTask.FromResult(LLMResult.Ok(StubDiffJson));
        }

        // 기본: 키 있으면 실제 LLM, 없으면 스텁. (크레딧 있으면 진짜 자연어→규칙 흐름까지 검증)
        [MenuItem("TeachAndFight/Integration/엔드투엔드 검증 - 가르침→컴파일→전투")]
        public static void RunEndToEnd()
        {
            var anthropic = new AnthropicLLMClient();
            ILLMClient client = anthropic.HasApiKey ? (ILLMClient)anthropic : new StubLLMClient();
            string mode = anthropic.HasApiKey ? "실제 LLM" : "스텁(키 없음)";
            RunEndToEndAsync(client, mode).Forget();
        }

        // 스텁 강제: LLM/크레딧 없이도 배관(컴파일 출력 규칙셋 → RuleEvaluator 발동)만 결정적으로 증명.
        [MenuItem("TeachAndFight/Integration/엔드투엔드 검증 (스텁 강제 - LLM/크레딧 없이 배관만)")]
        public static void RunEndToEndStub()
        {
            RunEndToEndAsync(new StubLLMClient(), "스텁 강제").Forget();
        }

        private static async UniTaskVoid RunEndToEndAsync(ILLMClient client, string mode)
        {
            // 1) 가르침 컴파일 (03장 파이프라인).
            var current = new RuleSet
            {
                Version = 1,
                FighterName = "제자",
                MaxSlots = 8,
                Rules = new List<Rule>(),
            };

            Debug.Log($"[E2E] 1/3 가르침 컴파일 시작 ({mode}) / 코치의 말: \"{CoachInput}\"");

            var compiled = await new TrainingCompiler(client).CompileAsync(current, CoachInput);
            Debug.Log($"[E2E] 컴파일 결과 Outcome={compiled.Outcome} / 규칙수 {compiled.ResultingRuleSet.Rules.Count} / 대사: {compiled.DiscipleReply}");

            if (compiled.Outcome != TrainingOutcome.Applied || compiled.ResultingRuleSet.Rules.Count == 0)
            {
                // 실패/거절 사유를 지금 보이는 파란 Debug.Log 스트림으로 찍는다(경고 필터와 무관하게 보이도록).
                // Failed면 LLM 호출/파싱 실패 상세(HTTP 상태 등)가 compiled.Errors에 담긴다.
                string why = compiled.Errors.Count > 0 ? string.Join(" / ", compiled.Errors) : "(상세 없음)";
                Debug.Log(
                    $"[E2E] === 실패 사유 === Outcome={compiled.Outcome} / 사유: {why}\n" +
                    "→ Failed면 LLM 호출/파싱 실패(키·네트워크 확인), NeedsConfirmation이면 LLM이 되묻기/거절한 것.");
                return;
            }

            var taughtRuleSet = compiled.ResultingRuleSet;
            var taughtIds = new HashSet<string>(taughtRuleSet.Rules.Select(r => r.Id));
            Debug.Log($"[E2E] 2/3 가르친 규칙 id: {string.Join(", ", taughtIds)} → 이 규칙셋으로 전투 시뮬레이션.");

            // 2) 그 규칙셋으로 RuleEvaluator 전투 (02장) — RuleEvaluatorVerificationMenu와 동일한 하네스 구성.
            var config = CombatConfigLoader.Load();
            var self = new GameObject("E2E_제자").AddComponent<FighterController>();
            var enemy = new GameObject("E2E_상대").AddComponent<FighterController>();
            try
            {
                float half = config.Arena.StartDistance * 0.5f;
                self.Init(config, enemy, -half);
                enemy.Init(config, self, half);

                var eventLog = new EventLog();
                var evaluator = new RuleEvaluator(taughtRuleSet, eventLog);
                evaluator.OnRuleFired += id => Debug.Log($"[E2E] 규칙 발동: {id}");

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

                // 3) 판정: 가르친 규칙이 실제로 발동했는가?
                var firedIds = eventLog.Events
                    .Where(e => e.Type == "rule_fired" && e.RuleId != null)
                    .Select(e => e.RuleId)
                    .ToList();
                var taughtFired = firedIds.Where(id => taughtIds.Contains(id)).Distinct().ToList();

                Debug.Log($"[E2E] 3/3 종료(경과 {matchTime:0.0}s) / rule_fired {firedIds.Count}건 / EventLog 총 {eventLog.Events.Count}건.");

                if (taughtFired.Count > 0)
                    Debug.Log($"[E2E] ✅ 엔드투엔드 성립 — 가르친 규칙 [{string.Join(", ", taughtFired)}]이 실제 전투에서 발동됨. " +
                              "자연어 가르침 → 컴파일 → 검증 → 전투 반영 흐름이 코드상 이어져 있음이 확인됨.");
                else
                    Debug.LogWarning($"[E2E] ⚠ 가르친 규칙이 이번 경기에서 한 번도 발동되지 않음. " +
                                     $"발동된 규칙: {(firedIds.Count > 0 ? string.Join(", ", firedIds.Distinct()) : "없음")}. " +
                                     "규칙 조건이 이번 시뮬레이션 상황에서 참이 안 됐을 수 있음 — 조건이 즉시 참이 되는 가르침으로 재확인할 것.");
            }
            finally
            {
                Object.DestroyImmediate(self.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }
    }
}
