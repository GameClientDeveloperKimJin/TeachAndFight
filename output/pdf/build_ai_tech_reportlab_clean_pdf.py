import html
import re
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
    ListFlowable,
    ListItem,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output" / "pdf"
AI_DIR = Path(
    r"C:\Users\Main\Downloads\AI 활용 문서\ExportBlock-f23024a5-9df7-44bd-a56b-37f4d2c8ece7-Part-1"
)
AI_MD = AI_DIR / "4 AI 활용 기술 문서 3b59e9363a5d80ae8932c2e15f2c7463.md"
AI_IMAGE = AI_DIR / "AI 활용 프로세스.png"

PAGE_W, PAGE_H = A4
MARGIN_X = 15 * mm
MARGIN_TOP = 15 * mm
MARGIN_BOTTOM = 13 * mm
CONTENT_W = PAGE_W - 2 * MARGIN_X


def register_fonts():
    pdfmetrics.registerFont(TTFont("KR", r"C:\Windows\Fonts\malgun.ttf"))
    pdfmetrics.registerFont(TTFont("KR-Bold", r"C:\Windows\Fonts\malgunbd.ttf"))


def make_styles():
    s = getSampleStyleSheet()
    s.add(ParagraphStyle("TitleKR", fontName="KR-Bold", fontSize=20, leading=27, alignment=TA_CENTER, spaceAfter=12))
    s.add(ParagraphStyle("H1KR", fontName="KR-Bold", fontSize=15.5, leading=20, spaceBefore=11, spaceAfter=6))
    s.add(ParagraphStyle("H2KR", fontName="KR-Bold", fontSize=12.2, leading=16, spaceBefore=8, spaceAfter=4))
    s.add(ParagraphStyle("H3KR", fontName="KR-Bold", fontSize=10.4, leading=14, spaceBefore=6, spaceAfter=3))
    s.add(ParagraphStyle("BodyKR", fontName="KR", fontSize=8.3, leading=11.8, spaceAfter=3))
    s.add(ParagraphStyle("BulletKR", fontName="KR", fontSize=8.1, leading=11.4))
    s.add(ParagraphStyle("SmallKR", fontName="KR", fontSize=7.1, leading=9.8))
    s.add(ParagraphStyle("SmallBoldKR", fontName="KR-Bold", fontSize=7.1, leading=9.8, textColor=colors.white))
    s.add(
        ParagraphStyle(
            "BoxKR",
            fontName="KR",
            fontSize=7.8,
            leading=11.1,
            spaceAfter=4,
        )
    )
    return s


S = None


def inline(text):
    text = html.escape(text)
    text = re.sub(r"`([^`]+)`", r"<font name='KR-Bold' color='#D43C3C'>\1</font>", text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", text)
    text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r"\1 (\2)", text)
    return text


def p(text, style="BodyKR"):
    return Paragraph(inline(text), S[style])


def is_separator(line):
    return bool(re.match(r"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$", line))


def split_row(line):
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def raw_box(lines):
    cleaned = [line for line in lines if line.strip() and not is_separator(line)]
    text = "<br/>".join(inline(line) for line in cleaned)
    box = Table([[Paragraph(text, S["BoxKR"])]], colWidths=[CONTENT_W])
    box.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#F7F9FC")),
                ("BOX", (0, 0), (-1, -1), 0.35, colors.HexColor("#D5DCE8")),
                ("LEFTPADDING", (0, 0), (-1, -1), 7),
                ("RIGHTPADDING", (0, 0), (-1, -1), 7),
                ("TOPPADDING", (0, 0), (-1, -1), 6),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
            ]
        )
    )
    return [box, Spacer(1, 4)]


def render_table(lines):
    meaningful = [line for line in lines if line.strip()]
    simple = all(line.lstrip().startswith("|") for line in meaningful)
    if not simple:
        return raw_box(lines)

    rows = []
    for line in meaningful:
        if not is_separator(line):
            rows.append(split_row(line))
    if not rows:
        return []

    col_count = max(len(row) for row in rows)
    if col_count > 5:
        return raw_box(lines)
    rows = [row + [""] * (col_count - len(row)) for row in rows]
    widths = [CONTENT_W / col_count] * col_count
    data = []
    for y, row in enumerate(rows):
        style = "SmallBoldKR" if y == 0 else "SmallKR"
        data.append([Paragraph(inline(cell), S[style]) for cell in row])

    table = Table(data, colWidths=widths, repeatRows=1)
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#26324C")),
                ("GRID", (0, 0), (-1, -1), 0.25, colors.HexColor("#D5DCE8")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 4),
                ("RIGHTPADDING", (0, 0), (-1, -1), 4),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#FBFCFF")]),
            ]
        )
    )
    return [table, Spacer(1, 4)]


def scaled_image(path):
    img = Image(str(path))
    max_h = 60 * mm
    ratio = min(CONTENT_W / img.imageWidth, max_h / img.imageHeight, 1)
    img.drawWidth = img.imageWidth * ratio
    img.drawHeight = img.imageHeight * ratio
    return img


def markdown_to_story(text):
    text = text.replace("\r\n", "\n")
    text = text.replace("<aside>", "").replace("</aside>", "")
    text = text.replace("📌", "").replace("🔎", "")

    story = [Paragraph("4. AI 활용 기술 문서 - 상세본", S["TitleKR"])]
    lines = text.splitlines()
    i = 0
    in_code = False
    code = []
    bullets = []

    def flush_bullets():
        nonlocal bullets
        if bullets:
            story.append(
                ListFlowable(
                    [ListItem(p(item, "BulletKR"), leftIndent=9) for item in bullets],
                    bulletType="bullet",
                    leftIndent=12,
                    bulletFontName="KR",
                    bulletFontSize=5.5,
                    bulletColor=colors.HexColor("#3D5AFE"),
                )
            )
            bullets = []

    while i < len(lines):
        line = lines[i].rstrip()
        stripped = line.strip()

        if stripped.startswith("```"):
            if in_code:
                flush_bullets()
                story.extend(raw_box(code))
                code = []
                in_code = False
            else:
                flush_bullets()
                in_code = True
            i += 1
            continue

        if in_code:
            code.append(line)
            i += 1
            continue

        if not stripped:
            flush_bullets()
            story.append(Spacer(1, 2))
            i += 1
            continue

        if stripped == "---":
            flush_bullets()
            story.append(Spacer(1, 6))
            i += 1
            continue

        if stripped.startswith("|") and i + 1 < len(lines) and is_separator(lines[i + 1]):
            flush_bullets()
            block = [line, lines[i + 1].rstrip()]
            i += 2
            while i < len(lines):
                nxt = lines[i].rstrip()
                nxt_s = nxt.strip()
                if not nxt_s:
                    break
                if re.match(r"^#{1,6}\s+", nxt_s) or nxt_s == "---":
                    break
                block.append(nxt)
                i += 1
            story.extend(render_table(block))
            continue

        image = re.match(r"!\[[^\]]*\]\(([^)]+)\)", stripped)
        if image:
            flush_bullets()
            story.append(scaled_image(AI_IMAGE))
            story.append(Spacer(1, 5))
            i += 1
            continue

        heading = re.match(r"^(#{1,6})\s+(.+)$", stripped)
        if heading:
            flush_bullets()
            level = len(heading.group(1))
            style = "H1KR" if level == 1 else "H2KR" if level == 2 else "H3KR"
            story.append(p(heading.group(2), style))
            i += 1
            continue

        bullet = re.match(r"^[-*]\s+(?:\[[ xX]\]\s*)?(.+)$", stripped)
        dot = re.match(r"^•\s*(.+)$", stripped)
        numbered = re.match(r"^\d+\.\s+(.+)$", stripped)
        if bullet or dot or numbered:
            bullets.append((bullet or dot or numbered).group(1))
            i += 1
            continue

        flush_bullets()
        if stripped.startswith(">"):
            story.extend(raw_box([stripped.lstrip("> ").strip()]))
        else:
            story.append(p(stripped))
        i += 1

    flush_bullets()
    return story


def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("KR", 7)
    canvas.setFillColor(colors.HexColor("#747D90"))
    canvas.drawString(MARGIN_X, 8 * mm, "TEACH & FIGHT 제출 상세 문서")
    canvas.drawRightString(PAGE_W - MARGIN_X, 8 * mm, str(doc.page))
    canvas.restoreState()


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    register_fonts()
    global S
    S = make_styles()
    story = markdown_to_story(AI_MD.read_text(encoding="utf-8"))
    output = OUT / "4_AI_활용_기술_문서_상세본.pdf"
    doc = SimpleDocTemplate(
        str(output),
        pagesize=A4,
        leftMargin=MARGIN_X,
        rightMargin=MARGIN_X,
        topMargin=MARGIN_TOP,
        bottomMargin=MARGIN_BOTTOM,
        title="4. AI 활용 기술 문서 - 상세본",
        author="TEACH & FIGHT Team",
    )
    doc.build(story, onFirstPage=footer, onLaterPages=footer)
    print(output)


if __name__ == "__main__":
    main()
