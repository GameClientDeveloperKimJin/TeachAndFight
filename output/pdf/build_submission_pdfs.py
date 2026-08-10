from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    Image,
    KeepTogether,
    ListFlowable,
    ListItem,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output" / "pdf"
INTRO_ASSETS = Path(
    r"C:\Users\Main\Downloads\게임 소개 및 설명 문서(미완)\ExportBlock-68c2900d-201f-49ec-ab24-1abaec40025f-Part-1"
)
AI_ASSETS = Path(
    r"C:\Users\Main\Downloads\AI 활용 문서\ExportBlock-f23024a5-9df7-44bd-a56b-37f4d2c8ece7-Part-1"
)

PLAY_URL = "https://gameclientdeveloperkimjin.github.io/TeachAndFight/"
GITHUB_URL = "https://github.com/GameClientDeveloperKimJin/TeachAndFight"
YOUTUBE_URL = "https://youtu.be/0Yznc7Fgc3g"
REVIEWER = "dl_gameai_reviewer@nhn.com"

PAGE_W, PAGE_H = A4
MARGIN_X = 18 * mm
MARGIN_TOP = 18 * mm
MARGIN_BOTTOM = 16 * mm


def register_fonts():
    pdfmetrics.registerFont(TTFont("KR", r"C:\Windows\Fonts\malgun.ttf"))
    pdfmetrics.registerFont(TTFont("KR-Bold", r"C:\Windows\Fonts\malgunbd.ttf"))


def styles():
    base = getSampleStyleSheet()
    base.add(
        ParagraphStyle(
            name="CoverTitle",
            fontName="KR-Bold",
            fontSize=24,
            leading=31,
            alignment=TA_CENTER,
            textColor=colors.HexColor("#141821"),
            spaceAfter=8,
        )
    )
    base.add(
        ParagraphStyle(
            name="DocTitle",
            fontName="KR-Bold",
            fontSize=19,
            leading=25,
            textColor=colors.HexColor("#141821"),
            spaceAfter=8,
        )
    )
    base.add(
        ParagraphStyle(
            name="SubtitleKR",
            fontName="KR",
            fontSize=10.5,
            leading=16,
            alignment=TA_CENTER,
            textColor=colors.HexColor("#495160"),
            spaceAfter=18,
        )
    )
    base.add(
        ParagraphStyle(
            name="H1KR",
            fontName="KR-Bold",
            fontSize=15,
            leading=20,
            textColor=colors.HexColor("#1A2130"),
            spaceBefore=10,
            spaceAfter=7,
        )
    )
    base.add(
        ParagraphStyle(
            name="H2KR",
            fontName="KR-Bold",
            fontSize=11.5,
            leading=16,
            textColor=colors.HexColor("#273044"),
            spaceBefore=7,
            spaceAfter=4,
        )
    )
    base.add(
        ParagraphStyle(
            name="BodyKR",
            fontName="KR",
            fontSize=9.4,
            leading=14,
            textColor=colors.HexColor("#202633"),
            spaceAfter=4,
        )
    )
    base.add(
        ParagraphStyle(
            name="SmallKR",
            fontName="KR",
            fontSize=8.2,
            leading=12,
            textColor=colors.HexColor("#525A69"),
        )
    )
    base.add(
        ParagraphStyle(
            name="CellKR",
            fontName="KR",
            fontSize=8.2,
            leading=11,
            textColor=colors.HexColor("#202633"),
        )
    )
    base.add(
        ParagraphStyle(
            name="CellHeadKR",
            fontName="KR-Bold",
            fontSize=8.4,
            leading=11,
            textColor=colors.white,
        )
    )
    base.add(
        ParagraphStyle(
            name="CalloutKR",
            fontName="KR",
            fontSize=9,
            leading=14,
            leftIndent=7,
            rightIndent=7,
            borderColor=colors.HexColor("#D7DEEA"),
            borderWidth=0.7,
            borderPadding=7,
            backColor=colors.HexColor("#F6F8FC"),
            textColor=colors.HexColor("#283142"),
            spaceBefore=4,
            spaceAfter=8,
        )
    )
    return base


S = None


def p(text, style="BodyKR"):
    return Paragraph(text, S[style])


def h(text):
    return Paragraph(text, S["H1KR"])


def h2(text):
    return Paragraph(text, S["H2KR"])


def bullets(items):
    return ListFlowable(
        [ListItem(p(item), leftIndent=10) for item in items],
        bulletType="bullet",
        start="circle",
        leftIndent=14,
        bulletFontName="KR",
        bulletFontSize=7,
        bulletColor=colors.HexColor("#3D5AFE"),
    )


def check(items):
    rows = []
    for done, text in items:
        mark = "완료" if done else "확인 필요"
        rows.append([p(mark, "CellKR"), p(text, "CellKR")])
    table = Table(rows, colWidths=[24 * mm, 142 * mm])
    table.setStyle(
        TableStyle(
            [
                ("GRID", (0, 0), (-1, -1), 0.25, colors.HexColor("#D9DEE9")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("BACKGROUND", (0, 0), (0, -1), colors.HexColor("#EEF3FF")),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
                ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 5),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
            ]
        )
    )
    return table


def table(headers, rows, col_widths):
    data = [[p(x, "CellHeadKR") for x in headers]]
    data.extend([[p(str(x), "CellKR") for x in row] for row in rows])
    t = Table(data, colWidths=col_widths, repeatRows=1)
    t.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#25304A")),
                ("GRID", (0, 0), (-1, -1), 0.25, colors.HexColor("#D8DEEA")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
                ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 5),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#FAFBFE")]),
            ]
        )
    )
    return t


def scaled_image(path, width_mm=166):
    img = Image(str(path))
    max_w = width_mm * mm
    ratio = max_w / img.imageWidth
    img.drawWidth = max_w
    img.drawHeight = img.imageHeight * ratio
    return img


def header_footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("KR", 7.5)
    canvas.setFillColor(colors.HexColor("#737B8C"))
    canvas.drawString(MARGIN_X, 10 * mm, "TEACH & FIGHT 제출 문서")
    canvas.drawRightString(PAGE_W - MARGIN_X, 10 * mm, f"{doc.page}")
    canvas.restoreState()


def build_pdf(filename, title, subtitle, body):
    path = OUT / filename
    doc = SimpleDocTemplate(
        str(path),
        pagesize=A4,
        leftMargin=MARGIN_X,
        rightMargin=MARGIN_X,
        topMargin=MARGIN_TOP,
        bottomMargin=MARGIN_BOTTOM,
        title=title,
        author="TEACH & FIGHT Team",
    )
    story = [Paragraph(title, S["DocTitle"])]
    if subtitle:
        story.append(Paragraph(subtitle, S["SmallKR"]))
        story.append(Spacer(1, 6))
    story.extend(body)
    doc.build(story, onFirstPage=header_footer, onLaterPages=header_footer)
    return path


def submission_summary():
    body = []
    body.append(p("AI 게임 제작 해커톤 제출용으로 제출 항목 5개를 정리한 체크 문서입니다.", "CalloutKR"))
    body.append(h("1. 제출물 목록"))
    body.append(
        table(
            ["번호", "제출물", "제출 형태", "비고"],
            [
                ["1", "플레이 가능한 빌드 및 소스 코드", "웹 브라우저 실행 + 전체 소스", "GitHub Pages 링크"],
                ["2", "플레이 동영상", "30-60초 실제 플레이 영상", "YouTube 링크"],
                ["3", "게임 소개 및 설명 문서", "게임 개요, 플레이 방법, 실행 방법", "PDF"],
                ["4", "AI 활용 기술 문서", "AI 도구, 프롬프트, 활용 내역", "PDF"],
                ["5", "팀원 롤 기술서", "팀원별 역할과 담당 영역", "PDF, 개인 참여 시 생략 가능"],
            ],
            [13 * mm, 45 * mm, 55 * mm, 53 * mm],
        )
    )
    body.append(h("2. 제출 링크"))
    body.append(bullets([f"웹 빌드: {PLAY_URL}", f"전체 소스: {GITHUB_URL}", f"플레이 영상: {YOUTUBE_URL}"]))
    body.append(h("3. 최종 체크"))
    body.append(
        check(
            [
                (True, "웹 빌드와 전체 소스 링크가 준비되어 있음"),
                (True, "YouTube 플레이 영상 링크가 준비되어 있음"),
                (True, "3, 4, 5번 제출용 PDF를 생성함"),
                (False, "저장소 public 여부 확인. 비공개 제출 시 심사 계정 초대: " + REVIEWER),
                (False, "시크릿 창 또는 캐시 없는 브라우저에서 웹 링크 실행 확인"),
                (False, "한글 입력, 한글 표시, LLM 제자 대화 정상 동작 확인"),
            ]
        )
    )
    return build_pdf("0_제출물_정리.pdf", "TEACH & FIGHT - 제출물 정리", "AI 게임 제작 해커톤 제출용", body)


def game_intro():
    body = []
    body.append(p("자연어로 가르친 규칙대로 싸우는 AI 제자 코칭 배틀입니다. 플레이어는 직접 조작 대신 훈련실에서 한국어 문장으로 제자에게 규칙을 가르치고, 제자는 그 규칙에 따라 자동으로 1대1 전투를 수행합니다.", "CalloutKR"))
    body.append(h("1. 게임 제목 및 한 줄 소개"))
    body.append(bullets(["게임 제목: TEACH & FIGHT", "한 줄 소개: 자연어 가르침을 전투 규칙으로 바꾸어 제자를 성장시키는 AI 코칭 격투 게임"]))
    body.append(h("2. 게임 목표"))
    body.append(p("훈련실에서 제자에게 상황별 행동 규칙을 가르친 뒤, 5명의 상대를 순서대로 격파하는 것이 목표입니다. 경기 중 플레이어는 직접 공격하거나 이동하지 않고, 사전에 가르친 규칙의 품질로 승패를 가릅니다."))
    body.append(h("3. 조작 및 플레이 방법"))
    body.append(
        bullets(
            [
                "훈련실 채팅창에 한국어 문장으로 가르침을 입력합니다. 예: '상대가 궁 쓰면 뒤로 대시해', '거리 1.5보다 멀면 접근해'.",
                "LLM이 자연어를 조건(when)과 행동(do)으로 이루어진 규칙으로 컴파일합니다.",
                "규칙 슬롯에서 현재 규칙과 우선순위를 확인하고, 필요 없는 규칙은 삭제할 수 있습니다.",
                "경기 시작 후 제자는 RuleEvaluator가 0.1초 단위로 판단한 규칙에 따라 자동 전투를 합니다.",
                "락커룸에서는 경기 결과, 규칙 발동 통계, 제자의 회고를 확인하고 다음 상대에 대비합니다.",
            ]
        )
    )
    body.append(h("4. 화면 흐름"))
    body.append(
        table(
            ["화면", "역할", "주요 기능"],
            [
                ["훈련실", "가르침 입력", "자연어 입력, 규칙 슬롯 확인, 경기 시작"],
                ["경기", "자동 전투", "HP/스태미나/타이머 표시, 규칙 라벨 표시, 배속 및 일시정지"],
                ["락커룸", "피드백", "승패 결과, 규칙 발동 통계, 제자 회고, 재도전 또는 다음 상대"],
            ],
            [27 * mm, 35 * mm, 104 * mm],
        )
    )
    body.append(Spacer(1, 5))
    body.append(KeepTogether([h2("훈련실 예시"), scaled_image(INTRO_ASSETS / "훈련실.png", width_mm=150)]))
    body.append(Spacer(1, 5))
    body.append(KeepTogether([h2("결과 화면 예시"), scaled_image(INTRO_ASSETS / "결과.png", width_mm=150)]))
    body.append(h("5. 종료 조건"))
    body.append(
        bullets(
            [
                "승리: 상대 HP를 먼저 0으로 만듭니다.",
                "패배: 제자 HP가 먼저 0이 되거나, 제한 시간 60초 안에 승리하지 못합니다.",
                "최종 클리어: 러쉬, 철벽, 그림자, 카멜레온, 사범까지 총 5명의 상대를 모두 격파합니다.",
            ]
        )
    )
    body.append(h("6. 실행 방법"))
    body.append(
        bullets(
            [
                f"웹 브라우저에서 플레이 링크 접속: {PLAY_URL}",
                "별도 설치나 유료 라이선스 없이 실행할 수 있습니다.",
                "PC 실행 파일(.exe)이 아닌 WebGL 웹 빌드로 제출합니다.",
                f"플레이 영상 링크: {YOUTUBE_URL}",
            ]
        )
    )
    return build_pdf("3_게임_소개_및_설명_문서.pdf", "3. 게임 소개 및 설명 문서", "게임 개요, 플레이 방법, 실행 방법", body)


def ai_tech():
    body = []
    body.append(p("이 프로젝트의 AI 활용은 두 층으로 나뉩니다. 첫째, 개발 도구로서 AI 코딩 에이전트를 사용해 구현, 디버깅, 문서화, 배포 문제 해결을 수행했습니다. 둘째, 게임 기능으로서 LLM이 플레이어의 자연어 가르침을 규칙 JSON으로 컴파일하고 경기 후 회고를 생성합니다.", "CalloutKR"))
    body.append(h("1. AI 활용 개요"))
    body.append(
        table(
            ["구분", "사용 도구", "활용 내용", "최종 판단"],
            [
                ["개발 도구", "Claude Code", "Unity C# 구현, 리팩터링, 테스트 작성, 오류 분석, 문서화", "개발자가 스펙 확정, diff 검토, 최종 검증"],
                ["제품 기능", "Anthropic Claude API (Haiku 계열)", "훈련실 자연어 규칙 컴파일, 락커룸 경기 회고 생성", "RuleValidator와 플레이테스트로 검증"],
                ["아트 보조", "이미지 생성 AI, ChatGPT", "캐릭터 콘셉트와 스프라이트 제작 보조", "라이선스와 사용 가능 범위 확인"],
            ],
            [24 * mm, 38 * mm, 67 * mm, 37 * mm],
        )
    )
    body.append(h("2. 제품 내 AI 구조"))
    body.append(
        bullets(
            [
                "훈련 컴파일: 플레이어의 한국어 입력을 diff JSON 형식의 규칙 변경분으로 변환합니다.",
                "검증 단계: C# RuleValidator가 fact, op, action, priority, slot 제한을 최종 확인합니다.",
                "전투 루프: LLM은 전투 중 호출되지 않습니다. 전투는 로컬 RuleEvaluator가 0.1초 단위로 판단합니다.",
                "경기 회고: EventLog 요약과 규칙 발동 통계를 바탕으로 제자 말투의 3문장 이내 회고를 생성합니다.",
            ]
        )
    )
    body.append(KeepTogether([h2("AI 활용 프로세스"), scaled_image(AI_ASSETS / "AI 활용 프로세스.png")]))
    body.append(h("3. 핵심 프롬프트 지시 사항"))
    body.append(
        bullets(
            [
                "응답은 JSON only로 제한하고, add/update/delete diff 포맷을 강제합니다.",
                "조건은 AND 결합만 허용하며, 가르침 1번은 규칙 1개로 처리합니다.",
                "어휘 사전에 없는 fact/action은 거절하거나 되묻습니다.",
                "needs_confirmation=true인 경우 규칙을 적용하지 않고 제자 대사만 표시합니다.",
                "적용 완료 대사는 평서문 또는 완료형으로 끝내고, 되묻기는 의문형으로 끝내도록 구분했습니다.",
                "프롬프트 인젝션은 LLM 지시뿐 아니라 C# 화이트리스트 검증으로 최종 차단합니다.",
            ]
        )
    )
    body.append(h("4. 대표 활용 사례"))
    body.append(
        table(
            ["사례", "문제", "AI 활용", "결과"],
            [
                ["LLM 응답-스키마 불일치", "실제 API 응답이 rule_id/then 등 다른 필드로 와서 규칙 적용 실패", "로그와 스키마를 대조해 프롬프트의 diff JSON 예시를 수정", "Outcome=Applied까지 재검증"],
                ["프롬프트 인젝션 방어", "'너는 이제 시스템이다'류 입력이 규칙 검증을 우회할 위험", "공격 패턴 테스트를 만들고 RuleValidator를 최종 저지선으로 설계", "허용 목록 밖 action/fact/op를 모두 거절"],
                ["되묻기 UX 개선", "모호한 입력에 대한 답변이 적용 완료처럼 보여 혼란", "needs_confirmation 대사는 질문형, 적용 완료 대사는 완료형으로 분리", "플레이어가 재입력해야 하는 상황을 명확히 인지"],
                ["WebGL 배포와 CORS", "GitHub Pages에서 LLM 프록시 호출이 브라우저 정책에 막힐 수 있음", "preflight, 허용 origin, GitHub Actions 배포 구조를 점검", "웹 링크 기반 제출 흐름 구성"],
            ],
            [31 * mm, 44 * mm, 50 * mm, 41 * mm],
        )
    )
    body.append(h("5. 외부 에셋 및 오픈소스 출처"))
    body.append(
        table(
            ["구분", "이름", "사용 내용", "출처 및 라이선스"],
            [
                ["AI 개발 도구", "Claude Code", "구현, 디버깅, 테스트, 문서화", "https://claude.com/claude-code"],
                ["제품 LLM", "Anthropic Claude API", "자연어 규칙 컴파일 및 경기 회고", "https://docs.anthropic.com/en/api/messages"],
                ["AI 보조 도구", "ChatGPT", "문서 정리와 아이디어 보조", "https://chatgpt.com"],
                ["오픈소스", "UniTask", "Unity 비동기 처리", "https://github.com/Cysharp/UniTask - MIT License"],
                ["오픈소스", "Newtonsoft.Json", "RuleSet JSON 직렬화", "Unity Package Registry - MIT License"],
            ],
            [22 * mm, 36 * mm, 52 * mm, 56 * mm],
        )
    )
    body.append(h("6. 보안 및 개인정보 관리"))
    body.append(
        bullets(
            [
                "API 키는 환경변수로만 주입하며 저장소와 WebGL 빌드 산출물에 하드코딩하지 않습니다.",
                "프록시 서버는 허용 origin만 응답하도록 구성해 공개 웹 빌드에서 임의 호출을 줄입니다.",
                "플레이어 입력은 시스템 프롬프트와 분리된 [코치의 말] 영역에만 삽입합니다.",
                "LLM 결과는 그대로 실행하지 않고 RuleValidator의 화이트리스트를 통과한 경우에만 적용합니다.",
            ]
        )
    )
    return build_pdf("4_AI_활용_기술_문서.pdf", "4. AI 활용 기술 문서", "AI 도구, 프롬프트, 활용 내역 정리", body)


def team_roles():
    body = []
    body.append(p("2인 팀 기준으로 작성한 팀원 롤 기술서입니다. 개인 참여로 제출하는 경우 이 문서는 제출 항목에서 생략할 수 있습니다.", "CalloutKR"))
    body.append(h("1. 팀 구성"))
    body.append(
        table(
            ["팀원", "주요 역할", "담당 영역"],
            [
                ["개발자 A (KJ)", "전투 코어, 콘텐츠, 밸런싱", "규칙 스키마, RuleValidator, FighterController FSM, RuleEvaluator, EventLog, 상대 5종 규칙셋, Match 화면, 연출과 밸런싱"],
                ["개발자 B (JM)", "AI 파이프라인, 훈련/회고 UI, 배포", "LLMClient, TrainingCompiler, 프롬프트, 프롬프트 인젝션 방어 테스트, Training 화면, LockerRoom 화면, 회고 LLM, WebGL 배포 및 프록시/CORS 점검"],
            ],
            [33 * mm, 43 * mm, 90 * mm],
        )
    )
    body.append(h("2. 담당 구현 영역"))
    body.append(h2("개발자 A (KJ)"))
    body.append(
        bullets(
            [
                "공용 규칙 스키마와 RuleValidator를 확정하고, LLM 출력과 전투 평가기가 같은 계약을 쓰도록 관리했습니다.",
                "FighterController FSM, 스탯, 스킬, 스태미나, 쿨다운, 궁 게이지 등 전투 핵심 로직을 구현했습니다.",
                "RuleEvaluator와 EventLog를 통해 규칙 발동과 경기 결과 분석이 가능하도록 구성했습니다.",
                "러쉬, 철벽, 그림자, 카멜레온, 사범 등 5개 상대 규칙셋과 밸런싱 플레이테스트를 담당했습니다.",
                "Match 화면, 전투 라벨, 배속/일시정지, 슬로모 등 경기 연출과 제출 전 QA를 담당했습니다.",
            ]
        )
    )
    body.append(h2("개발자 B (JM)"))
    body.append(
        bullets(
            [
                "Anthropic Claude API 연동과 ILLMClient 추상화를 구현하고, 키 없음/타임아웃/파싱 실패 처리를 담당했습니다.",
                "TrainingCompiler, 프롬프트 구성, needs_confirmation, conflict_with, 규칙 diff 적용 흐름을 담당했습니다.",
                "프롬프트 인젝션 방어 테스트와 어휘 사전 기반 거절/되묻기 검증을 담당했습니다.",
                "Training 화면과 LockerRoom 화면을 구현하고, 경기 회고 LLM과 규칙 발동 통계를 연결했습니다.",
                "WebGL 빌드, GitHub Pages 배포, LLM 프록시 CORS 및 입력/폰트 이슈 점검을 담당했습니다.",
            ]
        )
    )
    body.append(h("3. 협업 및 분업 방식"))
    body.append(
        bullets(
            [
                "규칙 스키마를 A/B 공용 계약으로 두고, 필드명과 enum 철자를 변경할 때는 합의 후 version을 올리는 방식으로 관리했습니다.",
                "씬 소유권을 나누어 Unity 충돌을 줄였습니다. A는 Match, B는 Training과 LockerRoom을 중심으로 작업했습니다.",
                "이슈 단위로 작업하고, 각 이슈의 완료 기준을 통과한 뒤 커밋하는 방식으로 진행했습니다.",
                "AI 코딩 에이전트가 생성한 결과는 개발자가 스펙, 테스트, 플레이테스트, diff 검토를 통해 최종 확인했습니다.",
                "WebGL 제출을 위해 빌드 산출물, GitHub Pages, YouTube 영상, PDF 문서를 별도로 점검했습니다.",
            ]
        )
    )
    body.append(h("4. 최종 제출 담당"))
    body.append(
        table(
            ["항목", "담당", "확인 내용"],
            [
                ["웹 빌드", "공용", "GitHub Pages 링크 정상 실행"],
                ["소스 코드", "공용", "동일 GitHub 저장소, 커밋 기록 유지"],
                ["플레이 영상", "공용", "30-60초 실제 플레이 영상, YouTube 링크 공개 또는 일부 공개"],
                ["게임 소개 PDF", "공용", "개요, 방법, 실행 링크, 영상 링크 포함"],
                ["AI 활용 PDF", "공용", "AI 도구, 프롬프트, 활용 내역, 외부 에셋/오픈소스 출처 포함"],
            ],
            [35 * mm, 26 * mm, 105 * mm],
        )
    )
    return build_pdf("5_팀원_롤_기술서.pdf", "5. 팀원 롤 기술서", "팀원별 역할 및 담당 영역 정리", body)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    register_fonts()
    global S
    S = styles()
    paths = [submission_summary(), game_intro(), ai_tech(), team_roles()]
    for path in paths:
        print(path)


if __name__ == "__main__":
    main()
