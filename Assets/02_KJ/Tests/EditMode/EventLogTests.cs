using System.Collections.Generic;
using NUnit.Framework;
using TeachAndFight.Combat;

namespace TeachAndFight.Core.Tests
{
    public class EventLogTests
    {
        [Test]
        public void Record_RuleFired_IncludesRuleId()
        {
            var log = new EventLog();

            log.Record(12.3f, "self", "rule_fired", "rule_03");

            Assert.AreEqual(1, log.Events.Count);
            var ev = log.Events[0];
            Assert.AreEqual(12.3f, ev.Time);
            Assert.AreEqual("self", ev.Actor);
            Assert.AreEqual("rule_fired", ev.Type);
            Assert.AreEqual("rule_03", ev.RuleId);
        }

        [Test]
        public void Record_MultipleEvents_KeepsMatchOrder()
        {
            var log = new EventLog();

            log.Record(0.1f, "self", "rule_fired", "rule_01");
            log.Record(1.5f, "enemy", "hit");
            log.Record(60f, "self", "match_end");

            Assert.AreEqual(3, log.Events.Count);
            Assert.AreEqual("rule_fired", log.Events[0].Type);
            Assert.AreEqual("hit", log.Events[1].Type);
            Assert.AreEqual("match_end", log.Events[2].Type);
        }

        [Test]
        public void Record_DetailDefaultsToEmptyDictionary_WhenOmitted()
        {
            var log = new EventLog();

            log.Record(0f, "self", "hit");

            Assert.IsNotNull(log.Events[0].Detail);
            Assert.AreEqual(0, log.Events[0].Detail.Count);
        }

        [Test]
        public void ToJson_ContainsRuleFiredAndRuleId()
        {
            var log = new EventLog();
            log.Record(12.3f, "self", "rule_fired", "rule_03", new Dictionary<string, object> { { "action", "heavy_attack" } });

            var json = log.ToJson();

            StringAssert.Contains("\"rule_fired\"", json);
            StringAssert.Contains("\"rule_03\"", json);
        }

        [Test]
        public void Clear_RemovesAllEvents()
        {
            var log = new EventLog();
            log.Record(0f, "self", "rule_fired", "rule_01");

            log.Clear();

            Assert.AreEqual(0, log.Events.Count);
        }
    }
}
