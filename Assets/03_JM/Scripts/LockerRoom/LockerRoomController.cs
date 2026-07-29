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
            if (rematchButton != null) rematchButton.onClick.AddListener(OnRematch);

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
            var top = _presenter.TopFiredRules(3);
            if (top.Count == 0)
                return "발동한 규칙 없음";
            return string.Join("\n", top.Select(r => $"{r.Label}  ×{r.Count}"));
        }

        private void OnToTraining() => GameFlow.EnsureExists().GoToTraining();
        private void OnRematch() => GameFlow.EnsureExists().GoToMatch();
    }
}
