using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;

namespace TeachAndFight.Training.UI
{
    // Training 화면의 UI 비의존 코어(테스트 가능). MonoBehaviour(View)는 이 프레젠터에 위임만 한다.
    // 04장 동작 규칙을 여기서 결정: 빈 입력 무시 / Applied만 슬롯 반영 / 그 외는 말풍선만 / 슬롯 직접 삭제.
    public sealed class TrainingPresenter
    {
        // 03장: 되묻기 후속 입력이 이전 질문을 기억하도록 직전 2턴("코치: .../제자: ...")을 들고 다닌다.
        private const int MaxDialogueTurns = 2;

        private readonly GameSession _session;
        private readonly TrainingCompiler _compiler;
        private readonly List<string> _recentDialogue = new List<string>();

        public TrainingPresenter(GameSession session, TrainingCompiler compiler)
        {
            _session = session;
            _compiler = compiler;
        }

        // 현재 제자 규칙셋(세션 소유). View는 이걸 읽어 슬롯을 그린다.
        public RuleSet Current => _session.DiscipleRuleSet;

        public int MaxSlots => Current.MaxSlots;
        public int UsedSlots => Current.Rules.Count;
        public int RemainingSlots => Current.MaxSlots - Current.Rules.Count;

        // 코치의 자연어 입력을 한 번 처리한다. 빈 입력이면 컴파일 없이 무시.
        // Applied면 세션 규칙셋을 갱신(그 외 상태는 규칙셋 불변).
        public async UniTask<TrainingTurnResult> TeachAsync(string coachInput, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(coachInput))
                return TrainingTurnResult.IgnoredInput();

            var compiled = await _compiler.CompileAsync(_session.DiscipleRuleSet, coachInput, _recentDialogue, ct);

            if (compiled.Outcome == TrainingOutcome.Applied)
                _session.DiscipleRuleSet = compiled.ResultingRuleSet; // ApplyOps가 깊은 복사한 신규 규칙셋

            RecordTurn(coachInput, compiled.DiscipleReply);

            return new TrainingTurnResult
            {
                Outcome = compiled.Outcome,
                DiscipleReply = compiled.DiscipleReply,
                ConflictWith = compiled.ConflictWith,
                SlotsChanged = compiled.Outcome == TrainingOutcome.Applied,
            };
        }

        // 직전 대화 기록에 이번 턴을 추가하고 최근 2턴(코치+제자 총 4줄)만 남긴다.
        private void RecordTurn(string coachInput, string discipleReply)
        {
            _recentDialogue.Add($"코치: {coachInput}");
            _recentDialogue.Add($"제자: {discipleReply}");

            int maxLines = MaxDialogueTurns * 2;
            if (_recentDialogue.Count > maxLines)
                _recentDialogue.RemoveRange(0, _recentDialogue.Count - maxLines);
        }

        // 슬롯 [×] 삭제: LLM 없이 규칙셋에서 직접 제거. 제거되면 true.
        public bool RemoveRule(string ruleId)
        {
            if (string.IsNullOrEmpty(ruleId))
                return false;
            return Current.Rules.RemoveAll(r => r.Id == ruleId) > 0;
        }
    }
}
