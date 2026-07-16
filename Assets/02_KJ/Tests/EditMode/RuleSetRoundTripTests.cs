using NUnit.Framework;
using Newtonsoft.Json;
using TeachAndFight.Core.Rules;

namespace TeachAndFight.Core.Tests
{
    public class RuleSetRoundTripTests
    {
        // DEV_SPEC.md 01장 RuleSet JSON 스키마 예시
        private const string SampleJson = @"{
          ""version"": 1,
          ""fighter_name"": ""제자"",
          ""max_slots"": 5,
          ""rules"": [
            {
              ""id"": ""rule_01"",
              ""label"": ""궁 회피"",
              ""when"": [
                { ""fact"": ""enemy_action"", ""op"": ""=="", ""value"": ""ultimate_startup"" },
                { ""fact"": ""self_stamina_pct"", ""op"": "">="", ""value"": 20 }
              ],
              ""do"": { ""action"": ""dash"", ""params"": { ""direction"": ""away"" } },
              ""priority"": 8,
              ""source_utterance"": ""상대가 궁 쓰면 일단 대시로 빠져""
            }
          ]
        }";

        [Test]
        public void Load_SampleRuleSet_PassesValidator()
        {
            var ruleSet = JsonConvert.DeserializeObject<RuleSet>(SampleJson);

            var errors = RuleValidator.ValidateRuleSet(ruleSet);

            CollectionAssert.IsEmpty(errors);
        }

        [Test]
        public void RoundTrip_DeserializeSerializeDeserialize_IsLossless()
        {
            var first = JsonConvert.DeserializeObject<RuleSet>(SampleJson);
            var reserialized = JsonConvert.SerializeObject(first);
            var second = JsonConvert.DeserializeObject<RuleSet>(reserialized);

            Assert.AreEqual(first.Version, second.Version);
            Assert.AreEqual(first.FighterName, second.FighterName);
            Assert.AreEqual(first.MaxSlots, second.MaxSlots);
            Assert.AreEqual(first.Rules.Count, second.Rules.Count);

            var r1 = first.Rules[0];
            var r2 = second.Rules[0];
            Assert.AreEqual(r1.Id, r2.Id);
            Assert.AreEqual(r1.Label, r2.Label);
            Assert.AreEqual(r1.Priority, r2.Priority);
            Assert.AreEqual(r1.SourceUtterance, r2.SourceUtterance);
            Assert.AreEqual(r1.When.Count, r2.When.Count);
            Assert.AreEqual(r1.When[0].Fact, r2.When[0].Fact);
            Assert.AreEqual(r1.When[0].Op, r2.When[0].Op);
            Assert.AreEqual(r1.When[0].Value.ToString(), r2.When[0].Value.ToString());
            Assert.AreEqual(r1.Do.Action, r2.Do.Action);
        }
    }
}
