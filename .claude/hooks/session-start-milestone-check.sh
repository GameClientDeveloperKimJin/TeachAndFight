#!/usr/bin/env bash
# 세션 시작 시 GitHub 이슈(#3~#20) open/closed로 현재 W단계 판정.
# 매핑 원본: docs/DEV_SPEC.md 맨 아래 "이슈 매핑" 표 (여기 값은 그 표의 스냅샷).
set -uo pipefail

emit() {
  # 최소한의 JSON 문자열 이스케이프 (외부 jq 의존 없이)
  local s="$1"
  s="${s//\\/\\\\}"
  s="${s//\"/\\\"}"
  printf '{"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"%s"}}' "$s"
}

if ! command -v gh >/dev/null 2>&1; then
  emit "[세션 시작 점검] gh CLI 없음 - 이슈 상태 자동조회 불가, docs/IMPLEMENTATION_PLAN.md '세션 시작 프로토콜' 대로 수동 확인 필요"
  exit 0
fi

open_numbers=" $(gh issue list --state open --json number --jq '.[].number' 2>/dev/null | tr '\n' ' ') "

w1="3 4 5 6"
w2="7 8 9 10 11"
w3="12 13 14 15 16"
w4="17 18 19 20"

current_w=""
report=""
for w in 1 2 3 4; do
  case "$w" in
    1) issues="$w1" ;;
    2) issues="$w2" ;;
    3) issues="$w3" ;;
    4) issues="$w4" ;;
  esac

  w_open=""
  for i in $issues; do
    case "$open_numbers" in
      *" $i "*) w_open="$w_open #$i" ;;
    esac
  done

  if [ -n "$w_open" ]; then
    [ -z "$current_w" ] && current_w="$w"
    report="${report}W${w}:open${w_open}; "
  else
    report="${report}W${w}:closed; "
  fi
done

if [ -z "$current_w" ]; then
  emit "[세션 시작 점검] #3~#20 전체 closed. ($report) 다음 작업 사용자에게 확인."
else
  next_w=$((current_w + 1))
  emit "[세션 시작 점검] 현재 W${current_w} 진행중. ($report) 마일스톤 게이트: W${current_w} 이슈(개발자A+개발자B+공용) 전부 closed 전엔 W${next_w} 코드 작성 금지 - 담당자 무관 예외 없음. 세션 시작 시 이 상태를 사용자에게 먼저 보고할 것."
fi
