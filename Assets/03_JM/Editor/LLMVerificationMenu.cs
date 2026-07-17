using Cysharp.Threading.Tasks;
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
    }
}
