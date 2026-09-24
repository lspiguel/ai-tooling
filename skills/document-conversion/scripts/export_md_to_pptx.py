"""Export a markdown outline to a PowerPoint deck.

Usage:
    python export_md_to_pptx.py <outline.md> [-o out.pptx] [--reference-doc template.pptx] [--slide-level N]

pandoc builds the slides: with the default slide level, each level-2 heading starts a slide and each level-1
heading makes a section title slide. A ::: notes block becomes the slide's speaker notes, and ::::: columns
splits a slide in two. See references/authoring-for-export.md for the outline conventions.

Mermaid blocks and local SVG images are rendered to PNG first, since PowerPoint does not show mmdc SVG labels.
Layouts, fonts and colours come from --reference-doc (a .pptx or .potx whose layouts keep PowerPoint's default
names), or pandoc's plain default. Needs pandoc; mmdc, a browser or ImageMagick when the outline uses them.
"""
import argparse
import subprocess
import sys
import tempfile
from pathlib import Path

from convert_common import default_output, prepare_for_pandoc, require, resource_path


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("markdown", type=Path)
    parser.add_argument("-o", "--output", type=Path, help="PPTX file to write (default: <stem>.pptx beside the source)")
    parser.add_argument("--reference-doc", type=Path, help="template deck for layouts, fonts and colours")
    parser.add_argument("--slide-level", type=int, help="heading level that starts a slide (pandoc infers it by default)")
    a = parser.parse_args()

    src = a.markdown.resolve()
    if not src.is_file():
        sys.exit(f"not found: {src}")
    out = default_output(src, ".pptx", a.output)
    folder = src.parent
    stem = src.name.removesuffix(".md")
    print(f"Exporting {src.name}")
    with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as tmp:
        work = Path(tmp)
        prepared = work / f"{stem}.prepared.md"
        md = src.read_bytes().decode("utf-8")
        prepared.write_bytes(prepare_for_pandoc(md, folder, work, stem, svg_to_png_links=True).encode("utf-8"))
        args = [require("pandoc"), str(prepared), "-f", "markdown", "-t", "pptx",
                f"--resource-path={resource_path(folder, work)}", "-o", str(out)]
        if a.reference_doc:
            args += ["--reference-doc", str(a.reference_doc.resolve())]
        if a.slide_level:
            args += ["--slide-level", str(a.slide_level)]
        result = subprocess.run(args, cwd=folder, capture_output=True, text=True, encoding="utf-8")
        for line in result.stderr.splitlines():
            print(f"  pandoc: {line}")
        if result.returncode != 0:
            sys.exit(f"pandoc failed on {src}")
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
