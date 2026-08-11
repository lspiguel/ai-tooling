# Wiki mechanics

The skill assumes a wiki site edited in markdown and reachable via git. The mechanics below are structural — getting them wrong produces a page that exists but does not appear. They are verified against **Azure DevOps wikis**; other products (GitHub wikis, GitLab wikis, static site generators) differ on most of them. When working in a different product, check each point against its documentation before relying on it, and above all match what the existing wiki already does.

## Page and folder naming

**Hyphens render as spaces** (Azure DevOps). A page titled "[Feature] functionality" is the file `[Feature]-functionality.md`. Never create a file or folder with a literal space, even when the user names the page with spaces.

**A folder needs a sibling page of the same name to be navigable** (Azure DevOps). `Modules/[Module]/` requires `Modules/[Module].md` next to it. Creating the folder alone produces child pages with no parent.

## Sidebar ordering

**`.order` files control sidebar ordering** (Azure DevOps). Every folder has one, listing its child page names without the `.md`. A page absent from `.order` may not appear. When adding a page, add it to the `.order` in the position that matches the existing logic — alphabetical, or appended, whichever the file already does.

## Links

**Links are root-relative and drop the extension** (Azure DevOps): `/Modules/[Module]/[Feature]-functionality`, not a relative path and not `.md`. The root README in some wikis uses relative `.md` links instead — match whatever that file already does rather than imposing the other form.

## In-page table of contents

**`[[_TOC_]]`** on its own line generates the in-page table of contents (Azure DevOps). Put it after the opening paragraph and the companion link.

## Diagrams

**Mermaid fenced code blocks render** (verified in Azure DevOps; other wikis vary in whether and how mermaid is supported). Use `flowchart` with `subgraph` per layer for component maps and `sequenceDiagram` for runtime flows.
