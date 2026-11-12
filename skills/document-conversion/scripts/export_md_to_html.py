"""Export a markdown document to one self-contained HTML file, for sharing without wiki or repository access.

Usage:
    python export_md_to_html.py <document.md> [-o out.html] [--css style.css] [--toc]

Images, the stylesheet and rendered mermaid diagrams (```mermaid and ::: mermaid blocks, as PNG) are embedded,
so the file can be mailed or attached on its own. The stylesheet defaults to assets/document.css in this skill,
which is also print-ready for export_md_to_pdf.py. Needs pandoc, and mmdc when the document has mermaid.
"""
import argparse
import subprocess
import sys
import tempfile
from pathlib import Path

from convert_common import default_output, first_heading, prepare_for_pandoc, require, resource_path

DEFAULT_CSS = Path(__file__).resolve().parent.parent / "assets" / "document.css"


def build_html(md_path, out, work, css=None, toc=False, extra_head=None):
    """Write a self-contained HTML rendering of md_path to out, using work for intermediate files."""
    md_path, work = Path(md_path).resolve(), Path(work)
    folder = md_path.parent
    stem = md_path.name.removesuffix(".md")
    md = md_path.read_bytes().decode("utf-8")
    prepared = work / f"{stem}.prepared.md"
    prepared.write_bytes(prepare_for_pandoc(md, folder, work, stem).encode("utf-8"))

    args = [require("pandoc"), str(prepared), "-f", "markdown", "-t", "html5", "--standalone", "--embed-resources",
            f"--resource-path={resource_path(folder, work)}", "--css", str(Path(css or DEFAULT_CSS).resolve()),
            "--metadata", f"pagetitle={first_heading(md) or stem}", "-o", str(Path(out).resolve())]
    if toc:
        args.append("--toc")
    if extra_head:
        args += ["--include-in-header", str(extra_head)]
    result = subprocess.run(args, cwd=folder, capture_output=True, text=True, encoding="utf-8")
    for line in result.stderr.splitlines():
        print(f"  pandoc: {line}")
    if result.returncode != 0:
        sys.exit(f"pandoc failed on {md_path}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("markdown", type=Path)
    parser.add_argument("-o", "--output", type=Path, help="HTML file to write (default: <stem>.html beside the source)")
    parser.add_argument("--css", type=Path, help="stylesheet to embed instead of assets/document.css")
    parser.add_argument("--toc", action="store_true", help="add a table of contents")
    a = parser.parse_args()

    src = a.markdown.resolve()
    if not src.is_file():
        sys.exit(f"not found: {src}")
    out = default_output(src, ".html", a.output)
    print(f"Exporting {src.name}")
    with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as tmp:
        build_html(src, out, tmp, a.css, a.toc)
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
