using UnityEngine;

namespace TeachAndFight.Training.UI
{
    // 코치/제자 대화 로그. content 아래에 ChatLineView 프리팹을 쌓는다.
    // ScrollRect content로 쓰면 자동 스크롤되도록 배선 가이드 참조.
    public sealed class ChatLogView : MonoBehaviour
    {
        [SerializeField] private Transform content;       // 줄들이 쌓이는 부모(Vertical Layout Group 권장)
        [SerializeField] private ChatLineView linePrefab; // 한 줄 프리팹

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
        }
    }
}
