#!/usr/bin/env bash
# 세션 시작 시 팀원에게 './start' 실행 안내만 표시.
# 실제 W단계/브랜치 진행 상태 점검 로직은 저장소 루트 ./start.sh 참고.
set -uo pipefail

esc() {
  local s="$1"
  s="${s//\\/\\\\}"
  s="${s//\"/\\\"}"
  printf '%s' "$s"
}

msg="[세션 시작 안내] '/gogo' 입력해서 현재 W단계 진행 상태 + 팀원 브랜치 상태 확인할 것. (gh CLI 필요)"
printf '{"systemMessage":"%s","hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"%s"}}' "$(esc "$msg")" "$(esc "$msg")"
