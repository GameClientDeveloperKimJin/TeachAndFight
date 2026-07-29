#!/usr/bin/env bash
# 수동 실행용: 세션 시작 시 팀원 각자 `./start` 입력해서 W단계 + 브랜치 진행 상태 확인.
# 이슈 매핑 원본: docs/DEV_SPEC.md 맨 아래 "이슈 매핑" 표 (여기 값은 그 표의 스냅샷).
set -uo pipefail

if ! command -v gh >/dev/null 2>&1; then
  echo "[세션 시작 점검] gh CLI 없음 - 이슈 상태 자동조회 불가, docs/IMPLEMENTATION_PLAN.md '세션 시작 프로토콜' 대로 수동 확인 필요"
  exit 0
fi

open_numbers=" $(gh issue list --state open --json number --jq '.[].number' 2>/dev/null | tr '\n' ' ') "

w1="3 4 5 6"
w2="7 8 9 10 11"
w3="12 13 14 15 16"
w4="17 18 19 20"

current_w=""
issue_report=""
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
    issue_report="${issue_report}W${w}:open${w_open}
"
  else
    issue_report="${issue_report}W${w}:closed
"
  fi
done

# --- 팀원 브랜치(dev_*) 진행 상태 ---
branch_report=""
if command -v git >/dev/null 2>&1 && git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  git fetch origin --quiet 2>/dev/null || true

  base="origin/develop"
  git rev-parse --verify "$base" >/dev/null 2>&1 || base="origin/main"

  for ref in $(git for-each-ref --format='%(refname:short)' 'refs/remotes/origin/dev_*' 2>/dev/null); do
    name="${ref#origin/}"
    ahead=$(git rev-list --count "$base..$ref" 2>/dev/null || echo "?")
    last_date=$(git log -1 --format='%ci' "$ref" 2>/dev/null | cut -c1-10)
    last_subject=$(git log -1 --format='%s' "$ref" 2>/dev/null)
    if [ "$ahead" = "0" ]; then
      branch_report="${branch_report}${name}: develop 대비 진전 없음(마지막 ${last_date} '${last_subject}')
"
    else
      branch_report="${branch_report}${name}: +${ahead}커밋(마지막 ${last_date} '${last_subject}')
"
    fi
  done
fi

if [ -z "$branch_report" ]; then
  branch_report="(팀원 브랜치 조회 불가 또는 dev_* 브랜치 없음)"
fi

# --- 담당자별 TODO (이슈 제목 [A]/[B]/[공용] 태그 기준, DEV_SPEC.md 매핑: 개발자A=KJ, 개발자B=JM) ---
a_todo=""
b_todo=""
common_todo=""
while IFS=$'\t' read -r num title; do
  [ -z "$num" ] && continue
  case "$title" in
    *"[A]"*) a_todo="${a_todo}  #${num} ${title}
" ;;
    *"[B]"*) b_todo="${b_todo}  #${num} ${title}
" ;;
    *"[공용]"*) common_todo="${common_todo}  #${num} ${title}
" ;;
  esac
done <<EOF
$(gh issue list --state open --json number,title --jq '.[] | "\(.number)\t\(.title)"' 2>/dev/null)
EOF

echo "===== 세션 시작 점검 ====="
echo "--- 이슈 상태 ---"
printf '%s' "$issue_report"
echo "--- 브랜치 진행 상태 ---"
printf '%s' "$branch_report"
echo "--- 담당자별 TODO (개발자A=KJ, 개발자B=JM) ---"
if [ -n "$a_todo" ]; then
  echo "개발자A(KJ):"
  printf '%s' "$a_todo"
else
  echo "개발자A(KJ): 없음 (open 이슈 없음, 대기)"
fi
if [ -n "$b_todo" ]; then
  echo "개발자B(JM):"
  printf '%s' "$b_todo"
else
  echo "개발자B(JM): 없음 (open 이슈 없음, 대기)"
fi
if [ -n "$common_todo" ]; then
  echo "공용:"
  printf '%s' "$common_todo"
fi

if [ -z "$current_w" ]; then
  echo "이슈: #3~#20 전체 closed. 다음 작업 확인 필요."
else
  next_w=$((current_w + 1))
  echo "현재 W${current_w} 진행중."
  echo "마일스톤 게이트: W${current_w}의 이슈+실작업(개발자A+개발자B+공용) 전부 끝나기 전엔 W${next_w} 코드 작성 금지 - 담당자 무관 예외 없음."
  echo "브랜치 진전 없는 팀원이 있으면 이슈 open여부와 무관하게 그 사람 몫이 안 끝난 것으로 간주."
fi
