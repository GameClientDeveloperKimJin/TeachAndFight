using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeachAndFight.Combat
{
    // 경기 중 이벤트 기록 포맷 (02장). actor는 이 EventLog 소유 파이터 기준 self|enemy.
    public class MatchEvent
    {
        [JsonProperty("t")] public float Time;
        [JsonProperty("actor")] public string Actor;
        [JsonProperty("type")] public string Type;
        [JsonProperty("rule_id")] public string RuleId;
        [JsonProperty("detail")] public Dictionary<string, object> Detail;
    }

    // #8 W2 EventLog - 락커룸 리플레이/LLM 회고의 데이터 소스. 파이터별로 1개씩 소유.
    public class EventLog
    {
        private readonly List<MatchEvent> events = new List<MatchEvent>();

        public IReadOnlyList<MatchEvent> Events => events;

        public void Record(float t, string actor, string type, string ruleId = null, Dictionary<string, object> detail = null)
        {
            events.Add(new MatchEvent
            {
                Time = t,
                Actor = actor,
                Type = type,
                RuleId = ruleId,
                Detail = detail ?? new Dictionary<string, object>(),
            });
        }

        public void Clear() => events.Clear();

        public string ToJson() => JsonConvert.SerializeObject(events);
    }
}
