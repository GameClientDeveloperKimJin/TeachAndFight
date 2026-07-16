using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeachAndFight.Core.Rules
{
    public class RuleSet
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("fighter_name")] public string FighterName;
        [JsonProperty("max_slots")] public int MaxSlots;
        [JsonProperty("rules")] public List<Rule> Rules = new List<Rule>();
    }
}
