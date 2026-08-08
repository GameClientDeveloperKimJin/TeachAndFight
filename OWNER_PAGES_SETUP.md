# [요청] GitHub Pages 배포 켜주세요 (클릭 1번)

안녕하세요. 해커톤 제출용 **웹 플레이 링크**를 위해 GitHub Pages 배포를 켜야 하는데,
이건 저장소 **owner(관리자) 권한**이 있어야 할 수 있어서 요청드립니다.
**설정 한 곳만 바꾸면 끝**이고, 나머지(빌드 파일·배포 워크플로·서버 CORS)는 제가 이미 다 올려놨습니다.

---

## 해주실 것 — 딱 이거 하나

1. 아래 링크로 이동 (또는 저장소 → **Settings → Pages**)

   👉 https://github.com/GameClientDeveloperKimJin/TeachAndFight/settings/pages

2. **"Build and deployment"** 항목에서 **Source** 를 **`GitHub Actions`** 로 선택

   - 기본값이 `Deploy from a branch` 로 되어 있을 텐데, 그걸 **`GitHub Actions`** 로 바꿔주시면 됩니다.
   - 저장 버튼이 따로 없고, 선택하는 즉시 적용됩니다.

**이게 전부입니다.** 다른 설정은 건드릴 필요 없습니다.

---

## 바꾸면 자동으로 일어나는 일

- `main` 브랜치의 `WebGLBuild/` 폴더(이미 push 완료)를 `.github/workflows/deploy-pages.yml` 워크플로가 자동으로 GitHub Pages에 배포합니다.
- 1~2분 뒤 아래 주소에서 게임이 열립니다:

  **https://gameclientdeveloperkimjin.github.io/TeachAndFight/**

- 배포 진행 상황은 저장소 **Actions 탭 → "Deploy WebGL to GitHub Pages"** 에서 확인할 수 있습니다. (초록불이면 성공)

---

## 참고 — 제가 이미 끝낸 작업 (건드리실 것 없음)

- ✅ Unity WebGL 빌드 완료 → `WebGLBuild/` 폴더에 커밋, `main` 에 push
- ✅ GitHub Pages 자동배포 워크플로 추가 (`.github/workflows/deploy-pages.yml`)
- ✅ WebGL 로딩용 `.nojekyll` 포함 (Unity 파일 무시 방지)
- ✅ LLM 프록시 서버 CORS 설정 — 위 github.io 주소에서 호출 허용

즉, **Source 를 `GitHub Actions` 로 바꾸는 것만** 해주시면 웹 제출 링크가 바로 살아납니다.
감사합니다! 🙏
