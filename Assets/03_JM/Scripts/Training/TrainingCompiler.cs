using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using TeachAndFight.Core.Rules;
using TeachAndFight.Training.LLM;
using UnityEngine;

namespace TeachAndFight.Training
{
    // #10 훈련 컴파일 파이프라인. 자연어 -> LLM -> RuleDiff -> RuleValidator -> 규칙셋 반영.
    // 03장 "게임 측 처리 흐름"을 구현. LLM은 여기서만 부르고 전투 루프엔 안 들어간다.
    public sealed class TrainingCompiler
    {
        private readonly ILLMClient _client;

        // Rejected(검증 탈락) 시 제자 대사. 존댓말 톤(03장 rule 6). 수정은 이 상수만.
        public const string RejectedReply = "무슨 말인지 모르겠어요. 다시 알려주시겠어요?";

        // needs_confirmation인데 대사가 비어 온 경우 최소 대체.
        private const string EmptyReplyFallback = "음... 조금만 더 자세히 말씀해 주시겠어요?";

        public TrainingCompiler(ILLMClient client)
        {
            _client = client;
        }

        public async UniTask<TrainingCompileResult> CompileAsync(
            RuleSet current, string coachInput, CancellationToken cancellationToken = default)
        {
            int remainingSlots = current.MaxSlots - current.Rules.Count;
            string ruleSetJson = JsonConvert.SerializeObject(current);
            string userMessage = TrainingPromptBuilder.BuildUserMessage(ruleSetJson, remainingSlots, coachInput);

            var llm = await _client.CompleteAsync(
                TrainingPromptBuilder.SystemPrompt, userMessage, cancellationToken);

            // 1) LLM 호출/폴백 실패
            if (!llm.Success)
                return Failed(current, llm.Text, $"LLM 실패: {llm.Failure} {llm.ErrorDetail}");

            // 2) 응답 -> RuleDiff 파싱 실패
            if (!RuleDiffParser.TryParse(llm.Text, out var diff, out var parseError))
                return Failed(current, LLMResult.FallbackReply, parseError);

            var reply = string.IsNullOrWhiteSpace(diff.DiscipleReply) ? EmptyReplyFallback : diff.DiscipleReply;

            // 3) needs_confirmation -> 적용하지 않음(거절/되묻기/모순)
            if (diff.NeedsConfirmation)
            {
                return new TrainingCompileResult
                {
                    Outcome = TrainingOutcome.NeedsConfirmation,
                    ResultingRuleSet = current,
                    DiscipleReply = reply,
                    ConflictWith = diff.ConflictWith,
                };
            }

            // 4) 적용 시도 -> RuleValidator가 최종 저지선(어휘/슬롯/중복)
            var apply = RuleValidator.ApplyOps(current, diff.Ops);
            if (!apply.Success)
            {
                Debug.LogWarning($"[Training] LLM은 적용을 요청했으나 검증 탈락 -> Rejected. {string.Join(" / ", apply.Errors)}");
                var rejected = new TrainingCompileResult
                {
                    Outcome = TrainingOutcome.Rejected,
                    ResultingRuleSet = current,
                    DiscipleReply = RejectedReply,
                };
                rejected.Errors.AddRange(apply.Errors);
                return rejected;
            }

            return new TrainingCompileResult
            {
                Outcome = TrainingOutcome.Applied,
                ResultingRuleSet = apply.UpdatedRuleSet,
                DiscipleReply = reply,
            };
        }

        private static TrainingCompileResult Failed(RuleSet current, string reply, string error)
        {
            var result = new TrainingCompileResult
            {
                Outcome = TrainingOutcome.Failed,
                ResultingRuleSet = current,
                DiscipleReply = reply,
            };
            result.Errors.Add(error);
            return result;
        }
    }
}
