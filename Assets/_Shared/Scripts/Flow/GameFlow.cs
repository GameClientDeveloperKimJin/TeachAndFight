using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeachAndFight.Flow
{
    // 04장 씬 흐름 관리 싱글턴: Title → Training ⇄ Match → LockerRoom → Training.
    // 규칙셋/경기결과는 Session(GameSession)에 담아 DontDestroyOnLoad로 씬 간 유실 없이 전달.
    // #14 단계에서는 최소 스캐폴드(세션 보관 + 씬 로드)만 구현. 전체 순환/데이터 인계 세부는 #16(공용)에서 확장.
    public sealed class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        // 씬 이름 (Build Settings에 등록되어 있어야 함 — 배선 가이드 참조).
        public const string TrainingScene = "Training";
        public const string MatchScene = "Match";
        public const string LockerRoomScene = "LockerRoom";

        // 세 씬이 공유하는 세션 데이터. 항상 non-null이 되도록 EnsureExists가 보장.
        public GameSession Session { get; private set; }

        private void Awake()
        {
            // 중복 싱글턴 방지: 씬 재진입 시 옛 인스턴스가 남아 이중 실행되는 사고를 막는다.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (Session == null)
                Session = GameSession.NewGame();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // Training 씬을 단독 실행(에디터 Play)해도 GameFlow가 없으면 만들어 세션을 보장한다.
        // 반환된 인스턴스의 Session은 항상 non-null.
        public static GameFlow EnsureExists()
        {
            if (Instance == null)
            {
                var go = new GameObject("GameFlow");
                Instance = go.AddComponent<GameFlow>(); // Awake가 Session 초기화 + DontDestroyOnLoad 처리
            }
            return Instance;
        }

        public void StartNewGame(int opponentIndex = 1)
        {
            Session = GameSession.NewGame(opponentIndex);
        }

        // Training에서 [경기 시작 ▶]: 세션의 DiscipleRuleSet은 이미 채워진 상태로 Match 씬 로드.
        public void GoToMatch() => SceneManager.LoadScene(MatchScene);

        public void GoToTraining() => SceneManager.LoadScene(TrainingScene);

        public void GoToLockerRoom() => SceneManager.LoadScene(LockerRoomScene);
    }
}
