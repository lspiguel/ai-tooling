# document-conversion — build notes

Working record of how this skill was scoped and built. Not loaded by the skill; kept for the people maintaining it. Remove or fold into the PR description once the skill is stable.

## Intent

Convert project documentation between two families of formats:

| Family | Formats | Why |
|---|---|---|
| **Editable** | Markdown, Mermaid, SVG, HTML, CSV | An agent can read, diff and change them reliably |
| **Shareable** | DOCX, PPTX, XLSX, PDF, PNG | What people open, review, sign off and pass around |

**Import** is shareable → editable. **Publish** is editable → shareable. The skill is about conversion, not about any one documentation process. It is deliberately not coupled to `d365ce-documenting`.

## Starting point — existing scripts reviewed

| Script | Conversions |
|---|---|
| `tooling/scripts/Convert-ToMarkdownAndSVG.ps1` | DOCX/PPTX/XLSX → Markdown (images to `.attachments/<doc>-imageN.ext`); `.mmd` → SVG and PNG |
| `tooling/scripts/export_md_to_docx.py` | Markdown → DOCX (styles from the committed DOCX, 9 pt body, sensitivity label carried over; SVG links → PNG; mermaid → PNG) |
| `tooling/scripts/export-html-to-png.ps1` | HTML → PNG, cropped to content |

## Decisions (2026-09-24)

1. **Scope for v1:** every High and Medium conversion from the review, except draw.io.
   - High: DOCX with comments/tracked changes → Markdown keeping them; XLSX → CSV per sheet; CSV → XLSX; PDF → Markdown; Markdown → PDF; Markdown with mermaid → Markdown with rendered images.
   - Medium: Markdown → self-contained HTML; SVG → PNG standalone; HTML → PDF; Markdown outline → PPTX.
   - Deferred: draw.io ↔ PNG/SVG/PDF (Medium, excluded by decision); all Low rows — Visio → Mermaid/draw.io, image → Mermaid, HTML/`.msg`/`.eml` → Markdown.
2. **Location:** scripts move into `skills/document-conversion/scripts/`. **Exception:** `tooling/scripts/Convert-ToMarkdownAndSVG.ps1` stays where it is, untouched. It is the whole-folder, restartable bulk converter; skipping existing outputs is an intentional optimization, not a defect. The skill points at it for bulk imports.
3. **Language follows the library.** PowerShell and Python are both assumed available. The skill carries install guidance and a prerequisite check, preferring Chocolatey (winget as fallback).
4. **Azure DevOps wiki conventions** for naming what the scripts save (`.attachments/`, `<doc>-imageN.ext`). Used for naming clarity only; the skill is not tied to any wiki workflow.
5. **Name:** `document-conversion`.

## Design choices made while building

| Choice | Reason |
|---|---|
| The Markdown master is never modified by a publish script | Keeps one editable source; every shareable output is derived and reproducible |
| Mermaid is rendered to PNG, not SVG, for every shareable output | mmdc SVGs use `foreignObject` for labels, which Word, PowerPoint and many viewers do not render |
| Mermaid PNGs always on a white background | The folder script used transparent and the DOCX script white; outputs looked different by route |
| One shared Python module (`convert_common.py`) for browser lookup, mermaid, SVG → PNG and HTML → PDF | The DOCX script only looked for Edge at two fixed paths; the HTML script looked for Chrome or Edge. One lookup now |
| New publish scripts write intermediate files to a temp folder and use pandoc `--resource-path` | Avoids `*.tmp.md` files next to the source. `export_md_to_docx.py` keeps its existing behaviour (tmp files, `_docx-assets/`) to avoid changing a script already in use |
| Tracked changes and comments are kept automatically on import when the DOCX has any | Silently accepting reviewer feedback is the costliest failure; the agent can still resolve them explicitly |
| CSV import/export keeps every cell as text unless asked to infer types | Leading zeros in IDs, codes and phone numbers are lost by type inference |
| XLSX → CSV names files `NN-<sheet>.csv` | Keeps sheet order through the round trip; CSV → XLSX strips the prefix |
| `csv_to_xlsx.py --base` writes values into a copy of the original workbook | The high-value case is a client workbook with formatting and dropdowns; rebuilding from CSV would lose them. Keeps a formula while the CSV still shows its cached value; matches sheet names that were sanitized for file names |
| PDF import through `pdftotext` only; markitdown not used | markitdown's PDF path is pdfminer text too, so it adds a dependency for no structural gain. Scanned PDFs (OCR) are out of scope |
| Single-file importer (`import_to_markdown.py`) alongside the bulk folder script | Needs `--force` re-import, comment handling and per-file output; the bulk script stays untouched per decision 2 |
| Imports write pipe tables (`markdown-simple_tables-multiline_tables-grid_tables`) and `--wrap=none` | Pipe tables render in the wiki and diff well; unwrapped paragraphs keep agent edits from re-flowing whole paragraphs |
| Imported images numbered by first use in the document | pandoc-produced DOCX names media `rIdNN`, so numbering by file name gave arbitrary order |
| `<div class="page-break"></div>` is the one page-break syntax | PDF honours it through the stylesheet; the DOCX export turns it into a Word break; invisible in the wiki |

## Findings while building

| Finding | Effect |
|---|---|
| **Edge returns before its headless screenshot/PDF is written** (reproduced three times with fresh profiles) | The original `export_md_to_docx.py` silently lost SVG images when Edge was the browser: pandoc replaced them with alt text. Fixed by waiting for the output file in `convert_common.run_browser` and in `export-html-to-png.ps1` |
| The original DOCX export ignored `::: mermaid` blocks (Azure DevOps wiki syntax) | Fixed through the shared `MERMAID_BLOCK` pattern |
| pandoc round-trips comments and tracked changes: DOCX → spans → DOCX | Enables the review round trip. Comment replies (`parent=`) do not thread on export |
| pandoc's PPTX reader drops speaker notes and bullet structure; its PPTX writer drops inline images | Documented in `references/fidelity.md` |
| On this machine `pdftotext` exists only inside Git Bash, not on the Windows PATH | The prerequisite check reports it missing from PowerShell, which is correct; noted in `references/setup.md` |
| Excel is not installed on the build machine | Cached formula values were simulated by editing the sheet XML; "Excel recalculates on open" is stated only as "open and save in Excel" |

## Progress

- [x] Review existing scripts, list conversions, propose additions
- [x] Decisions recorded
- [x] Branch `tooling/document-conversion-skill`; `export-html-to-png.ps1` and `export_md_to_docx.py` moved with `git mv`
- [x] Shared module and new scripts (`convert_common.py`, `import_to_markdown.py`, `xlsx_to_csv.py`, `csv_to_xlsx.py`, `render_mermaid.py`, `export_md_to_html.py`, `export_md_to_pdf.py`, `export_md_to_pptx.py`, `export_html_to_pdf.py`, `export_svg_to_png.py`); `export_md_to_docx.py` refactored onto the shared module
- [x] Prerequisite check script (`Test-ConversionPrerequisites.ps1`) and `references/setup.md`
- [x] SKILL.md, `references/fidelity.md`, `references/authoring-for-export.md`, `assets/document.css`
- [x] Scripts verified against sample documents (DOCX, reviewed DOCX, PPTX, XLSX with formula/validation/odd sheet name, PDF, markdown with both mermaid syntaxes, SVG diagram and icon)
- [ ] Test prompts drafted and reviewed with the user
- [ ] Eval runs (with and without skill), review, iterate
- [ ] Description optimization
- [ ] Repository wiring (README / INSTRUCTIONS mentions if wanted), PR
