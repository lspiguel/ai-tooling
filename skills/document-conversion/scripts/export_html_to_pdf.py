"""Print an HTML file to PDF through Chrome or Edge headless.

Usage:
    python export_html_to_pdf.py <page.html> [-o out.pdf]

Page size and margins come from the page's own @page CSS; the browser's header and footer are left off.
Relative images, scripts and stylesheets load from beside the file. Scripts get five seconds of virtual time
to finish drawing (for example client-side mermaid) before the page is printed.
"""
import argparse
import sys
import tempfile
from pathlib import Path

from convert_common import default_output, html_to_pdf


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("html", type=Path)
    parser.add_argument("-o", "--output", type=Path, help="PDF file to write (default: <stem>.pdf beside the source)")
    a = parser.parse_args()

    src = a.html.resolve()
    if not src.is_file():
        sys.exit(f"not found: {src}")
    out = default_output(src, ".pdf", a.output)
    with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as tmp:
        html_to_pdf(src, out, tmp)
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
