using NUnit.Framework;
using UnityEngine;
using TeachAndFight.Combat;
using TeachAndFight.Combat.Demo;

namespace TeachAndFight.Core.Tests
{
    public class MatchDemoRunnerTests
    {
        [Test]
        public void HardcodedMatch_EndsNaturallyWithin60Seconds()
        {
            var go = new GameObject("MatchDemoRunner");
            var runner = go.AddComponent<MatchDemoRunner>();
            runner.EnsureInitialized();

            const float dt = 0.1f;
            const float hardLimit = 60f + 0.5f; // #6 완료 기준: 60초 내 종료
            float simulated = 0f;

            while (!runner.Outcome.HasValue && simulated < hardLimit)
            {
                runner.Step(dt);
                simulated += dt;
            }

            Assert.IsTrue(runner.Outcome.HasValue, "60초 내에 경기가 종료되어야 함");
            Assert.LessOrEqual(simulated, hardLimit);

            Object.DestroyImmediate(runner.FighterA.gameObject);
            Object.DestroyImmediate(runner.FighterB.gameObject);
            Object.DestroyImmediate(go);
        }
    }
}
