"""Render SVG files to PNG, for places that take images but not SVG: work items, chat, slides, email.

Usage:
    python export_svg_to_png.py <file.svg> [more.svg ...] [-o out.png] [--scale N]

Icons (64 px or smaller) go through ImageMagick with a transparent background, at 4x by default; anything larger
through Chrome or Edge headless at 2x by default, so text and fonts render as a browser shows them. The output is
<stem>.png beside each input unless -o is given (single input only).

Mermaid SVGs render correctly here, because the browser draws their HTML labels. To produce a PNG straight from a
.mmd source, use render_mermaid.py instead.
"""
import argparse
import sys
import tempfile
from pathlib import Path

from convert_common import png_size, svg_to_png


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("svg", type=Path, nargs="+")
    parser.add_argument("-o", "--output", type=Path, help="PNG to write (single input only)")
    parser.add_argument("--scale", type=int, choices=range(1, 9), metavar="1-8",
                        help="pixel density multiplier (default: 4 for icons, 2 otherwise)")
    a = parser.parse_args()
    if a.output and len(a.svg) > 1:
        sys.exit("-o can only be used with a single input file")

    with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as tmp:
        for svg in a.svg:
            src = svg.resolve()
            if not src.is_file():
                sys.exit(f"not found: {src}")
            out = (a.output or src.with_suffix(".png")).resolve()
            svg_to_png(src, out, tmp, a.scale)
            w, h = png_size(out)
            print(f"{out}  ({w} x {h})")


if __name__ == "__main__":
    main()
