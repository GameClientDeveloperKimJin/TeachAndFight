using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TeachAndFight.Art.EditorTools
{
    public static class IronWallAnimationAssetBuilder
    {
        private const string Root = "Assets/_Shared/Art/Characters/IronWall";
        private const float FrameRate = 8f;
        private const int PixelsPerUnit = 128;

        private static readonly ClipSpec[] Clips =
        {
            new("Idle", "idle", "IronWall_Idle", true),
            new("Move", "move", "IronWall_Move", true),
            new("Dash", "dash", "IronWall_Dash", false),
            new("AttackLight", "attack_light", "IronWall_Attack_Light", false),
            new("AttackHeavy", "attack_heavy", "IronWall_Attack_Heavy", false),
            new("AttackUltimate", "attack_ultimate", "IronWall_Attack_Ultimate", false),
            new("HitStun", "hitstun", "IronWall_HitStun", false),
            new("Down", "down", "IronWall_Down", false),
        };

        [MenuItem("TeachAndFight/Art/Build IronWall Animations")]
        public static void BuildIronWallAnimations()
        {
            Directory.CreateDirectory(ToSystemPath(Root));

            foreach (var clip in Clips)
            {
                BuildClip(clip);
            }

            BuildPreviewController();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("IronWall animation clips and preview controller generated.");
        }

        [MenuItem("TeachAndFight/Art/Verify IronWall Animations")]
        public static void BuildAndVerifyIronWallAnimations()
        {
            BuildIronWallAnimations();
            VerifyClips();
            Debug.Log("IronWall animation verification passed.");
        }

        private static void BuildClip(ClipSpec spec)
        {
            var folder = $"{Root}/{spec.Folder}";
            var sprites = LoadSprites(folder, spec.FilePrefix);
            if (sprites.Count == 0)
            {
                throw new InvalidOperationException($"No sprites found for {spec.Name} in {folder}.");
            }

            var animationClip = new AnimationClip
            {
                frameRate = FrameRate,
                name = spec.Name,
            };

            var keyframes = sprites
                .Select((sprite, index) => new ObjectReferenceKeyframe
                {
                    time = index / FrameRate,
                    value = sprite,
                })
                .ToArray();

            var binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite",
            };

            AnimationUtility.SetObjectReferenceCurve(animationClip, binding, keyframes);

            var settings = AnimationUtility.GetAnimationClipSettings(animationClip);
            settings.loopTime = spec.Loop;
            AnimationUtility.SetAnimationClipSettings(animationClip, settings);

            var outPath = $"{folder}/{spec.Name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(animationClip, outPath);
            }
            else
            {
                EditorUtility.CopySerialized(animationClip, existing);
                EditorUtility.SetDirty(existing);
            }
        }

        private static List<Sprite> LoadSprites(string assetFolder, string filePrefix)
        {
            var systemFolder = ToSystemPath(assetFolder);
            if (!Directory.Exists(systemFolder))
            {
                throw new DirectoryNotFoundException(systemFolder);
            }

            var pngPaths = Directory.GetFiles(systemFolder, "*.png", SearchOption.TopDirectoryOnly)
                .Select(ToAssetPath)
                .Where(path => IsFramePath(path, filePrefix))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var path in pngPaths)
            {
                ConfigureSpriteImporter(path);
            }

            AssetDatabase.Refresh();

            return pngPaths
                .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path))
                .Where(sprite => sprite != null)
                .ToList();
        }

        private static bool IsFramePath(string assetPath, string filePrefix)
        {
            var stem = Path.GetFileNameWithoutExtension(assetPath);
            var expectedPrefix = $"ironwall_{filePrefix}_";
            if (!stem.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var frameNumber = stem[expectedPrefix.Length..];
            return frameNumber.Length == 2 && frameNumber.All(char.IsDigit);
        }

        private static void ConfigureSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.SaveAndReimport();
        }

        private static void BuildPreviewController()
        {
            var controllerPath = $"{Root}/IronWallPreview.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            var stateMachine = controller.layers[0].stateMachine;
            foreach (var state in stateMachine.states)
            {
                stateMachine.RemoveState(state.state);
            }

            for (var i = 0; i < Clips.Length; i++)
            {
                var spec = Clips[i];
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Root}/{spec.Folder}/{spec.Name}.anim");
                var state = stateMachine.AddState(spec.Name, new Vector3(250, i * 70, 0));
                state.motion = clip;

                if (i == 0)
                {
                    stateMachine.defaultState = state;
                }
            }
        }

        private static void VerifyClips()
        {
            var go = new GameObject("IronWallAnimationVerification");
            var renderer = go.AddComponent<SpriteRenderer>();

            try
            {
                foreach (var spec in Clips)
                {
                    var clipPath = $"{Root}/{spec.Folder}/{spec.Name}.anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null)
                    {
                        throw new InvalidOperationException($"Missing animation clip: {clipPath}");
                    }

                    var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
                    var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (frames == null || frames.Length < 4)
                    {
                        throw new InvalidOperationException($"{spec.Name} should have at least 4 sprite frames.");
                    }

                    foreach (var frame in frames)
                    {
                        clip.SampleAnimation(go, frame.time);
                        if (renderer.sprite == null)
                        {
                            throw new InvalidOperationException($"{spec.Name} produced a null sprite at {frame.time:0.###}s.");
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static string ToSystemPath(string assetPath)
        {
            var relative = assetPath.Replace("Assets/", string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, relative);
        }

        private static string ToAssetPath(string systemPath)
        {
            var normalized = systemPath.Replace('\\', '/');
            var assetsIndex = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            return assetsIndex >= 0 ? normalized[(assetsIndex + 1)..] : normalized;
        }

        private readonly struct ClipSpec
        {
            public ClipSpec(string folder, string filePrefix, string name, bool loop)
            {
                Folder = folder;
                FilePrefix = filePrefix;
                Name = name;
                Loop = loop;
            }

            public string Folder { get; }
            public string FilePrefix { get; }
            public string Name { get; }
            public bool Loop { get; }
        }
    }
}
