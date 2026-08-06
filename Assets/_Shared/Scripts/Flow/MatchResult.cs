using System.Collections.Generic;
using TeachAndFight.Combat;

namespace TeachAndFight.Flow
{
    // 계획서 접점 계약의 경기 결과. Match(#13, A)가 종료 시 생성 → GameSession.LastMatch에 담김 →
    // LockerRoom(#15, B)이 회고 프롬프트 구성에 소비.
    // ⚠ 계획서 초안엔 EventLog 타입이 EventLogEntry로 적혀 있으나 실제 구현 타입은 02장 EventLog의 MatchEvent다.
    //    실제 코드 기준으로 MatchEvent를 쓴다(철자 통일). 이 시그니처 변경 시 상대(A)에게 먼저 알릴 것.
    public sealed class MatchResult
    {
        public bool Won;
        public float SelfHpPct;
        public float EnemyHpPct;

        // 양쪽 통틀어 실제로 맞은 타격 수(교착 상태 감지용, LockerRoom에서 사용). 필드 추가만이라
        // 기존 소비 코드엔 영향 없음.
        public int HitsLanded;

        // 02장 EventLog 이벤트 목록 (#8 산출물). rule_fired 빈도/피격 직전 상태 요약에 사용.
        public List<MatchEvent> EventLog = new List<MatchEvent>();
    }
}
