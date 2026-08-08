using System.Collections.Generic;
using System.IO;
using TeachAndFight.Combat;
using TeachAndFight.Combat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TeachAndFight.Training.EditorTools
{
    // #22 검증/평가: FighterAnimatorBridge가 실제 전투 상태에 따라 애니메이션을 재생하는지 확인 +
    // Play 중 A/B 파이터의 캐릭터를 6종 중 실시간 교체해 비교(BridgeBattleSelector).
    // MatchDemoRunner(하드코딩 AI vs AI 오토배틀)에 스프라이트+Animator+브릿지 파이터 2명을 넣는다.
    // (KJ의 Match.unity 씬은 건드리지 않고 별도 검증 씬 — 06장 씬 소유권 준수.)
    public static class BridgeBattleSceneBuilder
    {
        private const string CharactersDir = "Assets/_Shared/Art/Characters";
        private const string ScenePath = "Assets/_Shared/Art/_Preview/BridgeBattlePreview.unity";
        private const string ArenaBackgroundPath = "Assets/_Shared/Art/Backgrounds/TournamentArena_16x9.png";

        [MenuItem("TeachAndFight/Build/Create Bridge Battle Preview (character switch)")]
        public static void Build()
        {
            var chars = DiscoverCharacters();
            if (chars.Count == 0)
            {
                Debug.LogError($"[BridgeBattle] {CharactersDir}에서 캐릭터를 못 찾음 ({{Char}}Preview.controller 필요)");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.14f, 0.18f);
            cam.transform.position = new Vector3(0f, 1.2f, -10f);
            BuildArenaBackground();

            // 기본 파이터: 목록 첫 두 캐릭터(Play에서 셀렉터로 교체 가능).
            string defaultA = chars[0];
            string defaultB = chars.Count > 1 ? chars[1] : chars[0];
            var fighterA = BuildFighter(defaultA);
            var fighterB = BuildFighter(defaultB);

            // MatchDemoRunner에 두 파이터 주입.
            var runner = new GameObject("MatchDemoRunner").AddComponent<MatchDemoRunner>();
            var rso = new SerializedObject(runner);
            rso.FindProperty("fighterA").objectReferenceValue = fighterA.GetComponent<FighterController>();
            rso.FindProperty("fighterB").objectReferenceValue = fighterB.GetComponent<FighterController>();
            rso.ApplyModifiedPropertiesWithoutUndo();

            // 캐릭터 셀렉터: 옵션 6종 + A/B 브릿지 배선.
            var selector = new GameObject("BattleSelector").AddComponent<BridgeBattleSelector>();
            var sso = new SerializedObject(selector);
            sso.FindProperty("bridgeA").objectReferenceValue = fighterA.GetComponent<FighterAnimatorBridge>();
            sso.FindProperty("bridgeB").objectReferenceValue = fighterB.GetComponent<FighterAnimatorBridge>();
            var opts = sso.FindProperty("options");
            opts.arraySize = chars.Count;
            for (int i = 0; i < chars.Count; i++)
            {
                var el = opts.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("name").stringValue = chars[i];
                el.FindPropertyRelative("controller").objectReferenceValue = LoadController(chars[i]);
                el.FindPropertyRelative("idleSprite").objectReferenceValue = LoadIdle(chars[i]);
            }
            sso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BridgeBattle] 완료 → {ScenePath} (기본 {defaultA} vs {defaultB}). " +
                      $"Play: 오토배틀 + 우측 상단 버튼으로 A/B 캐릭터를 {chars.Count}종 실시간 교체.");
        }

        private static List<string> DiscoverCharacters()
        {
            var result = new List<string>();
            foreach (var sub in AssetDatabase.GetSubFolders(CharactersDir))
            {
                var name = Path.GetFileName(sub);
                if (name.StartsWith("_"))
                    continue; // _Preview 등 제외
                if (LoadController(name) != null)
                    result.Add(name);
            }
            result.Sort();
            return result;
        }

        private static GameObject BuildFighter(string character)
        {
            var go = new GameObject(character, typeof(SpriteRenderer), typeof(Animator),
                typeof(FighterController), typeof(FighterAnimatorBridge));
            go.transform.localScale = new Vector3(0.35f, 0.35f, 1f); // 소스 스프라이트가 커서 축소(조정 가능)

            var sr = go.GetComponent<SpriteRenderer>();
            var idle = LoadIdle(character);
            if (idle != null) sr.sprite = idle;

            go.GetComponent<Animator>().runtimeAnimatorController = LoadController(character);

            var bridge = go.GetComponent<FighterAnimatorBridge>();
            var so = new SerializedObject(bridge);
            so.FindProperty("fighter").objectReferenceValue = go.GetComponent<FighterController>();
            so.FindProperty("animator").objectReferenceValue = go.GetComponent<Animator>();
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            so.FindProperty("characterPrefix").stringValue = character;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        private static RuntimeAnimatorController LoadController(string character)
            => AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                $"{CharactersDir}/{character}/{character}Preview.controller");

        private static Sprite LoadIdle(string character)
            => AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{CharactersDir}/{character}/Idle/{character.ToLowerInvariant()}_idle_01.png");

        private static void BuildArenaBackground()
        {
            EnsureSpriteImport(ArenaBackgroundPath);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArenaBackgroundPath);
            if (sprite == null)
            {
                Debug.LogError($"[BridgeBattle] Arena background sprite not found: {ArenaBackgroundPath}");
                return;
            }

            var go = new GameObject("TournamentArenaBackground", typeof(SpriteRenderer));
            go.transform.position = new Vector3(0f, 1.2f, 6f);

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -100;

            var worldHeight = sprite.bounds.size.y;
            if (worldHeight > 0f)
            {
                var scale = 8f / worldHeight;
                go.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private static void EnsureSpriteImport(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            var changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
            {
                importer.spritePixelsPerUnit = 100f;
                changed = true;
            }
            if (changed)
                importer.SaveAndReimport();
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;
            var parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            var leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath))
                return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
