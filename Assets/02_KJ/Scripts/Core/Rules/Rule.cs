using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeachAndFight.Core.Rules
{
    public class Rule
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("label")] public string Label;
        [JsonProperty("when")] public List<Condition> When = new List<Condition>();
        [JsonProperty("do")] public RuleAction Do;
        [JsonProperty("priority")] public int Priority;
        [JsonProperty("source_utterance")] public string SourceUtterance;
    }
}
