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

        [Header("캐릭터 아트 (비워두면 기존 사각형 placeholder로 동작)")]
        [Tooltip("제자(주인공) 애니메이터 컨트롤러: DisciplePreview.controller")]
        [SerializeField] private RuntimeAnimatorController discipleController;
        [Tooltip("상대 컨트롤러 5종. 순서대로 1층~5층: Rush, IronWall, Shadow, Chameleon, Master")]
        [SerializeField] private RuntimeAnimatorController[] opponentControllers = new RuntimeAnimatorController[5];
        [Tooltip("실제 스프라이트(PPU 128)가 커서 아레나에 맞게 축소. Play 화면 보며 조절.")]
        [SerializeField] private float characterScale = 0.25f;

        [Tooltip("캐릭터 스프라이트 정렬 순서. 배경(월드 스프라이트)은 이 값보다 낮게 두면 캐릭터가 배경 위에 보인다.")]
        [SerializeField] private int characterSortingOrder = 0;

        [Header("배경 (월드 스프라이트로 캐릭터 뒤에 자동 배치)")]
        [Tooltip("경기 배경 스프라이트. 지정하면 카메라 전체를 덮도록 캐릭터 뒤에 깔린다. UI 배경 Image는 지우거나 비활성화할 것.")]
        [SerializeField] private Sprite backgroundSprite;
        [Tooltip("배경 정렬 순서. 캐릭터(기본 0)보다 낮아야 뒤로 간다.")]
        [SerializeField] private int backgroundSortingOrder = -10;

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

            CreateBackground();

            selfFighter = CreateFighter("제자", discipleController, Color.cyan);
            enemyFighter = CreateFighter(session.CurrentOpponent.FighterName, OpponentControllerFor(session.OpponentIndex), Color.red);

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

        // 배경을 UI가 아닌 월드 스프라이트로 캐릭터 뒤에 깐다(A 방식). 카메라 전체를 덮도록 스케일.
        // backgroundSprite 미지정 시 아무것도 안 하므로 안전.
        private void CreateBackground()
        {
            if (backgroundSprite == null)
                return;

            var go = new GameObject("Background_World");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = backgroundSprite;
            sr.sortingOrder = backgroundSortingOrder; // 캐릭터(기본 0)보다 낮음 → 뒤로.

            var cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            if (cam == null || !cam.orthographic)
            {
                go.transform.position = Vector3.zero;
                return;
            }

            go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
            FitSpriteToCamera(sr, cam, go.transform);
        }

        // 오쏘그래픽 카메라 화면을 완전히 덮도록 스프라이트를 균일 스케일(cover). 여백 없이 꽉 채움.
        private static void FitSpriteToCamera(SpriteRenderer sr, Camera cam, Transform t)
        {
            if (sr.sprite == null)
                return;
            var size = sr.sprite.bounds.size; // 스케일 1일 때 월드 크기
            if (size.x <= 0f || size.y <= 0f)
                return;

            float worldHeight = cam.orthographicSize * 2f;
            float worldWidth = worldHeight * cam.aspect;
            float scale = Mathf.Max(worldWidth / size.x, worldHeight / size.y);
            t.localScale = new Vector3(scale, scale, 1f);
        }

        // OpponentIndex(1~5) → 해당 층 상대 컨트롤러. 미할당/범위밖이면 null(→ 사각형 폴백).
        private RuntimeAnimatorController OpponentControllerFor(int opponentIndex)
        {
            int i = opponentIndex - 1;
            if (opponentControllers != null && i >= 0 && i < opponentControllers.Length)
                return opponentControllers[i];
            return null;
        }

        // 컨트롤러가 있으면 실제 캐릭터(Animator + FighterAnimatorBridge)로, 없으면 기존 사각형 placeholder로 생성.
        // 컴포넌트 추가 순서 중요: FighterAnimatorBridge는 FighterController/Animator를 RequireComponent 하므로 먼저 붙인다.
        private FighterController CreateFighter(string name, RuntimeAnimatorController controller, Color placeholderColor)
        {
            var go = new GameObject(name);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = characterSortingOrder; // 배경(월드 스프라이트)보다 위에 오도록 명시.

            if (controller != null)
            {
                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                var fighter = go.AddComponent<FighterController>();

                var bridge = go.AddComponent<FighterAnimatorBridge>();
                bridge.Configure(fighter, animator, renderer, PrefixOf(controller));

                go.transform.localScale = Vector3.one * characterScale;
                return fighter;
            }

            // 폴백: 아트 컨트롤러 미할당 시 기존 사각형 대체 스프라이트.
            renderer.sprite = GetPlaceholderSprite();
            renderer.color = placeholderColor;
            go.transform.localScale = new Vector3(0.8f, 1.8f, 1f);
            return go.AddComponent<FighterController>();
        }

        // "IronWallPreview" → "IronWall". 상태이름 규칙 {prefix}_Idle 과 맞추기 위해 접미 "Preview" 제거.
        private static string PrefixOf(RuntimeAnimatorController controller)
        {
            const string suffix = "Preview";
            var n = controller.name;
            return n.EndsWith(suffix) ? n.Substring(0, n.Length - suffix.Length) : n;
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
