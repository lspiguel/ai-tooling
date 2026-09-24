# Authoring markdown that exports well

The publish scripts read pandoc markdown, which is a superset of what wikis render. This page covers the conventions that change the output. Everything here also renders sensibly in an Azure DevOps wiki, except where a row says otherwise.

## Diagrams and images

| Want | Write | Why |
|---|---|---|
| A diagram that stays editable | A `` ```mermaid `` block, or `::: mermaid` … `:::` (the Azure DevOps wiki form) | Both are rendered to PNG by every publish script |
| An image in a document | `![Alt text](.attachments/name.png)` **on its own line** | PPTX drops images that sit inside a sentence. DOCX, HTML and PDF show them either way |
| A specific print width | `![Alt](img.png){width=3in}` | pandoc attribute. Azure DevOps wiki shows the braces as text, so use it only in documents meant for export |
| An SVG | Link it normally | DOCX and PPTX get a PNG rendering; HTML and PDF use the SVG as is. Size comes from the SVG's `width`/`height`, printed at 120 px per inch, capped at 6.5 in |
| A small icon inline | An SVG of 64 px or less | Printed 0.2 in wide, rendered at 4× |

Keep diagram text short. Mermaid PNGs are scaled to fit the page width, so a wide diagram prints small.

## Page breaks (DOCX and PDF)

A line holding only this starts a new page:

```markdown
<div class="page-break"></div>
```

The PDF stylesheet breaks the page there, and `export_md_to_docx.py` turns the line into a Word page break. A wiki renders it as an empty element, so nothing shows. PPTX and HTML ignore it.

## Slide decks (PPTX)

Write the deck as an outline:

```markdown
---
title: Onboarding Playback
subtitle: Sprint 4 review
---

# Delivered                  ← section title slide

## What changed              ← a slide

- Account plugin validates duplicates
- Onboarding flow sends welcome email

::: notes
Speaker notes: mention the rule was agreed with the data lead.
:::

## Before and after

:::::: columns
::: column
**Before**

- Manual checks
:::
::: column
**After**

- Automatic validation
:::
::::::
```

| Rule | Why |
|---|---|
| `#` makes a section slide, `##` a content slide | pandoc picks the slide level as the highest heading level that is directly followed by content somewhere in the outline. One stray paragraph under a `#` heading changes it, so pass `--slide-level 2` when the outline is irregular |
| The YAML `title`/`subtitle` makes the title slide | Omit it to start straight at the first slide |
| At most about six bullets per slide | Overflowing text is not split onto a new slide |
| One image per slide, on its own line | Images are sized to the content area; an image inline in text is dropped |
| `--reference-doc template.pptx` for branding | Layouts must keep PowerPoint's default names (*Title Slide*, *Title and Content*, *Section Header*, *Two Content*, *Comparison*, *Content with Caption*, *Blank*); pandoc picks layouts by name |

Speaker notes don't survive a PPTX → markdown import, so keep the markdown outline as the master.

## Documents for Word (DOCX)

- **Styles come from the committed `<stem>.docx`.** Style it once in Word (fonts, heading colours, margins, header and footer, sensitivity label), commit it, and every later export reuses it. Body text is set to 9 pt and headings scale from there, by design of the export script.
- **Title block:** YAML `title`, `subtitle`, `author` and `date` use Word's Title, Subtitle, Author and Date styles.
- **Comments and revisions:** the spans in [SKILL.md](../SKILL.md#review-round-trip) become real Word comments and tracked changes. Give every comment a unique `id`, and pair each `.comment-start` with a `.comment-end` of the same `id`.
- **Tables:** keep them to pipe tables. Cells cannot hold lists or multiple paragraphs in a pipe table. Split the content into rows, or accept a grid table (which the wiki won't render).

## Documents for PDF and HTML

- The stylesheet is [assets/document.css](../assets/document.css). Pass `--css` for a different one, and keep its `@page` and `@media print` rules if it is for PDF.
- `--toc` adds a table of contents from the headings.
- Relative links to other `.md` pages are kept as links, and will be broken for anyone without the wiki. Link to the published wiki URL, or remove them, before sending.
