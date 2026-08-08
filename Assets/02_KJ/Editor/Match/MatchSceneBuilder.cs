using System.Collections.Generic;
using System.IO;
using TeachAndFight.Match.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TeachAndFight.Match.EditorTools
{
    // #13 Match.unity를 코드로 생성하는 에디터 빌더 (TrainingSceneBuilder/LockerRoomSceneBuilder와 동일 방식).
    public static class MatchSceneBuilder
    {
        private const string ScenesDir = "Assets/01_Scenes";
        private const string ScenePath = ScenesDir + "/Match.unity";
        private const string ArenaBackgroundPath = "Assets/_Shared/Art/Backgrounds/TournamentArena_16x9.png";

        private static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static readonly Color Panel = new Color(0.16f, 0.17f, 0.20f, 0.9f);

        [MenuItem("TeachAndFight/Build/Create or Rebuild Match Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Main Camera: 아레나(월드 X -6~6, config.Arena.Width=12) 전체가 보이도록 ---
            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
            cam.orthographic = true;
            cam.orthographicSize = 4.5f;
            cam.transform.position = new Vector3(0, 1f, -10f);

            BuildArenaBackground();

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var canvasT = canvasGo.GetComponent<RectTransform>();

            // --- 상단: 좌우 HUD(HP/스태미나) + 중앙 타이머 ---
            var selfHudGo = BuildHud("SelfHud", canvasT, new Vector2(0, 1), new Vector2(-700, -110));
            var enemyHudGo = BuildHud("EnemyHud", canvasT, new Vector2(1, 1), new Vector2(700, -110));

            var timerText = NewText("TimerText", canvasT, "60", 48, TextAnchor.MiddleCenter);
            Anchor(timerText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(160, 70));

            // --- 중앙: 아레나 수평 트랙(제자 좌 / 상대 우) ---
            var trackGo = NewPanel("ArenaTrack", canvasT, new Color(1, 1, 1, 0.08f));
            Anchor(trackGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 6));
            var trackRt = trackGo.GetComponent<RectTransform>();

            var selfMarker = NewMarker("SelfMarker", trackGo.transform, Color.cyan);
            var enemyMarker = NewMarker("EnemyMarker", trackGo.transform, Color.red);

            // --- 하단: 배속/일시정지 버튼 ---
            var pauseBtn = NewButton("PauseButton", canvasT, "⏸");
            Anchor(pauseBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-180, 60), new Vector2(80, 56));
            var halfBtn = NewButton("SpeedHalfButton", canvasT, "0.5x");
            Anchor(halfBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-80, 60), new Vector2(80, 56));
            var oneBtn = NewButton("SpeedOneButton", canvasT, "1x");
            Anchor(oneBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(20, 60), new Vector2(80, 56));
            var twoBtn = NewButton("SpeedTwoButton", canvasT, "2x");
            Anchor(twoBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(120, 60), new Vector2(80, 56));

            // --- 결과 배너(숨김 시작) ---
            var bannerRoot = NewPanel("ResultBanner", canvasT, new Color(0, 0, 0, 0.7f));
            Anchor(bannerRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 200));
            var bannerText = NewText("Text", bannerRoot.transform, "", 56, TextAnchor.MiddleCenter);
            Fill(bannerText.rectTransform, 10);
            bannerRoot.SetActive(false);

            // --- 컨트롤러 + 배선 ---
            var controllerGo = new GameObject("MatchScreen");
            var controller = controllerGo.AddComponent<MatchScreenController>();
            SetField(controller, "selfHud", selfHudGo.GetComponent<FighterHudView>());
            SetField(controller, "enemyHud", enemyHudGo.GetComponent<FighterHudView>());
            SetField(controller, "timerText", timerText);
            SetField(controller, "arenaTrack", trackRt);
            SetField(controller, "selfMarker", selfMarker);
            SetField(controller, "enemyMarker", enemyMarker);
            SetField(controller, "pauseButton", pauseBtn);
            SetField(controller, "speedHalfButton", halfBtn);
            SetField(controller, "speedOneButton", oneBtn);
            SetField(controller, "speedTwoButton", twoBtn);
            SetField(controller, "resultBannerRoot", bannerRoot);
            SetField(controller, "resultBannerText", bannerText);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MatchSceneBuilder] 완료 → {ScenePath}. Play 시 세션 규칙셋(없으면 백지)으로 자동 대전 시작.");
        }

        // ---------- 조립 헬퍼 ----------

        private static GameObject BuildHud(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos)
        {
            var root = NewPanel(name, parent, Panel);
            Anchor(root.GetComponent<RectTransform>(), anchor, anchor, anchoredPos, new Vector2(560, 130));

            var nameText = NewText("Name", root.transform, name, 24, TextAnchor.UpperLeft);
            Anchor(nameText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -18), new Vector2(-20, 30));

            var hpSlider = NewSlider("HpSlider", root.transform, new Color(0.85f, 0.25f, 0.25f));
            Anchor(hpSlider.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(-20, 22));

            var staminaSlider = NewSlider("StaminaSlider", root.transform, new Color(0.25f, 0.65f, 0.35f));
            Anchor(staminaSlider.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -78), new Vector2(-20, 16));

            var ruleLabel = NewText("RuleLabel", root.transform, "", 22, TextAnchor.MiddleCenter);
            ruleLabel.color = new Color(1f, 0.85f, 0.3f);
            Anchor(ruleLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -108), new Vector2(-20, 30));

            var hud = root.AddComponent<FighterHudView>();
            SetField(hud, "nameText", nameText);
            SetField(hud, "hpSlider", hpSlider);
            SetField(hud, "staminaSlider", staminaSlider);
            SetField(hud, "ruleLabelText", ruleLabel);

            return root;
        }

        private static RectTransform NewMarker(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(20, 20);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        // ---------- UI 헬퍼(TrainingSceneBuilder와 동일 패턴) ----------

        private static GameObject NewPanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text NewText(string name, Transform parent, string content, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = UIFont;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = content;
            return t;
        }

        private static Button NewButton(string name, Transform parent, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.30f, 0.42f, 0.78f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var t = NewText("Text", go.transform, label, 22, TextAnchor.MiddleCenter);
            Fill(t.rectTransform, 4);
            return btn;
        }

        private static Slider NewSlider(string name, Transform parent, Color fillColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var slider = go.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            bgGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            Fill(bgGo.GetComponent<RectTransform>(), 0);

            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(go.transform, false);
            Fill(fillAreaGo.GetComponent<RectTransform>(), 2);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = fillColor;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            slider.fillRect = fillRt;
            slider.targetGraphic = fillImg;
            slider.value = 1f;
            return slider;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static void Fill(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        private static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[MatchSceneBuilder] 필드 '{field}' 없음 on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildArenaBackground()
        {
            EnsureSpriteImport(ArenaBackgroundPath);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArenaBackgroundPath);
            if (sprite == null)
            {
                Debug.LogError($"[MatchSceneBuilder] Arena background sprite not found: {ArenaBackgroundPath}");
                return;
            }

            var go = new GameObject("TournamentArenaBackground", typeof(SpriteRenderer));
            go.transform.position = new Vector3(0f, 1f, 6f);

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -100;

            var worldHeight = sprite.bounds.size.y;
            if (worldHeight > 0f)
            {
                var scale = 9f / worldHeight;
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
