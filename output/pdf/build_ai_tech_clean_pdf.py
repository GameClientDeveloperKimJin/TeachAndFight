import html
import os
import re
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output" / "pdf"
TMP = ROOT / "tmp" / "pdfs"
AI_DIR = Path(
    r"C:\Users\Main\Downloads\AI 활용 문서\ExportBlock-f23024a5-9df7-44bd-a56b-37f4d2c8ece7-Part-1"
)
AI_MD = AI_DIR / "4 AI 활용 기술 문서 3b59e9363a5d80ae8932c2e15f2c7463.md"
AI_IMAGE = AI_DIR / "AI 활용 프로세스.png"

NODE = Path(r"C:\Users\Main\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe")
NODE_MODULES = Path(r"C:\Users\Main\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules")


def inline_md(text: str) -> str:
    text = html.escape(text)
    text = re.sub(r"`([^`]+)`", r"<code>\1</code>", text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", text)
    text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r'<a href="\2">\1</a>', text)
    return text


def is_table_separator(line: str) -> bool:
    return bool(re.match(r"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$", line))


def split_table_row(line: str):
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def render_table_block(lines):
    meaningful = [line for line in lines if line.strip()]
    simple = all(line.lstrip().startswith("|") for line in meaningful)
    if not simple:
        raw = "\n".join(lines)
        return f'<pre class="md-table-raw">{inline_md(raw)}</pre>'

    rows = []
    for line in meaningful:
        if is_table_separator(line):
            continue
        rows.append(split_table_row(line))

    if not rows:
        return ""

    col_count = max(len(row) for row in rows)
    rows = [row + [""] * (col_count - len(row)) for row in rows]
    out = ["<table>"]
    for idx, row in enumerate(rows):
        tag = "th" if idx == 0 else "td"
        out.append("<tr>" + "".join(f"<{tag}>{inline_md(cell)}</{tag}>" for cell in row) + "</tr>")
    out.append("</table>")
    return "\n".join(out)


def render_markdown(md: str) -> str:
    md = md.replace("\r\n", "\n")
    md = md.replace("<aside>", '<div class="aside">').replace("</aside>", "</div>")
    md = md.replace("📌", "").replace("🔎", "")

    lines = md.splitlines()
    body = []
    i = 0
    in_code = False
    code_lines = []
    list_open = False

    def close_list():
        nonlocal list_open
        if list_open:
            body.append("</ul>")
            list_open = False

    while i < len(lines):
        line = lines[i].rstrip()
        stripped = line.strip()

        if stripped.startswith("```"):
            if in_code:
                body.append("<pre class='code'>" + html.escape("\n".join(code_lines)) + "</pre>")
                code_lines = []
                in_code = False
            else:
                close_list()
                in_code = True
            i += 1
            continue

        if in_code:
            code_lines.append(line)
            i += 1
            continue

        if not stripped:
            close_list()
            i += 1
            continue

        if stripped == "---":
            close_list()
            body.append("<hr>")
            i += 1
            continue

        if stripped.startswith("|") and i + 1 < len(lines) and is_table_separator(lines[i + 1]):
            close_list()
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
            body.append(render_table_block(block))
            continue

        image_match = re.match(r"!\[[^\]]*\]\(([^)]+)\)", stripped)
        if image_match:
            close_list()
            body.append(f'<figure><img src="{AI_IMAGE.as_uri()}"><figcaption>AI 활용 프로세스</figcaption></figure>')
            i += 1
            continue

        heading = re.match(r"^(#{1,6})\s+(.+)$", stripped)
        if heading:
            close_list()
            level = min(len(heading.group(1)), 4)
            body.append(f"<h{level}>{inline_md(heading.group(2))}</h{level}>")
            i += 1
            continue

        bullet = re.match(r"^[-*]\s+(?:\[([ xX])\]\s*)?(.+)$", stripped)
        dot_bullet = re.match(r"^•\s*(.+)$", stripped)
        if bullet or dot_bullet:
            if not list_open:
                body.append("<ul>")
                list_open = True
            content = bullet.group(2) if bullet else dot_bullet.group(1)
            checkbox = ""
            if bullet and bullet.group(1):
                checkbox = "완료: " if bullet.group(1).lower() == "x" else "미완료: "
            body.append(f"<li>{inline_md(checkbox + content)}</li>")
            i += 1
            continue

        number = re.match(r"^\d+\.\s+(.+)$", stripped)
        if number:
            if not list_open:
                body.append("<ul>")
                list_open = True
            body.append(f"<li>{inline_md(number.group(1))}</li>")
            i += 1
            continue

        close_list()
        if stripped.startswith(">"):
            body.append(f'<blockquote>{inline_md(stripped.lstrip("> ").strip())}</blockquote>')
        else:
            body.append(f"<p>{inline_md(stripped)}</p>")
        i += 1

    close_list()
    return "\n".join(body)


def build_html():
    css = """
@page { size: A4; margin: 15mm 14mm 16mm 14mm; }
* { box-sizing: border-box; }
body {
  font-family: "Malgun Gothic", "Noto Sans KR", Arial, sans-serif;
  color: #18202f;
  font-size: 10.2px;
  line-height: 1.52;
}
h1 {
  font-size: 26px;
  text-align: center;
  margin: 0 0 18px;
  page-break-after: avoid;
}
h2 {
  font-size: 19px;
  margin: 22px 0 8px;
  padding-top: 2px;
  page-break-after: avoid;
}
h3 {
  font-size: 14.5px;
  margin: 16px 0 6px;
  page-break-after: avoid;
}
h4 {
  font-size: 12.3px;
  margin: 12px 0 5px;
  page-break-after: avoid;
}
p { margin: 0 0 7px; }
ul { margin: 3px 0 9px 18px; padding: 0; }
li { margin: 0 0 4px; padding-left: 2px; }
hr { border: 0; border-top: 1px solid #d8deea; margin: 14px 0; }
strong { font-weight: 700; }
code {
  font-family: Consolas, "Malgun Gothic", monospace;
  background: #f2f4f8;
  color: #d83232;
  padding: 1px 3px;
  border-radius: 3px;
}
blockquote, .aside {
  border: 1px solid #d3dbe8;
  background: #f5f8fd;
  border-radius: 8px;
  padding: 9px 11px;
  margin: 8px 0 12px;
}
table {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
  margin: 8px 0 13px;
  page-break-inside: auto;
}
tr { page-break-inside: avoid; }
th {
  background: #26324c;
  color: white;
  font-weight: 700;
}
th, td {
  border: 1px solid #d5dce8;
  padding: 6px 7px;
  vertical-align: top;
  word-break: keep-all;
  overflow-wrap: anywhere;
}
tr:nth-child(even) td { background: #fbfcff; }
.md-table-raw {
  white-space: pre-wrap;
  font-family: "Malgun Gothic", "Noto Sans KR", Arial, sans-serif;
  font-size: 9.3px;
  line-height: 1.5;
  background: #f8fafc;
  border: 1px solid #d8deea;
  border-radius: 6px;
  padding: 8px 10px;
  margin: 8px 0 13px;
  overflow-wrap: anywhere;
}
.code {
  white-space: pre-wrap;
  font-family: Consolas, "Malgun Gothic", monospace;
  font-size: 8.8px;
  line-height: 1.42;
  background: #111827;
  color: #f9fafb;
  border-radius: 6px;
  padding: 10px 12px;
  margin: 8px 0 13px;
  overflow-wrap: anywhere;
}
figure { margin: 10px 0 16px; page-break-inside: avoid; }
figure img { width: 100%; display: block; }
figcaption { color: #687386; font-size: 9px; text-align: center; margin-top: 4px; }
a { color: #2451d6; text-decoration: none; }
"""
    content = render_markdown(AI_MD.read_text(encoding="utf-8"))
    return f"""<!doctype html>
<html lang="ko">
<head>
  <meta charset="utf-8">
  <title>4. AI 활용 기술 문서</title>
  <style>{css}</style>
</head>
<body>
{content}
</body>
</html>
"""


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    TMP.mkdir(parents=True, exist_ok=True)
    html_path = TMP / "4_AI_활용_기술_문서_깨짐수정.html"
    pdf_path = OUT / "4_AI_활용_기술_문서_상세본.pdf"
    html_path.write_text(build_html(), encoding="utf-8")

    printer = TMP / "print_ai_pdf.js"
    printer.write_text(
        f"""
const {{ chromium }} = require('playwright');
(async () => {{
  const browser = await chromium.launch({{ headless: true }});
  const page = await browser.newPage();
  await page.goto({html_path.as_uri()!r}, {{ waitUntil: 'load' }});
  await page.pdf({{
    path: {str(pdf_path)!r},
    format: 'A4',
    printBackground: true,
    preferCSSPageSize: true
  }});
  await browser.close();
}})().catch(err => {{
  console.error(err);
  process.exit(1);
}});
""",
        encoding="utf-8",
    )
    env = os.environ.copy()
    env["NODE_PATH"] = str(NODE_MODULES)
    subprocess.run([str(NODE), str(printer)], check=True, env=env)
    print(pdf_path)


if __name__ == "__main__":
    main()
