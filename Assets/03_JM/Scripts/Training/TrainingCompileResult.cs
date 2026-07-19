using System.Collections.Generic;
using TeachAndFight.Core.Rules;

namespace TeachAndFight.Training
{
    // 훈련 컴파일 결과 상태 (03장 완료기준 4케이스와 1:1).
    public enum TrainingOutcome
    {
        Applied,           // 규칙셋에 반영됨
        NeedsConfirmation, // 적용 안 함 - 거절/되묻기/모순(되물어야 함)
        Rejected,          // LLM은 적용하라 했으나 RuleValidator가 거부(최종 저지선)
        Failed             // LLM 호출/파싱 실패 - 폴백
    }

    // TrainingCompiler 산출물. UI(#14)가 그대로 소비한다.
    public sealed class TrainingCompileResult
    {
        public TrainingOutcome Outcome;

        // Applied면 갱신된 규칙셋, 그 외엔 입력 규칙셋 그대로(변경 없음).
        public RuleSet ResultingRuleSet;

        // 말풍선에 띄울 제자 대사(항상 채워짐).
        public string DiscipleReply;

        // 모순(conflict) 시 충돌한 rule id, 없으면 null.
        public string ConflictWith;

        // Rejected/Failed 진단용(사용자 노출 X, 로깅용).
        public List<string> Errors = new List<string>();

        public bool Applied => Outcome == TrainingOutcome.Applied;
    }
}
