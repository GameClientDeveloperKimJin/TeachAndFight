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
        };
    }
}
