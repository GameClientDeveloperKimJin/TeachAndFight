using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TeachAndFight.Core.Rules;
using TeachAndFight.Flow;
using TeachAndFight.Training.LLM;

namespace TeachAndFight.LockerRoom
{
    // LockerRoom 화면의 UI 비의존 코어(테스트 가능). MonoBehaviour(View)는 이 프레젠터에 위임한다.
    public sealed class LockerRoomPresenter
    {
        private readonly GameSession _session;
        private readonly ILLMClient _client;

        public LockerRoomPresenter(GameSession session, ILLMClient client)
        {
            _session = session;
            _client = client;
        }

        public MatchResult Match => _session.LastMatch;
        public RuleSet RuleSet => _session.DiscipleRuleSet;

        public bool HasMatch => Match != null;
        public bool Won => Match != null && Match.Won;
        public float SelfHpPct => Match?.SelfHpPct ?? 0f;
        public float EnemyHpPct => Match?.EnemyHpPct ?? 0f;

        public string ResultBanner => Match == null ? "경기 기록 없음" : (Match.Won ? "승리!" : "패배...");

        // rule_fired 발동 통계 상위 N (락커룸 표시용).
        public List<FiredRule> TopFiredRules(int top = 3) => MatchStats.TopFiredRules(Match, RuleSet, top);

        // 회고 LLM(호출2). 경기 기록 없으면 폴백.
        public UniTask<RecapResult> RecapAsync(CancellationToken cancellationToken = default)
        {
            if (Match == null)
                return UniTask.FromResult(RecapResult.Fail());
            return RecapService.GetRecapAsync(_client, Match, RuleSet, cancellationToken);
        }
    }
}
