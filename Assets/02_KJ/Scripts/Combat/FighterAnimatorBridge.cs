using UnityEngine;

namespace TeachAndFight.Combat
{
    // #22 캐릭터 비주얼 브릿지 (개발자 B 단독). FighterController 상태를 읽어 캐릭터 애니메이션을 재생한다.
    // 방식: {characterPrefix}Preview.controller(상태 8개)를 런타임 컨트롤러로 재사용 → animator.Play("{prefix}_{clip}").
    //   (이슈 원안의 Animator 파라미터/Any State 방식 대신, 코드에서 상태를 직접 재생 — 결과 동일, 그래프 authoring 불필요.)
    // 좌우 반전은 여기서만 처리: 스프라이트 기본이 오른쪽을 보므로 FacingRight=false일 때 flipX
    //   (FighterController는 위치만 계산하고 시각 반전은 모른다 — 계획서 원칙).
    [RequireComponent(typeof(FighterController))]
    [RequireComponent(typeof(Animator))]
    public sealed class FighterAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private FighterController fighter;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;

        // 상태 이름 접두("IronWall"/"Shadow"/"Master" …). 재생 상태 = "{prefix}_{clip}".
        [SerializeField] private string characterPrefix = "IronWall";

        private string _currentState;

        private void Reset()
        {
            fighter = GetComponent<FighterController>();
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Awake()
        {
            if (fighter == null) fighter = GetComponent<FighterController>();
            if (animator == null) animator = GetComponent<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // 런타임 캐릭터 교체(배틀 프리뷰 셀렉터용). 접두 변경 + 캐시 리셋 → 다음 프레임에 새 컨트롤러 상태로 재생.
        public void SetCharacterPrefix(string prefix)
        {
            characterPrefix = prefix;
            _currentState = null;
        }

        // FighterController.Tick이 Update 타이밍에 상태를 갱신하므로, 그 뒤(LateUpdate)에 시각을 반영한다.
        private void LateUpdate()
        {
            if (fighter == null || animator == null)
                return;

            if (spriteRenderer != null)
                spriteRenderer.flipX = !fighter.FacingRight;

            var state = $"{characterPrefix}_{ClipFor(fighter.State, fighter.CommittedAction)}";
            if (state != _currentState)
            {
                _currentState = state;
                animator.Play(state, 0, 0f); // 전이/파라미터 없이 상태 직접 재생
            }
        }

        // 02장 FighterState(+공격 종류) → 클립 접미사.
        private static string ClipFor(FighterState state, ActionType committed)
        {
            switch (state)
            {
                case FighterState.Idle: return "Idle";
                case FighterState.Move: return "Move";
                case FighterState.Dash: return "Dash";
                case FighterState.HitStun: return "HitStun";
                case FighterState.Down: return "Down";
                case FighterState.AttackStartup:
                case FighterState.AttackActive:
                case FighterState.Recovery:
                    return AttackClip(committed);
                default:
                    return "Idle";
            }
        }

        private static string AttackClip(ActionType committed)
        {
            switch (committed)
            {
                case ActionType.HeavyAttack: return "Attack_Heavy";
                case ActionType.Ultimate: return "Attack_Ultimate";
                default: return "Attack_Light"; // LightAttack 및 예외 케이스
            }
        }
    }
}
