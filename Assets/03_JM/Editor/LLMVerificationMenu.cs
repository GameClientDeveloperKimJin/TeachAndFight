using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TeachAndFight.Core.Rules;
using TeachAndFight.Training.LLM;
using UnityEditor;
using UnityEngine;

namespace TeachAndFight.Training.EditorTools
{
    // #9 수동 검증 하네스 (03장 프롬프트 품질 확인용).
    // ANTHROPIC_API_KEY가 준비되면 메뉴에서 실행해 실제 응답을 콘솔로 확인한다.
    // 키가 없으면 폴백 경로가 동작하는지 확인된다(게임이 죽지 않음).
    public static class LLMVerificationMenu
    {
        private const string SampleRuleSet =
            "{\"version\":1,\"fighter_name\":\"제자\",\"max_slots\":8,\"rules\":[]}";

        [MenuItem("TeachAndFight/LLM/훈련 컴파일 프롬프트 검증 - 샘플 호출")]
        public static void RunSampleCompile()
        {
            RunSampleCompileAsync().Forget();
        }

        private static async UniTaskVoid RunSampleCompileAsync()
        {
            var client = new AnthropicLLMClient();
            Debug.Log($"[LLM 검증] API 키 감지: {client.HasApiKey} (env {LLMSettings.ApiKeyEnvVar})");

            const string coachInput = "상대가 궁 쓰면 대시로 피해";
            var userMessage = TrainingPromptBuilder.BuildUserMessage(SampleRuleSet, 8, coachInput);

            Debug.Log($"[LLM 검증] 코치의 말: {coachInput}");
            var result = await client.CompleteAsync(TrainingPromptBuilder.SystemPrompt, userMessage);

            if (result.Success)
                Debug.Log($"[LLM 검증] ✅ 응답:\n{result.Text}");
            else
                Debug.LogWarning($"[LLM 검증] ⚠ 실패({result.Failure}) → 폴백 대사: \"{result.Text}\"\n상세: {result.ErrorDetail}");
        }

        // #10 전체 파이프라인 검증: 실호출 -> 파싱 -> 검증 -> 규칙셋 반영까지 한 번에 확인.
        // Outcome=Applied + 규칙 1개 추가가 뜨면 "유효 diff 생성" 항목이 라이브로 증명된다.
        [MenuItem("TeachAndFight/LLM/훈련 컴파일 파이프라인 검증 (TrainingCompiler)")]
        public static void RunPipelineCompile()
        {
            RunPipelineAsync().Forget();
        }

        private static async UniTaskVoid RunPipelineAsync()
        {
            var client = new AnthropicLLMClient();
            var compiler = new TrainingCompiler(client);

            var current = new RuleSet
            {
                Version = 1,
                FighterName = "제자",
                MaxSlots = 8,
                Rules = new List<Rule>(),
            };

            const string coachInput = "상대가 궁 쓰면 대시로 피해";
            Debug.Log($"[파이프라인 검증] 키 감지 {client.HasApiKey} / 코치의 말: {coachInput}");

            var r = await compiler.CompileAsync(current, coachInput);

            Debug.Log($"[파이프라인 검증] Outcome={r.Outcome} / 규칙수 {r.ResultingRuleSet.Rules.Count} / 대사: {r.DiscipleReply}");

            if (r.ResultingRuleSet.Rules.Count > 0)
            {
                var rule = r.ResultingRuleSet.Rules[0];
                var cond = rule.When != null && rule.When.Count > 0 ? rule.When[0] : null;
                Debug.Log($"[파이프라인 검증] 추가된 규칙: id={rule.Id} label={rule.Label} priority={rule.Priority} " +
                          $"when={(cond != null ? $"{cond.Fact} {cond.Op} {cond.Value}" : "-")} do={rule.Do?.Action}");
            }

            if (r.ConflictWith != null)
                Debug.Log($"[파이프라인 검증] conflict_with={r.ConflictWith}");

            if (r.Errors.Count > 0)
                Debug.LogWarning($"[파이프라인 검증] errors: {string.Join(" / ", r.Errors)}");
        }
    }
}
