using TeachAndFight.Core.Rules;

namespace TeachAndFight.Flow
{
    // 계획서 "A↔B 접점 계약"의 세션 데이터. GameFlow가 DontDestroyOnLoad로 들고 다닌다.
    // Training/Match/LockerRoom 세 씬이 이 하나의 인스턴스를 공유해 규칙셋·경기결과를 주고받는다.
    // 필드명/타입은 01·02장 스키마와 동일해야 하며, 시그니처 변경 시 상대(A)에게 먼저 알린다.
    public sealed class GameSession
    {
        // 플레이어가 훈련실에서 가르친 제자 규칙셋 (#4 스키마). Training에서 채우고 Match가 소비.
        public RuleSet DiscipleRuleSet;

        // 이번 상대 규칙셋 (opponent_0N.json, #12 산출물). 지금은 이름 표시용으로만 쓰고 로드는 #12에서.
        public RuleSet CurrentOpponent;

        // 몇 번째 상대인가 (1~5). 상단 배너 표시에 사용.
        public int OpponentIndex = 1;

        // 직전 경기 결과. LockerRoom(#15) 회고 입력. 첫 진입 시 null.
        public MatchResult LastMatch;

        // 빈 규칙셋으로 시작(백지 1차전 보장 — #12 완료기준). max_slots 기본 8(01장).
        public static GameSession NewGame(int opponentIndex = 1, int maxSlots = 8) => new GameSession
        {
            OpponentIndex = opponentIndex,
            DiscipleRuleSet = new RuleSet
            {
                Version = 1,
                FighterName = "제자",
                MaxSlots = maxSlots,
                Rules = new System.Collections.Generic.List<Rule>(),
            },
        };
    }
}
