# To-do / Pending — workflows release polish

Working list compiled from all files under `workflows/`. The per-file "Open points for iteration" sections have been moved here verbatim (grouped by file); the "Spotted during review" sections at the end list additional edits found while sweeping the folder.

---

## Open questions moved out of the activity guides

### Column 1 — General-Purpose AI Assistants

**[1.1] Specification** (`1-general-purpose-assistants/1.1-specification.md`)
- [x] The **reconciliation** and **requirement-mining** prompts are new drafts (not from the README) — do they match how you actually phrase these?

**[2.1] Planning** (`1-general-purpose-assistants/2.1-planning.md`)
- [x] The design/sequencing/risk prompts are new drafts (no README source) — adjust to your house style.

**[3.1] Tasking** (`1-general-purpose-assistants/3.1-tasking.md`)
- [x] The checklist prompts are new drafts — the READMEs have no tasking-specific prompts. Do you have existing tasking prompts in your personal library ([B.1]) to fold in?
- [x] Confirm your standard **DoD items** so the first prompt's list stops being a guess.
- [x] **Which ADO import mechanism do you actually use** — `az boards` CLI, REST API, or CSV bulk import? The script prompt currently offers all three; pin it once confirmed.

**[4.1] Implementation** (`1-general-purpose-assistants/4.1-implementation.md`)
- [x] The FetchXML/OData prompts are borrowed from the column-3 README (they work identically in chat) — OK to duplicate across both columns, or should one link to the other?
- [x] The Power Fx and rubber-ducking prompts are new drafts — review.
- [x] How deep should maker-portal copilot coverage go here? The matrix stubs it as future column [4] — currently just one bridging prompt.

**[5.1] Validation / Peer review** (`1-general-purpose-assistants/5.1-validation-peer-review.md`)
- [x] UAT-script, design-review, and cross-check prompts are new drafts (README only covers test-case generation + accessibility) — review.
- [x] Should the design-review prompt be split per artifact type (solution design vs integration design vs data-migration plan)?

**[6.1] Documentation** (`1-general-purpose-assistants/6.1-documentation.md`)
- [x] Release-notes, user-guide, and training prompts are new drafts (README covers runbook + changelog) — review.
- [x] ~~What's the actual publishing flow to the ADO wiki~~ — resolved: n/a, wikis are not part of the column-1 workflow at all. Removed every ADO-wiki reference from column 1: the matrix's `[6] Documentation` cell and `[6.1]` detail paragraph, and the `6.1-documentation.md` prereq table row. (Wiki publishing is exclusively a column-3 concern — see `6.3-documentation.md`'s "Publish to the ADO wiki" procedure.)

### Column 3 — Coding Assistants & Agentic Environments

**[1.3] Specification** (`3-coding-assistants/1.3-specification.md`)
- [x] The gap-analysis, spec, and backlog prompts are new drafts built from the matrix cell; the two impact prompts come from the README. Does the gap-analysis flow match the planned **`d365-gap-analysis` skill** (build backlog) closely enough to become its seed?
- [x] ~~Which ADO MCP do you target~~ — resolved: no ADO MCP. Standardized on the **Azure DevOps CLI** (`az boards`, via the `azure-devops` extension) for all ADO work-item reads/writes across column 3, matching column 1's own `az boards` choice ([3.1-tasking.md](../1-general-purpose-assistants/3.1-tasking.md)). Updated: the matrix ([A.2] Boards, [B]/[C] setup rows, [1.3]/[3.3] cells, build-backlog table), `c.3-project-setup.md` §7 (auth steps: `az extension add --name azure-devops` + `az devops configure --defaults`), `b.3-initial-setup.md` §7 (note pointing at C.3 §7), `1.3-specification.md`, `2.3-planning.md`, `3.3-tasking.md` (including its title), and both READMEs. Dataverse MCP and Jira/GitHub-via-MCP are untouched — this only changes how ADO boards specifically are reached.

**[2.3] Planning** (`3-coding-assistants/2.3-planning.md`)
- [x] ~~The matrix mentions an "architecture skill" for ADR drafting~~ — resolved: no such skill is planned. Removed the "(architecture skill)" parenthetical from the matrix's `[2] Planning / Design` cell; the ADR prompt in `2.3-planning.md` stands on its own.
- [x] ~~Estimation: kept in column 1's sprint planning ([3.1]) for now~~ — resolved: yes. Added a repo-grounded "Estimation rationale grounded in the repo" activity to `2.3-planning.md` (cites real touched components, plugin/flow complexity, cross-solution dependencies, and test-coverage gaps instead of column-1's category-only cost drivers), plus a matching review-checkpoint row. Updated the matrix's `[2] Planning / Design` cell, the `[2.3]` detail paragraph, and the column-3 README's guide table to mention it.

**[3.3] Tasking** (`3-coding-assistants/3.3-tasking.md`)
- [x] ~~Confirm your branch-naming convention~~ — resolved: branches are per-story, named `{username}/{storyId}-{shortened-kebab-title}` (e.g. `jsmith/1234-add-account-validation`). Updated the "Scaffold the branch" prompt in `3.3-tasking.md` from the bracketed placeholder to this convention.
- [x] ~~Should branch scaffolding be per-story or per-task~~ — resolved: per-story.
- [x] ~~Is the "pre-flight for the coding agent" step something you want as a standard gate, or optional?~~ — resolved: removed. Dropped the "Pre-flight a task for the async coding agent" prompt from `3.3-tasking.md` entirely, and the matching "coding-agent pre-flight" mention from the column-3 README's guide table.

**[4.3] Implementation** (`3-coding-assistants/4.3-implementation.md`)
- [x] ~~This file curates a subset and links to the README for the rest~~ — resolved: inline everything. Copied in every prompt previously reached only via a "More (...): [README § ...]" link — plugin mock-fix/targeted-mock/error-code prompts, JS code-explanation/ribbon-command prompts, PCF hooks-migration/dead-code/mock-harness prompts, the full Azure Functions config-class/logging/JSON-output-testing/dynamic-field-handling set, and FetchXML→QueryExpression + unused-components — removed the "More (...)" link lines, and dropped the review-checkpoints table's "(From the README guardrails...)" attribution since the table is now self-contained. The README itself is untouched — duplication across both files is intentional.
- [x] ~~Should the Dataverse coding-agent plugin (preview) get its own activity section~~ — resolved: no such plugin exists, removed every mention. Fixed in the matrix (`[4] Implementation / Build` cell, the `[A.5] Dataverse MCP server` section's closing sentence, and the "Dataverse plugin for coding agents + Business skills" roadmap-watch bullet dropped entirely) and in `4.3-implementation.md` ("Environment gestures... go through pac CLI" — the plugin alternative removed).
- [x] ~~FetchXML Test Fixture Manager (Phase 1 done, per the build backlog) — worth an activity section here?~~ — resolved: it doesn't exist. Removed its row entirely from the matrix's build-backlog table.

**[5.3] Validation / Peer review** (`3-coding-assistants/5.3-validation-peer-review.md`)
- [x] Plugin A is specced but not built — should this file describe the manual git-diff fallback as the *current* procedure more prominently?
- [x] Test-run gate: is "run the test suite" enforced in CI already, or is prompting the assistant to run them the current mechanism?

**[6.3] Documentation** (`3-coding-assistants/6.3-documentation.md`)
- [x] ~~Publishing mechanics: are docs pushed to the ADO wiki via the wiki's git repo, or edited in the web UI?~~ — resolved: via the wiki's git repo. Added a "Publish to the ADO wiki" procedure to `6.3-documentation.md` (clone `<project>.wiki`, edit/commit/push) and updated the prereq table row accordingly.
- [x] ~~Should the Context Exporter CI refresh get a scaffold pipeline YAML here now, or wait until the headless run exists?~~ — resolved: wait. The "refresh the grounding" manual-procedure sections were removed from both `6.1-documentation.md` and `6.3-documentation.md` rather than scaffolding a YAML — no inline speculation on the CI refresh until it's actually built.

---

## Spotted during review — text fixes needed

Typos / grammar (mechanical, safe to fix):

- [x] `1.1-specification.md` + `1.3-specification.md` — "costructed" → "constructed"; "managent" → "management"; rewrote the awkward "Capturing intent" bullet.
- [x] `1.3-specification.md` — "before committing to one redaction" — reworded to "formulation".
- [x] `2.3-planning.md` — "team menbers" → "members"; "ellaborate" → "elaborate".
- [x] `3.1-tasking.md` + `3.3-tasking.md` — "technical tea" → "technical team"; completed the cut-off sentence to "…an explicit Definition of Done."; added the missing period after "assign tasks".
- [x] `4.1-implementation.md` + `4.3-implementation.md` — "were applicable … were not" → "where applicable … where not".
- [x] `5.1-validation-peer-review.md` + `5.3-validation-peer-review.md` — "and AI review comes *after*…" → "an AI review comes *after*…".
- [x] `6.3-documentation.md` — "in plase" → "in place".
- [x] `c.1-project-setup.md` + `c.3-project-setup.md` — folder-tree comment "Contenxt packs" → "Context packs".

Broken or inconsistent cross-references:

- [x] `6.1-documentation.md` link retargeted from the nonexistent "C.1 §6" to `[C.1 §2](./c.1-project-setup.md#2-install-the-context-exporter-plugin)` (the "Run the specs" step lives inside §2 as step 9, which has no anchor of its own).
- [x] `6.3-documentation.md` link fixed from "C.1 §9" to `[C.1 §4](../1-general-purpose-assistants/c.1-project-setup.md#4-refresh-cadence)`.
- [x] `b.1-initial-setup.md` + `b.3-initial-setup.md` — "see section 10" → "see section 12".
- [x] `4.3-implementation.md` — dropped the unverifiable "Plugin 9 — PCF Scaffold Orchestrator" callout; no such item exists anywhere else in the repo (the matrix only substantiates Plugin A), so removing beat fabricating a backlog entry. Revisit if this plugin is real and should be added to the matrix's build-backlog table.
- [x] `ai-augmented-d365ce-activity-matrix.md` — removed the self-referencing "see [Considerations](#considerations)" from inside the Considerations section.

Content / naming to verify:

- [x] **PandaDoc → Pandoc** (`b.1` §8, `b.3` §8, both bootstrap appendices): corrected tool name, package id (`choco install pandoc`), description, and the "Telling your AI assistant" prompt lines in both files.
- [x] `b.1` and `b.3` shared the identical title "Package Managers and Tools for Work Automation" — retitled to `# [B.1] Initial Setup — user-driven tooling: package managers, CLI tools, and general tools for AI-augmented functional work` and `# [B.3] Initial Setup — agent-driven tooling: Visual Studio, package managers, CLI tools, and IDEs`, matching the phrasing already used in both READMEs' guide tables.
- [x] `b.1`/`b.3` duplicate most sections (Chocolatey, VS Code, PAC CLI, Azure CLI, Pandoc, mmdc, Git, GitHub Desktop) — decide: intentional duplication (each column self-contained) or extract a shared base file. - INTENTIONAL
- [x] **"Plugin B" is referenced but never specced.** `ai-augmented-d365ce-activity-matrix.md` line 45 says grounding "makes drift detection (Plugin A) and import pre-flight (Plugin B) meaningful," but the build-backlog table only lists Plugin A — Plugin B has no row, no name, no notes anywhere in the repo. Either add it to the backlog table (if it's a real planned item, e.g. a solution-import pre-flight checker) or drop the parenthetical from line 45.

---

## Structural pendings

- [x] **Empty taxonomy folders** — `2-desktop-automation-agents/`, `4-platform-embedded-copilots/`, `5-agent-builder-frameworks/`, `6-ai-augmented-specialty-tools/` contain only `.gitkeep`. Decide for release: add a stub README in each (pointing at the matrix's "Not covered" section), or remove the folders until content exists.
- [x] ~~Column-3 README unmapped prompts~~ — resolved: no concern. Leaving them in the README as-is; no reorganization needed before release.
- [x] **Matrix "Roadmap watch" dates** — entries cite Jun 2026 / May 2026 sources; re-verify links and statuses (previews → GA?) at release time.
- [x] ~~`matrix-infographic.html` / `palette.css` — confirm the infographic reflects the final matrix wording~~ — resolved: no full reconciliation pass (the infographic is a condensed poster, not meant to mirror the markdown verbatim). Fixed only the one factual leftover: removed the `Fixture Manager` badge from the Implementation/Build cell, since that item was confirmed not to exist and was already dropped from the matrix's build-backlog table.
