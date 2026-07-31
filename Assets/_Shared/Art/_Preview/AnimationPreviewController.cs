using UnityEngine;

namespace TeachAndFight.ArtPreview
{
    // 캐릭터 애니메이션 8종을 게임 씬에서 눈으로 확인하는 프리뷰 하네스(#22 아트 검증용).
    // 프리뷰 컨트롤러엔 전이/파라미터가 없으므로 Animator.Play(상태이름)로 직접 상태를 재생한다.
    // 재사용: 철벽/그림자/사범 등 다른 캐릭터도 stateNames만 바꿔 그대로 쓴다.
    [RequireComponent(typeof(Animator))]
    public sealed class AnimationPreviewController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;

        // Animator Controller의 상태 이름들(예: IronWall_Idle …). 빌더가 채운다.
        [SerializeField] private string[] stateNames;

        // 자동 재생 시 각 클립을 보여주는 시간(초).
        [SerializeField] private float interval = 1.5f;
        [SerializeField] private bool autoPlay = true;

        private int _index;
        private float _timer;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            PlayIndex(0);
        }

        private void Update()
        {
            if (!autoPlay || stateNames == null || stateNames.Length == 0)
                return;

            _timer += Time.deltaTime; // 프레임레이트 무관하게 시간 기반
            if (_timer >= interval)
            {
                _timer = 0f;
                PlayIndex(_index + 1);
            }
        }

        private void PlayIndex(int i)
        {
            if (animator == null || stateNames == null || stateNames.Length == 0)
                return;
            int n = stateNames.Length;
            _index = ((i % n) + n) % n;
            animator.Play(stateNames[_index], 0, 0f); // 전이/파라미터 없이 상태 직접 재생
        }

        private void OnGUI()
        {
            const int w = 280;
            GUILayout.BeginArea(new Rect(20, 20, w, 250), GUI.skin.box);

            var current = (stateNames != null && stateNames.Length > 0)
                ? $"클립 {_index + 1}/{stateNames.Length}\n{stateNames[_index]}"
                : "상태 없음 (stateNames 비어있음)";
            GUILayout.Label(current);

            GUILayout.Space(6);
            if (GUILayout.Button("◀ 이전")) { autoPlay = false; PlayIndex(_index - 1); }
            if (GUILayout.Button("다음 ▶")) { autoPlay = false; PlayIndex(_index + 1); }
            autoPlay = GUILayout.Toggle(autoPlay, " 자동 재생");

            if (spriteRenderer != null)
            {
                if (GUILayout.Button(spriteRenderer.flipX ? "좌우반전: ON (왼쪽 바라봄)" : "좌우반전: OFF (오른쪽 바라봄)"))
                    spriteRenderer.flipX = !spriteRenderer.flipX;
            }

            GUILayout.EndArea();
        }
    }
}
