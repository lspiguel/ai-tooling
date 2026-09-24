"""Export a markdown document to DOCX with pandoc.

Usage:
    python export_md_to_docx.py <path/to/document.md>

Writes next to the markdown file, where <stem> is its name without ".md" and without a trailing ".prompt":
  <stem>.docx-export.tmp.md               local SVG image links rewritten to PNGs in _docx-assets/
  <stem>.docx-export.with-mermaid.tmp.md  mermaid blocks replaced by PNGs (re-rendered only when the source changed)
  <stem>.docx                             built by pandoc

Styles and page setup come from the committed <stem>.docx when there is one (pandoc's default otherwise),
with body text at 9 pt and headings scaled up from there. A Microsoft Purview sensitivity label on the
committed DOCX is carried over.

Rendering: SVG mockups and diagrams through Microsoft Edge headless at 2x, SVG icons (64 px or smaller)
through ImageMagick at 4x, mermaid through mmdc. Needs pandoc, plus Edge, ImageMagick or mmdc for
whichever of those the document uses.
"""
import argparse
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

ASSETS_DIR = "_docx-assets"
ICON_MAX_PX = 64
ICON_WIDTH_IN = 0.2
SVG_PX_PER_IN = 120     # a 390 px phone mockup prints 3.25 in wide
MAX_WIDTH_IN = 6.5      # Letter with 1 in margins
MERMAID_SCALE = 3

# Style sizes in half-points: body 9 pt, everything else scaled up from there.
DEFAULT_SIZE = 18
STYLE_SIZES = {
    "Normal": 18,
    "Heading1": 36, "Heading1Char": 36,
    "Heading2": 28, "Heading2Char": 28,
    "Heading3": 24, "Heading3Char": 24,
    "Heading4": 20,
    "Title": 48, "TitleChar": 48,
    "Subtitle": 24, "SubtitleChar": 24,
    "Author": 20, "Date": 20,
    "IntenseQuoteChar": 18, "QuoteChar": 18, "NoSpacing": 18,
}

EDGE_PATHS = [
    Path(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
]


def require(name):
    path = shutil.which(name)
    if not path:
        sys.exit(f"{name} is required on PATH")
    return path


def find_edge():
    for path in EDGE_PATHS:
        if path.exists():
            return path
    sys.exit("Microsoft Edge is required to render SVG mockups")


def svg_size(svg):
    tag = re.search(r"<svg\b[^>]*>", svg.read_text(encoding="utf-8", errors="ignore"), re.S).group(0)
    w = re.search(r'(?<![\w-])width="([\d.]+)(?:px)?"', tag)
    h = re.search(r'(?<![\w-])height="([\d.]+)(?:px)?"', tag)
    if w and h:
        return float(w.group(1)), float(h.group(1))
    vb = re.search(r'viewBox="\s*[-\d.]+[\s,]+[-\d.]+[\s,]+([\d.]+)[\s,]+([\d.]+)', tag)
    return float(vb.group(1)), float(vb.group(2))


def png_width(png):
    return int.from_bytes(png.read_bytes()[16:20], "big")


def rewrite_images(md, folder):
    """Point local SVG image links at PNGs in _docx-assets and give each a display width."""
    renders = {}

    def repl(m):
        alt, rel = m.group(1), m.group(2)
        svg = folder / rel
        if not svg.exists():
            print(f"  missing, left as is: {rel}")
            return m.group(0)
        name = Path(rel).as_posix().removeprefix("./").replace("../", "up__").replace("/", "__")[:-4] + ".png"
        w, h = svg_size(svg)
        icon = max(w, h) <= ICON_MAX_PX
        width = ICON_WIDTH_IN if icon else min(MAX_WIDTH_IN, w / SVG_PX_PER_IN)
        renders[name] = (svg, icon, w, h)
        return f"![{alt}]({ASSETS_DIR}/{name}){{width={width:.2f}in}}"

    out = re.sub(r"!\[([^\]]*)\]\((?![a-zA-Z][\w+.-]*:)([^)\s]+\.svg)\)", repl, md)
    return out, renders


def replace_mermaid(md, assets, nl):
    """Replace each mermaid block with its PNG, re-rendering only when the block changed."""
    count = 0

    def repl(m):
        nonlocal count
        count += 1
        body = m.group(1)
        mmd = assets / f"mermaid-diagram-{count}.mmd"
        png = assets / f"mermaid-diagram-{count}.png"
        norm = lambda s: s.replace("\r\n", "\n").strip()
        if mmd.exists() and png.exists() and norm(mmd.read_text(encoding="utf-8")) == norm(body):
            print(f"  mermaid-diagram-{count}: unchanged, keeping PNG")
        else:
            mmd.write_bytes((norm(body).replace("\n", nl) + nl).encode("utf-8"))
            print(f"  mermaid-diagram-{count}: rendering")
            subprocess.run([require("mmdc"), "-i", str(mmd), "-o", str(png), "-s", str(MERMAID_SCALE), "-b", "white"],
                           check=True, capture_output=True)
        width = min(MAX_WIDTH_IN, png_width(png) / (MERMAID_SCALE * 96))
        return f"![Mermaid diagram {count}]({ASSETS_DIR}/{png.name}){{width={width:.2f}in}}"

    return re.sub(r"```mermaid\r?\n(.*?)\r?\n```", repl, md, flags=re.S)


def render(renders, assets, work):
    edge = None
    for name, (svg, icon, w, h) in renders.items():
        png = assets / name
        print(f"  {name}")
        if icon:
            subprocess.run([require("magick"), "-background", "none", "-density", "384", str(svg), str(png)], check=True)
        else:
            edge = edge or find_edge()
            subprocess.run([str(edge), "--headless=new", "--disable-gpu", "--hide-scrollbars",
                            f"--user-data-dir={work / 'edge-profile'}", "--force-device-scale-factor=2",
                            f"--window-size={round(w)},{round(h)}", f"--screenshot={png}", svg.resolve().as_uri()],
                           check=True, capture_output=True, timeout=120)


def previous_docx(docx, work):
    """The committed DOCX, so re-runs never build on their own output; the file on disk if it is untracked."""
    try:
        data = subprocess.run(["git", "show", f"HEAD:./{docx.name}"], cwd=docx.parent,
                              check=True, capture_output=True).stdout
    except (subprocess.CalledProcessError, FileNotFoundError):
        return docx if docx.exists() else None
    prev = work / "previous.docx"
    prev.write_bytes(data)
    return prev


def build_reference_doc(base_docx, ref_docx):
    """Copy the base DOCX with style sizes rewritten, an empty body, and no images or links."""
    with zipfile.ZipFile(base_docx) as zin:
        styles = zin.read("word/styles.xml").decode("utf-8")
        styles = re.sub(r'(<w:rPrDefault><w:rPr>.*?<w:sz w:val=")\d+(")', rf"\g<1>{DEFAULT_SIZE}\2", styles, count=1, flags=re.S)
        styles = re.sub(r'(<w:rPrDefault><w:rPr>.*?<w:szCs w:val=")\d+(")', rf"\g<1>{DEFAULT_SIZE}\2", styles, count=1, flags=re.S)

        for style_id, size in STYLE_SIZES.items():
            m = re.search(r'<w:style [^>]*w:styleId="%s".*?</w:style>' % style_id, styles, re.S)
            if not m:
                continue
            block = m.group(0)
            sz = f'<w:sz w:val="{size}"/><w:szCs w:val="{size}"/>'
            if "<w:sz " in block:
                block = re.sub(r'<w:sz w:val="\d+"\s*/>', f'<w:sz w:val="{size}"/>', block)
                block = re.sub(r'<w:szCs w:val="\d+"\s*/>', f'<w:szCs w:val="{size}"/>', block)
            elif "<w:rPr>" in block:
                # sz/szCs come after color in the rPr sequence and before u, lang and the rest.
                start = block.index("<w:rPr>")
                tail = re.search(r"<w:(highlight|u|effect|bdr|shd|fitText|vertAlign|rtl|cs|em|lang|eastAsianLayout|specVanish|oMath)[\s/>]",
                                 block[start:])
                at = start + tail.start() if tail else block.index("</w:rPr>")
                block = block[:at] + sz + block[at:]
            else:
                block = block.replace("</w:style>", f"<w:rPr>{sz}</w:rPr></w:style>", 1)
            styles = styles[:m.start()] + block + styles[m.end():]

        # pandoc keeps the reference doc's relationships, so leftover images would ride along as orphans.
        document = zin.read("word/document.xml").decode("utf-8")
        sect_pr = re.findall(r"<w:sectPr\b.*?</w:sectPr>", document, re.S)
        body = f"<w:body><w:p/>{sect_pr[-1] if sect_pr else ''}</w:body>"
        document = re.sub(r"<w:body>.*</w:body>", lambda _: body, document, flags=re.S)
        rels = zin.read("word/_rels/document.xml.rels").decode("utf-8")
        rels = re.sub(r'<Relationship [^>]*Type="[^"]*/(image|hyperlink)"[^>]*/>', "", rels)
        replaced = {"word/styles.xml": styles, "word/document.xml": document, "word/_rels/document.xml.rels": rels}

        with zipfile.ZipFile(ref_docx, "w", zipfile.ZIP_DEFLATED) as zout:
            for item in zin.infolist():
                if item.filename.startswith(("word/media/", "docMetadata/")):
                    continue
                data = replaced[item.filename].encode("utf-8") if item.filename in replaced else zin.read(item.filename)
                zout.writestr(item, data)


def add_label(label, new_docx, out_docx):
    """Add the sensitivity label part, its content type and its package relationship."""
    with zipfile.ZipFile(new_docx) as zin, zipfile.ZipFile(out_docx, "w", zipfile.ZIP_DEFLATED) as zout:
        for item in zin.infolist():
            if item.filename == "docMetadata/LabelInfo.xml":
                continue
            data = zin.read(item.filename)
            if item.filename == "[Content_Types].xml" and b"/docMetadata/LabelInfo.xml" not in data:
                data = data.replace(b"</Types>", b'<Override PartName="/docMetadata/LabelInfo.xml" '
                                    b'ContentType="application/vnd.ms-office.classificationlabels+xml"/></Types>')
            elif item.filename == "_rels/.rels" and b"classificationlabels" not in data:
                data = data.replace(b"</Relationships>", b'<Relationship Id="rIdLabelInfo" '
                                    b'Type="http://schemas.microsoft.com/office/2020/02/relationships/classificationlabels" '
                                    b'Target="docMetadata/LabelInfo.xml"/></Relationships>')
            zout.writestr(item, data)
        zout.writestr("docMetadata/LabelInfo.xml", label)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("markdown", type=Path, help="markdown file to export")
    md_path = parser.parse_args().markdown.resolve()
    if not md_path.is_file():
        sys.exit(f"not found: {md_path}")

    pandoc = require("pandoc")
    folder = md_path.parent
    stem = md_path.name.removesuffix(".md").removesuffix(".prompt")
    docx = folder / f"{stem}.docx"
    assets = folder / ASSETS_DIR
    assets.mkdir(exist_ok=True)

    raw = md_path.read_bytes().decode("utf-8")
    nl = "\r\n" if "\r\n" in raw else "\n"

    with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as tmp:
        work = Path(tmp)

        print("Rewriting SVG image links")
        md, renders = rewrite_images(raw, folder)
        (folder / f"{stem}.docx-export.tmp.md").write_bytes(md.encode("utf-8"))

        print("Replacing mermaid blocks")
        md = replace_mermaid(md, assets, nl)
        md_final = folder / f"{stem}.docx-export.with-mermaid.tmp.md"
        md_final.write_bytes(md.encode("utf-8"))

        print("Rendering SVGs")
        render(renders, assets, work)

        referenced = set(renders) | {p.name for p in assets.glob("mermaid-diagram-*")}
        stale = sorted(p.name for p in assets.iterdir() if p.name not in referenced)
        if stale:
            print("  not referenced any more (left in place):", ", ".join(stale))

        print("Building reference doc")
        prev = previous_docx(docx, work)
        base = prev
        if base is None:
            base = work / "pandoc-default.docx"
            subprocess.run([pandoc, "-o", str(base), "--print-default-data-file", "reference.docx"], check=True)
        ref = work / "reference.docx"
        build_reference_doc(base, ref)

        print("Running pandoc")
        out = work / "out.docx"
        subprocess.run([pandoc, md_final.name, "-f", "markdown", "-t", "docx", "--reference-doc", str(ref),
                        "-o", str(out)], cwd=folder, check=True)

        label = None
        if prev is not None:
            with zipfile.ZipFile(prev) as z:
                if "docMetadata/LabelInfo.xml" in z.namelist():
                    label = z.read("docMetadata/LabelInfo.xml")
        if label:
            print("Carrying over sensitivity label")
            add_label(label, out, work / "labelled.docx")
            out = work / "labelled.docx"

        shutil.copyfile(out, docx)
    print(f"Wrote {docx}")


if __name__ == "__main__":
    main()
