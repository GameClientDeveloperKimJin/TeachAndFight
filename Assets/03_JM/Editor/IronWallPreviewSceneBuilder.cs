using System.Collections.Generic;
using System.IO;
using TeachAndFight.ArtPreview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TeachAndFight.Training.EditorTools
{
    // #22 아트 검증: 캐릭터 애니메이션 8종을 게임 씬에서 재생 확인하는 프리뷰 씬 생성.
    // 6캐릭터가 폴더/컨트롤러/스프라이트 명명 규칙이 동일하므로 하나의 빌더로 처리(철벽·그림자·사범 …).
    // (파일명은 IronWall… 이지만 실제로는 범용 빌더 — 캐릭터는 메뉴/파라미터로 구분.)
    public static class CharacterAnimationPreviewBuilder
    {
        // 클립 접미사(전 캐릭터 공통). 상태이름 = "{캐릭터}_{접미사}".
        private static readonly string[] Suffixes =
        {
            "Idle", "Move", "Dash", "Attack_Light", "Attack_Heavy", "Attack_Ultimate", "HitStun", "Down",
        };

        [MenuItem("TeachAndFight/Build/Create IronWall Animation Preview Scene")]
        public static void BuildIronWall() => BuildFor("IronWall");

        [MenuItem("TeachAndFight/Build/Create Shadow Animation Preview Scene")]
        public static void BuildShadow() => BuildFor("Shadow");

        [MenuItem("TeachAndFight/Build/Create Master Animation Preview Scene")]
        public static void BuildMaster() => BuildFor("Master");

        // 명명 규칙:
        //   폴더      Assets/_Shared/Art/Characters/{Char}
        //   컨트롤러  {Char}Preview.controller
        //   상태이름  {Char}_Idle …
        //   Idle프레임 Idle/{char소문자}_idle_01.png
        //   씬        _Preview/{Char}Preview.unity
        private static void BuildFor(string character)
        {
            string baseDir = $"Assets/_Shared/Art/Characters/{character}";
            string controllerPath = $"{baseDir}/{character}Preview.controller";
            string idleFramePath = $"{baseDir}/Idle/{character.ToLowerInvariant()}_idle_01.png";
            string previewDir = $"{baseDir}/_Preview";
            string scenePath = $"{previewDir}/{character}Preview.unity";

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogError($"[AnimPreview] 컨트롤러 못 찾음: {controllerPath}");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(previewDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 카메라 (스프라이트 PPU 128, 소스 ~1024px → 오쏘 사이즈 6이면 넉넉히 들어옴)
            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.16f, 0.20f);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // 캐릭터: SpriteRenderer + Animator(프리뷰 컨트롤러) + 프리뷰 스크립트
            var go = new GameObject(character, typeof(SpriteRenderer), typeof(Animator), typeof(AnimationPreviewController));
            var sr = go.GetComponent<SpriteRenderer>();
            var initSprite = AssetDatabase.LoadAssetAtPath<Sprite>(idleFramePath);
            if (initSprite != null) sr.sprite = initSprite; // Play 전에도 씬에 보이도록

            var animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            // 프리뷰 스크립트 배선 (private [SerializeField]는 SerializedObject로)
            var preview = go.GetComponent<AnimationPreviewController>();
            var so = new SerializedObject(preview);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            so.FindProperty("interval").floatValue = 1.5f;
            so.FindProperty("autoPlay").boolValue = true;
            var namesProp = so.FindProperty("stateNames");
            namesProp.arraySize = Suffixes.Length;
            for (int i = 0; i < Suffixes.Length; i++)
                namesProp.GetArrayElementAtIndex(i).stringValue = $"{character}_{Suffixes[i]}";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AnimPreview] {character} 완료 → {scenePath}. Play: 8클립 자동 순환 + 좌상단 버튼(이전/다음/자동/좌우반전).");
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
