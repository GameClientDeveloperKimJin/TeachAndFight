using UnityEngine;

namespace TeachAndFight.Combat.Demo
{
    public enum MatchOutcome
    {
        FighterAWins,
        FighterBWins,
        Draw, // 동률 = 무승부 = (토너먼트 규칙상) 패배 처리
    }

    // #6 W1 데모: 하드코딩 규칙셋 2개로 AI vs AI 1v1. 60초 타임업 또는 KO로 종료.
    public class MatchDemoRunner : MonoBehaviour
    {
        [SerializeField] private FighterController fighterA;
        [SerializeField] private FighterController fighterB;
        [SerializeField] private bool paused;
        [SerializeField] [Range(0.5f, 2f)] private float timeScale = 1f;

        private const float DecisionInterval = 0.1f; // 02장 RuleEvaluator 틱 주기

        private CombatConfig config;
        private IHardcodedBrain brainA;
        private IHardcodedBrain brainB;
        private float matchTimer;
        private float decisionTimer;

        public MatchOutcome? Outcome { get; private set; }
        public float TimeLeft => Mathf.Max(0f, matchTimer);
        public FighterController FighterA => fighterA;
        public FighterController FighterB => fighterB;

        private bool initialized;

        private void Awake() => EnsureInitialized();

        // 테스트/에디터 코드에서 Awake 호출 시점에 의존하지 않고 명시적으로 초기화할 수 있도록 공개
        public void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;

            config = CombatConfigLoader.Load();
            brainA = new RushBrain();
            brainB = new TurtleBrain();

            if (fighterA == null)
                fighterA = CreatePlaceholderFighter("FighterA_Rush", Color.red);
            if (fighterB == null)
                fighterB = CreatePlaceholderFighter("FighterB_Turtle", Color.blue);

            float half = config.Arena.StartDistance * 0.5f;
            fighterA.Init(config, fighterB, -half);
            fighterB.Init(config, fighterA, half);

            fighterA.OnDown += _ => Conclude();
            fighterB.OnDown += _ => Conclude();

            matchTimer = config.Match.DurationSec;
            decisionTimer = 0f;
            Outcome = null;
        }

        private static Sprite placeholderSprite;

        // Inspector에 스프라이트 있는 캐릭터를 직접 할당 안 했을 때(테스트/빠른 확인용) 눈에 보이는 대체용 사각형 부여
        private static FighterController CreatePlaceholderFighter(string name, Color color)
        {
            var go = new GameObject(name);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.color = color;
            go.transform.localScale = new Vector3(0.8f, 1.8f, 1f); // 1x1 사각형 스프라이트를 사람 비율로
            return go.AddComponent<FighterController>();
        }

        private static Sprite GetPlaceholderSprite()
        {
            if (placeholderSprite != null)
                return placeholderSprite;

            var texture = Texture2D.whiteTexture;
            // pixelsPerUnit = texture.width -> 스프라이트 1개 = 월드 1x1 유닛 정사각형
            placeholderSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0f), texture.width);
            return placeholderSprite;
        }

        private void Update()
        {
            if (Outcome.HasValue || paused)
                return;

            Step(Time.deltaTime * timeScale);
        }

        public void Step(float dt)
        {
            if (Outcome.HasValue)
                return;

            matchTimer -= dt;
            decisionTimer -= dt;

            if (decisionTimer <= 0f)
            {
                decisionTimer += DecisionInterval;
                Decide(fighterA, fighterB, brainA);
                Decide(fighterB, fighterA, brainB);
            }

            fighterA.Tick(dt);
            fighterB.Tick(dt);

            if (!Outcome.HasValue && matchTimer <= 0f)
                ConcludeByTimeout();
        }

        private static void Decide(FighterController self, FighterController enemy, IHardcodedBrain brain)
        {
            if (self.State != FighterState.Idle && self.State != FighterState.Move)
                return;

            var cmd = brain.Decide(self, enemy);
            if (!self.TryPerform(cmd))
                self.TryPerform(ActionCommand.Idle()); // 자원 부족 시 폴백
        }

        private void Conclude()
        {
            if (Outcome.HasValue)
                return;

            bool aDown = fighterA.State == FighterState.Down;
            bool bDown = fighterB.State == FighterState.Down;

            if (aDown && bDown) Outcome = MatchOutcome.Draw;
            else if (aDown) Outcome = MatchOutcome.FighterBWins;
            else if (bDown) Outcome = MatchOutcome.FighterAWins;

            LogResult();
        }

        private void ConcludeByTimeout()
        {
            if (Mathf.Approximately(fighterA.HpPct, fighterB.HpPct))
                Outcome = MatchOutcome.Draw;
            else
                Outcome = fighterA.HpPct > fighterB.HpPct ? MatchOutcome.FighterAWins : MatchOutcome.FighterBWins;

            LogResult();
        }

        private void LogResult()
        {
            Debug.Log($"[MatchDemo] {Outcome} - A(HP {fighterA.HpPct:0}%) vs B(HP {fighterB.HpPct:0}%), 경과 {config.Match.DurationSec - matchTimer:0.0}s");
        }

        private void OnGUI()
        {
            if (fighterA == null || fighterB == null)
                return;

            GUI.Label(new Rect(10, 10, 500, 20), $"A(Rush)  HP {fighterA.HpPct:0}%  Stamina {fighterA.StaminaPct:0}%  Ult {fighterA.UltGaugePct:0}%  State {fighterA.State}");
            GUI.Label(new Rect(10, 30, 500, 20), $"B(Turtle) HP {fighterB.HpPct:0}%  Stamina {fighterB.StaminaPct:0}%  Ult {fighterB.UltGaugePct:0}%  State {fighterB.State}");
            GUI.Label(new Rect(10, 50, 500, 20), $"Time left: {TimeLeft:0.0}s");

            if (Outcome.HasValue)
                GUI.Label(new Rect(10, 70, 500, 20), $"Result: {Outcome}");
        }
    }
}
