using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeachAndFight.Core.Rules
{
    // LLM 컴파일러 → 게임. 01장 "규칙 diff 포맷".
    public class RuleOp
    {
        [JsonProperty("op")] public string Op; // "add" | "update" | "delete"
        [JsonProperty("id")] public string Id; // update/delete 대상
        [JsonProperty("rule")] public Rule Rule; // add/update 내용
    }

    public class RuleDiff
    {
        [JsonProperty("ops")] public List<RuleOp> Ops = new List<RuleOp>();
        [JsonProperty("disciple_reply")] public string DiscipleReply;
        [JsonProperty("needs_confirmation")] public bool NeedsConfirmation;
        [JsonProperty("conflict_with")] public string ConflictWith;
    }
}
