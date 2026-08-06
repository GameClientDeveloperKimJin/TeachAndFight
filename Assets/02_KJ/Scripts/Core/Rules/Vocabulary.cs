using System.Collections.Generic;

namespace TeachAndFight.Core.Rules
{
    // 01장 어휘 사전. 여기 없는 fact/action은 컴파일 거부 대상.
    // C# 여기 철자 <-> LLM 시스템 프롬프트 철자가 항상 동일해야 함.
    public static class Vocabulary
    {
        public static readonly HashSet<string> Facts = new HashSet<string>
        {
            "self_hp_pct", "self_stamina_pct", "self_ult_gauge",
            "enemy_hp_pct", "enemy_stamina_pct", "distance",
            "enemy_action", "time_left", "self_action",
            // #26 Tier1 확장: 벽 거리 / 상태 유지시간 / 최근 헛스윙 카운트
            "self_wall_dist", "self_action_duration", "enemy_action_duration",
            "enemy_whiff_count",
        };

        // enemy_action / self_action : 문자열 fact, ==/!= 만 허용
        public static readonly HashSet<string> StringFacts = new HashSet<string>
        {
            "enemy_action", "self_action",
        };

        // enemy_action / self_action 이 가질 수 있는 값
        public static readonly HashSet<string> ActionStateValues = new HashSet<string>
        {
            "idle", "approach", "retreat", "light_startup", "heavy_startup",
            "ultimate_startup", "dash", "whiff_recovery", "hit_stun",
            // 실제 상태값 아님 - light_startup/heavy_startup 둘 다에 매칭되는 포괄 값(RuleEvaluator에서
            // 특수 처리, 궁 제외). "공격 준비하면" 같은 문장을 규칙 1개로 표현하게 해줌.
            "attack_startup",
        };

        public static readonly HashSet<string> Ops = new HashSet<string>
        {
            "==", "!=", ">", "<", ">=", "<=",
        };

        public static readonly HashSet<string> StringOnlyOps = new HashSet<string>
        {
            "==", "!=",
        };

        // do.action 에 쓸 수 있는 행동
        public static readonly HashSet<string> Actions = new HashSet<string>
        {
            "approach", "retreat", "keep_distance", "dash",
            "light_attack", "heavy_attack", "ultimate", "idle",
            // #26 Tier2 확장: 기존 Attack_Light 클립 재사용, 신규 애니 없음
            "counter_attack", "feint",
        };

        public static readonly Dictionary<string, (double Min, double Max)> FactRanges =
            new Dictionary<string, (double Min, double Max)>
        {
            { "self_hp_pct", (0, 100) },
            { "self_stamina_pct", (0, 100) },
            { "self_ult_gauge", (0, 100) },
            { "enemy_hp_pct", (0, 100) },
            { "enemy_stamina_pct", (0, 100) },
            { "distance", (0, 12) },
            { "time_left", (0, 60) },
            // 기본 combat_config.json 기준(arena.width=12 → half=6). distance와 동일 가정.
            { "self_wall_dist", (0, 6) },
            { "self_action_duration", (0, 60) },
            { "enemy_action_duration", (0, 60) },
            { "enemy_whiff_count", (0, 20) },
        };
    }
}
