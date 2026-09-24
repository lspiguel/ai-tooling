"""Render mermaid diagrams to images, for places that do not render mermaid themselves.

Usage:
    python render_mermaid.py <page.md> [-o out.md] [--format png|svg]
    python render_mermaid.py <diagram.mmd> [--format png|svg|both]

A markdown page: every ```mermaid and ::: mermaid block is rendered to .attachments/<stem>-mermaid-N.<ext>
beside the output, and the output page links the image instead of the block. The block's source is kept as
.attachments/<stem>-mermaid-N.mmd and named in an HTML comment under the image, so the diagram stays editable.
Output defaults to <stem>.rendered.md; the source page is never changed. A diagram is re-rendered only when its
source changed since the last run.

A .mmd file: rendered to <stem>.png and/or <stem>.svg beside it.

PNG (the default) is rendered at 3x on white. SVG is sharper at any size but mmdc draws its labels with HTML
(foreignObject), which browsers show and Word, PowerPoint and most image viewers do not - use PNG for anything
leaving a browser.
"""
import argparse
import sys
from pathlib import Path

from convert_common import MERMAID_BLOCK, render_mermaid, run_mmdc


def render_page(src, out, fmt):
    md = src.read_bytes().decode("utf-8")
    nl = "\r\n" if "\r\n" in md else "\n"
    attachments = out.parent / ".attachments"
    stem = src.name.removesuffix(".md")
    count = 0

    def repl(m):
        nonlocal count
        count += 1
        name = f"{stem}-mermaid-{count}"
        attachments.mkdir(exist_ok=True)
        rendered = render_mermaid(m.group("body"), attachments / f"{name}.mmd", attachments / f"{name}.{fmt}", nl)
        print(f"  {name}.{fmt}: {'rendered' if rendered else 'unchanged'}")
        return f"![Diagram {count}](.attachments/{name}.{fmt}){nl}<!-- mermaid source: .attachments/{name}.mmd -->"

    md = MERMAID_BLOCK.sub(repl, md)
    if not count:
        print("  no mermaid blocks found")
    numbered = (p for p in attachments.glob(f"{stem}-mermaid-*") if p.stem.rsplit("-", 1)[1].isdigit())
    stale = sorted(p.name for p in numbered if int(p.stem.rsplit("-", 1)[1]) > count)
    if stale:
        print("  no longer referenced (left in place):", ", ".join(stale))
    out.write_bytes(md.encode("utf-8"))


def render_file(src, fmt):
    for ext in ["png", "svg"] if fmt == "both" else [fmt]:
        out = src.with_suffix(f".{ext}")
        run_mmdc(src, out)
        print(f"  {out.name}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("source", type=Path, help="markdown page or .mmd file")
    parser.add_argument("-o", "--output", type=Path, help="markdown page to write (default: <stem>.rendered.md)")
    parser.add_argument("--format", choices=["png", "svg", "both"], default="png")
    a = parser.parse_args()

    src = a.source.resolve()
    if not src.is_file():
        sys.exit(f"not found: {src}")
    print(f"Rendering {src.name}")
    if src.suffix.lower() == ".mmd":
        render_file(src, a.format)
        return
    if a.format == "both":
        sys.exit("--format both applies to .mmd files; a page links one image per diagram")
    out = (a.output or src.with_name(src.name.removesuffix(".md") + ".rendered.md")).resolve()
    if out == src:
        sys.exit("the output would overwrite the source page; choose another -o")
    out.parent.mkdir(parents=True, exist_ok=True)
    render_page(src, out, a.format)
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
