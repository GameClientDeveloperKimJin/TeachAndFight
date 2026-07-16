using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace TeachAndFight.Combat
{
    public static class CombatConfigLoader
    {
        private const string ResourcePath = "Data/Config/combat_config";

        // 매 호출마다 Resources에서 새로 읽는다 - 인스펙터/파일에서 config 값 수정 시 즉시 반영 확인용
        public static CombatConfig Load()
        {
            var textAsset = Resources.Load<TextAsset>(ResourcePath);
            if (textAsset == null)
                throw new FileNotFoundException($"combat_config.json not found at Resources/{ResourcePath}.json");

            return JsonConvert.DeserializeObject<CombatConfig>(textAsset.text);
        }
    }
}
