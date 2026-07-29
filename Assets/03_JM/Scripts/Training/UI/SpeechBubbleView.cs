using UnityEngine;
using UnityEngine.UI;

namespace TeachAndFight.Training.UI
{
    // 제자 말풍선. LLM 대기 중 "음..." 표시, 응답 오면 대사 표시.
    public sealed class SpeechBubbleView : MonoBehaviour
    {
        [SerializeField] private GameObject root;   // 말풍선 전체(껐다 켰다)
        [SerializeField] private Text label;

        // 대기 모션 텍스트(04장). 필요 시 인스펙터에서 교체.
        [SerializeField] private string thinkingText = "음...";

        public void ShowThinking() => Show(thinkingText);

        public void Show(string text)
        {
            if (root != null) root.SetActive(true);
            if (label != null) label.text = text;
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
