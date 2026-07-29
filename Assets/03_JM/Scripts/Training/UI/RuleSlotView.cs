using System;
using TeachAndFight.Core.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace TeachAndFight.Training.UI
{
    // 규칙 슬롯 한 칸: [R1 궁 회피  P8  ×]. 라벨/우선순위 뱃지/삭제 버튼.
    // 항목 클릭 시 source_utterance 툴팁(있으면) — 툴팁 표시는 배선에서 tooltipTarget에 연결.
    public sealed class RuleSlotView : MonoBehaviour
    {
        [SerializeField] private Text labelText;
        [SerializeField] private Text priorityBadge;
        [SerializeField] private Button deleteButton;

        private string _ruleId;
        private Action<string> _onDelete;

        // 이 슬롯이 표현하는 규칙의 source_utterance(툴팁용). 없으면 빈 문자열.
        public string SourceUtterance { get; private set; }

        public void Bind(Rule rule, Action<string> onDelete)
        {
            _ruleId = rule.Id;
            _onDelete = onDelete;
            SourceUtterance = rule.SourceUtterance ?? string.Empty;

            if (labelText != null) labelText.text = rule.Label;
            if (priorityBadge != null) priorityBadge.text = $"P{rule.Priority}";

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(() => _onDelete?.Invoke(_ruleId));
            }
        }
    }
}
