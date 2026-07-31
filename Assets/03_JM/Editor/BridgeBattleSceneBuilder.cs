using System.Collections.Generic;
using System.IO;
using TeachAndFight.Combat;
using TeachAndFight.Combat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TeachAndFight.Training.EditorTools
{
    // #22 검증: FighterAnimatorBridge가 실제 전투 상태에 따라 애니메이션을 재생하는지 확인하는 씬.
    // MatchDemoRunner(하드코딩 AI vs AI 오토배틀)에 스프라이트+Animator+브릿지를 붙인 파이터 2명을 넣어,
    // 실제 Idle/Move/Dash/Attack/HitStun/Down 상태가 애니메이션으로 나오는지 눈으로 검증.
    // (KJ의 Match.unity 씬은 건드리지 않고 별도 검증 씬으로 — 06장 씬 소유권 준수.)
    public static class BridgeBattleSceneBuilder
    {
        private const string CharactersDir = "Assets/_Shared/Art/Characters";
        private const string ScenePath = "Assets/_Shared/Art/_Preview/BridgeBattlePreview.unity";

        [MenuItem("TeachAndFight/Build/Create Bridge Battle Preview (IronWall vs Shadow)")]
        public static void Build()
        {
            var ctrlA = LoadController("IronWall");
            var ctrlB = LoadController("Shadow");
            if (ctrlA == null || ctrlB == null)
                return;

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

            var fighterA = BuildFighter("IronWall", ctrlA);
            var fighterB = BuildFighter("Shadow", ctrlB);

            // MatchDemoRunner에 두 파이터 주입(null이 아니면 placeholder 대신 이걸 사용).
            var runnerGo = new GameObject("MatchDemoRunner");
            var runner = runnerGo.AddComponent<MatchDemoRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("fighterA").objectReferenceValue = fighterA.GetComponent<FighterController>();
            so.FindProperty("fighterB").objectReferenceValue = fighterB.GetComponent<FighterController>();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BridgeBattle] 완료 → {ScenePath}. Play: 철벽 vs 그림자 오토배틀 — 상태 따라 애니 재생 + 좌우반전 확인.");
        }

        private static GameObject BuildFighter(string character, RuntimeAnimatorController controller)
        {
            var go = new GameObject(character, typeof(SpriteRenderer), typeof(Animator),
                typeof(FighterController), typeof(FighterAnimatorBridge));
            go.transform.localScale = new Vector3(0.35f, 0.35f, 1f); // 소스 스프라이트가 커서 축소(조정 가능)

            var sr = go.GetComponent<SpriteRenderer>();
            var idle = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{CharactersDir}/{character}/Idle/{character.ToLowerInvariant()}_idle_01.png");
            if (idle != null) sr.sprite = idle;

            go.GetComponent<Animator>().runtimeAnimatorController = controller;

            // 브릿지 배선: 컴포넌트 참조는 Awake에서 GetComponent로도 잡히지만 명시적으로 세팅 + prefix.
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
        {
            var path = $"{CharactersDir}/{character}/{character}Preview.controller";
            var c = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            if (c == null)
                Debug.LogError($"[BridgeBattle] 컨트롤러 못 찾음: {path}");
            return c;
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
