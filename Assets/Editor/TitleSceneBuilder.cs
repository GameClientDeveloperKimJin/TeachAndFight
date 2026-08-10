using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TeachAndFight.Title.EditorTools
{
    // TitleScene.unity에 "아무 키나 → Match" + "[게임 방법] 패널" UI를 조립하는 에디터 빌더.
    // 기존 씬을 열어 없는 오브젝트만 이름 기준으로 추가하므로 여러 번 실행해도 안전하다.
    public static class TitleSceneBuilder
    {
        private const string ScenesDir = "Assets/01_Scenes";
        private const string ScenePath = ScenesDir + "/TitleScene.unity";
        private const string TitleImagePath = "Assets/_Shared/Art/Backgrounds/title.png";
        private const string HowToImagePath = "Assets/_Shared/Art/Backgrounds/bg.png";
        private const string KoreanFontPath = "Assets/Fonts/NanumGothic.ttf";

        private static Font UIFont =>
            AssetDatabase.LoadAssetAtPath<Font>(KoreanFontPath)
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color Board = new Color(0.14f, 0.15f, 0.18f, 0.97f);
        private static readonly Color Accent = new Color(0.30f, 0.42f, 0.78f);

        private const string HowToBody =
            "1. 훈련 (Training)\n" +
            "   제자에게 \"말\"로 전술을 가르친다.\n" +
            "   예) \"체력이 절반 밑이면 방어해\", \"거리가 멀면 접근해\"\n" +
            "\n" +
            "2. 경기 (Match)\n" +
            "   가르친 규칙대로 제자가 스스로 싸운다.\n" +
            "   경기 중 직접 조작은 불가. 배속/일시정지만 가능하다.\n" +
            "\n" +
            "3. 라커룸 (Locker Room)\n" +
            "   경기를 복기하고, 다음 훈련에서 규칙을 고쳐 다시 도전한다.\n" +
            "\n" +
            "목표 : 상대의 패턴을 읽고, 이기는 규칙을 만들어라.";

        [MenuItem("TeachAndFight/Build/Create or Update Title Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(ScenesDir);

            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureCamera(scene);
            EnsureEventSystem();

            var canvasT = EnsureCanvas();
            EnsureBackground(canvasT);

            var pressAnyKey = EnsureText(canvasT, "PressAnyKeyText", "아무 키나 누르세요", 40, TextAnchor.MiddleCenter);
            Anchor(pressAnyKey.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 180), new Vector2(900, 70));

            var howToButton = EnsureButton(canvasT, "HowToButton", "게임 방법");
            Anchor(howToButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(260, 66));

            var panel = EnsureHowToPanel(canvasT, out var closeButton);

            // --- 컨트롤러 + 배선 ---
            var controllerGo = EnsureRootObject(scene, "TitleController");
            var controller = controllerGo.GetComponent<TitleController>();
            if (controller == null)
                controller = controllerGo.AddComponent<TitleController>();

            SetField(controller, "howToPanel", panel);
            SetField(controller, "howToButton", howToButton);
            SetField(controller, "howToCloseButton", closeButton);
            SetField(controller, "pressAnyKeyLabel", pressAnyKey);

            panel.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath, first: true);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TitleSceneBuilder] 완료 → {ScenePath}. 아무 키/클릭 → Match, [게임 방법] → 안내 패널.");
        }

        // ---------- 조립 ----------

        private static void EnsureCamera(Scene scene)
        {
            var camGo = FindRootObject(scene, "Main Camera");
            var cam = camGo != null ? camGo.GetComponent<Camera>() : null;
            if (cam == null)
                cam = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.transform.position = new Vector3(0, 0, -10f);
            }

            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            cam.orthographic = true;
        }

        private static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (es == null)
                es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();

            // 시작 시 아무 것도 선택되어 있지 않아야 Enter/Space가 버튼을 눌러버리지 않는다.
            es.firstSelectedGameObject = null;
        }

        private static RectTransform EnsureCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = go.GetComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (canvas.GetComponent<CanvasScaler>() == null)
                canvas.gameObject.AddComponent<CanvasScaler>();
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas.GetComponent<RectTransform>();
        }

        // 손으로 만들어 둔 배경 Image("Image" 또는 "TitleBackground")를 그대로 쓰고, 없으면 title.png로 만든다.
        private static void EnsureBackground(RectTransform canvasT)
        {
            var existing = canvasT.Find("TitleBackground");
            if (existing == null)
                existing = canvasT.Find("Image");
            Image img;
            if (existing != null)
            {
                img = GetOrAdd<Image>(existing.gameObject);
            }
            else
            {
                var go = new GameObject("TitleBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(canvasT, false);
                img = go.GetComponent<Image>();
            }

            if (img.sprite == null)
                img.sprite = LoadSprite(TitleImagePath);

            img.raycastTarget = false;
            img.preserveAspect = true;
            Fill(img.rectTransform, 0);
            img.rectTransform.SetAsFirstSibling();
        }

        private static GameObject EnsureHowToPanel(RectTransform canvasT, out Button closeButton)
        {
            var panelT = canvasT.Find("HowToPanel");
            GameObject panel;
            if (panelT != null)
            {
                panel = panelT.gameObject;
                panel.SetActive(true); // 조립 동안만 켜 둔다. 마지막에 다시 끈다.
            }
            else
            {
                panel = NewPanel("HowToPanel", canvasT, Dim);
            }

            var panelRt = panel.GetComponent<RectTransform>();
            Fill(panelRt, 0);
            var dimImg = GetOrAdd<Image>(panel);
            dimImg.color = Dim;
            dimImg.raycastTarget = true; // 뒤쪽 버튼 클릭 차단

            var boardT = panelRt.Find("Board");
            GameObject board = boardT != null ? boardT.gameObject : NewPanel("Board", panelRt, Board);
            var boardImg = GetOrAdd<Image>(board);
            if (boardImg.sprite == null)
            {
                var sprite = LoadSprite(HowToImagePath);
                if (sprite != null)
                {
                    boardImg.sprite = sprite;
                    boardImg.color = new Color(1f, 1f, 1f, 0.97f);
                }
            }
            Anchor(board.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 800));

            var boardRt = board.GetComponent<RectTransform>();

            // 이미지 위 가독성을 위한 반투명 판.
            var scrim = boardRt.Find("Scrim") != null
                ? boardRt.Find("Scrim").gameObject
                : NewPanel("Scrim", boardRt, new Color(0.05f, 0.06f, 0.08f, 0.82f));
            Fill(scrim.GetComponent<RectTransform>(), 0);
            GetOrAdd<Image>(scrim).raycastTarget = false;
            scrim.transform.SetAsFirstSibling();

            var title = EnsureText(boardRt, "TitleText", "게임 방법", 46, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1100, 70));

            var body = EnsureText(boardRt, "BodyText", HowToBody, 27, TextAnchor.UpperLeft);
            body.lineSpacing = 1.25f;
            Anchor(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1060, 500));

            closeButton = EnsureButton(boardRt, "CloseButton", "닫기");
            Anchor(closeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(220, 62));

            var hint = EnsureText(boardRt, "HintText", "(아무 키나 눌러도 닫힙니다)", 20, TextAnchor.MiddleCenter);
            hint.color = new Color(1f, 1f, 1f, 0.6f);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(600, 30));

            panelRt.SetAsLastSibling();
            return panel;
        }

        // ---------- 헬퍼 ----------

        private static Sprite LoadSprite(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[TitleSceneBuilder] 스프라이트 없음: {path}");
                return null;
            }
            EnsureSpriteImport(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureSpriteImport(string assetPath)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer))
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
            if (changed)
                importer.SaveAndReimport();
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static GameObject NewPanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text EnsureText(Transform parent, string name, string content, int size, TextAnchor align)
        {
            var t = parent.Find(name);
            GameObject go = t != null
                ? t.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            if (t == null)
                go.transform.SetParent(parent, false);

            var text = GetOrAdd<Text>(go);
            text.font = UIFont;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static Button EnsureButton(Transform parent, string name, string label)
        {
            var t = parent.Find(name);
            GameObject go = t != null
                ? t.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            if (t == null)
                go.transform.SetParent(parent, false);

            var img = GetOrAdd<Image>(go);
            img.color = Accent;
            img.raycastTarget = true;

            var btn = GetOrAdd<Button>(go);
            btn.targetGraphic = img;

            var text = EnsureText(go.transform, "Text", label, 26, TextAnchor.MiddleCenter);
            Fill(text.rectTransform, 4);
            return btn;
        }

        private static GameObject FindRootObject(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name)
                    return go;
            return null;
        }

        private static GameObject EnsureRootObject(Scene scene, string name)
        {
            return FindRootObject(scene, name) ?? new GameObject(name);
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
                Debug.LogError($"[TitleSceneBuilder] 필드 '{field}' 없음 on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        // 타이틀은 첫 씬이어야 하므로 빌드 세팅 0번에 둔다.
        private static void AddSceneToBuildSettings(string scenePath, bool first)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var idx = scenes.FindIndex(s => s.path == scenePath);
            if (idx >= 0)
            {
                scenes[idx].enabled = true;
                if (!first || idx == 0)
                {
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
                var existing = scenes[idx];
                scenes.RemoveAt(idx);
                scenes.Insert(0, existing);
            }
            else
            {
                var entry = new EditorBuildSettingsScene(scenePath, true);
                if (first) scenes.Insert(0, entry);
                else scenes.Add(entry);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
