import html
import re
from pathlib import Path
from urllib.parse import unquote

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    Image,
    ListFlowable,
    ListItem,
    PageBreak,
    Paragraph,
    Preformatted,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output" / "pdf"
INTRO_DIR = Path(
    r"C:\Users\Main\Downloads\게임 소개 및 설명 문서(미완)\ExportBlock-68c2900d-201f-49ec-ab24-1abaec40025f-Part-1"
)
AI_DIR = Path(
    r"C:\Users\Main\Downloads\AI 활용 문서\ExportBlock-f23024a5-9df7-44bd-a56b-37f4d2c8ece7-Part-1"
)
INTRO_MD = INTRO_DIR / "3 게임 소개 및 설명 문서 3b49e9363a5d803aad06d9ea11778435.md"
AI_MD = AI_DIR / "4 AI 활용 기술 문서 3b59e9363a5d80ae8932c2e15f2c7463.md"

PLAY_URL = "https://gameclientdeveloperkimjin.github.io/TeachAndFight/"
GITHUB_URL = "https://github.com/GameClientDeveloperKimJin/TeachAndFight"
YOUTUBE_URL = "https://youtu.be/0Yznc7Fgc3g"

PAGE_W, PAGE_H = A4
MARGIN_X = 17 * mm
MARGIN_TOP = 15 * mm
MARGIN_BOTTOM = 14 * mm


def register_fonts():
    pdfmetrics.registerFont(TTFont("KR", r"C:\Windows\Fonts\malgun.ttf"))
    pdfmetrics.registerFont(TTFont("KR-Bold", r"C:\Windows\Fonts\malgunbd.ttf"))


def make_styles():
    base = getSampleStyleSheet()
    base.add(
        ParagraphStyle(
            "TitleKR",
            fontName="KR-Bold",
            fontSize=21,
            leading=28,
            textColor=colors.HexColor("#151A25"),
            alignment=TA_CENTER,
            spaceAfter=10,
        )
    )
    base.add(
        ParagraphStyle(
            "H1KR",
            fontName="KR-Bold",
            fontSize=15,
            leading=20,
            textColor=colors.HexColor("#151A25"),
            spaceBefore=9,
            spaceAfter=5,
        )
    )
    base.add(
        ParagraphStyle(
            "H2KR",
            fontName="KR-Bold",
            fontSize=12.2,
            leading=17,
            textColor=colors.HexColor("#20283A"),
            spaceBefore=7,
            spaceAfter=4,
        )
    )
    base.add(
        ParagraphStyle(
            "H3KR",
            fontName="KR-Bold",
            fontSize=10.4,
            leading=14,
            textColor=colors.HexColor("#2E374A"),
            spaceBefore=5,
            spaceAfter=3,
        )
    )
    base.add(
        ParagraphStyle(
            "BodyKR",
            fontName="KR",
            fontSize=8.6,
            leading=12.2,
            textColor=colors.HexColor("#202633"),
            spaceAfter=3,
        )
    )
    base.add(
        ParagraphStyle(
            "BulletKR",
            fontName="KR",
            fontSize=8.4,
            leading=12,
            leftIndent=5,
            textColor=colors.HexColor("#202633"),
        )
    )
    base.add(
        ParagraphStyle(
            "SmallKR",
            fontName="KR",
            fontSize=7.4,
            leading=10.5,
            textColor=colors.HexColor("#202633"),
        )
    )
    base.add(
        ParagraphStyle(
            "SmallBoldKR",
            fontName="KR-Bold",
            fontSize=7.4,
            leading=10.5,
            textColor=colors.white,
        )
    )
    base.add(
        ParagraphStyle(
            "CalloutKR",
            fontName="KR",
            fontSize=8.5,
            leading=12.5,
            leftIndent=7,
            rightIndent=7,
            borderColor=colors.HexColor("#D2DAE8"),
            borderWidth=0.6,
            borderPadding=7,
            backColor=colors.HexColor("#F5F7FC"),
            spaceBefore=4,
            spaceAfter=6,
        )
    )
    base.add(
        ParagraphStyle(
            "CodeKR",
            fontName="Courier",
            fontSize=6.8,
            leading=8.5,
            leftIndent=5,
            borderColor=colors.HexColor("#D6DCE8"),
            borderWidth=0.5,
            borderPadding=5,
            backColor=colors.HexColor("#F7F8FA"),
            textColor=colors.HexColor("#111827"),
            spaceBefore=3,
            spaceAfter=5,
        )
    )
    return base


S = None


def normalize_markdown(text, *, is_intro=False):
    text = text.replace("\r\n", "\n")
    text = text.replace("<aside>", "").replace("</aside>", "")
    text = text.replace("📌", "").replace("🔎", "")
    if is_intro:
        text = text.replace("*(추후 작성 예정)*", "")
        text = re.sub(
            r"(## 실행 방법: 게임 설치 및 실행 방법\n)(?:\s*)---",
            f"\\1\n- 웹 브라우저에서 아래 GitHub Pages 링크로 접속하면 바로 실행할 수 있습니다.\n- 플레이 링크: {PLAY_URL}\n- 별도 설치나 유료 라이선스는 필요하지 않습니다.\n- PC 실행 파일(.exe)이 아닌 WebGL 웹 빌드로 제출합니다.\n\n---",
            text,
        )
        text = re.sub(
            r"(## 플레이 링크 또는 설치 방법 \(웹 URL / APK·테스트 링크\)\n)(?:\s*)---",
            f"\\1\n- 웹 빌드: {PLAY_URL}\n- 전체 소스: {GITHUB_URL}\n\n---",
            text,
        )
        text = re.sub(
            r"(## 플레이 영상 링크\n)(?:\s*)$",
            f"\\1\n- YouTube: {YOUTUBE_URL}\n",
            text,
        )
    return text


def md_inline(text):
    text = html.escape(text)
    text = re.sub(r"`([^`]+)`", r"<font name='Courier'>\1</font>", text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", text)
    text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r"\1 (\2)", text)
    return text


def paragraph(text, style="BodyKR"):
    return Paragraph(md_inline(text), S[style])


def image_flow(path, page_width=PAGE_W - 2 * MARGIN_X):
    img = Image(str(path))
    max_w = page_width
    max_h = 82 * mm
    ratio = min(max_w / img.imageWidth, max_h / img.imageHeight, 1)
    img.drawWidth = img.imageWidth * ratio
    img.drawHeight = img.imageHeight * ratio
    return img


def resolve_image(src, base_dir):
    name = unquote(src)
    path = base_dir / name
    if path.exists():
        return path
    if base_dir == INTRO_DIR:
        if "201" in src or "%201" in src:
            return INTRO_DIR / "결과.png"
        return INTRO_DIR / "훈련실.png"
    if base_dir == AI_DIR:
        return AI_DIR / "AI 활용 프로세스.png"
    return None


def clean_table_row(line):
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def table_from_rows(rows):
    if not rows:
        return []
    col_count = max(len(r) for r in rows)
    fixed = [r + [""] * (col_count - len(r)) for r in rows]
    available = PAGE_W - 2 * MARGIN_X
    widths = [available / col_count] * col_count
    data = []
    for y, row in enumerate(fixed):
        style = "SmallBoldKR" if y == 0 else "SmallKR"
        data.append([Paragraph(md_inline(cell), S[style]) for cell in row])
    t = Table(data, colWidths=widths, repeatRows=1)
    t.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#26324C")),
                ("GRID", (0, 0), (-1, -1), 0.25, colors.HexColor("#D4DAE6")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 4),
                ("RIGHTPADDING", (0, 0), (-1, -1), 4),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#FAFBFE")]),
            ]
        )
    )
    return [t, Spacer(1, 4)]


def markdown_to_flowables(text, base_dir):
    story = []
    lines = text.splitlines()
    i = 0
    in_code = False
    code_lines = []
    bullet_items = []

    def flush_bullets():
        nonlocal bullet_items
        if bullet_items:
            story.append(
                ListFlowable(
                    [ListItem(paragraph(item, "BulletKR"), leftIndent=9) for item in bullet_items],
                    bulletType="bullet",
                    leftIndent=12,
                    bulletFontName="KR",
                    bulletFontSize=6,
                    bulletColor=colors.HexColor("#3D5AFE"),
                )
            )
            bullet_items = []

    def flush_code():
        nonlocal code_lines
        if code_lines:
            code = "\n".join(code_lines)
            story.append(Preformatted(code[:7000], S["CodeKR"]))
            if len(code) > 7000:
                story.append(paragraph("※ 코드 블록이 길어 일부를 생략했습니다.", "SmallKR"))
            code_lines = []

    while i < len(lines):
        line = lines[i].rstrip()

        if line.startswith("```"):
            if in_code:
                in_code = False
                flush_code()
            else:
                flush_bullets()
                in_code = True
                code_lines = []
            i += 1
            continue

        if in_code:
            code_lines.append(line)
            i += 1
            continue

        if not line.strip():
            flush_bullets()
            story.append(Spacer(1, 2))
            i += 1
            continue

        if line.strip() == "---":
            flush_bullets()
            story.append(Spacer(1, 5))
            i += 1
            continue

        img_match = re.match(r"!\[[^\]]*\]\(([^)]+)\)", line.strip())
        if img_match:
            flush_bullets()
            img = resolve_image(img_match.group(1), base_dir)
            if img and img.exists():
                story.append(image_flow(img))
                story.append(Spacer(1, 5))
            i += 1
            continue

        if line.lstrip().startswith("|") and "|" in line.strip()[1:]:
            flush_bullets()
            rows = []
            while i < len(lines) and lines[i].lstrip().startswith("|"):
                row_line = lines[i].rstrip()
                if not re.match(r"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$", row_line):
                    rows.append(clean_table_row(row_line))
                i += 1
            story.extend(table_from_rows(rows))
            continue

        heading = re.match(r"^(#{1,4})\s+(.+)$", line)
        if heading:
            flush_bullets()
            level = len(heading.group(1))
            text_part = heading.group(2)
            style = "H1KR" if level == 1 else "H2KR" if level == 2 else "H3KR"
            story.append(paragraph(text_part, style))
            i += 1
            continue

        stripped = line.strip()
        bullet = re.match(r"^[-*]\s+\[?[xX ]?\]?\s*(.+)$", stripped)
        numbered = re.match(r"^\d+\.\s+(.+)$", stripped)
        dot_bullet = re.match(r"^•\s*(.+)$", stripped)
        if bullet or numbered or dot_bullet:
            item = (bullet or numbered or dot_bullet).group(1)
            bullet_items.append(item)
            i += 1
            continue

        flush_bullets()
        if stripped.startswith(">"):
            story.append(paragraph(stripped.lstrip("> ").strip(), "CalloutKR"))
        else:
            story.append(paragraph(stripped))
        i += 1

    flush_bullets()
    flush_code()
    return story


def header_footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("KR", 7)
    canvas.setFillColor(colors.HexColor("#747D90"))
    canvas.drawString(MARGIN_X, 8.5 * mm, "TEACH & FIGHT 제출 상세 문서")
    canvas.drawRightString(PAGE_W - MARGIN_X, 8.5 * mm, str(doc.page))
    canvas.restoreState()


def build_pdf(path, title, story):
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
    doc.build(story, onFirstPage=header_footer, onLaterPages=header_footer)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    register_fonts()
    global S
    S = make_styles()

    intro_text = normalize_markdown(INTRO_MD.read_text(encoding="utf-8"), is_intro=True)
    ai_text = normalize_markdown(AI_MD.read_text(encoding="utf-8"))

    intro_story = [Paragraph("3. 게임 소개 및 설명 문서 - 상세본", S["TitleKR"])]
    intro_story.extend(markdown_to_flowables(intro_text, INTRO_DIR))
    build_pdf(OUT / "3_게임_소개_및_설명_문서_상세본.pdf", "3. 게임 소개 및 설명 문서 - 상세본", intro_story)

    ai_story = [Paragraph("4. AI 활용 기술 문서 - 상세본", S["TitleKR"])]
    ai_story.extend(markdown_to_flowables(ai_text, AI_DIR))
    build_pdf(OUT / "4_AI_활용_기술_문서_상세본.pdf", "4. AI 활용 기술 문서 - 상세본", ai_story)

    print(OUT / "3_게임_소개_및_설명_문서_상세본.pdf")
    print(OUT / "4_AI_활용_기술_문서_상세본.pdf")


if __name__ == "__main__":
    main()
