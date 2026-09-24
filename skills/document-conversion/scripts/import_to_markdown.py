"""Import one DOCX, PPTX, XLSX or PDF file as markdown an agent can edit.

Usage:
    python import_to_markdown.py <file> [-o out.md] [--force] [--track-changes auto|accept|reject|all] [--layout]

Writes <stem>.md next to the source unless -o is given. Existing output is not overwritten without --force.

DOCX and PPTX: pandoc. Embedded images go to .attachments/<doc>-imageN.<ext> beside the markdown, the Azure
DevOps wiki convention, and image size attributes are dropped. Tracked changes and comments are kept by default
when the document has any (--track-changes auto), as pandoc spans:
  [inserted]{.insertion author=".." date=".."}   [removed]{.deletion ..}
  [comment text]{.comment-start id="0" author=".."}commented range[]{.comment-end id="0"}
export_md_to_docx.py turns those spans back into Word revisions and comments.

XLSX: one markdown table per sheet, for reading. To edit sheet data, use xlsx_to_csv.py instead.

PDF: text through pdftotext (poppler), in reading order, with a <!-- page N --> marker at each page start.
Headings, lists and tables are not reconstructed; --layout keeps column alignment for table-heavy pages.
Scanned PDFs have no text layer and produce empty pages.

For a whole folder, restartable, use tooling/scripts/Convert-ToMarkdownAndSVG.ps1 instead.
"""
import argparse
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

from convert_common import require

IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".emf", ".wmf", ".svg"}


def safe_doc_name(name):
    return re.sub(r'[\\/:*?"<>|]', "", re.sub(r"\s+", "-", name))


def docx_review_marks(path):
    """Count tracked insertions, deletions and comments in a DOCX."""
    with zipfile.ZipFile(path) as z:
        doc = z.read("word/document.xml").decode("utf-8", errors="ignore")
        comments = z.read("word/comments.xml").decode("utf-8", errors="ignore") if "word/comments.xml" in z.namelist() else ""
    return len(re.findall(r"<w:ins\b", doc)), len(re.findall(r"<w:del\b", doc)), len(re.findall(r"<w:comment\b", comments))


def relink_media(md, work, media_dir, md_path, doc_name):
    """Copy extracted images to .attachments/<doc>-imageN.<ext>, numbered in order of first use, and relink them."""
    images = [p for p in (work / media_dir).rglob("*") if p.is_file() and p.suffix.lower() in IMAGE_EXTENSIONS]
    # pandoc writes the path it extracted to, relative to its working directory; either slash direction.
    written = {p: {p.relative_to(work).as_posix(), str(p.relative_to(work))} for p in images}
    first_use = lambda p: min((i for i in (md.find(w) for w in written[p]) if i >= 0), default=len(md))
    images.sort(key=lambda p: (first_use(p), p.as_posix()))
    if images:
        attachments = md_path.parent / ".attachments"
        attachments.mkdir(exist_ok=True)
    for index, image in enumerate(images, 1):
        ext = ".jpg" if image.suffix.lower() == ".jpeg" else image.suffix.lower()
        target = f"{doc_name}-image{index}{ext}"
        shutil.copyfile(image, md_path.parent / ".attachments" / target)
        for w in written[image]:
            md = md.replace(w, f".attachments/{target}")
    # Image size attributes and titles such as (.attachments/x.png "Picture 2") do not render in Azure DevOps wiki.
    md = re.sub(r"(!\[[^\]]*\]\([^)]*\))\{[^}]*\}", r"\1", md)
    md = re.sub(r'\((\.attachments/[^)\s]+)\s+"[^"]*"\)', r"(\1)", md)
    return md, len(images)


def import_office(src, out, fmt, track_changes):
    pandoc = require("pandoc")
    # Pipe tables render in wikis and diff well; tables they cannot hold fall back to HTML.
    args = [pandoc, f"--from={fmt}", "--to=markdown-simple_tables-multiline_tables-grid_tables", "--wrap=none"]
    if fmt == "docx":
        ins, dels, comments = docx_review_marks(src)
        marked = bool(ins or dels or comments)
        mode = ("all" if marked else "accept") if track_changes == "auto" else track_changes
        if marked:
            print(f"  review marks: {ins} insertion(s), {dels} deletion(s), {comments} comment(s) - track changes: {mode}")
        args.append(f"--track-changes={mode}")

    with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as tmp:
        work = Path(tmp)
        if fmt in ("docx", "pptx"):
            args.append("--extract-media=extracted")
        result = subprocess.run([*args, str(src)], cwd=work, capture_output=True, text=True, encoding="utf-8")
        if result.returncode != 0:
            sys.exit(f"pandoc failed on {src}:\n{result.stderr.strip()}")
        for line in result.stderr.splitlines():
            print(f"  pandoc: {line}")
        md = result.stdout
        if (work / "extracted").exists():
            md, count = relink_media(md, work, "extracted", out, safe_doc_name(src.stem))
            if count:
                print(f"  extracted {count} image(s) to {out.parent / '.attachments'}")
    out.write_text(md, encoding="utf-8", newline="\n")


def import_pdf(src, out, layout):
    args = [require("pdftotext"), "-enc", "UTF-8"]
    if layout:
        args.append("-layout")
    result = subprocess.run([*args, str(src), "-"], capture_output=True)
    if result.returncode != 0:
        sys.exit(f"pdftotext failed on {src}:\n{result.stderr.decode(errors='ignore').strip()}")
    pages = result.stdout.decode("utf-8", errors="replace").replace("\r\n", "\n").split("\f")
    if pages and not pages[-1].strip():
        pages.pop()
    empty = sum(1 for p in pages if not p.strip())
    parts = [f"<!-- page {n} -->\n\n{p.strip()}\n" for n, p in enumerate(pages, 1)]
    out.write_text("\n".join(parts), encoding="utf-8", newline="\n")
    print(f"  {len(pages)} page(s); plain text only - headings, lists and tables need restoring by hand")
    if empty:
        print(f"  {empty} page(s) have no text layer (scanned?); those need OCR, which this script does not do")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("source", type=Path)
    parser.add_argument("-o", "--output", type=Path, help="markdown file to write (default: <stem>.md beside the source)")
    parser.add_argument("--force", action="store_true", help="overwrite an existing output file")
    parser.add_argument("--track-changes", choices=["auto", "accept", "reject", "all"], default="auto",
                        help="DOCX only. auto keeps changes and comments when there are any (default)")
    parser.add_argument("--layout", action="store_true", help="PDF only: keep physical layout (tables, columns)")
    a = parser.parse_args()

    src = a.source.resolve()
    if not src.is_file():
        sys.exit(f"not found: {src}")
    out = (a.output or src.with_suffix(".md")).resolve()
    if out.exists() and not a.force:
        sys.exit(f"{out} exists; pass --force to overwrite it")
    out.parent.mkdir(parents=True, exist_ok=True)

    ext = src.suffix.lower()
    print(f"Importing {src.name}")
    if ext in (".docx", ".pptx", ".xlsx"):
        import_office(src, out, ext[1:], a.track_changes)
    elif ext == ".pdf":
        import_pdf(src, out, a.layout)
    else:
        sys.exit(f"unsupported file type: {ext} (expected .docx, .pptx, .xlsx or .pdf)")
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
