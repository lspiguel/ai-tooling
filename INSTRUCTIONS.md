# AI Tooling — Instructions for AI Tools

Guidance for AI tools analyzing, planning, recommending and implementing tasks and artifacts within the **AI Tooling** repository. Read this before making changes here; it is the tool-agnostic instruction file that `CLAUDE.md`, `AGENTS.md` and `.github/copilot-instructions.md` all point at.

**Precedence:** an explicit user instruction wins; otherwise this document governs. Where it is silent, follow the conventions visible in the surrounding files.

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

---

## Hard rules

These are the ones most easily broken by a well-meaning edit.

### 1. No client data, ever

No client names, tenant IDs, org URLs, environment GUIDs, record data, connection strings, secrets, or user names — in documentation, sample configs, queries, templates, tests, or commit messages. Use the established placeholders: `<client>-Context/`, `<client>-d365/`, `<client>-wiki/`, `[TableName]`, `[FieldName]`, `[ClientName]`.

The playbook's own boundary notice (`LEGAL.md`, prepended to every exporter output) exists for this reason — do not weaken or bypass it.

### 2. Taxonomy columns do not cross-reference each other

Guides under [workflows/1-general-purpose-assistants/](/workflows/1-general-purpose-assistants/) must not link to or discuss guides under [workflows/3-coding-assistants/](/workflows/3-coding-assistants/), and vice versa. Each column is a self-contained path for a reader who only has that column's tooling; a column-1 reader being told "prefer 3.3 if you have coding-assistant tooling" is noise at best.

Cross-column navigation lives in exactly two places: [README.md](/README.md) and the [matrix](/workflows/ai-augmented-d365ce-activity-matrix.md). Do not reintroduce "column-N counterpart" links in guide headers or body text.

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
- **Version bumps touch two files.** `AssemblyVersion` / `FileVersion` / `Version` in [D365ContextExporter.csproj](/tooling/D365ContextExporter/D365ContextExporter/D365ContextExporter.csproj) and `<version>` in [D365ContextExporter.nuspec](/tooling/D365ContextExporter/D365ContextExporter/D365ContextExporter.nuspec), kept identical. Scheme is `1.YYYY.M.N`.
- **Changing `SampleConfig/` changes what users get on upgrade.** Those files are embedded resources deployed to the user's base directory on first run and redeployed when the plugin version differs from their `version.txt`. A user's `LEGAL.md` and custom files are never overwritten — keep it that way.
- **New template filters** go in [TemplateFilters.cs](/tooling/D365ContextExporter/D365ContextExporter/Helpers/TemplateFilters.cs) and must be added to the built-in function table in the [tooling README](/tooling/D365ContextExporter/README.md).
- CI ([build-and-pack-d365-context-exporter.yml](/.github/workflows/build-and-pack-d365-context-exporter.yml)) builds, tests and packs on push and PR to `main` under `tooling/D365ContextExporter/**`. Publishing uses trusted publishing — do not add credentials to the workflow.

---

## Scripts

[scripts/](/scripts/) holds standalone utilities. Where a script is needed on both platforms, ship the pair (`azdevops-auth.ps1` / `azdevops-auth.sh`) with matching behaviour. PowerShell is the primary surface; scripts take parameters rather than hardcoded paths and must not embed credentials.

---

## Skills

[skills/](/skills/) holds reusable reference guides for AI assistants — condensed, decision-oriented documents (see [Power Automate Cloud Flow Deployment Options](/skills/Power-Automate-Cloud-Flow-Deployment-Options.md)). Lead with the decision table, then the detail. A skill answers "which option do I reach for and why", not "here is everything about the topic".

---

## Working agreements

- **Branch, don't commit to `main`.** Topic branches are `<area>/<slug>` — `docs/`, `tooling/`, `scripts/`, `workflow/`, `github-workflows/`. Merge via pull request.
- **Scope the change to the ask.** This repository is edited in small, reviewable passes; a documentation fix should not restructure a guide.
- **Verify links, anchors and versions** touched by a change before proposing it as done.
- **State what was not done.** If part of a task is blocked, finish the rest and say which part was left and why.
- **Ask when a change would alter the structure** — the matrix rows/columns, the taxonomy categories, the three-repository model. Those are editorial decisions, not implementation details.

---

[Back](/README.md)
