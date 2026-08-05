using UnityEngine;
using UnityEngine.UI;

namespace TeachAndFight.Training.UI
{
    // 코치/제자 대화 로그. content 아래에 ChatLineView 프리팹을 쌓는다.
    // content를 ScrollRect의 Content로 배선하면(씬에서 ScrollRect/Viewport/Mask 구성 필요)
    // 새 줄이 추가될 때마다 자동으로 맨 아래로 스크롤된다.
    public sealed class ChatLogView : MonoBehaviour
    {
        private const int MaxLines = 50; // 무한정 쌓이는 것 방지 - 오래된 줄부터 제거

        [SerializeField] private Transform content;       // 줄들이 쌓이는 부모(Vertical Layout Group 권장)
        [SerializeField] private ChatLineView linePrefab; // 한 줄 프리팹
        [SerializeField] private ScrollRect scrollRect;   // content를 담은 ScrollRect(선택, 있으면 자동 스크롤)

        public const string CoachSpeaker = "코치";
        public const string DiscipleSpeaker = "제자";

        public void AppendCoach(string text) => Append(CoachSpeaker, text);
        public void AppendDisciple(string text) => Append(DiscipleSpeaker, text);

        public void Append(string speaker, string text)
        {
            if (content == null || linePrefab == null || string.IsNullOrEmpty(text))
                return;

            var line = Instantiate(linePrefab, content);
            line.Set(speaker, text);

            while (content.childCount > MaxLines)
                Destroy(content.GetChild(0).gameObject);

            if (scrollRect != null)
                ScrollToBottomNextFrame();
        }

        private void ScrollToBottomNextFrame()
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
