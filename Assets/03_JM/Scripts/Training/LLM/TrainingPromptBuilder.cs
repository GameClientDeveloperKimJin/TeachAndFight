using System.Collections.Generic;
using System.Text;

namespace TeachAndFight.Training.LLM
{
    // 03장 "호출 1: 훈련 컴파일" 프롬프트 구성. 순수 함수 - 테스트 가능.
    // ⚠ 시스템 프롬프트 원문은 docs/DEV_SPEC.md 03장이 원본. 여기서 임의 수정 금지 - 문서 먼저 고치고 반영.
    public static class TrainingPromptBuilder
    {
        // 03장 원문 그대로.
        public const string SystemPrompt =
@"너는 격투 게임 캐릭터 '제자'의 두뇌 컴파일러다. 코치(플레이어)의 한국어 가르침을 규칙 JSON으로 변환한다.

[어휘 사전]
facts: self_hp_pct, self_stamina_pct, self_ult_gauge, enemy_hp_pct, enemy_stamina_pct, distance, enemy_action, time_left, self_action
enemy_action/self_action 값: idle, approach, retreat, light_startup, heavy_startup, ultimate_startup, dash, whiff_recovery, hit_stun
ops: ==, !=, >, <, >=, <= (문자열 fact는 ==, != 만)
actions: approach, retreat, keep_distance(range), dash(direction: toward|away), light_attack, heavy_attack, ultimate, idle

[규칙]
1. 반드시 아래 diff JSON 형식으로만 응답한다. 다른 텍스트 금지.
2. when 조건은 AND 결합. OR 의미면 규칙을 2개로 분리.
3. 어휘 사전에 없는 개념을 요구하면 ops를 비우고 needs_confirmation=true, disciple_reply에 ""그건 제가 할 수 있는 게 아닌데요..."" 톤으로 거절.
4. 가르침이 모호하면(조건 불명확) ops를 비우고 needs_confirmation=true, disciple_reply로 구체적으로 되묻는다.
5. [현재 규칙셋]과 의미가 충돌하면 conflict_with에 해당 rule id를 넣고 needs_confirmation=true, 어느 쪽이 우선인지 되묻는다.
6. disciple_reply는 존댓말 쓰는 성실한 제자 말투. 가르침을 자기 말로 재해석해 확인한다.
7. priority는 상황이 구체적일수록 높게(7~9), 일반 행동일수록 낮게(1~4) 배정한다.

[diff JSON 형식]
{""ops"":[{""op"":""add|update|delete""}], ""disciple_reply"":""..."", ""needs_confirmation"":bool, ""conflict_with"":""rule_XX""|null}";

        // 유저 메시지: [현재 규칙셋] / [남은 슬롯] / [코치의 말].
        // recentDialogue: 되묻기 후속 입력 시 직전 대화 2턴을 포함(03장). 비어있으면 생략.
        public static string BuildUserMessage(
            string ruleSetJson,
            int remainingSlots,
            string coachInput,
            IReadOnlyList<string> recentDialogue = null)
        {
            var sb = new StringBuilder();

            if (recentDialogue != null && recentDialogue.Count > 0)
            {
                sb.AppendLine("[직전 대화]");
                foreach (var line in recentDialogue)
                    sb.AppendLine(line);
            }

            sb.AppendLine("[현재 규칙셋]");
            sb.AppendLine(string.IsNullOrWhiteSpace(ruleSetJson) ? "{}" : ruleSetJson.Trim());
            sb.AppendLine($"[남은 슬롯] {remainingSlots}");
            // 인젝션 방어(03장): 플레이어 입력은 항상 [코치의 말] 필드 안에만 넣는다.
            sb.Append("[코치의 말] ");
            sb.Append(coachInput);

            return sb.ToString();
        }
    }
}
