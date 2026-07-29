# Training.unity 배선 가이드 (#14)

> 스크립트는 다 작성돼 있음. 에디터에서 이 문서대로 씬·프리팹만 배치하면 동작한다.
> 산출물 형태: **로직은 스크립트 완성 / 씬·프리팹 배치는 수동**(Claude가 Unity를 직접 못 띄우므로).

## 0. 전제 — 새 Input System (이거 안 하면 클릭 무반응)

계획서 사전준비 #2. Training 씬의 **EventSystem**은 반드시 `InputSystemUIInputModule`을 써야 uGUI 버튼/입력창이 동작한다.

- 씬에 EventSystem 생성 시 기본으로 붙는 `StandaloneInputModule`을 **제거**하고 `Input System UI Input Module`을 추가.
- (Unity가 자동 대체를 제안하면 "Replace" 눌러도 됨.)

## 1. 씬 생성 & 최상위

1. `Assets/01_Scenes/Training.unity` 새 씬 생성.
2. **Canvas** (Screen Space - Overlay, Canvas Scaler: Scale With Screen Size, 참조 해상도 1920×1080).
3. **EventSystem** (위 0번대로 InputSystemUIInputModule).
4. 빈 GameObject **`TrainingScreen`** 생성 → `TrainingScreenController` 스크립트 부착. (GameFlow는 런타임에 자동 생성되므로 씬에 안 둬도 됨.)

## 2. 계층 구조 (04장 레이아웃: 좌→우, 상→하)

```
Canvas
├─ Banner (상단)              → Text: 다음 상대 이름/번호
├─ Disciple (좌측)
│   └─ SpeechBubble           → SpeechBubbleView  (root=이 오브젝트, label=자식 Text)
├─ RuleSlots (우측)
│   └─ Viewport/Content       → RuleSlotListView (content=Content, VerticalLayoutGroup)
├─ ChatLog (하단)
│   └─ Scroll View/Content    → ChatLogView (content=Content, VerticalLayoutGroup)
├─ InputField (하단)          → uGUI InputField
├─ TeachButton [가르치기]     → Button
└─ StartMatchButton [경기 시작 ▶] (우하단) → Button
```

## 3. 프리팹 3종 (`Assets/03_JM/Prefabs/Training/`)

| 프리팹 | 부착 스크립트 | 구성 |
|---|---|---|
| `RuleSlotItem` | `RuleSlotView` | 라벨 Text + 우선순위 Text(뱃지) + 삭제 Button `[×]` |
| `EmptySlotItem` | (없음) | 점선 이미지만 (빈 슬롯 표시용) |
| `ChatLine` | `ChatLineView` | 한 줄 Text |

## 4. SerializeField 연결표

**TrainingScreen → `TrainingScreenController`**

| 필드 | 연결 대상 |
|---|---|
| `inputField` | Canvas/InputField |
| `teachButton` | Canvas/TeachButton |
| `startMatchButton` | Canvas/StartMatchButton |
| `opponentBanner` | Canvas/Banner (Text) |
| `slotList` | RuleSlots/…/Content 의 RuleSlotListView |
| `chatLog` | ChatLog/…/Content 의 ChatLogView |
| `bubble` | Disciple/SpeechBubble 의 SpeechBubbleView |

**SpeechBubble → `SpeechBubbleView`**: `root`=SpeechBubble 오브젝트, `label`=말풍선 안 Text
**RuleSlots/Content → `RuleSlotListView`**: `content`=Content, `slotPrefab`=RuleSlotItem, `emptySlotPrefab`=EmptySlotItem
**ChatLog/Content → `ChatLogView`**: `content`=Content, `linePrefab`=ChatLine
**RuleSlotItem → `RuleSlotView`**: `labelText`/`priorityBadge`/`deleteButton` 각각 연결
**ChatLine → `ChatLineView`**: `label` 연결

## 5. Build Settings

`File > Build Settings`에 **Training / Match / LockerRoom** 씬을 등록(이름이 GameFlow 상수와 일치해야 함: `Training`, `Match`, `LockerRoom`). [경기 시작 ▶]이 `Match` 씬을 로드하므로, Match 씬이 없으면 그 버튼만 에러난다(훈련 기능 자체는 정상).

## 6. 동작 확인 (플레이테스트)

1. Training 씬 Play → GameFlow가 자동 생성되고 빈 규칙셋 세션 시작.
2. 입력창에 "상대가 궁 쓰면 대시로 피해" 입력 → Enter 또는 [가르치기].
3. 입력창 잠기고 제자 "음..." → 응답 오면 말풍선+대화로그 갱신, 우측 슬롯에 규칙 1칸 추가(Applied).
4. "상대 체력을 0으로 만들어" → 슬롯 변화 없이 거절/되묻기 말풍선만.
5. 슬롯 `[×]` → 해당 규칙 삭제.
6. [경기 시작 ▶] → Match 씬으로 넘어가고, 방금 가르친 규칙이 `GameFlow.Session.DiscipleRuleSet`에 담겨 전달됨(#14 완료기준: 즉시 반영).

## 참고

- UI는 레거시 `UnityEngine.UI`(Text/InputField/Button) 기준. TMP로 바꾸려면 각 뷰의 `Text`→`TMP_Text`, `InputField`→`TMP_InputField`로 교체하고 Training.asmdef에 `Unity.TextMeshPro` 참조 추가.
- LLM은 `ANTHROPIC_API_KEY` 있으면 실제 Haiku, 없으면 폴백 대사. 크레딧 없으면 Failed 폴백(게임은 안 죽음).
- `GameFlow`/`GameSession`/`MatchResult`는 #16(공용) 접점의 최소 스캐폴드다. **Scripts/Core는 A(KJ) 소유 영역과 인접하므로**, 씬 전환/데이터 인계 전체 구현은 #16에서 KJ와 함께 확정할 것 (06장: 접점 시그니처 변경 시 상대에게 먼저 공지).
