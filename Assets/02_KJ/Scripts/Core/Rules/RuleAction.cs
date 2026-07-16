using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeachAndFight.Core.Rules
{
    public class RuleAction
    {
        [JsonProperty("action")] public string Action;
        [JsonProperty("params")] public Dictionary<string, object> Params;
    }
}
