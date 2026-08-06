using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TeachAndFight.Combat;
using TeachAndFight.Flow;

namespace TeachAndFight.Match.UI
{
    // Match.unity 화면 컨트롤러(04장). 뷰(uGUI)와 MatchPresenter를 잇는다.
    // 전투 판정/규칙 발동은 전부 Presenter에 있고, 여기선 파이터 생성 + UI 갱신 + 배속/연출 + 씬 전환만 담당.
    public sealed class MatchScreenController : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private FighterHudView selfHud;
        [SerializeField] private FighterHudView enemyHud;
        [SerializeField] private Text timerText;

        [Header("아레나(수평 위치 표시)")]
        [SerializeField] private RectTransform arenaTrack;
        [SerializeField] private RectTransform selfMarker;
        [SerializeField] private RectTransform enemyMarker;

        [Header("배속/일시정지")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button speedHalfButton;
        [SerializeField] private Button speedOneButton;
        [SerializeField] private Button speedTwoButton;

        [Header("결과 배너")]
        [SerializeField] private GameObject resultBannerRoot;
        [SerializeField] private Text resultBannerText;

        private const float BigHitSlowMoDuration = 0.3f; // 04장: 결정타 0.3초 슬로모
        private const float BigHitSlowMoScale = 0.3f;
        private const float ResultBannerHoldSeconds = 2f; // 04장: 승/패 배너 2초 후 자동 전환

        // 유저 피드백: 서로 안 부딪히고 거리만 유지하는 상태가 오래가면(예: 철벽류 상대)
        // 화면이 멈춘 것처럼 보임 - 이 시간 넘으면 "얼어붙은 게 아니라 교착 상태"임을 알려준다.
        // MatchPresenter.StalemateAutoConcludeSec(15초)에서 실제로 패배 조기 종료되므로 그 전에 경고.
        private const float StalemateHintThresholdSec = 6f;

        private MatchPresenter presenter;
        private FighterController selfFighter;
        private FighterController enemyFighter;
        private CombatConfig config;

        private bool paused;
        private float timeScale = 1f;
        private float slowMoFactor = 1f;
        private bool slowMoActive;
        private float sinceLastHit;

        private void Start()
        {
            var session = GameFlow.EnsureExists().Session;
            if (session.CurrentOpponent == null)
                session.CurrentOpponent = OpponentRuleSetLoader.Load(session.OpponentIndex);

            config = CombatConfigLoader.Load();

            selfFighter = CreatePlaceholderFighter("제자", Color.cyan);
            enemyFighter = CreatePlaceholderFighter(session.CurrentOpponent.FighterName, Color.red);

            float half = config.Arena.StartDistance * 0.5f;
            selfFighter.Init(config, enemyFighter, -half);
            enemyFighter.Init(config, selfFighter, half);

            selfHud?.SetName(session.DiscipleRuleSet?.FighterName ?? "제자");
            enemyHud?.SetName(session.CurrentOpponent.FighterName);
            RefreshHud();

            if (resultBannerRoot != null)
                resultBannerRoot.SetActive(false);

            presenter = new MatchPresenter(session, config, selfFighter, enemyFighter);
            presenter.OnRuleFired += HandleRuleFired;
            presenter.OnBigHit += HandleBigHit;
            presenter.OnAttackWhiff += HandleAttackWhiff;
            presenter.OnConcluded += HandleConcluded;
            presenter.OnAnyHitLanded += () => sinceLastHit = 0f;

            if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseClicked);
            if (speedHalfButton != null) speedHalfButton.onClick.AddListener(() => SetTimeScale(0.5f));
            if (speedOneButton != null) speedOneButton.onClick.AddListener(() => SetTimeScale(1f));
            if (speedTwoButton != null) speedTwoButton.onClick.AddListener(() => SetTimeScale(2f));
        }

        private void OnDestroy()
        {
            if (pauseButton != null) pauseButton.onClick.RemoveListener(OnPauseClicked);
        }

        private void Update()
        {
            if (presenter == null || presenter.Concluded || paused)
                return;

            float dt = Time.deltaTime * timeScale * slowMoFactor;
            presenter.Step(dt);
            sinceLastHit += dt;
            RefreshHud();
            UpdateTimer();
            UpdateArenaMarkers();
        }

        private void RefreshHud()
        {
            selfHud?.Refresh(selfFighter);
            enemyHud?.Refresh(enemyFighter);
        }

        private void UpdateTimer()
        {
            if (timerText == null)
                return;

            timerText.text = sinceLastHit >= StalemateHintThresholdSec
                ? $"{presenter.TimeLeft:0}\n(교착 상태 - 계속되면 패배 처리됩니다)"
                : $"{presenter.TimeLeft:0}";
        }

        private void UpdateArenaMarkers()
        {
            if (arenaTrack == null || selfMarker == null || enemyMarker == null || config == null)
                return;

            float halfArena = config.Arena.Width * 0.5f;
            float trackWidth = arenaTrack.rect.width;
            selfMarker.anchoredPosition = new Vector2(NormalizedX(selfFighter.transform.position.x, halfArena, trackWidth), selfMarker.anchoredPosition.y);
            enemyMarker.anchoredPosition = new Vector2(NormalizedX(enemyFighter.transform.position.x, halfArena, trackWidth), enemyMarker.anchoredPosition.y);
        }

        private static float NormalizedX(float worldX, float halfArena, float trackWidth)
            => (worldX / halfArena) * (trackWidth * 0.5f);

        private void HandleRuleFired(FighterController who, string ruleId, string label)
        {
            var hud = who == selfFighter ? selfHud : enemyHud;
            hud?.ShowRuleLabel(label);
        }

        private void HandleBigHit(FighterController victim, float damagePct)
        {
            StartCoroutine(SlowMoRoutine());
        }

        private void HandleAttackWhiff(FighterController who)
        {
            var hud = who == selfFighter ? selfHud : enemyHud;
            hud?.ShowRuleLabel("헛침!");
        }

        private IEnumerator SlowMoRoutine()
        {
            slowMoActive = true;
            slowMoFactor = BigHitSlowMoScale;
            yield return new WaitForSecondsRealtime(BigHitSlowMoDuration);
            slowMoFactor = 1f;
            slowMoActive = false;
        }

        private void HandleConcluded(MatchResult result)
        {
            // 결정타가 곧바로 경기를 끝낸 경우, 슬로모 연출이 끝날 때까지 배너를 늦춰서
            // 결정타 순간이 배너에 묻히지 않고 실제로 보이게 한다.
            if (slowMoActive)
                StartCoroutine(ShowBannerAfterSlowMo(result));
            else
                ShowBanner(result);
        }

        private IEnumerator ShowBannerAfterSlowMo(MatchResult result)
        {
            yield return new WaitUntil(() => !slowMoActive);
            ShowBanner(result);
        }

        private void ShowBanner(MatchResult result)
        {
            if (resultBannerRoot != null)
                resultBannerRoot.SetActive(true);
            if (resultBannerText != null)
                resultBannerText.text = result.Won ? "승리!" : "패배...";

            StartCoroutine(GoToLockerRoomAfterDelay());
        }

        private IEnumerator GoToLockerRoomAfterDelay()
        {
            yield return new WaitForSecondsRealtime(ResultBannerHoldSeconds);
            GameFlow.EnsureExists().GoToLockerRoom();
        }

        private void OnPauseClicked()
        {
            paused = !paused;
        }

        private void SetTimeScale(float scale)
        {
            timeScale = scale;
        }

        private static Sprite placeholderSprite;

        // 캐릭터 에셋(#21) 전이라 눈에 보이는 사각형 대체 스프라이트 사용.
        private static FighterController CreatePlaceholderFighter(string name, Color color)
        {
            var go = new GameObject(name);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.color = color;
            go.transform.localScale = new Vector3(0.8f, 1.8f, 1f);
            return go.AddComponent<FighterController>();
        }

        private static Sprite GetPlaceholderSprite()
        {
            if (placeholderSprite != null)
                return placeholderSprite;

            var texture = Texture2D.whiteTexture;
            placeholderSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0f), texture.width);
            return placeholderSprite;
        }
    }
}
