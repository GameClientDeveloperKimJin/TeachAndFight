using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using TeachAndFight.Core.Rules;

namespace TeachAndFight.Combat
{
    // #12 산출물. opponent_0N.json(1~5) 로드 - GameSession.OpponentIndex가 그대로 파일 번호.
    public static class OpponentRuleSetLoader
    {
        public const int MinIndex = 1;
        public const int MaxIndex = 5;

        public static RuleSet Load(int opponentIndex)
        {
            string resourcePath = $"Data/Rules/opponent_{opponentIndex:00}";
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
                throw new FileNotFoundException($"opponent ruleset not found at Resources/{resourcePath}.json");

            return JsonConvert.DeserializeObject<RuleSet>(textAsset.text);
        }
    }
}
