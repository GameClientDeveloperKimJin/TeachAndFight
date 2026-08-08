using System.IO;
using TeachAndFight.Training.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TeachAndFight.Training.EditorTools
{
    // #14 Training.unity를 코드로 생성하는 에디터 빌더.
    // Unity MCP 없이도 메뉴 한 번으로 씬 + 프리팹 3종 + EventSystem(InputSystem 모듈) + 모든 SerializeField 배선을 정확히 만든다.
    // (손으로 쓴 씬/프리팹 YAML은 GUID·참조가 깨지기 쉬워, Unity API로 만드는 이 방식이 안전.)
    public static class TrainingSceneBuilder
    {
        private const string ScenesDir = "Assets/01_Scenes";
        private const string ScenePath = ScenesDir + "/Training.unity";
        private const string PrefabDir = "Assets/03_JM/Prefabs/Training";

        // 한글 표시용 폰트. LegacyRuntime.ttf(내장)는 한글 글리프가 없어 WebGL에서 □로 나오므로
        // 나눔고딕을 우선 사용하고, 없을 때만 내장 폰트로 폴백한다. (에디터에서는 OS 폰트 대체로 한글이 보이지만
        // WebGL 빌드에는 OS 폰트 대체가 없어 반드시 한글 글리프가 포함된 폰트가 필요하다.)
        private static Font UIFont =>
            AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NanumGothic.ttf")
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static readonly Color Ink = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color Panel = new Color(0.16f, 0.17f, 0.20f, 0.9f);

        [MenuItem("TeachAndFight/Build/Create or Rebuild Training Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(ScenesDir);
            EnsureFolder(PrefabDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Main Camera (빈 씬이라 없으면 "No cameras rendering" 경고) ---
            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
            cam.orthographic = true;

            // --- EventSystem: 새 Input System 모듈 (계획서 사전준비 #2) ---
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // --- Canvas (1920x1080 기준) ---
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var canvasT = canvasGo.GetComponent<RectTransform>();

            // --- 프리팹 3종 먼저 만들어 두고 리스트 뷰에 연결 ---
            var slotPrefab = BuildRuleSlotPrefab();
            var emptyPrefab = BuildEmptySlotPrefab();
            var chatLinePrefab = BuildChatLinePrefab();

            // --- 상단 배너 ---
            var banner = NewText("Banner", canvasT, "다음 상대 #1", 34, TextAnchor.MiddleCenter);
            Anchor(banner.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(-40, 70));

            // --- 좌측: 제자 말풍선 ---
            var bubbleGo = NewPanel("SpeechBubble", canvasT, new Color(1f, 1f, 1f, 0.95f));
            Anchor(bubbleGo.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(280, 120), new Vector2(460, 160));
            var bubbleLabel = NewText("Label", bubbleGo.transform, "", 26, TextAnchor.MiddleLeft);
            bubbleLabel.color = Ink;
            Fill(bubbleLabel.rectTransform, 16);
            var bubble = bubbleGo.AddComponent<SpeechBubbleView>();
            SetField(bubble, "root", bubbleGo);
            SetField(bubble, "label", bubbleLabel);
            bubbleGo.SetActive(false); // 시작 시 숨김(컨트롤러가 Hide 호출)

            // --- 우측: 규칙 슬롯 리스트 ---
            var slotsPanel = NewPanel("RuleSlots", canvasT, Panel);
            Anchor(slotsPanel.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-330, -430), new Vector2(600, 640));
            var slotsContent = NewVerticalContent("Content", slotsPanel.transform);
            var slotList = slotsPanel.AddComponent<RuleSlotListView>();
            SetField(slotList, "content", slotsContent.transform);
            SetField(slotList, "slotPrefab", slotPrefab);
            SetField(slotList, "emptySlotPrefab", emptyPrefab.gameObject);

            // --- 하단: 대화 로그 ---
            var chatPanel = NewPanel("ChatLog", canvasT, Panel);
            // 입력창(하단) 위로 올려 겹침 제거.
            Anchor(chatPanel.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(520, 300), new Vector2(980, 360));
            var chatContent = NewVerticalContent("Content", chatPanel.transform);
            var chatLog = chatPanel.AddComponent<ChatLogView>();
            SetField(chatLog, "content", chatContent.transform);
            SetField(chatLog, "linePrefab", chatLinePrefab);

            // --- 입력창 ---
            var input = NewInputField("InputField", canvasT, "가르칠 내용을 입력…");
            Anchor(input.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(520, 60), new Vector2(760, 64));

            // --- 버튼 2종 ---
            var teachBtn = NewButton("TeachButton", canvasT, "가르치기");
            Anchor(teachBtn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(980, 60), new Vector2(160, 64));

            var startBtn = NewButton("StartMatchButton", canvasT, "경기 시작 ▶");
            Anchor(startBtn.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-140, 60), new Vector2(240, 72));

            // --- 컨트롤러 + 배선 ---
            var controllerGo = new GameObject("TrainingScreen");
            var controller = controllerGo.AddComponent<TrainingScreenController>();
            SetField(controller, "inputField", input);
            SetField(controller, "teachButton", teachBtn);
            SetField(controller, "startMatchButton", startBtn);
            SetField(controller, "opponentBanner", banner);
            SetField(controller, "slotList", slotList);
            SetField(controller, "chatLog", chatLog);
            SetField(controller, "bubble", bubble);

            // --- 저장 + Build Settings 등록 ---
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TrainingSceneBuilder] 완료 → {ScenePath} (프리팹: {PrefabDir}). " +
                      "Play 눌러 가르치기→슬롯→경기 시작 확인.");
        }

        // ---------- 프리팹 빌더 ----------

        private static RuleSlotView BuildRuleSlotPrefab()
        {
            var go = NewPanelRaw("RuleSlotItem", new Color(0.22f, 0.23f, 0.27f));
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 56;
            var row = go.AddComponent<HorizontalLayoutGroup>();
            row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.padding = new RectOffset(12, 12, 6, 6); row.spacing = 8;

            var label = NewText("Label", go.transform, "규칙", 24, TextAnchor.MiddleLeft);
            label.GetComponent<LayoutElement>().flexibleWidth = 1;

            var badge = NewText("Priority", go.transform, "P0", 22, TextAnchor.MiddleCenter);
            badge.GetComponent<LayoutElement>().preferredWidth = 56;

            var del = NewButton("Delete", go.transform, "×");
            del.GetComponent<LayoutElement>().preferredWidth = 44;

            var view = go.AddComponent<RuleSlotView>();
            SetField(view, "labelText", label);
            SetField(view, "priorityBadge", badge);
            SetField(view, "deleteButton", del);

            return SavePrefabComponent<RuleSlotView>(go, "RuleSlotItem");
        }

        private static RectTransform BuildEmptySlotPrefab()
        {
            var go = NewPanelRaw("EmptySlotItem", new Color(1, 1, 1, 0.06f));
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 56;
            var hint = NewText("Hint", go.transform, "· · · · ·", 22, TextAnchor.MiddleCenter);
            hint.color = new Color(1, 1, 1, 0.35f);
            Fill(hint.rectTransform, 4);
            var prefab = SavePrefab(go, "EmptySlotItem");
            return prefab.GetComponent<RectTransform>();
        }

        private static ChatLineView BuildChatLinePrefab()
        {
            // Text를 루트에 직접 둬서 VerticalLayoutGroup이 "줄바꿈된 실제 높이"를 반영(고정 높이로 자르지 않음).
            // 이전엔 30px 고정이라 긴 제자 대사가 잘려 안 보였음.
            var go = new GameObject("ChatLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var t = go.GetComponent<Text>();
            t.font = UIFont;
            t.fontSize = 22;
            t.color = Color.white;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var view = go.AddComponent<ChatLineView>();
            SetField(view, "label", t);
            return SavePrefabComponent<ChatLineView>(go, "ChatLine");
        }

        // ---------- UI 헬퍼 ----------

        private static GameObject NewPanel(string name, Transform parent, Color color)
        {
            var go = NewPanelRaw(name, color);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject NewPanelRaw(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text NewText(string name, Transform parent, string content, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = UIFont;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.text = content;
            return t;
        }

        private static Button NewButton(string name, Transform parent, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.30f, 0.42f, 0.78f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var t = NewText("Text", go.transform, label, 24, TextAnchor.MiddleCenter);
            Fill(t.rectTransform, 4);
            return btn;
        }

        private static InputField NewInputField(string name, Transform parent, string placeholder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var bg = go.GetComponent<Image>();
            bg.color = Color.white;
            var input = go.GetComponent<InputField>();

            var text = NewText("Text", go.transform, "", 24, TextAnchor.MiddleLeft);
            text.color = Ink;
            text.supportRichText = false;
            Fill(text.rectTransform, 10);

            var ph = NewText("Placeholder", go.transform, placeholder, 24, TextAnchor.MiddleLeft);
            ph.color = new Color(0.4f, 0.4f, 0.4f);
            ph.fontStyle = FontStyle.Italic;
            Fill(ph.rectTransform, 10);

            input.textComponent = text;
            input.placeholder = ph;
            input.targetGraphic = bg;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        private static GameObject NewVerticalContent(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            // 위 가장자리에 고정하고 ContentSizeFitter로 "아래로" 성장 (상하 stretch+Fitter 충돌 회피 —
            // stretch면 항목 수가 적을 때 리스트가 화면 밖으로 밀려 대화 로그가 안 보였음).
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -10);
            rt.sizeDelta = new Vector2(-20, 0); // 좌우 10 패딩, 높이는 Fitter가 결정
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandHeight = false; v.spacing = 6;
            v.childAlignment = TextAnchor.UpperLeft;
            var fit = go.GetComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
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

        // private [SerializeField] 필드도 SerializedObject로 안전하게 배선.
        private static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[TrainingSceneBuilder] 필드 '{field}' 없음 on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 에셋 헬퍼 ----------

        private static T SavePrefabComponent<T>(GameObject temp, string fileName) where T : Component
            => SavePrefab(temp, fileName).GetComponent<T>();

        private static GameObject SavePrefab(GameObject temp, string fileName)
        {
            var path = $"{PrefabDir}/{fileName}.prefab";
            var asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp); // 씬의 임시 인스턴스 제거(프리팹만 남김)
            return asset;
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
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath))
                return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
