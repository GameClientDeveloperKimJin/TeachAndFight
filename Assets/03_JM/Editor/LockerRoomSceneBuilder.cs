using System.Collections.Generic;
using System.IO;
using TeachAndFight.LockerRoom;
using TeachAndFight.Training.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TeachAndFight.Training.EditorTools
{
    // #15 LockerRoom.unity를 코드로 생성하는 에디터 빌더. (TrainingSceneBuilder와 동일 방식 — 참조 안전.)
    // 최소 구성: 결과 배너 + HP + 규칙 발동 통계 + 회고 말풍선 + [훈련실로]/[재경기]. 회고는 실제 Haiku(호출2).
    public static class LockerRoomSceneBuilder
    {
        private const string ScenesDir = "Assets/01_Scenes";
        private const string ScenePath = ScenesDir + "/LockerRoom.unity";

        private static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static readonly Color Ink = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color Panel = new Color(0.16f, 0.17f, 0.20f, 0.9f);

        [MenuItem("TeachAndFight/Build/Create or Rebuild LockerRoom Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
            cam.orthographic = true;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var canvasT = canvasGo.GetComponent<RectTransform>();

            // 상단: 결과 배너 + HP
            var banner = NewText("ResultBanner", canvasT, "결과", 60, TextAnchor.MiddleCenter);
            Anchor(banner.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(700, 110));
            var hp = NewText("HpText", canvasT, "내 HP -% · 상대 HP -%", 32, TextAnchor.MiddleCenter);
            Anchor(hp.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(700, 60));

            // 중앙: 발동 규칙 통계
            var statsPanel = NewPanel("Stats", canvasT, Panel);
            Anchor(statsPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-430, 40), new Vector2(560, 360));
            var statsTitle = NewText("Title", statsPanel.transform, "발동 규칙", 30, TextAnchor.UpperLeft);
            Anchor(statsTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(-40, 50));
            // 제목과 겹치지 않게 아래로 내림(center pivot이라 anchoredPos.y + height/2 가 박스 상단).
            var statsText = NewText("StatsText", statsPanel.transform, "", 28, TextAnchor.UpperLeft);
            Anchor(statsText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -210), new Vector2(-40, 240));

            // 중앙 우: 제자 회고 말풍선
            var bubbleGo = NewPanel("RecapBubble", canvasT, new Color(1f, 1f, 1f, 0.95f));
            Anchor(bubbleGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(320, 40), new Vector2(620, 360));
            var bubbleLabel = NewText("Label", bubbleGo.transform, "", 28, TextAnchor.UpperLeft);
            bubbleLabel.color = Ink;
            Fill(bubbleLabel.rectTransform, 24);
            var bubble = bubbleGo.AddComponent<SpeechBubbleView>();
            SetField(bubble, "root", bubbleGo);
            SetField(bubble, "label", bubbleLabel);

            // 하단: 이동 버튼 2종
            var toTraining = NewButton("ToTrainingButton", canvasT, "훈련실로 가기");
            Anchor(toTraining.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-180, 90), new Vector2(300, 80));
            var rematch = NewButton("RematchButton", canvasT, "바로 재경기");
            Anchor(rematch.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(180, 90), new Vector2(300, 80));

            // 컨트롤러 + 배선
            var controllerGo = new GameObject("LockerRoomScreen");
            var controller = controllerGo.AddComponent<LockerRoomController>();
            SetField(controller, "resultBanner", banner);
            SetField(controller, "hpText", hp);
            SetField(controller, "statsText", statsText);
            SetField(controller, "recapBubble", bubble);
            SetField(controller, "toTrainingButton", toTraining);
            SetField(controller, "rematchButton", rematch);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LockerRoomSceneBuilder] 완료 → {ScenePath}. Play 시 SampleMatch로 단독 데모(회고 LLM 호출).");
        }

        // ---------- UI 헬퍼 (TrainingSceneBuilder와 동일 패턴) ----------

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
            var t = NewText("Text", go.transform, label, 26, TextAnchor.MiddleCenter);
            Fill(t.rectTransform, 4);
            return btn;
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
                Debug.LogError($"[LockerRoomSceneBuilder] 필드 '{field}' 없음 on {target.GetType().Name}");
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
