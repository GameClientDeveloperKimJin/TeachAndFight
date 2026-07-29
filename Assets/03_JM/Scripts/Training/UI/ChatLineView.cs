using UnityEngine;
using UnityEngine.UI;

namespace TeachAndFight.Training.UI
{
    // 대화 로그의 한 줄. 코치/제자 구분해 표시. ChatLogView가 프리팹으로 찍어낸다.
    public sealed class ChatLineView : MonoBehaviour
    {
        [SerializeField] private Text label;

        public void Set(string speaker, string text)
        {
            if (label != null)
                label.text = $"{speaker}: {text}";
        }
    }
}
