using System.Collections.Generic;
using UnityEngine;

namespace TeachAndFight.Combat
{
    // 배틀 프리뷰용 캐릭터 셀렉터(#22 평가 도구): 오토배틀 중 A/B 파이터의 캐릭터
    // (스프라이트 + Animator 컨트롤러 + 브릿지 접두)를 실시간으로 바꿔 6종을 비교/판단한다.
    public sealed class BridgeBattleSelector : MonoBehaviour
    {
        [System.Serializable]
        public sealed class CharacterOption
        {
            public string name;
            public RuntimeAnimatorController controller;
            public Sprite idleSprite;
        }

        [SerializeField] private FighterAnimatorBridge bridgeA;
        [SerializeField] private FighterAnimatorBridge bridgeB;
        [SerializeField] private List<CharacterOption> options = new List<CharacterOption>();

        private void Apply(FighterAnimatorBridge bridge, CharacterOption opt)
        {
            if (bridge == null || opt == null)
                return;

            var animator = bridge.GetComponent<Animator>();
            if (animator != null)
                animator.runtimeAnimatorController = opt.controller;

            var sr = bridge.GetComponent<SpriteRenderer>();
            if (sr != null && opt.idleSprite != null)
                sr.sprite = opt.idleSprite;

            bridge.SetCharacterPrefix(opt.name);
        }

        private void OnGUI()
        {
            const int w = 220;
            GUILayout.BeginArea(new Rect(Screen.width - w - 16, 16, w, 460), GUI.skin.box);
            GUILayout.Label("캐릭터 교체 (Play 중 실시간)");
            DrawColumn("A (좌측 파이터)", bridgeA);
            GUILayout.Space(10);
            DrawColumn("B (우측 파이터)", bridgeB);
            GUILayout.EndArea();
        }

        private void DrawColumn(string title, FighterAnimatorBridge bridge)
        {
            GUILayout.Label(title);
            foreach (var opt in options)
                if (opt != null && !string.IsNullOrEmpty(opt.name) && GUILayout.Button(opt.name))
                    Apply(bridge, opt);
        }
    }
}
