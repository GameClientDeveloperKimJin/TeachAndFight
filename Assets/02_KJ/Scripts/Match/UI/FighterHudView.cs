using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TeachAndFight.Combat;

namespace TeachAndFight.Match.UI
{
    // 파이터 1명 몫의 HUD: 이름 + HP/스태미나 바 + 머리 위 발동 규칙 라벨(04장: 0.3s 스케일 펀치).
    public sealed class FighterHudView : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private Text ruleLabelText;

        private Coroutine punchRoutine;

        public void SetName(string fighterName)
        {
            if (nameText != null)
                nameText.text = fighterName;
        }

        public void Refresh(FighterController fighter)
        {
            if (fighter == null)
                return;
            if (hpSlider != null) hpSlider.value = fighter.HpPct / 100f;
            if (staminaSlider != null) staminaSlider.value = fighter.StaminaPct / 100f;
        }

        public void ShowRuleLabel(string label)
        {
            if (ruleLabelText == null)
                return;
            ruleLabelText.text = label;
            if (punchRoutine != null)
                StopCoroutine(punchRoutine);
            punchRoutine = StartCoroutine(PunchScale());
        }

        private IEnumerator PunchScale()
        {
            const float duration = 0.3f;
            var rt = ruleLabelText.rectTransform;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / duration);
                float scale = 1f + 0.3f * Mathf.Sin(progress * Mathf.PI); // 튀었다 복귀하는 펀치 곡선
                rt.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            punchRoutine = null;
        }
    }
}
