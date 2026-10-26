# CLAUDE.md

Read **[INSTRUCTIONS.md](/INSTRUCTIONS.md)** before working in this repository. It is the single, tool-agnostic instruction file for the AI Tooling repository and it governs conventions, hard rules, and working agreements here.

Start there, then [README.md](/README.md) for orientation.

The sections below are a codebase map and command reference. They do not restate INSTRUCTIONS.md; where the two overlap, INSTRUCTIONS.md wins.

## What this repository is

A **playbook plus supporting tooling** for AI-augmented delivery on Dynamics 365 CE and the Power Platform. Most of it is markdown. The only compiled code is the D365 Context Exporter. The thesis: context is the grounding for AI work, so the repo documents how to build that context and how to use it at each delivery activity.

## Layout

| Path | Contents |
|---|---|
| `workflows/` | The Activity × Tooling Matrix (`ai-augmented-d365ce-activity-matrix.md`), HTML/PNG infographics with shared `palette.css` / `infographic.css`, and per-activity guides |
| `workflows/1-general-purpose-assistants/` | Column 1 guides (`a.1`–`c.1`, `1.1`–`6.1`) for chat assistants |
| `workflows/3-coding-assistants/` | Column 3 guides (`a.3`–`c.3`, `1.3`–`6.3`) for coding assistants and agents |
| `workflows/2-…`, `4-…`, `5-…`, `6-…` | Empty (`.gitkeep`) — staged taxonomies, not yet developed |
| `docs/` | `ai-tool-taxonomy.md`, `security-prompt-injection.md`, and `agents/` capability write-ups per AI product |
| `skills/` | Reusable assistant skills: `d365ce-planning`, `d365ce-documenting`, `power-automate-flow-editing` (each a `SKILL.md` plus `references/` and, for the flow skill, `scripts/`) |
| `scripts/` | Standalone utilities — Azure DevOps auth (`.ps1`/`.sh` pair), encode/decode, markdown/SVG conversion, HTML→PNG export |
| `tooling/D365ContextExporter/` | XrmToolBox plugin that exports Dataverse metadata into markdown context packs |
| `tooling/D365McpBridge/` | **Spec only** (`SPEC.md`, draft): a planned XrmToolBox plugin exposing a local MCP endpoint with human-authorized calls. No code yet |
| `.github/workflows/` | CI for the Context Exporter; `.github/copilot-instructions.md` and `AGENTS.md` are pointers to INSTRUCTIONS.md like this file |
| `nupkg/` | Pack output drop (git-ignored contents) |

## D365 Context Exporter

`net48` WinForms XrmToolBox plugin. Solution: `tooling/D365ContextExporter/D365ContextExporter.sln`.

```bash
dotnet restore tooling/D365ContextExporter/D365ContextExporter.sln
dotnet build   tooling/D365ContextExporter/D365ContextExporter.sln --configuration Release
dotnet test    tooling/D365ContextExporter/D365ContextExporter.Tests/D365ContextExporter.Tests.csproj
# single test
dotnet test tooling/D365ContextExporter/D365ContextExporter.Tests/D365ContextExporter.Tests.csproj --filter "FullyQualifiedName~PathResolverTests"
```

Building and packing run on `windows-latest` in CI (`dotnet` 8.x SDK, `nuget pack` against the `.nuspec`).

### How it fits together

Under `tooling/D365ContextExporter/D365ContextExporter/`:

- **`SampleConfig/`** — JSON export specs (`*.context-exporter-config.json`, validated by `context-exporter.schema.json`) and `LEGAL.md`. Embedded as resources and deployed to the user's base directory on first run.
- **`Models/`** — `ExportJob`, `QueryDefinition`, `OutputSettings`, `PythonSettings`: the deserialised spec.
- **`Queries/`** — three runners that execute a spec's queries: `FetchXmlQueryRunner`, `MetadataQueryRunner`, `WebApiQueryRunner`.
- **`Orchestration/`** — `ExportJobRunner` drives a job; `IntermediateJsonBuilder` shapes query results into template variables; `TemplateRenderer` renders Scriban templates to markdown.
- **`Helpers/`** — `ConfigValidator`, `PathResolver`, `FirstRunHelper`, `EntityJsonSerializer`, and `TemplateFilters` (custom Scriban functions).
- **`UI/`** and `ContextExporterPluginControl.cs` — WinForms controls (spec picker, directory picker, progress, preview, welcome form).

Pipeline: spec JSON → run queries → intermediate JSON → Scriban templates → markdown (with `LEGAL.md` prepended) in the output directory.

Tests mirror the source folders (`HelpersTests`, `ModelsTests`, `OrchestrationTests`, `QueriesTests`). The `*RealDataTests` files exercise realistic fixtures; keep them free of client data.

Plugin-specific rules (Scriban `from_json`/`to_json`, no extra DLLs, late binding, two-file version bump, SampleConfig upgrade behaviour) are in INSTRUCTIONS.md under *Tooling conventions*.

## Working in the docs

- Guide naming is `<activity>.<column>-<slug>.md`; every guide opens with the `# [N.C]` title and `> **Matrix cell:**` line, and the anchor must resolve.
- Adding or renaming a guide means updating the matrix, the root README table, and the column README in the same change.
- Columns 1 and 3 must not link to each other; cross-column links live only in README.md and the matrix.
- There is no markdown link checker or lint step in CI — verify links and anchors by hand.
