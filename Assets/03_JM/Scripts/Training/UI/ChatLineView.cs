using UnityEngine;
using UnityEngine.UI;

namespace TeachAndFight.Training.UI
{
    // 대화 로그의 한 줄. 코치/제자 구분해 표시. ChatLogView가 프리팹으로 찍어낸다.
    public sealed class ChatLineView : MonoBehaviour
    {
        [SerializeField] private Text label;

        // 프리팹 RectTransform이 고정 100x100이라(원본 디자인 크기) 긴 대사가 줄바꿈되면
        // 다음 줄과 겹쳐 보였던 버그는 부모(ChatLogView.content)의 VerticalLayoutGroup이
        // childControlHeight=true로 각 줄 높이를 직접 계산해서 고침 - 여기서 ContentSizeFitter를
        // 따로 추가하면 그 부모 계산이랑 서로 다투면서(둘 다 이 줄의 높이를 정하려 함) 대화가
        // 쌓일수록 레이아웃 재계산 비용이 커져 "응답 없음"까지 이어졌음(버그 리포트) - 제거.

        public void Set(string speaker, string text)
        {
            if (label != null)
                label.text = $"{speaker}: {text}";
        }
    }
}
