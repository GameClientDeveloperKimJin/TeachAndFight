using UnityEngine;
using UnityEngine.UI;

namespace TeachAndFight.Training.UI
{
    // 코치/제자 대화 로그. content 아래에 ChatLineView 프리팹을 쌓는다.
    // content를 ScrollRect의 Content로 배선하면(씬에서 ScrollRect/Viewport/Mask 구성 필요)
    // 새 줄이 추가될 때마다 자동으로 맨 아래로 스크롤된다.
    public sealed class ChatLogView : MonoBehaviour
    {
        private const int MaxLines = 50;        // 무한정 쌓이는 것 방지 - 오래된 줄부터 제거
        private const int KeepLinesAfterRule = 6; // 규칙 확정되면 그 맥락은 끝났으니 최근 몇 줄만 남김(버그 리포트: 채팅이 계속 쌓여 지저분함)

        [SerializeField] private Transform content;       // 줄들이 쌓이는 부모(Vertical Layout Group 권장)
        [SerializeField] private ChatLineView linePrefab; // 한 줄 프리팹
        [SerializeField] private ScrollRect scrollRect;   // content를 담은 ScrollRect(선택, 있으면 자동 스크롤)

        public const string CoachSpeaker = "코치";
        public const string DiscipleSpeaker = "제자";

        // content에 줄이 쌓여도 실제 텍스트 길이대로 높이가 안 늘어나 겹쳐 보이는 버그가 있었음
        // (ChatLine 프리팹이 고정 크기라서). content 쪽 레이아웃을 런타임에 보장해서 고침 -
        // 씬에 이미 제대로 구성돼 있어도 값만 맞춰 넣으니 안전(덮어써도 무해한 기본값).
        private void Awake()
        {
            if (content == null)
                return;

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
                vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            if (vlg.spacing < 4f)
                vlg.spacing = 4f;

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public void AppendCoach(string text) => Append(CoachSpeaker, text);
        public void AppendDisciple(string text) => Append(DiscipleSpeaker, text);

        public void Append(string speaker, string text)
        {
            if (content == null || linePrefab == null || string.IsNullOrEmpty(text))
                return;

            var line = Instantiate(linePrefab, content);
            line.Set(speaker, text);

            TrimTo(MaxLines);

            if (scrollRect != null)
                ScrollToBottomNextFrame();
        }

        // 규칙이 실제로 슬롯에 반영됐을 때 호출(TrainingScreenController). 그 시점까지의 대화는
        // 이미 규칙으로 정리됐으니 최근 몇 줄만 남기고 정리해 채팅창이 계속 안 늘어나게 한다.
        public void TrimOnRuleApplied()
        {
            TrimTo(KeepLinesAfterRule);
        }

        // 버그 리포트(응답 없음, CPU 100%, 매번 재현): Destroy()는 이번 프레임 끝나고 실제로
        // 지워짐 - 그래서 while(content.childCount > keepLast) Destroy(GetChild(0)) 이 패턴은
        // childCount가 루프 안에서 절대 안 줄어들어 무한루프가 됨(Unity 잘 알려진 함정).
        // 지울 개수를 미리 고정해서 인덱스로 순회하면 이 문제가 없음.
        private void TrimTo(int keepLast)
        {
            if (content == null)
                return;

            int excess = content.childCount - keepLast;
            for (int i = 0; i < excess; i++)
                Destroy(content.GetChild(i).gameObject);
        }

        // 버그 리포트(응답 없음/멈춤): Canvas.ForceUpdateCanvases()는 씬의 캔버스 전체를 매번
        // 강제로 다시 그려서 대화가 쌓일수록 호출 한 번 비용이 계속 커짐 - 채팅 로그(content)
        // 하나만 다시 계산하는 LayoutRebuilder로 범위를 좁혀서 이 비용을 없앤다.
        private void ScrollToBottomNextFrame()
        {
            if (content is RectTransform contentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
