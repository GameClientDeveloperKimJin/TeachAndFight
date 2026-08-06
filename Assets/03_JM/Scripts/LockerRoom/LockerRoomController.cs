using System.Linq;
using Cysharp.Threading.Tasks;
using TeachAndFight.Flow;
using TeachAndFight.Training.LLM;
using TeachAndFight.Training.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TeachAndFight.LockerRoom
{
    // LockerRoom.unity 화면 컨트롤러(04장 + 03장 호출2). 뷰(uGUI) ↔ LockerRoomPresenter 연결.
    // #13 Match가 없으면 SampleMatch로 단독 데모. 회고 LLM은 여기서만 호출(전투 루프 밖).
    public sealed class LockerRoomController : MonoBehaviour
    {
        [Header("결과")]
        [SerializeField] private Text resultBanner;
        [SerializeField] private Text hpText;

        [Header("통계 / 회고")]
        [SerializeField] private Text statsText;
        [SerializeField] private SpeechBubbleView recapBubble;

        [Header("이동")]
        [SerializeField] private Button toTrainingButton;
        [SerializeField] private Button rematchButton;

        private LockerRoomPresenter _presenter;

        private void Start()
        {
            var session = GameFlow.EnsureExists().Session;
            SampleMatch.EnsureForStandalone(session); // 실제 경기 결과가 있으면 건너뜀

            _presenter = new LockerRoomPresenter(session, new AnthropicLLMClient());

            if (resultBanner != null) resultBanner.text = _presenter.ResultBanner;
            if (hpText != null) hpText.text = $"내 HP {_presenter.SelfHpPct:0}% · 상대 HP {_presenter.EnemyHpPct:0}%";
            if (statsText != null) statsText.text = BuildStatsText();

            if (toTrainingButton != null) toTrainingButton.onClick.AddListener(OnToTraining);
            if (rematchButton != null)
            {
                rematchButton.onClick.AddListener(OnRematch);
                // 승리 + 다음 상대가 남아있으면 "재대결" 버튼을 "다음 상대로"로 바꿔서 재사용
                // (씬에 새 버튼을 안 만들어도 되게 - 새 버튼은 씬 파일 수동 배선이 필요해서 위험함).
                if (_presenter.HasNextOpponent)
                {
                    var label = rematchButton.GetComponentInChildren<Text>();
                    if (label != null) label.text = "다음 상대로";
                }
            }

            RunRecap().Forget();
        }

        private void OnDestroy()
        {
            if (toTrainingButton != null) toTrainingButton.onClick.RemoveListener(OnToTraining);
            if (rematchButton != null) rematchButton.onClick.RemoveListener(OnRematch);
        }

        private async UniTaskVoid RunRecap()
        {
            if (recapBubble != null) recapBubble.Show("헥헥...");
            var result = await _presenter.RecapAsync(this.GetCancellationTokenOnDestroy());
            if (recapBubble != null) recapBubble.Show(result.Text);
        }

        private string BuildStatsText()
        {
            // 교착으로 조기 종료된 판은 발동 규칙 통계(대부분 접근/후퇴 스팸)보다
            // 원인 피드백이 더 유용해서 그걸로 대체한다.
            if (_presenter.WasStalemate)
                return _presenter.StalemateFeedback;

            var top = _presenter.TopFiredRules(3);
            if (top.Count == 0)
                return "발동한 규칙 없음";
            return string.Join("\n", top.Select(r => $"{r.Label}  ×{r.Count}"));
        }

        private void OnToTraining() => GameFlow.EnsureExists().GoToTraining();

        // 원래 승리해도 계속 opponent_01만 재대결됐던 버그(OpponentIndex를 어디서도 안 올림) -
        // 다음 상대가 있으면 이 버튼이 그 진행을 맡고, 없으면(패배/이미 마지막 상대) 원래대로 같은 상대 재대결.
        private void OnRematch()
        {
            if (_presenter.HasNextOpponent)
            {
                _presenter.AdvanceToNextOpponent();
                GameFlow.EnsureExists().GoToTraining(); // 다음 상대 위해 규칙 다듬을 시간을 줌
                return;
            }

            GameFlow.EnsureExists().GoToMatch();
        }
    }
}
