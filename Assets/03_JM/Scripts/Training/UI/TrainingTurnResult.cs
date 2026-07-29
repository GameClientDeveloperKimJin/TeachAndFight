using TeachAndFight.Core.Rules;

namespace TeachAndFight.Training.UI
{
    // TrainingPresenter가 한 번의 "가르치기" 처리 후 View에 넘기는 결과.
    // View(MonoBehaviour)는 이 값만 보고 말풍선/대화로그/슬롯을 갱신한다(Unity 의존 없이 테스트 가능하게 분리).
    public sealed class TrainingTurnResult
    {
        // 빈 입력 등으로 컴파일을 아예 하지 않은 경우 true(대화/슬롯 변화 없음).
        public bool Ignored;

        // 컴파일 결과 상태(Applied/NeedsConfirmation/Rejected/Failed).
        public TrainingOutcome Outcome;

        // 제자 대사(말풍선 + 대화로그). Ignored면 null.
        public string DiscipleReply;

        // 모순(conflict) 시 충돌한 rule id, 없으면 null.
        public string ConflictWith;

        // 규칙 슬롯이 바뀌었는가(Applied일 때만 true) → View가 슬롯 리스트를 다시 그림.
        public bool SlotsChanged;

        public static TrainingTurnResult IgnoredInput() => new TrainingTurnResult { Ignored = true };
    }
}
