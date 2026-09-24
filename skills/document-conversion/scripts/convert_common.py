"""Shared helpers for the document-conversion scripts.

Import it from a script in the same folder: `from convert_common import ...`. Not meant to be run directly.
"""
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

ICON_MAX_PX = 64
ICON_WIDTH_IN = 0.2
SVG_PX_PER_IN = 120     # a 390 px phone mockup prints 3.25 in wide
MAX_WIDTH_IN = 6.5      # Letter with 1 in margins
MERMAID_SCALE = 3
MERMAID_BACKGROUND = "white"

# ```mermaid fences and Azure DevOps wiki ::: mermaid blocks.
MERMAID_BLOCK = re.compile(r"^(?P<fence>```|:::)[ \t]*mermaid[ \t]*\r?\n(?P<body>.*?)\r?\n(?P=fence)[ \t]*$", re.M | re.S)
# Explicit page break: honoured by the PDF stylesheet, turned into a Word page break by the DOCX export.
PAGE_BREAK = re.compile(r'^<div class="page-break">\s*</div>[ \t]*$', re.M)
OPENXML_PAGE_BREAK = '```{=openxml}\n<w:p><w:r><w:br w:type="page"/></w:r></w:p>\n```'
# Local (no URL scheme) SVG image links.
SVG_LINK = re.compile(r"!\[([^\]]*)\]\((?![a-zA-Z][\w+.-]*:)([^)\s]+\.svg)\)")

INSTALL_HINTS = {
    "pandoc": "choco install pandoc -y",
    "mmdc": "npm install -g @mermaid-js/mermaid-cli   (Node.js first: choco install nodejs-lts -y)",
    "magick": "choco install imagemagick -y",
    "pdftotext": "choco install poppler -y",
    "git": "choco install git -y",
}
SETUP_POINTER = "See references/setup.md, or run scripts/Test-ConversionPrerequisites.ps1 to check everything at once."


def require(name):
    """Path of a command-line tool, or exit with the install command for it."""
    path = shutil.which(name)
    if not path:
        sys.exit(f"{name} is required on PATH. Install with: {INSTALL_HINTS.get(name, 'see setup')}\n{SETUP_POINTER}")
    return path


def require_module(module, package=None):
    """Import a Python package, or exit with the pip command for it."""
    try:
        return __import__(module)
    except ImportError:
        sys.exit(f"Python package '{package or module}' is required: python -m pip install {package or module}\n{SETUP_POINTER}")


def find_browser():
    """Chrome or Edge, for headless rendering. DOC_CONVERSION_BROWSER overrides the search."""
    candidates = []
    if os.environ.get("DOC_CONVERSION_BROWSER"):
        candidates.append(Path(os.environ["DOC_CONVERSION_BROWSER"]))
    if sys.platform == "win32":
        for base in (os.environ.get("ProgramFiles"), os.environ.get("ProgramFiles(x86)"), os.environ.get("LOCALAPPDATA")):
            if base:
                candidates.append(Path(base, "Google", "Chrome", "Application", "chrome.exe"))
        for base in (os.environ.get("ProgramFiles"), os.environ.get("ProgramFiles(x86)")):
            if base:
                candidates.append(Path(base, "Microsoft", "Edge", "Application", "msedge.exe"))
    elif sys.platform == "darwin":
        candidates += [Path("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"),
                       Path("/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge")]
    for name in ("google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "microsoft-edge", "msedge", "chrome"):
        found = shutil.which(name)
        if found:
            candidates.append(Path(found))
    for path in candidates:
        if path.is_file():
            return path
    sys.exit("Chrome or Edge is required for rendering (choco install googlechrome -y). "
             "Set DOC_CONVERSION_BROWSER to the executable if it is installed somewhere unusual.\n" + SETUP_POINTER)


def run_browser(args, url, work, output):
    """Run the browser headless against a file URL with a throwaway profile, and wait for `output` to be written.

    Edge on Windows can return before its screenshot or PDF is on disk, so the exit alone is not proof of output.
    """
    output = Path(output)
    base = ["--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run", "--no-default-browser-check",
            "--allow-file-access-from-files", f"--user-data-dir={Path(work) / 'browser-profile'}"]
    subprocess.run([str(find_browser()), *base, *args, url], check=True, capture_output=True, timeout=180)
    deadline = time.monotonic() + 60
    last = -1
    while time.monotonic() < deadline:
        size = output.stat().st_size if output.exists() else -1
        if size > 0 and size == last:
            return
        last = size
        time.sleep(0.5)
    sys.exit(f"the browser produced no {output.suffix} output for {url}")


def svg_size(svg):
    """Intrinsic width and height of an SVG, from width/height or else the viewBox."""
    tag = re.search(r"<svg\b[^>]*>", Path(svg).read_text(encoding="utf-8", errors="ignore"), re.S).group(0)
    w = re.search(r'(?<![\w-])width="([\d.]+)(?:px)?"', tag)
    h = re.search(r'(?<![\w-])height="([\d.]+)(?:px)?"', tag)
    if w and h:
        return float(w.group(1)), float(h.group(1))
    vb = re.search(r'viewBox="\s*[-\d.]+[\s,]+[-\d.]+[\s,]+([\d.]+)[\s,]+([\d.]+)', tag)
    return float(vb.group(1)), float(vb.group(2))


def is_icon(svg):
    return max(svg_size(svg)) <= ICON_MAX_PX


def svg_display_width_in(svg):
    """Print width in inches: icons small, everything else at SVG_PX_PER_IN, capped at the text width."""
    w, h = svg_size(svg)
    return ICON_WIDTH_IN if max(w, h) <= ICON_MAX_PX else min(MAX_WIDTH_IN, w / SVG_PX_PER_IN)


def svg_to_png(svg, png, work, scale=None):
    """Render an SVG to PNG: icons (64 px or smaller) through ImageMagick at 4x, anything else through the browser at 2x.

    A browser window cannot be made icon-sized, which is why small SVGs go through ImageMagick instead.
    """
    svg, png = Path(svg), Path(png)
    w, h = svg_size(svg)
    if max(w, h) <= ICON_MAX_PX:
        density = 96 * (scale or 4)
        subprocess.run([require("magick"), "-background", "none", "-density", str(density), str(svg), str(png)], check=True)
    else:
        run_browser([f"--force-device-scale-factor={scale or 2}", f"--window-size={round(w)},{round(h)}",
                     f"--screenshot={png.resolve()}"], svg.resolve().as_uri(), work, png)
    if not png.exists():
        sys.exit(f"no PNG was produced for {svg}")


def png_size(png):
    data = Path(png).read_bytes()[16:24]
    return int.from_bytes(data[:4], "big"), int.from_bytes(data[4:], "big")


def normalize_newlines(text):
    return text.replace("\r\n", "\n").strip()


def render_mermaid(source, mmd, out, nl="\n"):
    """Render one diagram to PNG (white background, 3x) or SVG, chosen by the extension of `out`.

    `mmd` receives the source. Rendering is skipped when `mmd` already holds the same source and `out` exists,
    so re-running over an unchanged document is fast. Returns True when it rendered.
    """
    mmd, out = Path(mmd), Path(out)
    if mmd.exists() and out.exists() and normalize_newlines(mmd.read_text(encoding="utf-8")) == normalize_newlines(source):
        return False
    mmd.write_bytes((normalize_newlines(source).replace("\n", nl) + nl).encode("utf-8"))
    run_mmdc(mmd, out)
    return True


def run_mmdc(mmd, out):
    """Render a .mmd file to PNG (white background, 3x) or SVG, chosen by the extension of `out`."""
    mmd, out = Path(mmd), Path(out)
    args = [require("mmdc"), "-i", str(mmd), "-o", str(out), "-b", MERMAID_BACKGROUND]
    if out.suffix.lower() == ".png":
        args += ["-s", str(MERMAID_SCALE)]
    result = subprocess.run(args, capture_output=True, text=True)
    if result.returncode != 0 or not out.exists():
        sys.exit(f"mmdc failed on {mmd}:\n{result.stderr.strip() or result.stdout.strip()}")


def mermaid_png_width_in(png):
    return min(MAX_WIDTH_IN, png_size(png)[0] / (MERMAID_SCALE * 96))


def prepare_for_pandoc(md, folder, work, stem, svg_to_png_links=False):
    """Replace mermaid blocks with PNG images rendered into `work`; optionally do the same for local SVG links.

    The returned markdown references the new PNGs by bare file name, so pandoc must be run with `work` on its
    resource path. The source markdown is not touched.
    """
    work = Path(work)
    count = 0

    def mermaid(m):
        nonlocal count
        count += 1
        png = work / f"{stem}-mermaid-{count}.png"
        render_mermaid(m.group("body"), work / f"{stem}-mermaid-{count}.mmd", png)
        return f"![]({png.name}){{width={mermaid_png_width_in(png):.2f}in}}"

    md = MERMAID_BLOCK.sub(mermaid, md)
    if count:
        print(f"  rendered {count} mermaid diagram(s)")

    if svg_to_png_links:
        def svg(m):
            alt, rel = m.group(1), m.group(2)
            src = Path(folder) / rel
            if not src.exists():
                print(f"  missing, left as is: {rel}")
                return m.group(0)
            png = work / (Path(rel).as_posix().removeprefix("./").replace("../", "up__").replace("/", "__")[:-4] + ".png")
            svg_to_png(src, png, work)
            return f"![{alt}]({png.name}){{width={svg_display_width_in(src):.2f}in}}"

        md = SVG_LINK.sub(svg, md)
    return md


def resource_path(*folders):
    return os.pathsep.join(str(Path(f)) for f in folders)


def first_heading(md):
    m = re.search(r"^#\s+(.+?)\s*#*\s*$", md, re.M)
    return m.group(1) if m else None


def html_to_pdf(html, pdf, work):
    """Print an HTML file to PDF through the browser, without the browser's own header and footer."""
    pdf = Path(pdf).resolve()
    if pdf.exists():
        pdf.unlink()
    run_browser(["--no-pdf-header-footer", "--print-to-pdf-no-header", "--virtual-time-budget=5000",
                 f"--print-to-pdf={pdf}"], Path(html).resolve().as_uri(), work, pdf)


def default_output(src, suffix, output=None):
    return Path(output).resolve() if output else Path(src).with_suffix(suffix)
