# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 6 (6000.0.38f1) 2D project, URP render pipeline, new Input System (`Assets/InputSystem_Actions.inputactions`).
No git repo initialized yet. No gameplay scripts written yet — project is a fresh 2D template.

## Conventions

- Organize `Assets/Scripts/` by feature (e.g. `Scripts/Combat/`, `Scripts/Teaching/`), not by type.

## Workflow

- 세션 시작 시(개발자 A/B 누구든) `docs/IMPLEMENTATION_PLAN.md`의 "AI 세션 시작 프로토콜" 섹션을 따라 GitHub 이슈(#3~#20) 상태와 git 브랜치를 조회해 현재 W단계를 파악하고 먼저 사용자에게 보고할 것.
- 직전 Wn의 이슈(개발자A + 개발자B + 공용 전부)가 하나라도 open이면 Wn+1 작업은 절대 시작하지 않는다 — 내 담당(A)이 다 끝났어도 팀원(B) 담당이 안 끝났으면 동일하게 막는다. 예외 없음.
