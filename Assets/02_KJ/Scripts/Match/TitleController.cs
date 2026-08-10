using TeachAndFight.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeachAndFight.Title
{
    // 어셈블리: TeachAndFight.Match (Assets/02_KJ/Scripts/Match/TeachAndFight.Match.asmdef)
    // 타이틀 화면 컨트롤러.
    // (1) 아무 키/클릭 → Match 씬으로 이동.
    // (2) [게임 방법] 버튼 → 안내 패널 활성화. 패널이 열려 있는 동안은 아무 키/클릭이 "닫기"로만 동작한다.
    public sealed class TitleController : MonoBehaviour
    {
        [SerializeField] private string nextSceneName = GameFlow.MatchScene;
        [SerializeField] private GameObject howToPanel;
        [SerializeField] private Button howToButton;
        [SerializeField] private Button howToCloseButton;
        [SerializeField] private Graphic pressAnyKeyLabel;
        [SerializeField] private float blinkPeriod = 1.2f;

        private bool _transitioning;

        // 패널을 여닫은 그 프레임의 입력이 곧바로 반대 동작으로 이어지는 것을 막는다.
        private int _inputBlockedFrame = -1;

        private void Awake()
        {
            if (howToPanel != null)
                howToPanel.SetActive(false);
            if (howToButton != null)
                howToButton.onClick.AddListener(OpenHowTo);
            if (howToCloseButton != null)
                howToCloseButton.onClick.AddListener(CloseHowTo);
        }

        private void OnDestroy()
        {
            if (howToButton != null)
                howToButton.onClick.RemoveListener(OpenHowTo);
            if (howToCloseButton != null)
                howToCloseButton.onClick.RemoveListener(CloseHowTo);
        }

        private void Update()
        {
            UpdateBlink();

            if (_transitioning || Time.frameCount == _inputBlockedFrame)
                return;

            var howToOpen = howToPanel != null && howToPanel.activeSelf;

            if (AnyKeyPressed())
            {
                if (howToOpen) CloseHowTo();
                else StartMatch();
                return;
            }

            if (PointerPressed())
            {
                if (howToOpen)
                {
                    // 패널이 열려 있으면 어디를 눌러도 닫기. (닫기 버튼 클릭도 같은 결과)
                    CloseHowTo();
                    return;
                }

                // 버튼 위에서의 클릭은 버튼(onClick)이 처리하므로 여기서는 무시한다.
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                StartMatch();
            }
        }

        public void OpenHowTo()
        {
            if (howToPanel == null)
                return;
            howToPanel.SetActive(true);
            _inputBlockedFrame = Time.frameCount;
            ClearSelection();
        }

        public void CloseHowTo()
        {
            if (howToPanel == null)
                return;
            howToPanel.SetActive(false);
            _inputBlockedFrame = Time.frameCount;
            ClearSelection();
        }

        // 버튼이 selected로 남아 있으면 Space/Enter가 UI Submit(버튼)과 anyKey(씬 이동)로
        // 같은 프레임에 갈라진다. 선택을 비워 Submit 경로 자체를 없앤다.
        private static void ClearSelection()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void StartMatch()
        {
            if (_transitioning)
                return;
            _transitioning = true;

            // 세션(GameSession)이 없는 상태로 Match에 진입하지 않도록 보장.
            GameFlow.EnsureExists();

            var scene = string.IsNullOrEmpty(nextSceneName) ? GameFlow.MatchScene : nextSceneName;
            SceneManager.LoadScene(scene);
        }

        private static bool AnyKeyPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        }

        private static bool PointerPressed()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            var touch = Touchscreen.current;
            return touch != null && touch.primaryTouch.press.wasPressedThisFrame;
        }

        private void UpdateBlink()
        {
            if (pressAnyKeyLabel == null || blinkPeriod <= 0f)
                return;

            var t = 0.5f * (1f + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / blinkPeriod));
            var c = pressAnyKeyLabel.color;
            c.a = Mathf.Lerp(0.25f, 1f, t);
            pressAnyKeyLabel.color = c;
        }
    }
}
