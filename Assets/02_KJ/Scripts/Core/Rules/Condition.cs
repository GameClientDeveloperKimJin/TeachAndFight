using Newtonsoft.Json;

namespace TeachAndFight.Core.Rules
{
    public class Condition
    {
        [JsonProperty("fact")] public string Fact;
        [JsonProperty("op")] public string Op;
        [JsonProperty("value")] public object Value;
    }
}
