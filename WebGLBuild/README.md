# WebGLBuild — GitHub Pages 배포 폴더

Unity 에서 **WebGL 빌드 결과물을 이 폴더 안에 그대로** 넣으세요.
`main` 브랜치에 push 하면 `.github/workflows/deploy-pages.yml` 가 자동으로 GitHub Pages 에 배포합니다.

빌드 후 이 폴더 구조는 아래처럼 되어야 합니다 (index.html 이 이 폴더 바로 아래에 있어야 함):

```
WebGLBuild/
├─ .nojekyll          ← 지우지 마세요 (Unity 파일이 무시되는 것 방지)
├─ index.html
├─ Build/
│  ├─ *.loader.js
│  ├─ *.framework.js
│  ├─ *.data
│  └─ *.wasm
└─ TemplateData/
```

## Unity 빌드 방법 (요약)
1. Unity Hub → 이 버전(6000.0.38f1)에 **WebGL Build Support** 모듈이 설치돼 있는지 확인 (없으면 Add Modules).
2. File → Build Profiles(또는 Build Settings) → **Web / WebGL** 선택 → Switch Platform.
3. **Player Settings → Publishing Settings → Compression Format = `Disabled`** (또는 `Decompression Fallback` 체크).
   - GitHub Pages 는 `.br`/`.gz` 를 올바른 헤더로 못 줘서, 이걸 안 하면 로딩이 실패합니다.
4. Build → 출력 폴더를 이 `WebGLBuild/` 로 지정하고 빌드.
5. `git add WebGLBuild && git commit && git push origin main`.

## 배포 URL
`https://gameclientdeveloperkimjin.github.io/TeachAndFight/`

(최초 1회: GitHub 저장소 → Settings → Pages → **Source = GitHub Actions** 로 설정해야 워크플로가 배포됩니다.)
