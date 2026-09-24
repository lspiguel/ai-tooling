---
name: document-conversion
description: Converts project documentation between formats people share (DOCX, PPTX, XLSX, PDF, PNG) and formats an agent can edit reliably (Markdown, Mermaid, SVG, HTML, CSV), in both directions, using bundled pandoc, mmdc, headless-browser and openpyxl scripts. Covers importing Word, PowerPoint, Excel and PDF files into markdown or CSV, keeping reviewer comments and tracked changes; publishing markdown to Word, PDF, PowerPoint or self-contained HTML; rendering mermaid and SVG diagrams to PNG; writing edited CSVs back into the original workbook without losing its formatting; and checking or installing the tools needed. Use whenever a document or diagram has to change format - someone hands over a .docx, .pptx, .xlsx or .pdf to work from, a markdown page or design doc must go out as Word, PDF or slides, a reviewed Word document comes back with comments, a spreadsheet needs editing, or a diagram needs to be an image - even if the request only says "send this to the client", "make this a deck" or "read this spec".
---

# Document conversion

Agents work well on text: Markdown, Mermaid, SVG, HTML and CSV can be read whole, edited precisely and diffed. People work from DOCX, PPTX, XLSX, PDF and PNG. This skill moves documents between the two families:

| Direction | From → To | Why |
|---|---|---|
| **Import** | Shareable → editable | So the agent can read and change the content |
| **Publish** | Editable → shareable | So people can review, sign off and pass it on |

**The editable file is the master.** A published DOCX, PDF, PPTX or HTML file is a derived output: regenerate it rather than hand-edit it. When a shareable file comes back with changes, import it and carry the changes into the master. Don't replace the master with the import, because every import loses something (see [fidelity.md](./references/fidelity.md)).

All scripts are in [scripts/](./scripts/). Run them with `python` or `pwsh`/`powershell` from any folder; they write beside the source unless given `-o`. Every script has `--help` (or `Get-Help`) with its full options.

---

## Decide: which conversion

| You have | You need | Run | Notes |
|---|---|---|---|
| `.docx` | Markdown to read or edit | `python scripts/import_to_markdown.py doc.docx` | Images to `.attachments/<doc>-imageN.ext`. Comments and tracked changes are kept automatically when present |
| `.docx` returned with review comments | The comments, to address them | same, default `--track-changes auto` | See [Review round trip](#review-round-trip) |
| `.pptx` | Markdown | `python scripts/import_to_markdown.py deck.pptx` | Slide titles, text, tables, images. Layout, bullets and speaker notes are lost |
| `.xlsx` | To **read** a small sheet | `python scripts/import_to_markdown.py book.xlsx` | One markdown table per sheet |
| `.xlsx` | To **edit** data | `python scripts/xlsx_to_csv.py book.xlsx` | `<stem>-sheets/NN-<sheet>.csv` |
| Edited CSVs | `.xlsx` that keeps the original formatting | `python scripts/csv_to_xlsx.py book-sheets -o book-updated.xlsx --base book.xlsx` | See [Spreadsheet round trip](#spreadsheet-round-trip) |
| CSVs the agent wrote | A new `.xlsx` | `python scripts/csv_to_xlsx.py data.csv more.csv -o book.xlsx` | Styled header, filters, frozen row; `--infer-types` for numbers and dates |
| `.pdf` | Markdown | `python scripts/import_to_markdown.py spec.pdf` | Plain text with `<!-- page N -->` markers. `--layout` for tables. No OCR |
| A folder of `.docx`/`.pptx`/`.xlsx`/`.mmd` | Markdown / SVG / PNG for all of it | `tooling/scripts/Convert-ToMarkdownAndSVG.ps1 -Path <folder>` | Repository script, restartable: skips anything already converted |
| Markdown | `.docx` | `python scripts/export_md_to_docx.py doc.md` | Styles from the committed `<stem>.docx`; sensitivity label kept |
| Markdown | `.pdf` | `python scripts/export_md_to_pdf.py doc.md [--paper a4]` | Browser print of the HTML below |
| Markdown | One `.html` file to mail or attach | `python scripts/export_md_to_html.py doc.md [--toc]` | Everything embedded |
| Markdown outline | `.pptx` | `python scripts/export_md_to_pptx.py deck.md [--reference-doc template.pptx]` | Outline rules in [authoring-for-export.md](./references/authoring-for-export.md) |
| Markdown with mermaid | Markdown with images, for a wiki or viewer that does not render mermaid | `python scripts/render_mermaid.py page.md` | Writes `<stem>.rendered.md`; source kept as `.mmd` |
| `.mmd` | `.png` / `.svg` | `python scripts/render_mermaid.py diagram.mmd --format both` | |
| `.svg` | `.png` | `python scripts/export_svg_to_png.py a.svg b.svg` | For work items, chat, slides, email |
| `.html` | `.png` (cropped to content) | `powershell -File scripts/export-html-to-png.ps1 -Path page.html` | Windows; social cards, one-page visuals |
| `.html` | `.pdf` | `python scripts/export_html_to_pdf.py page.html` | Page size from the page's `@page` CSS |

**Choosing among publish formats:**

| The reader needs to | Publish as |
|---|---|
| Edit, comment or track changes, or it must follow a Word template | DOCX |
| Read and sign off, and must not change it | PDF |
| Present it | PPTX |
| Open it anywhere without Office, with working links | HTML |
| See it inline in a work item, chat or slide | PNG |

**Not covered here:** draw.io and Visio diagrams, OCR of scanned PDFs, turning a photo or screenshot into a diagram, and HTML or email import. Say so rather than improvising a lossy route.

---

## Procedure

### 1. Check the tools

```powershell
.\scripts\Test-ConversionPrerequisites.ps1 -Conversion md-to-pdf
```

It lists each tool the chosen conversions need, its version or `MISSING`, and the install command (Chocolatey when elevated, winget otherwise, npm/pip for packages). Omit `-Conversion` to check everything. A script that lacks a tool also stops and prints the install command.

**Installing is the user's decision.** Show them what is missing and the command, and ask before running anything, including the check script's `-Install` switch. Chocolatey needs an administrator shell. After installing, a new shell is needed before the tool is on PATH. [setup.md](./references/setup.md) covers each tool, other platforms and common install failures.

### 2. Convert

Run the script from the decision table. Keep the source's folder layout in mind:

- **Attachments** go to `.attachments/` beside the output, named `<doc>-imageN.ext` or `<stem>-mermaid-N.png`. This is the Azure DevOps wiki convention, and relative links keep working when the page and its `.attachments/` move together.
- **Existing outputs are protected.** Imports refuse to overwrite without `--force`, and `csv_to_xlsx.py` refuses to write over its `--base`. Before forcing, check whether the existing file holds edits that the new one would lose.
- **Generated temp files.** `export_md_to_docx.py` leaves `*.docx-export*.tmp.md` and a `_docx-assets/` folder beside the source, so a repository using it should ignore `*.tmp.md`. The other publish scripts work in a temp folder.

### 3. Verify the output

A script exiting cleanly doesn't prove the output is right. Check what matters for this conversion:

| Conversion | Check |
|---|---|
| Any import | Read the result. Headings, tables and lists came through, and the image count matches the script's report |
| PDF import | Structure is gone. Restore headings and tables by hand for anything that will be edited, and treat numbers read from reflowed tables with suspicion |
| PPTX import | Slide titles and text came through. Speaker notes, bullet structure and shape-drawn diagrams do not; say so if the deck relies on them |
| XLSX → CSV | The script's notes: formulas written as values, **never-calculated formulas** (empty cells), merged ranges, hidden sheets |
| CSV → XLSX `--base` | The report: formulas kept, replaced or cleared. "Replaced" means an edited cell overwrote a formula; confirm that was intended |
| Any publish | Open or inspect it. For DOCX/PPTX, re-import to markdown and compare. For PDF/PNG, render it and look |
| Mermaid | Diagrams render. A syntax error stops the script with mmdc's message |

### 4. Hand over

Tell the user where the output is, what was lost or needs checking (from step 3), and what is left for them to do. For example: "comments were addressed in the markdown; the DOCX needs sending back to the reviewer".

---

## Review round trip

For a Word document that comes back from a reviewer:

1. **Import it:** `python scripts/import_to_markdown.py returned.docx -o returned.md`. Comments and tracked changes arrive as pandoc spans:
   ```markdown
   [inserted]{.insertion author="R. Reviewer" date="..."}  [removed]{.deletion author=... }
   [Comment text]{.comment-start id="0" author="R. Reviewer" date="..."}commented range[]{.comment-end id="0"}
   ```
2. **Address each comment in the master**, not in the import. The import has lost mermaid source (it's an image now) and any markdown the DOCX couldn't carry.
3. **To reply in the document**, add a comment span of your own in the master (new `id`, your `author`), then export. `export_md_to_docx.py` turns the spans into real Word comments and revisions. Replies don't thread under the original; they appear as separate comments on the range they cover.
4. **Before final publication**, remove the spans. Accept an insertion by keeping its text, and accept a deletion by removing it.

Use `--track-changes accept` to read the document as if every change were accepted, or `reject` for the version before review.

## Spreadsheet round trip

1. `python scripts/xlsx_to_csv.py mapping.xlsx` writes `mapping-sheets/01-<sheet>.csv`, and so on.
2. Edit the CSVs. **Edit rows in place, and add new rows at the end.** Inserting or deleting rows in the middle shifts everything below, so formulas and formatting below the change end up on the wrong rows.
3. `python scripts/csv_to_xlsx.py mapping-sheets -o mapping-updated.xlsx --base mapping.xlsx`.
4. Read the report, then open the result in Excel and save it there. openpyxl doesn't calculate formulas, so until Excel does, any tool reading the file (including `xlsx_to_csv.py`) sees them as empty.

Charts, images and shapes in the base are dropped (openpyxl can't carry them), and the script warns when the base has any. For such a workbook, hand the CSV changes to a person to apply, or publish a new workbook alongside.

---

## Guardrails: what a human must confirm

| Before | The human confirms |
|---|---|
| Installing tools | Which tools, by which installer, with admin rights where Chocolatey needs them |
| Overwriting with `--force` | The existing output has no edits of its own that would be lost |
| Treating an import as source | It was checked against the original. PDF and PPTX imports especially lose structure |
| Carrying review feedback into the master | Every comment and tracked change was addressed, not just the easy ones |
| Sending a DOCX/PDF/PPTX outside the team | It was opened and read as the recipient will see it, and review spans were removed |
| Sending anything to a client | No other client's names, data or environment details are embedded, including in images, comments and document properties |
| Replacing a workbook with a `--base` rebuild | Formulas reported as replaced were meant to be replaced, and nothing dropped (charts, images) was needed |

---

## References

| File | Read it when |
|---|---|
| [setup.md](./references/setup.md) | A tool is missing, an install fails, or you're on macOS or Linux |
| [fidelity.md](./references/fidelity.md) | Deciding whether a conversion is safe for this document, or explaining what was lost |
| [authoring-for-export.md](./references/authoring-for-export.md) | Writing markdown that will be published: slide outlines, page breaks, image sizes, comments |
