using Cysharp.Threading.Tasks;
using TeachAndFight.Flow;
using TeachAndFight.Training.LLM;
using UnityEngine;
using UnityEngine.UI;

namespace TeachAndFight.Training.UI
{
    // Training.unity 화면 컨트롤러(04장). 뷰(uGUI)와 TrainingPresenter를 잇는다.
    // 로직 결정은 전부 Presenter에 있고, 여기선 입력 수집 + 비동기 + 입력잠금/모션만 담당.
    public sealed class TrainingScreenController : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] private InputField inputField;
        [SerializeField] private Button teachButton;
        [SerializeField] private Button startMatchButton;

        [Header("표시")]
        [SerializeField] private Text opponentBanner;
        [SerializeField] private RuleSlotListView slotList;
        [SerializeField] private ChatLogView chatLog;
        [SerializeField] private SpeechBubbleView bubble;

        private TrainingPresenter _presenter;
        private bool _busy;

        private void Start()
        {
            // Training 씬 단독 실행도 되도록 GameFlow가 없으면 만들어 세션 확보.
            var session = GameFlow.EnsureExists().Session;

            // LLM은 훈련 컴파일에서만 호출(전투 루프엔 안 들어감). 키 없으면 폴백 경로로 동작.
            // 클라이언트 생성은 팩토리에 위임: 빌드=프록시 서버(키 미포함), 에디터=로컬 키 직접 호출.
            var compiler = new TrainingCompiler(LLMClientFactory.CreateDefault());
            _presenter = new TrainingPresenter(session, compiler);

            if (teachButton != null) teachButton.onClick.AddListener(OnTeachClicked);
            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchClicked);
            // Enter=전송 (04장). onSubmit은 입력창에서 Enter 시 발생.
            if (inputField != null) inputField.onSubmit.AddListener(_ => OnTeachClicked());

            if (bubble != null) bubble.Hide();
            UpdateBanner(session);
            RefreshSlots();
        }

        private void OnDestroy()
        {
            if (teachButton != null) teachButton.onClick.RemoveListener(OnTeachClicked);
            if (startMatchButton != null) startMatchButton.onClick.RemoveListener(OnStartMatchClicked);
            // inputField onSubmit은 람다라 개별 제거 대신 컨트롤러 파괴와 함께 정리됨(씬 종료).
        }

        private void OnTeachClicked()
        {
            if (_busy || _presenter == null || inputField == null)
                return;

            var text = inputField.text;
            if (string.IsNullOrWhiteSpace(text))
                return; // 빈 입력 무시(04장)

            TeachFlow(text).Forget();
        }

        // 한 번의 가르치기: 입력잠금 → 컴파일 대기(제자 "음...") → 결과 표시 → 잠금 해제.
        private async UniTaskVoid TeachFlow(string coachInput)
        {
            SetBusy(true);
            chatLog?.AppendCoach(coachInput);
            if (inputField != null) inputField.text = string.Empty;
            bubble?.ShowThinking();

            var result = await _presenter.TeachAsync(coachInput, this.GetCancellationTokenOnDestroy());

            // 진단용: Applied/NeedsConfirmation/Rejected/Failed 구분이 로그에 안 남아서
            // "왜 슬롯이 안 늘었나" 재현할 때 Rejected(RuleValidator)만 보이고 나머지는 안 보였음. 임시 아님 - 계속 남겨둠.
            Debug.Log($"[Training] Outcome={result.Outcome} slotsChanged={result.SlotsChanged} conflict={result.ConflictWith} reply={result.DiscipleReply}");

            // 빈 입력은 위에서 걸러지므로 Ignored는 사실상 안 옴 — 방어적으로만 처리.
            if (!result.Ignored)
            {
                bubble?.Show(result.DiscipleReply);
                chatLog?.AppendDisciple(result.DiscipleReply);

                // Applied일 때만 슬롯이 바뀌므로 그때만 다시 그림(04장: needs_confirmation은 슬롯 변화 없음).
                if (result.SlotsChanged)
                {
                    RefreshSlots();
                    chatLog?.TrimOnRuleApplied(); // 버그 리포트: 규칙 반영되면 대화 정리해서 안 쌓이게
                }
            }

            SetBusy(false);
        }

        private void OnStartMatchClicked()
        {
            if (_busy)
                return; // 컴파일 중엔 진입 막음(규칙 반영 도중 이탈 방지)

            // 규칙 0개여도 진입 허용(#12 백지 1차전 패배 보장). DiscipleRuleSet은 세션에 이미 담겨 있음.
            GameFlow.EnsureExists().GoToMatch();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (inputField != null) inputField.interactable = !busy;
            if (teachButton != null) teachButton.interactable = !busy;
            if (startMatchButton != null) startMatchButton.interactable = !busy;
        }

        private void RefreshSlots()
        {
            slotList?.Render(_presenter.Current, OnDeleteRule);
        }

        private void OnDeleteRule(string ruleId)
        {
            if (_busy || _presenter == null)
                return;

            if (_presenter.RemoveRule(ruleId))
                RefreshSlots();
        }

        private void UpdateBanner(GameSession session)
        {
            if (opponentBanner == null)
                return;

            var name = session.CurrentOpponent?.FighterName;
            opponentBanner.text = string.IsNullOrEmpty(name)
                ? $"다음 상대 #{session.OpponentIndex}"
                : $"다음 상대 #{session.OpponentIndex} · {name}";
        }
    }
}
