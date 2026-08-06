# AI Tooling — Instructions for AI Tools

Guidance for AI tools analyzing, planning, recommending and implementing tasks and artifacts within the **AI Tooling** repository. Read this before making changes here; it is the tool-agnostic instruction file that `CLAUDE.md`, `AGENTS.md` and `.github/copilot-instructions.md` all point at, and that is pasted verbatim into the chat-surface project configuration.

**Precedence:** an explicit user instruction wins; otherwise this document governs. Where it is silent, follow the conventions visible in the surrounding files.

---

## Surfaces

This document is the single source of truth for both surfaces the repository is worked on from. It is delivered two ways — committed here, and pasted into the project configuration of a chat assistant — and the two copies are kept identical. When this file changes, re-paste it.

| Surface | How it receives this document | Scope |
|---|---|---|
| **Repo-connected** — Claude Code, GitHub Copilot, Cursor, agents with the working tree checked out | Via the `CLAUDE.md` / `AGENTS.md` / `.github/copilot-instructions.md` pointer | All of it |
| **Chat** — Claude.ai or ChatGPT project, no working tree | Pasted into the project configuration | All of it except the repo-only sections below |

**Repo-only.** These assume a checkout and do not apply in a chat surface: [Working agreements](#working-agreements) (branching, pull requests), the `dotnet` restore/build/test commands under [Tooling conventions](#tooling-conventions--d365-context-exporter), and any instruction to verify links, anchors or versions by resolving them.

### Chat-surface conventions

- **The project knowledge is a flattened, read-only snapshot, not the repository.** Paths are collapsed and separators normalized — `1_1-specification.md` here is `workflows/1-general-purpose-assistants/1.1-specification.md` in the repo. It may lag `main`. Treat the repository as source of truth and say so when the difference matters.
- **Do not claim links, anchors or versions were verified.** They cannot be resolved from a chat surface. State which ones a person must check in the working tree before merging.
- **Deliver work as a downloadable file**, in the format it will live in (`.md`, `.html`, `.css`, `.ps1`), and state the repository path it is intended for. Do not deliver a guide as chat prose that then has to be retyped.
- **Product availability and roadmap claims are searched, not recalled.** Anything about wave releases, GA status, licensing or agent-model capability carries a source and a date in the footnote form described under [Style](#style). Model training data is behind the Power Platform release cadence — assume it is stale.
- **This file belongs in the project configuration, not the project files.** Configuration is always in context; an uploaded copy needs a retrieval step and will be the stale one. Keep one copy per chat surface.

---

## Principles

- Implement solutions that work and are genuinely helpful to the end user
- Deliver best-in-class quality and follow established conventions and best practices
- Minimize rework and cost
- Leverage AI as a productivity multiplier, not a replacement for judgment
- Maintain Human-in-the-Loop (HITL) principles throughout
- Integrate with the broader Development ecosystem: specially Microsoft tooling and SaaS — Azure, Visual Studio, VS Code, GitHub, ADO

---

## Orientation — read these first

This repository is a **playbook plus the tooling that supports it**, for AI-augmented delivery on Dynamics 365 CE and the Power Platform. Two things ground any work here:

| Read | For |
|---|---|
| [README.md](/README.md) | The core idea (context is the grounding), how work is organized, the repository map |
| [Activity × Tooling Matrix](/workflows/ai-augmented-d365ce-activity-matrix.md) | The canonical structure — activity rows × taxonomy columns; every workflow guide is a cell in it |
| [AI Tool Taxonomy](/docs/ai-tool-taxonomy.md) | What the numbered categories (1–6) mean and why classification is by deployment context, not autonomy |

The repository's own thesis applies to work inside it: **ground first, then write.** Read the neighbouring files before adding one.

**Pages marked preliminary are not guidance.** A page whose opening blockquote reads **Preliminary** is a draft in progress. Do not cite one as established repository guidance, and do not extend one without asking; its scope is still being decided.

**Where the work is.** Columns 1 and 3 carry the developed workflows. Taxonomies 2, 4, 5 and 6 — including the maker portals and Copilot Studio — are deliberately staged in the matrix's [Not covered](/workflows/ai-augmented-d365ce-activity-matrix.md#not-covered-taxonomies-other-than-1-and-3) section, not overlooked. Proposing work there is a structural change; see the last of the [Working agreements](#working-agreements).

---

## Hard rules

These are the ones most easily broken by a well-meaning edit.

### 1. No client data, ever

No client names, tenant IDs, org URLs, environment GUIDs, record data, connection strings, secrets, or user names — in documentation, sample configs, queries, templates, tests, or commit messages. Use the established placeholders: `<client>-Context/`, `<client>-d365/`, `<client>-wiki/`, `[TableName]`, `[FieldName]`, `[ClientName]`.

The playbook's own boundary notice (`LEGAL.md`, prepended to every exporter output) exists for this reason — do not weaken or bypass it.

### 2. Taxonomy columns do not cross-reference each other

Guides under [workflows/1-general-purpose-assistants/](/workflows/1-general-purpose-assistants/) must not link to or discuss guides under [workflows/3-coding-assistants/](/workflows/3-coding-assistants/), and vice versa. Each column is a self-contained path for a reader who only has that column's tooling; a column-1 reader being told "prefer 3.3 if you have coding-assistant tooling" is noise at best.

Cross-column navigation lives in exactly two places: [README.md](/README.md) and the [matrix](/workflows/ai-augmented-d365ce-activity-matrix.md). Do not reintroduce "column-N counterpart" links in guide headers or body text.

The same applies to advice given in conversation: a recommendation that assumes coding-assistant tooling is wrong for a reader working column 1. Establish which column is in play before recommending, and name the assumption if it is not stated.

### 3. Generated and ignored directories are not source of truth

| Path | Status |
|---|---|
| `/scaffolding/` | Git-ignored. Generated exporter runs kept for local inspection. Never commit; never cite as reference. |
| `tooling/D365ContextExporter/output/`, `runs/` | Git-ignored working output. |
| `.claude/`, `.cursor/`, `.copilot/`, `.continue/`, … | Git-ignored per-tool configuration. Personal, not repository policy. |
| `/nupkg/` | Build artifact drop (`.gitkeep` only). |

The shipped reference configuration for D365 Context Exporter is [SampleConfig/](/tooling/D365ContextExporter/D365ContextExporter/SampleConfig/) — that is what to read and edit.

---

## Documentation conventions

Most work in this repository is markdown. Match what is already there.

### Workflow guides

Files live in `workflows/<taxonomy-number>-<taxonomy-slug>/` and are named `<activity>.<taxonomy>-<slug>.md`, where activity is `a`, `b`, `c` (grounding and setup) or `1`–`6` (the delivery loop) — for example [a.3-grounding.md](/workflows/3-coding-assistants/a.3-grounding.md), [5.1-validation-peer-review.md](/workflows/1-general-purpose-assistants/5.1-validation-peer-review.md).

Every guide opens with the same two lines:

```markdown
# [4.3] Implementation / Build — short subtitle after an em dash

> **Matrix cell:** [[4.3]](../ai-augmented-d365ce-activity-matrix.md#43-implementation--build) · Column **[3] Coding Assistants & Agentic Environments** · Builds on [B.3](./b.3-initial-setup.md) + [C.3](./c.3-project-setup.md)
```

The matrix anchor must resolve — check the heading it points at before committing.

Prompts are fenced blocks, introduced by the surface and the context they assume:

````markdown
**AI:** GitHub Copilot Chat
**Context:** Open PCF index file

**Find dead code:**
```
Is there any dead code, unused imports, or unused state variables in this file?
```
````

Guides close with a guardrail / human-review table — what a person must check before accepting the output. Keep that section; it is the HITL principle made concrete.

### Adding or renaming a guide

Three places must stay in sync. Update all of them in the same change:

1. The [matrix](/workflows/ai-augmented-d365ce-activity-matrix.md) cell
2. The [README.md](/README.md) activity table
3. The folder README table — [column 1](/workflows/1-general-purpose-assistants/README.md) or [column 3](/workflows/3-coding-assistants/README.md)

### Style

- **Tables over prose** for anything comparative; bold lead-ins on list items.
- Em dashes for asides, `·` as an inline separator in header metadata lines.
- Plain, factual voice. No marketing register, no filler adjectives, no emoji outside existing decorative usage.
- **Function-based naming over product names** for anything generic — "work item tracking system", not "ADO" or "Jira", unless the guidance is genuinely specific to that product. The same convention applies to CSS custom properties: usage-based (`--primary`, `--surface-raised`), not colour-descriptive.
- External claims carry a numbered footnote with the source name, link, and a one-line note on what it supports — see the references block in [ai-tool-taxonomy.md](/docs/ai-tool-taxonomy.md).
- Root-level and `docs/` pages end with `[Back](/README.md)`. Workflow guides do not — their matrix-cell header carries navigation instead.
- Links are relative and repository-rooted; verify targets exist.

---

## Tooling conventions — D365 Context Exporter

The [D365 Context Exporter](/tooling/D365ContextExporter/README.md) is an XrmToolBox plugin (`net48`, WinForms, `Nullable` enabled, StyleCop.Analyzers enforced via [stylecop.json](/tooling/D365ContextExporter/D365ContextExporter/stylecop.json)). Tests are NUnit + Moq in [D365ContextExporter.Tests](/tooling/D365ContextExporter/D365ContextExporter.Tests/).

```bash
dotnet restore tooling/D365ContextExporter/D365ContextExporter.sln
dotnet build   tooling/D365ContextExporter/D365ContextExporter.sln --configuration Release
dotnet test    tooling/D365ContextExporter/D365ContextExporter.Tests/D365ContextExporter.Tests.csproj
```

### Rules specific to this plugin

- **Never call `object.from_json` / `object.to_json` in a Scriban template.** Scriban is compiled into the assembly from source with its System.Text.Json support removed; those built-ins throw at runtime. Query results are already parsed into template variables — there is nothing left to deserialise.
- **Do not add package references that ship a DLL next to the plugin.** A private `System.Text.Json.dll` caused XrmToolBox to lock the file, breaking update and uninstall from the Tool Library. If a dependency is unavoidable, source-include it (`PackageScribanIncludeSource` + `IncludeAssets="Build"` is the working pattern) and say why in the PR.
- **No early binding.** Tables change too fast in a live D365 CE environment for generated early-bound classes to be worth the regeneration cost. Use late-bound access throughout.
- **Version bumps touch two files.** `AssemblyVersion` / `FileVersion` / `Version` in [D365ContextExporter.csproj](/tooling/D365ContextExporter/D365ContextExporter/D365ContextExporter.csproj) and `<version>` in [D365ContextExporter.nuspec](/tooling/D365ContextExporter/D365ContextExporter/D365ContextExporter.nuspec), kept identical. Scheme is `1.YYYY.M.N`.
- **Changing `SampleConfig/` changes what users get on upgrade.** Those files are embedded resources deployed to the user's base directory on first run and redeployed when the plugin version differs from their `version.txt`. A user's `LEGAL.md` and custom files are never overwritten — keep it that way.
- **New template filters** go in [TemplateFilters.cs](/tooling/D365ContextExporter/D365ContextExporter/Helpers/TemplateFilters.cs) and must be added to the built-in function table in the [tooling README](/tooling/D365ContextExporter/README.md).
- CI ([build-and-pack-d365-context-exporter.yml](/.github/workflows/build-and-pack-d365-context-exporter.yml)) builds, tests and packs on push and PR to `main` under `tooling/D365ContextExporter/**`. Publishing uses trusted publishing — do not add credentials to the workflow.

---

## Scripts

[scripts/](/scripts/) holds standalone utilities. Where a script is needed on both platforms, ship the pair (`azdevops-auth.ps1` / `azdevops-auth.sh`) with matching behaviour. PowerShell is the primary surface; scripts take parameters rather than hardcoded paths and must not embed credentials.

---

## Skills

[skills/](/skills/) holds reusable skills for AI assistants. A skill is a folder named for the skill, holding a `SKILL.md` with `name` and `description` frontmatter, plus optional `references/` and `scripts/` — see [d365ce-planning](/skills/d365ce-planning/SKILL.md) and [power-automate-flow-editing](/skills/power-automate-flow-editing/SKILL.md).

`SKILL.md` carries the decision and the procedure — lead with the decision table, then the steps, and close with the guardrails a human must check. Depth goes in `references/`, loaded only when the step needs it. A skill answers "which option do I reach for, and how do I carry it out safely", not "here is everything about the topic".

---

## Working agreements

The first three are repo-only; the last two apply on every surface.

- **Branch, don't commit to `main`.** Topic branches are `<area>/<slug>` — `docs/`, `tooling/`, `scripts/`, `workflow/`, `github-workflows/`. Merge via pull request.
- **Verify links, anchors and versions** touched by a change before proposing it as done.
- **Scope the change to the ask.** This repository is edited in small, reviewable passes; a documentation fix should not restructure a guide.
- **State what was not done.** If part of a task is blocked, finish the rest and say which part was left and why. Surface the tradeoffs and name the assumptions a recommendation depends on rather than presenting one option as settled.
- **Ask when a change would alter the structure** — the matrix rows/columns, the taxonomy categories, the three-repository model, or which taxonomies are in scope. Those are editorial decisions, not implementation details.

---

[Back](/README.md)
