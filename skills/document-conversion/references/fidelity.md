# Fidelity — what each conversion keeps and loses

Read this before relying on a conversion for a particular document, and use it to tell the user what to check. "Verified" rows were exercised on sample documents with pandoc 3.8, mermaid-cli 11, Chrome and Edge on Windows. The rest follow from how the underlying tool works.

## Imports (shareable → editable)

| Conversion | Kept | Lost or changed |
|---|---|---|
| **DOCX → Markdown** | Headings, paragraphs, bold/italic, links, lists, simple tables (as pipe tables), images (to `.attachments/`, numbered in order of use), footnotes. **Comments and tracked changes** as spans (verified) | Page layout, headers and footers, text boxes, fonts and colours, custom styles, fields (TOC, cross-references become text), merged table cells. Tables too complex for a pipe table come through as HTML. Diagrams arrive as images: **mermaid source is not recovered** from a DOCX that was exported from markdown |
| **PPTX → Markdown** | Slide titles (as `##` headings), text, tables, images (verified) | **Speaker notes** (verified lost), bullet structure (bullets become paragraphs, verified), layout and positioning, shapes and SmartArt, animations. Image alt text becomes the embedded file name |
| **XLSX → Markdown** | Cell values as one table per sheet | Everything but values. Wide sheets become unreadable tables, so use CSV for anything with more than a handful of columns |
| **XLSX → CSV** | Values as last calculated by Excel, or formula text with `--formulas`. Sheet order via the `NN-` prefix. Dates as ISO 8601. Leading zeros in text cells (verified) | Formatting, merged-cell layout (value stays top-left), comments, data validation, charts and images. **Formulas never calculated** (workbook saved by a tool other than Excel) come out empty; the script counts them (verified). Sheet-name characters Windows forbids in file names become `_`; `--base` matches them back (verified) |
| **PDF → Markdown** | Text in reading order, page boundaries as `<!-- page N -->` (verified) | All structure: headings, lists and tables become plain lines, and table columns can come out one after another (verified). Images, links, form fields. Scanned pages have no text layer, so they come out empty (no OCR). Multi-column layouts may interleave; `--layout` keeps physical columns instead |

## Publishing (editable → shareable)

| Conversion | Kept | Lost or changed |
|---|---|---|
| **Markdown → DOCX** | Headings, paragraphs, lists, tables, links, footnotes, images. Mermaid (both block styles) and SVG rendered to PNG (verified). **Comment and tracked-change spans become real Word comments and revisions** (verified). Styles, page setup, headers and footers from the committed DOCX. Sensitivity label from the committed DOCX | Comment replies do not thread; each is its own comment (verified). Raw HTML is dropped. Table column widths are pandoc's choice. Inline icons are sized at 0.2 in |
| **Markdown → HTML** | Everything the browser renders. Images, CSS and mermaid PNGs embedded in one file (verified) | Nothing structural. Links to other wiki pages (relative `.md` links) will not resolve for the recipient; rewrite or remove them before sending |
| **Markdown → PDF** | As HTML, paginated at Letter or A4 with 0.75 in margins (verified). The stylesheet asks the browser to keep tables, figures and code blocks on one page where they fit | Word-template styling. For a PDF that must match a corporate Word template, export DOCX and save as PDF from Word. No PDF bookmarks |
| **Markdown → PPTX** | Section and slide titles, bullets, tables, images, mermaid as PNG, `::: notes` as speaker notes, `:::: columns` (verified). Theme and layouts from `--reference-doc` | **Inline images inside a sentence are dropped** (verified). Put each image in its own paragraph. Text that overflows a slide is not split, so check long slides |
| **Markdown → Markdown with rendered mermaid** | Every `` ```mermaid `` and `::: mermaid` block becomes a PNG link plus a comment naming the saved `.mmd` source (verified). Unchanged diagrams are not re-rendered (verified) | Nothing in the source page; it is not modified. The output page shows images, which no longer change when the diagram source does. Re-run after edits |
| **Mermaid → PNG** | 3× scale, white background | Transparency. The bulk repository script renders transparent PNGs, so outputs differ by route |
| **Mermaid → SVG** | Vector, sharp at any size | Labels are HTML (`foreignObject`): they show in browsers, but **not in Word, PowerPoint, Inkscape or most image viewers**. Use PNG for anything leaving a browser |
| **SVG → PNG** | Rendering exactly as the browser draws it, including web fonts available locally and mermaid SVGs | Fonts not installed on the machine fall back. Icons (64 px or smaller) go through ImageMagick, which draws text with its own font handling |
| **HTML → PNG** | Cropped to content against the corner pixel's colour | Content taller than `-MaxHeight` is cut, with a warning. Interactive state; animations are captured at five seconds |
| **HTML → PDF** | The page as printed, with `@page` size and margins | Anything the page hides with print CSS. Scripts get five seconds to run |
| **CSV → XLSX (new)** | Values as text; typed numbers, dates and booleans with `--infer-types` (integers with a leading zero stay text, verified). Styled, frozen, filtered header (verified) | Anything a CSV cannot hold |
| **CSV → XLSX (`--base`)** | Formatting, column widths, data validation (verified), conditional formatting, defined names, untouched sheets. Each cell's original type when the new text still parses as it (verified). Formulas whose calculated value is unchanged (verified) | **Charts, images and shapes** (openpyxl drops them; warned). Formulas in edited cells (reported). Inserted or deleted rows shift everything below, so formulas and formatting land on the wrong rows. Formulas are uncalculated until the workbook is opened and saved in Excel |

## Round trips worth knowing

| Round trip | Result |
|---|---|
| DOCX → MD → DOCX, with the original committed as `<stem>.docx` | Styles, page setup and sensitivity label come back. Comments and tracked changes survive both ways. Merged cells, text boxes and fields do not |
| MD → DOCX → MD | Text, tables and structure survive. Mermaid and SVG come back as PNGs, not source. **Never let this replace the markdown master** |
| XLSX → CSV → XLSX with `--base` | Values edited in place come back into the original formatting. Structural edits (row inserts) and charts do not |
| MD → PPTX → MD | Titles and text survive. Notes, bullets and inline images do not |
