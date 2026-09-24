"""Export a markdown document to PDF, for sign-off copies and anything that should not be edited.

Usage:
    python export_md_to_pdf.py <document.md> [-o out.pdf] [--paper letter|a4] [--css style.css] [--toc]

Builds the same self-contained HTML as export_md_to_html.py, then prints it through Chrome or Edge headless,
without the browser's own header and footer. Needs pandoc and a browser, and mmdc when the document has mermaid.
For a PDF that should match a Word template, export to DOCX with export_md_to_docx.py and save as PDF from Word.
"""
import argparse
import sys
import tempfile
from pathlib import Path

from convert_common import default_output, html_to_pdf
from export_md_to_html import build_html

PAPER = {"letter": "Letter", "a4": "A4"}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("markdown", type=Path)
    parser.add_argument("-o", "--output", type=Path, help="PDF file to write (default: <stem>.pdf beside the source)")
    parser.add_argument("--paper", choices=sorted(PAPER), default="letter")
    parser.add_argument("--css", type=Path, help="stylesheet to use instead of assets/document.css")
    parser.add_argument("--toc", action="store_true", help="add a table of contents")
    a = parser.parse_args()

    src = a.markdown.resolve()
    if not src.is_file():
        sys.exit(f"not found: {src}")
    out = default_output(src, ".pdf", a.output)
    print(f"Exporting {src.name}")
    with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as tmp:
        work = Path(tmp)
        page = work / "page.html"
        page.write_text(f"<style>@page {{ size: {PAPER[a.paper]}; }}</style>\n", encoding="utf-8")
        html = work / f"{src.stem}.html"
        build_html(src, html, work, a.css, a.toc, extra_head=page)
        html_to_pdf(html, out, work)
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
