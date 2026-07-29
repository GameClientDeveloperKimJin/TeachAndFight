using System;
using TeachAndFight.Core.Rules;
using UnityEngine;

namespace TeachAndFight.Training.UI
{
    // 규칙 슬롯 리스트. 채워진 규칙은 RuleSlotView로, 남은 칸은 점선 빈 슬롯으로 그린다.
    public sealed class RuleSlotListView : MonoBehaviour
    {
        [SerializeField] private Transform content;         // 슬롯들이 놓이는 부모(Vertical Layout Group 권장)
        [SerializeField] private RuleSlotView slotPrefab;   // 채워진 규칙 슬롯
        [SerializeField] private GameObject emptySlotPrefab; // 점선 빈 슬롯(선택)

        // 규칙셋 상태로 리스트 전체를 다시 그린다. onDelete는 [×] 콜백.
        public void Render(RuleSet ruleSet, Action<string> onDelete)
        {
            if (content == null || ruleSet == null)
                return;

            // 기존 자식 제거(간단·안전하게 전체 재생성 — 슬롯 수가 최대 8이라 비용 무시 가능).
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            if (slotPrefab != null)
            {
                foreach (var rule in ruleSet.Rules)
                {
                    var slot = Instantiate(slotPrefab, content);
                    slot.Bind(rule, onDelete);
                }
            }

            // 남은 슬롯을 점선 빈 칸으로 채움.
            if (emptySlotPrefab != null)
            {
                int empty = Mathf.Max(0, ruleSet.MaxSlots - ruleSet.Rules.Count);
                for (int i = 0; i < empty; i++)
                    Instantiate(emptySlotPrefab, content);
            }
        }
    }
}
