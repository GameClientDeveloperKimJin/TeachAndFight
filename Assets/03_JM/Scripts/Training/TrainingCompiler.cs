using System.Collections.Generic;
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
            RuleSet current, string coachInput, IReadOnlyList<string> recentDialogue = null,
            CancellationToken cancellationToken = default)
        {
            int remainingSlots = current.MaxSlots - current.Rules.Count;
            string ruleSetJson = JsonConvert.SerializeObject(current);
            string userMessage = TrainingPromptBuilder.BuildUserMessage(ruleSetJson, remainingSlots, coachInput, recentDialogue);

            var llm = await _client.CompleteAsync(
                TrainingPromptBuilder.SystemPrompt, userMessage, cancellationToken);

            // 1) LLM 호출/폴백 실패
            if (!llm.Success)
                return Failed(current, llm.Text, $"LLM 실패: {llm.Failure} {llm.ErrorDetail}");

            // 2) 응답 -> RuleDiff 파싱 실패
            if (!RuleDiffParser.TryParse(llm.Text, out var diff, out var parseError))
                return Failed(current, LLMResult.FallbackReply, parseError);

            var reply = string.IsNullOrWhiteSpace(diff.DiscipleReply) ? EmptyReplyFallback : diff.DiscipleReply;

            // 3) 순수 되묻기(거절/모호함/충돌) -> ops 없이 needs_confirmation만 온 경우. 적용 안 함.
            if (diff.NeedsConfirmation && diff.Ops.Count == 0)
            {
                return new TrainingCompileResult
                {
                    Outcome = TrainingOutcome.NeedsConfirmation,
                    ResultingRuleSet = current,
                    DiscipleReply = reply,
                    ConflictWith = diff.ConflictWith,
                };
            }

            // 유저 요구사항: 가르침 1번 = 규칙 1개. "약공이나 강공 준비하면" 같은 OR 뜻 문장을 LLM이
            // 규칙 2개(add 2번)로 쪼개 응답하는 경우가 있었음 - 프롬프트도 고쳤지만(2번 규칙) LLM이
            // 매번 지킨다는 보장이 없어서 최종 저지선을 코드에도 둔다(어휘 화이트리스트/RuleValidator와
            // 같은 방어 원칙). 대신 거절하지 않고 하나씩 나눠 가르쳐달라고 되묻는다.
            if (diff.Ops.Count > 1)
            {
                return new TrainingCompileResult
                {
                    Outcome = TrainingOutcome.NeedsConfirmation,
                    ResultingRuleSet = current,
                    DiscipleReply = "한 번에 여러 상황을 말씀하신 것 같아요. 하나씩 나눠서 가르쳐주시겠어요?",
                };
            }

            // 여기 도달하면 ops가 정확히 1개. needs_confirmation=true인데 op가 1개 있는 경우는
            // "A일 때 B하고 C일 때 D해줘"류 복합 문장(10번 규칙) - 확정된 첫 부분(A->B)은 그대로
            // 적용하고, 나머지(C->D)에 대한 되묻기는 disciple_reply 뒷부분에 실려 온다. 그래서
            // needs_confirmation 값과 무관하게 op 1개는 항상 적용을 시도한다.

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
