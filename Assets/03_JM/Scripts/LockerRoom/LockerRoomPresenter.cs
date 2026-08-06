using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TeachAndFight.Combat;
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

        // 승리 다음 상대로 진행하는 흐름이 원래 통째로 빠져있었음(OpponentIndex를 어디서도 증가
        // 안 시킴 - 이겨도 계속 opponent_01만 재대결됨). 여기서 이어준다.
        public bool HasNextOpponent => Won && _session.OpponentIndex < OpponentRuleSetLoader.MaxIndex;
        public bool IsGameCleared => Won && _session.OpponentIndex >= OpponentRuleSetLoader.MaxIndex;

        // 유저 피드백: 서로 안 부딪히기만 하다 타임업 지면(예: approach만 가르쳐서 도망형 상대를
        // 못 따라잡은 경우) 그냥 "패배..."로만 뜨면 왜 졌는지 안 보임 - 구분해서 알려준다.
        // 견제/시간끌기로 "이기는" 경우는 해당 안 됨(Won 조건 있음 - 정식 공략이라 건드리지 않음).
        private const int StalemateHitThreshold = 1;
        public bool WasStalemate => Match != null && !Match.Won && Match.HitsLanded <= StalemateHitThreshold;

        // 유저 요구사항: 교착 상태 원인을 규칙셋 기반으로 구체적으로 짚어준다(#26 QA에서 실제로
        // 발견된 패턴 - dash만 가르치고 대시 쿨다운 폴백을 안 가르쳐서 생긴 사범전 교착이 대표 사례).
        public string StalemateFeedback
        {
            get
            {
                if (!WasStalemate)
                    return null;

                bool hasApproach = false;
                bool hasDashToward = false;

                foreach (var rule in RuleSet?.Rules ?? new List<Rule>())
                {
                    var action = rule?.Do?.Action;
                    if (action == "approach" || action == "keep_distance")
                    {
                        hasApproach = true;
                    }
                    else if (action == "dash")
                    {
                        string dir = "toward";
                        if (rule.Do.Params != null && rule.Do.Params.TryGetValue("direction", out var d) && d != null)
                            dir = d.ToString();
                        if (dir != "away")
                            hasDashToward = true;
                    }
                }

                if (!hasApproach && !hasDashToward)
                    return "원인: 상대에게 다가가는 규칙이 아예 없어요. \"거리가 멀면 다가가\" 같은 규칙부터 가르쳐보세요.";

                if (hasDashToward && !hasApproach)
                    return "원인: 대시로 다가가는 규칙만 있어요. 대시는 쿨다운(1초)이 있어서 그 사이를 채워줄 \"대시 못 쓸 때는 걸어서 접근해\" 규칙도 같이 가르쳐보세요.";

                return "원인: 접근 규칙은 있는데도 안 부딪혔어요. 상대가 계속 도망 다니는 타입일 수 있어요 - 조건이나 우선순위를 점검해보세요.";
            }
        }

        public string ResultBanner
        {
            get
            {
                if (Match == null) return "경기 기록 없음";
                if (IsGameCleared) return "우승! 모든 상대를 이겼습니다";
                if (WasStalemate) return "교착 상태로 조기 종료 - 패배 처리됐어요";
                return Match.Won ? "승리!" : "패배...";
            }
        }

        // "다음 상대" 버튼에서 호출. 다음 인덱스로 넘기고, 이번 상대 정보는 비워서
        // Training 배너가 다음 상대 로드 전까지 옛 이름을 잘못 보여주지 않게 한다.
        public void AdvanceToNextOpponent()
        {
            if (!HasNextOpponent)
                return;

            _session.OpponentIndex++;
            _session.CurrentOpponent = null;
        }

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
