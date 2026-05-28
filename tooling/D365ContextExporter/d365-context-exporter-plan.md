# D365 CE Context Exporter — XrmToolBox Plugin with Python/Jinja2 Post-Processor

## Executive Summary

This plan describes an XrmToolBox plugin (**D365 CE Context Exporter**) that, given a local configuration file, executes a set of FetchXML queries and Dataverse Web API calls against a Microsoft Dynamics 365 CE environment, captures the results as an intermediate JSON file, and then invokes a Python post-processor that applies Jinja2 templates to produce a set of Markdown files. Those Markdown files are designed to be uploaded as grounding context to non-agentic AI assistants (Claude.ai, ChatGPT, Gemini, M365 Copilot chat) that cannot independently browse a codebase or a Dataverse environment.

The design deliberately separates concerns:

- **Query execution** (what to pull from Dataverse) lives in C# inside the XrmToolBox plugin, because that is where we already have an authenticated `IOrganizationService` and mature SDK support.
- **Transformation** (how the pulled data becomes Markdown) lives entirely in Python scripts and Jinja2 templates stored on disk as plain text. Nothing about the transformation is compiled, which lets practitioners author, review, and version new output formats without touching the plugin.
- **The bridge between them is a JSON file on disk**, which makes each run inspectable and the post-processing step independently re-runnable.

## Goals and Non-Goals

### Goals

- Produce well-structured Markdown files suitable as grounding uploads for general-purpose AI assistants.
- Allow authoring of new transformations by editing configuration + templates only — no C# recompile.
- Follow established project conventions (naming, folder structure, StyleCop, Repository pattern, extension methods, DI).
- Integrate cleanly into an existing D365CE repository alongside existing Plugins, PCF, WebResources, etc.
- Run entirely on a Windows PC with commonly-installed developer tooling.

### Non-Goals

- This plugin does not modify Dataverse data. It is read-only.
- It is not a generic ETL tool. The output target is always Markdown text files intended for LLM grounding.
- It does not execute the AI prompts themselves; it only produces the grounding files.
- It does not orchestrate uploads to AI products (users upload manually to claude.ai, chatgpt.com, etc.).

## Base Folder Structure

All runtime artefacts live under a single **Context-Exporter** working directory that the user points the plugin to. The structure is fixed:

```
Context-Exporter/
├── config/
│   ├── <SpecName>.context-exporter-config.json   ← one per spec
│   ├── queries/
│   │   └── *.fetch.xml                              ← shared across projects
│   └── transformations/
│       └── *.j2                                     ← Jinja2 templates, shared across projects
├── runs/
│   └── <timestamp>/
│       ├── output.<queryId>.fetch.json              ← raw result per query
│       ├── intermediate.json                        ← all result sets combined
│       └── output.md                                ← Markdown from transformation
└── output/
    └── <SpecName>.context.md                      ← latest grounding file (overwritten each run)
```

Key principles:
- **Config is per-spec.** Each `<SpecName>.context-exporter-config.json` selects which queries and transformation to run, plus spec-specific metadata.
- **Queries and transformations are shared.** FetchXML files and Jinja2 templates are authored once and referenced by any spec config.
- **Each run is immutable.** A `runs/<timestamp>/` folder captures the full pipeline snapshot: per-query raw JSON, the combined intermediate, and the rendered Markdown. The plugin never deletes run folders.
- **The `output/` folder is the grounding target.** After a successful run, `output.md` is copied to `output/<SpecName>.context.md`, overwriting any previous file. This is the file users upload to AI assistants.

## Solution Architecture

```
+-------------------------------+
|  XrmToolBox (Windows Forms)   |
|  .NET Framework 4.8           |
|                               |
|  +-------------------------+  |
|  |  D365.ContextExporter   |  |   config/<Project>.context-exporter-config.json
|  |       (plugin)          |<-+-- config/queries/*.fetch.xml
|  |                         |  |   config/transformations/*.j2
|  |  - Spec picker       |  |
|  |  - Query runner         |  |
|  |  - JSON serializer      |  |
|  |  - Python orchestrator  |  |
|  +-------------------------+  |
|              |                |
|  IOrganizationService         |
|  (from PluginControlBase)     |
+------|-----------------|------+
       |                 |
       v                 v
  Dataverse       runs/<timestamp>/
  (D365 CE)         output.<queryId>.fetch.json  (raw, per query)
                    intermediate.json            (combined)
                    output.md                    (rendered)
                         |
                         v  copy
               output/<SpecName>.context.md
```

The plugin hosts a WinForms UI. The user selects a **Context-Exporter base directory**, then picks a **project** from the list of `*.context-exporter-config.json` files discovered under `config/`. The plugin reads the chosen config, runs each query writing a `output.<queryId>.fetch.json` per query, assembles `intermediate.json`, then shells out to the configured `*.transform.py` script. Python reads `intermediate.json`, renders the Markdown, and writes `output.md` into the same run folder. The plugin then copies `output.md` to `output/<SpecName>.context.md`.

### Why shell out to Python instead of embedding it

Three options were considered for running Python inside a .NET 4.8 host:

- **IronPython** — pure managed implementation, but its ecosystem lags real CPython and Jinja2 runs best on CPython. It would also tie users to whatever IronPython version we ship.
- **Python.NET (pythonnet)** — embeds CPython in-process. Works, but tightly couples the plugin to a specific Python binary at load time, and any `pip install` mismatch crashes the XrmToolBox process.
- **Subprocess `python.exe`** — the plugin launches Python as a child process, passing JSON by file and reading stdout/stderr. Decouples the two runtimes, survives Python environment changes, and means any user-authored Python script can be swapped in. **Chosen for Phases 1–4.**

> **Phase 5 revision.** This decision was revisited once the full feature set was stable. An IronPython-based embedding was first attempted but abandoned — Jinja2 3.1.x uses `async def` constructs not supported by IronPython 3.4. The final solution replaces the Python post-processor entirely with .NET-native equivalents: **Scriban** for template rendering and a ported **`TemplateFilters.cs`** for custom filters. `transform.py`, `filters.py`, and all Python infrastructure are deleted. Templates are ported from Jinja2 to Scriban syntax (pipe filter syntax is identical; control-flow tags change from `{% %}` to `{{ }}`).

## Executing Environment

Target machine: **Windows 10/11 developer workstation**.

Required once per machine:

- Windows 10 22H2 or Windows 11, x64.
- .NET Framework 4.8 runtime (ships with current Windows; required by XrmToolBox 1.2025.x+).
- XrmToolBox latest stable release, installed from `xrmtoolbox.com`.
- Visual Studio 2022 (Community is fine) with the .NET desktop development workload — **only required for plugin developers**, not for end users of the plugin.
- ~~Python 3.11 or later (64-bit)~~ — **removed in Phase 5.** Templates are rendered in-process via Scriban; no external Python installation is required for end users.

Authenticated connection to Dataverse is inherited from XrmToolBox's Connection Manager — the plugin does not manage credentials itself. This aligns with XrmToolBox convention (`PluginControlBase.Service` gives you a ready-to-use `IOrganizationService`).

## Configuration Design

Configuration is **per-spec**: each project gets its own `<SpecName>.context-exporter-config.json` under `config/`. The plugin builds the spec list by scanning that directory and presenting the discovered spec names in a dropdown.

Suggested top-level schema:

```json
{
  "$schema": "../../schema/context-exporter.schema.json",
  "spec": "Contoso",
  "version": "1.0.0",
  "frontMatter": {
    "generatedBy": "D365 CE Context Exporter"
  },
  "python": {
    "interpreter": "auto",
    "venv": "%LOCALAPPDATA%/D365ContextExporter/venv"
  },
  "transformation": "entity-dictionary.j2",
  "queries": [
    {
      "id": "entities-with-attributes",
      "type": "fetchxml",
      "source": "entities-attributes.fetch.xml",
      "resultKey": "entityAttributes"
    },
    {
      "id": "optionsets-global",
      "type": "webapi",
      "method": "GET",
      "path": "GlobalOptionSetDefinitions",
      "select": ["Name", "DisplayName", "Options"],
      "resultKey": "globalOptionSets"
    },
    {
      "id": "security-roles",
      "type": "fetchxml",
      "source": "security-roles.fetch.xml",
      "resultKey": "securityRoles"
    }
  ]
}
```

Key principles:
- `spec` is the canonical spec name used for the output filename (`output/<SpecName>.context.md`).
- `transformation` names one Jinja2 template file from `config/transformations/`; query `source` values are resolved relative to `config/queries/`.
- Every query has a `resultKey` naming its slot in the intermediate JSON. The transformation script can reference any key it needs.
- There are no per-spec template or query files — only the config differs between projects.

## Components and Modules

### C# side — XrmToolBox plugin

Following the standard folder/namespace pattern, with the plugin name `D365ContextExporter`:

- **`D365ContextExporter` (root project)** — the plugin class itself (`ContextExporterPluginControl : PluginControlBase, IXrmToolBoxPluginControl`). Hosts the UI, wires events, owns nothing else.
- **`Orchestration/`** — orchestration logic.
  - `ExportJobRunner` — takes a loaded `ExportJob` and executes it end to end.
  - `IntermediateJsonBuilder` — assembles the intermediate JSON document from per-query results.
  - `PythonInvoker` — shells out to `python.exe`, captures stdout/stderr, surfaces failures.
- **`Queries/`** — Repository-pattern wrappers.
  - `FetchXmlQueryRunner` — executes a `FetchExpression` using `IOrganizationService.RetrieveMultiple`, handling paging automatically.
  - `WebApiQueryRunner` — executes Dataverse Web API calls using `HttpClient`, reusing the bearer token resolvable from the current `CrmServiceClient`/`ServiceClient`.
  - `MetadataQueryRunner` — wraps `RetrieveAllEntitiesRequest`, `RetrieveEntityRequest`, `RetrieveOptionSetRequest` for common grounding use cases (entity lists, attributes, option sets, relationships).
- **`Models/`** — strongly-typed configuration classes. `ExportJob`, `QueryDefinition`, `OutputDefinition`, `PythonSettings`, `OutputSettings`. Backed by `System.Text.Json` or `Newtonsoft.Json` (the Dataverse client already pulls Newtonsoft in transitively — reuse it).
- **`Helpers/`** — static utility classes.
  - `EntityJsonSerializer` — converts `Entity`/`EntityCollection` to plain dictionaries before JSON serialization (handles `EntityReference`, `OptionSetValue`, `Money`, `AliasedValue` cleanly).
  - `PathResolver` — resolves paths relative to the config file and expands environment variables (`%LOCALAPPDATA%`, `${CONFIG_DIR}`).
  - `ProcessRunner` — thin wrapper over `System.Diagnostics.Process` with timeout, cancellation, and captured streams.
- **`Utils/`** — copies of the shared utility files (`DataverseClientHelper.cs`, `EntityExtensionMethods.cs`, `EntityFactory.cs`, `JsonSerializerHelper.cs`).
- **`UI/`** — WinForms user controls.
  - `BaseDirectoryPickerControl` — folder picker for the Context-Exporter base directory; persisted in user settings.
  - `SpecPickerControl` — dropdown/list built by scanning `config/*.context-exporter-config.json`; shows spec names derived from file names.
  - `ExportProgressControl` — per-query progress, log panel, cancel button.
  - `OutputPreviewControl` — after a run, shows the generated `output.md` and the `output/<SpecName>.context.md` copy with open/reveal actions.

### Python side — post-processor

Python was used as a **post-processor only** in Phases 1–4. From Phase 5, this role is taken over by .NET-native code.

- ~~**`transform.py`**~~ — **deleted in Phase 5.** Orchestration logic absorbed into `TemplateRenderer.cs`.
- ~~**`filters.py`**~~ — **deleted in Phase 5.** All 19 filter functions ported to `TemplateFilters.cs`.
- ~~**`requirements.txt`**~~ — **deleted in Phase 5.** No Python packages are required.

### Queries

FetchXML files under `config/queries/*.fetch.xml`. Standard FetchXML authored in FetchXML Builder. One query per file for reusability and diffability. Shared across all projects.

## Artifacts Inventory

The following artifacts will be built. Each row maps to a concrete deliverable.

| # | Artifact | Type | Location | Notes |
|---|----------|------|----------|-------|
| 1 | `D365ContextExporter.sln` | VS Solution | `/Assemblies/XrmToolboxPlugins/D365ContextExporter/` | Standard repo layout |
| 2 | `D365ContextExporter.csproj` | C# project | same | SDK-style, `net48`, StyleCop on |
| 3 | `ContextExporterPluginControl.cs` | C# / UserControl | project root | The `IXrmToolBoxPluginControl` |
| 4 | `ExportJobRunner.cs` | C# static class | `Orchestration/` | Orchestration |
| 5 | `IntermediateJsonBuilder.cs` | C# static class | `Orchestration/` | Shapes the JSON contract |
| 6 | `PythonInvoker.cs` | C# class | `Orchestration/` | Subprocess management |
| 7 | `FetchXmlQueryRunner.cs` | C# static class | `Queries/` | Paging, error wrapping |
| 8 | `WebApiQueryRunner.cs` | C# class | `Queries/` | Uses `HttpClient`, reuses bearer |
| 9 | `MetadataQueryRunner.cs` | C# static class | `Queries/` | `RetrieveAllEntitiesRequest` etc. |
| 10 | `ExportJob.cs` + sibling models | C# POCOs | `Models/` | Configuration DTOs |
| 11 | `EntityJsonSerializer.cs` | C# static class | `Helpers/` | Cleans Dataverse types for JSON |
| 12 | `PathResolver.cs` | C# static class | `Helpers/` | Path + env-var expansion |
| 13 | `ProcessRunner.cs` | C# class | `Helpers/` | `Process` wrapper |
| 14 | Shared utility `*.cs` files | C# static classes | `Utils/` | Shared utility copies |
| 15 | `BaseDirectoryPickerControl.cs` | C# UserControl | `UI/` | Base folder chooser, persisted |
| 16 | `SpecPickerControl.cs` | C# UserControl | `UI/` | Spec list from config/*.json |
| 17 | `ExportProgressControl.cs` | C# UserControl | `UI/` | Progress + log |
| 18 | `OutputPreviewControl.cs` | C# UserControl | `UI/` | Run output + grounding file link |
| 19 | `stylecop.json` | Config | project root | StyleCop configuration |
| 20 | `transform.py` | Python script | `/python/` | Universal Jinja2 orchestrator, ~100 LOC |
| 21 | `filters.py` | Python module | `/python/` | Shared Jinja2 filters |
| 22 | `requirements.txt` | Python deps | `/python/` | Pinned versions |
| 23 | `context-exporter.schema.json` | JSON Schema | `/schema/` | IDE IntelliSense for spec configs |
| 24 | `entity-dictionary.j2` | Jinja2 | `config/transformations/` (solution root) | See catalog below |
| 25 | `security-model.j2` | Jinja2 | `config/transformations/` (solution root) | See catalog below |
| 26 | `optionsets.j2` | Jinja2 | `config/transformations/` (solution root) | See catalog below |
| 27 | `forms-and-views.j2` | Jinja2 | `config/transformations/` (solution root) | See catalog below |
| 28 | `solution-inventory.j2` | Jinja2 | `config/transformations/` (solution root) | See catalog below |
| 28 | `config/queries/*.fetch.xml` | FetchXML | `config/queries/` (solution root) | One per query, shared |
| 29 | `Sample.context-exporter-config.json` | JSON | `config/` (solution root) | Sample spec config |
| 30 | `README.md` | Markdown | repo root | User + developer docs |
| 31 | `D365ContextExporter.nuspec` | NuGet spec | project root | For XrmToolBox Tool Library listing |
| 32 | Unit test project | C# project | `/Tests/D365ContextExporter.Tests/` | Moq-based |
| 33 | Azure DevOps pipeline YAML | YAML | `/Pipeline/` | Build + pack + publish nupkg |

## Libraries and Dependencies

### C# — NuGet packages

- `XrmToolBoxPackage` (latest, currently `1.2025.10.74`, targets .NET Framework 4.8) — brings in XrmToolBox plugin contracts and the connection control.
- `Microsoft.PowerPlatform.Dataverse.Client` (latest stable) — the current supported Dataverse client. Use `ServiceClient` rather than the legacy `CrmServiceClient`.
- `Newtonsoft.Json` — already a transitive dependency; use it for config loading and intermediate JSON authoring to avoid dragging in `System.Text.Json` at 4.8.
- `StyleCop.Analyzers` 1.1.118+ — required for C# style enforcement.
- `Scriban` 5.12.x — .NET template engine; replaces Jinja2 from Phase 5. Added in Phase 5.
- `Moq` and `Moq.Contrib.HttpClient` — test project only.

### Python — pip packages

> **Removed in Phase 5.** Templates are rendered in-process by Scriban. `transform.py`, `filters.py`, and `requirements.txt` are deleted. ~~`Jinja2`~~, ~~`tiktoken`~~, ~~`markupsafe`~~ are no longer used.

### External tools (not libraries, but required)

- **Python 3.11+** — **removed as an end-user requirement in Phase 5.** No Python installation needed.
- **XrmToolBox** 1.2025.10.x or later.
- **Visual Studio 2022** (developers only).

## Data Flow

### Inputs

1. **Spec config** (`config/<SpecName>.context-exporter-config.json`) — one per spec; selected from the spec list in the plugin UI.
2. **Query files** (`config/queries/*.fetch.xml`) — shared; referenced by name in the spec config.
3. **Jinja2 template** (`config/transformations/<name>.j2`) — shared; one referenced per spec config.
4. **Live Dataverse connection** — from XrmToolBox's Connection Manager.

### Intermediate representation

A single JSON document per export run, written to the working directory. Structure:

```json
{
  "_meta": {
    "exportName": "Full CE Context Pack",
    "exportedAtUtc": "2026-04-21T14:22:05Z",
    "environment": {
      "url": "https://example.crm.dynamics.com",
      "orgName": "Example",
      "orgId": "..."
    },
    "frontMatter": { "spec": "<SpecName>" }
  },
  "entityAttributes": [
    { "logicalname": "account", "name": "accountid", "displayname": "Account ID", ... },
    { "logicalname": "account", "name": "name", "displayname": "Account Name", ... }
  ],
  "globalOptionSets": [ ... ],
  "securityRoles": [ ... ]
}
```

This file is preserved after the run (in a `runs/<timestamp>/` folder), which means the Python post-processor can be re-executed against it without reconnecting to Dataverse. This is useful during template development.

### Outputs

Each run produces exactly one `output.md` file (rendered by the transformation script) plus one raw `output.<queryId>.fetch.json` per query. After the run, the plugin copies `output.md` to `output/<SpecName>.context.md` in the base directory, overwriting any previous version. This final file is what the user uploads to an AI assistant. Each file starts with an optional YAML front-matter block carrying the values from `frontMatter` in the spec config, which most LLM-facing tools safely ignore but which helps humans.

### End-to-end flow

1. User opens the plugin inside XrmToolBox, which supplies an authenticated `IOrganizationService` via `PluginControlBase.Service`.
2. User selects the **Context-Exporter base directory** (persisted between sessions).
3. The plugin scans `config/*.context-exporter-config.json` and populates the **spec list**. User selects a spec.
4. User clicks **Run Export**. The plugin loads the spec config, resolves all paths relative to the base directory, and validates against the JSON schema.
5. A timestamped run folder `runs/<timestamp>/` is created.
6. For each `QueryDefinition`, the appropriate runner (`FetchXmlQueryRunner`, `WebApiQueryRunner`, `MetadataQueryRunner`) executes against Dataverse. Each result is written to `runs/<timestamp>/output.<queryId>.fetch.json`.
7. `IntermediateJsonBuilder` merges all per-query results plus `_meta` into `runs/<timestamp>/intermediate.json`.
8. `TemplateRenderer.Render()` (Phase 5+; previously `PythonInvoker` launching a Python subprocess) deserialises `intermediate.json` into a Scriban `ScriptObject`, registers the 19 custom filters from `TemplateFilters`, and renders the Scriban template in-process. Output is written to `runs/<timestamp>/output.md`.
9. *(Step merged into step 8 from Phase 5. Previously: `transform.py` rendered the Jinja2 template and wrote `output.md`.)*
10. The plugin copies `output.md` to `output/<SpecName>.context.md`, overwriting any previous version.
11. `OutputPreviewControl` shows the generated files and offers **Open folder** / **Open in VS Code** / **Copy path** actions. A prominent note indicates that `output/<SpecName>.context.md` is the file to upload to the AI assistant.

## Transformation Catalog (initial set)

Five Jinja2 templates ship with the plugin under `config/transformations/`. Each targets a common grounding need for non-agentic AI assistants working on D365 CE projects.

- **`entity-dictionary.j2`** — groups by entity logical name and, for each entity, emits a section with display name, primary attribute, ownership type, and a comma-separated list of attributes with their schema names and types.
- **`security-model.j2`** — for each security role, lists the business unit, a summary of privilege depth per entity (Org / BU / User / None), and associated teams.
- **`optionsets.j2`** — for each global option set, emits the logical name, display name, and a bulleted list of options (value, label, color if set).
- **`forms-and-views.j2`** — for each entity, lists its forms (by type: main, quick create, card) and its system views, with column lists as comma-separated schema names.
- **`solution-inventory.j2`** — lists all solutions in the environment with publisher, version, and a grouped summary of components (counted by type: entity, form, view, workflow, plugin assembly, etc.).

Practitioners add new transformations by dropping a `.j2` file into `config/transformations/` and referencing it by name in the spec config. No C# rebuild required.

## Project Structure and Code Organization

Placed into the repository following the standard layout:

```
/tooling/D365ContextExporter/
  D365ContextExporter.sln
  config/                               ← sample base directory; solution root doubles as the Context-Exporter dir
    Sample.context-exporter-config.json
    queries/
      entities-attributes.fetch.xml
      security-roles.fetch.xml
      optionsets-global.fetch.xml
    transformations/
      entity-dictionary.j2
      security-model.j2
      optionsets.j2
      forms-and-views.j2
      solution-inventory.j2
  output/                               ← created on first run
  runs/                                 ← created on first run
  D365ContextExporter/
    D365ContextExporter.csproj
    ContextExporterPluginControl.cs
    Orchestration/
      ExportJobRunner.cs
      IntermediateJsonBuilder.cs
      PythonInvoker.cs
    Queries/
      FetchXmlQueryRunner.cs
      WebApiQueryRunner.cs
      MetadataQueryRunner.cs
    Models/
      ExportJob.cs
      QueryDefinition.cs
      OutputDefinition.cs
      PythonSettings.cs
      OutputSettings.cs
    Helpers/
      EntityJsonSerializer.cs
      PathResolver.cs
      ProcessRunner.cs
    Utils/
      DataverseClientHelper.cs
      EntityExtensionMethods.cs
      EntityFactory.cs
      JsonSerializerHelper.cs
    UI/
      BaseDirectoryPickerControl.cs (+ .Designer.cs)
      SpecPickerControl.cs (+ .Designer.cs)
      ExportProgressControl.cs (+ .Designer.cs)
      OutputPreviewControl.cs (+ .Designer.cs)
    python/
      transform.py
      filters.py
      requirements.txt
    schema/
      context-exporter.schema.json
    stylecop.json
    D365ContextExporter.nuspec
  D365ContextExporter.Tests/
    D365ContextExporter.Tests.csproj
    OrchestrationTests/
    QueriesTests/
    HelpersTests/
/Pipeline/
  d365contextexporter-build.yml
```

The `python/` and `schema/` folders (inside the C# project) are packed into the XrmToolBox plugin's output directory at build time via MSBuild `CopyToOutputDirectory` items so they travel with the assembly. The `config/`, `output/`, and `runs/` folders live at the solution root and are not shipped with the plugin — they are the developer's own Context-Exporter working directory, checked in as the sample/default configuration.

## Implementation Phases

Work is broken into five phases, each independently deliverable.

### Phase 1 — Skeleton and wiring (sprint 1)

- Create the solution, projects, StyleCop config.
- Implement `ContextExporterPluginControl` with minimum UI: connect to org, base directory picker, spec list, run button, log panel.
- Implement `SpecPickerControl` — scans `config/*.context-exporter-config.json` and populates the spec dropdown.
- Wire `PluginControlBase.Service` through to a stub `ExportJobRunner` that just prints the selected spec config.
- Verify the `.nupkg` builds locally with `dotnet pack`.
- **Exit criteria:** the plugin installs into XrmToolBox, loads, connects to a Dataverse org, shows the spec list, and prints the loaded spec config.

### Phase 2 — Query execution (sprint 2)

- Implement `FetchXmlQueryRunner` (including paging with `PagingInfo`).
- Implement `MetadataQueryRunner` for entities, attributes, option sets, relationships.
- Implement `WebApiQueryRunner` with token reuse from `ServiceClient`.
- Implement `EntityJsonSerializer` with unit tests covering `EntityReference`, `OptionSetValue`, `Money`, `AliasedValue`, `EntityCollection`, null handling.
- Implement `IntermediateJsonBuilder`. Write `output.<queryId>.fetch.json` per query and `intermediate.json` to `runs/<timestamp>/`.
- **Exit criteria:** given a spec config with a couple of FetchXML + metadata queries, the plugin produces per-query raw files and a well-formed `intermediate.json` that can be opened and inspected.

### Phase 3 — Python post-processor and templates (sprint 3)

- Author `transform.py` and `filters.py`. Keep `transform.py` under 150 lines.
- Author the five initial Jinja2 templates listed in the Transformation Catalog.
- Implement `PythonInvoker` with interpreter discovery (explicit path > config's venv > `py -3` > `python` on PATH), timeout, stderr capture, and the copy step that writes `output/<SpecName>.context.md`.
- Implement `OutputPreviewControl` with a prominent call-to-action pointing to the `output/<SpecName>.context.md` grounding file.
- Document the Python venv bootstrap in the plugin's **first-run experience** — if no venv is found, the plugin offers to create one and run `pip install -r requirements.txt` for the user.
- **Exit criteria:** end-to-end run against a real sandbox org produces `output.md` in the run folder and copies it correctly to `output/<SpecName>.context.md`.

### Phase 4 — Polish, packaging, distribution (sprint 4)

- `transform.py` and  `filters.py` should reside and be used from the base config directory, subfolder `transformations` and not be overwritten if they already exist.
- Streamline the existing sample specs to provide a number of examples usage, scaling up complexity to the complete spec `SolutionsReference`. Move all these files to the D365ContextExporter project folder.
- Embed the sample specs, queries, transformation, and all associated files with the DLL to be using by the first time setup.
- Add a setup the first time the base directory is selected or if the saved base directory is found empty. The plugin should offer to create a reference configuration by a Message Box. If the user selects `yes`, the plugin needs to create subfolders `config` containing `queries`, `transformations`, specs, queries, python, jinja2 from the sample config. `runs` and `output` would be create by the plugin when executing.
- Add a new file named `LEGAL.md` (by default located at the base directori) that gets prepended at the very beginning of `output/<SpecName>.context.md` by the plugin (not by a transformation). This file is required and would be configured by the spec file on a `legal` field. It would contain text stating that the tool's output is copyrighted, confidential, and/or restricted in nature, and should serve as a warning to humans and AI tools that distribution, publishing, training of AI models, etc could be infringing on the content owner's rights.
- Config validation with clear error messages (missing `resultKey`, unresolvable query or transformation path, missing `spec` name).
- Cancellation support (`CancellationToken` flowed through runners).
- README with screenshots, quickstart, and sample configs.
- `.nuspec` with proper tags (`XrmToolBox`, `Documentation`, `AI`, `Markdown`) for Tool Library listing.
- Set up the GitHub Actions workflow to build and pack a `.nupkg`.
- Submit to XrmToolBox Tool Library following Jonas Rapp's guidance on NuGet publishing:
  - Create a NuGet account at [nuget.org](https://nuget.org) and generate an API key under your profile.
  - Ensure the `.nuspec` fills `Title`, `Version`, `Authors`, `Description`, and a `Tags` value that starts with `XrmToolBox` but is not exactly `XrmToolBox` (e.g. `XrmToolBox Documentation AI Markdown`).
  - All plugin files must be placed under a `Plugins/` folder in the package; declare a dependency on the `XrmToolBox` NuGet package at the minimum version the plugin was developed against.
  - Add a NuGet Pack task to the GitHub Actions workflow (or Azure DevOps pipeline) after build and tests pass, pointing at the `.nuspec` and stamping the version from the build number.
  - Push the resulting `.nupkg` to nuget.org using the stored API key.
  - Once published, the Tool Library will pick it up automatically — verify the listing appears in XrmToolBox under the expected tags.
  - **References:** [Jonas Rapp — Use Azure DevOps to publish XrmToolBox tools](https://jonasr.app/xtbvsts/) · [XrmToolBox — Deploy your tool in Tool Library](https://www.xrmtoolbox.com/documentation/for-developers/deploy-your-plugin-in-plugins-store/) · [XrmToolBox Wiki — Distribute plugins through NuGet](https://github.com/MscrmTools/XrmToolBox/wiki/Distribute-your-plugins-through-XrmToolBox-and-Nuget)
- Verify `jinja2` and `tiktoken` are listed in `requirements.txt` and included in the first-run bootstrap.
- JSON schema for configuration with `$schema` URL hint so VS Code gives IntelliSense.
- **Exit criteria:** plugin installable from XrmToolBox Tool Library by anyone with the required Python prerequisites.

### Phase 5 — .NET-native rendering via Scriban (late correction, sprint 5)

> **Note.** An IronPython-based embedding was originally planned for this phase. That approach was abandoned when Jinja2 3.1.x was found to use `async def` constructs not supported by IronPython 3.4's language compatibility layer. This phase replaces the Python post-processor with .NET-native equivalents instead.

- Replace `PythonInvoker` with a new `TemplateRenderer` class that renders templates in-process using **Scriban**, a .NET template engine with syntax closely derived from Liquid/Jinja2.
- Port all 19 custom filter functions from `filters.py` into a new `TemplateFilters.cs` class. Filters are registered as named delegates on Scriban's `ScriptObject` and invoked via the same pipe syntax (`{{ value | filter_name }}`).
- Absorb the `transform.py` orchestration logic (load JSON, build context, render, write `output.md`) directly into `TemplateRenderer.Render()`. `intermediate.json` is deserialized into a Scriban `ScriptObject` via a recursive `JToken` converter.
- Remove token counting (`tiktoken`) entirely. No `token_count.txt` sidecar is written and no token-count blockquote appears in output files.
- Delete `transform.py`, `filters.py`, `requirements.txt`, `PythonInvoker.cs`, `PythonBootstrapHelper.cs`, and `ProcessRunner.cs`. No Python files of any kind remain in the project.
- Port the six `.j2` template files from Jinja2 syntax to Scriban syntax (control-flow tags change from `{% %}` to `{{ }}`; pipe filter syntax is identical). Files keep the `.j2` extension so spec configs require no changes.
- Add `Scriban` NuGet package; add `Scriban.dll` to the nuspec. No Python runtime or IronPython packages are shipped.
- **Exit criteria:** plugin installs and runs with no Python on the user's machine; all templates and filters produce equivalent output to the Jinja2 originals; no token-count blockquote appears in any output file.

## Testing Strategy

Following the project's testing conventions (Moq-based, no Fakes frameworks):

- **Unit tests for query runners** using Moq on `IOrganizationService`. Every runner gets coverage for happy path, empty results, paging, and common exception types.
- **Unit tests for `EntityJsonSerializer`** covering every Dataverse type the serializer handles.
- **Unit tests for `PathResolver`** covering env-var expansion, relative path resolution, UNC paths.
- **Unit tests for `PythonInvoker`** using `Moq.Contrib.HttpClient`-style patterns (actually using a fake `IProcessFactory` abstraction so tests don't spawn real processes).
- **Integration test** run manually against a sandbox org for each template. Automated integration tests are out of scope — XrmToolBox plugins are fundamentally UI-coupled.
- **Python side** — `transform.py` is small enough that a single `pytest` file with snapshot tests against known-good Markdown outputs is sufficient.

## Packaging and Distribution

- Built as a signed NuGet package following the XrmToolBox Tool Library convention.
- The `.nuspec` tags must include `XrmToolBox` to appear in the Tool Library.
- From Phase 5, the nuspec ships the plugin DLL, PDB, `Scriban.dll`, and the `schema\` directory under `Plugins\`. No Python files are included. Sample configs, templates, and query files are embedded as assembly resources and deployed by `FirstRunHelper` on first run — they are not loose files in the nupkg.
- Assembly version, file version, and NuGet version are kept in sync via the ADO `Assembly Info` task so the plugin passes the Tool Library validation criteria.
- ADO pipeline (`/Pipeline/d365contextexporter-build.yml`) modeled on the standard Azure Function build template but targeting pack rather than deploy.

## Risks and Mitigations

- ~~**Python not installed on the user's machine.**~~ **Resolved in Phase 5.** No Python installation is required; Scriban renders templates in-process.
- **Templates can loop forever.** Mitigation (Phase 5): Scriban's `TemplateContext` supports `LoopLimit` (default 100,000 iterations) which terminates runaway loops and throws a `ScribanException`. Set an explicit `context.LoopLimit` appropriate for large Dataverse orgs.
- **Large Dataverse orgs can produce enormous intermediate JSON files.** Mitigation: the intermediate is streamed to disk (not held fully in memory) by using `Newtonsoft.Json`'s `JsonTextWriter` directly. Per-query row limits can be set in the config (`maxRecords`).
- **AI tools consuming the Markdown may have upload size limits.** Mitigation: templates are designed to be split by topic (five initial templates rather than one giant file). The config can target multiple small outputs instead of one big one.

## Future Enhancements (out of scope for v1)

- A transformation marketplace where practitioners share `.j2` templates by domain (Field Service, Sales, Customer Service, Power Pages).
- An "update grounding" mode that detects changes in Dataverse since the last run and only regenerates affected Markdown files.
- A companion **Claude Code / Cursor integration** that watches the output folder and automatically refreshes the AI tool's context.
- YAML front-matter conventions that let LLMs cite specific sections back to the user (e.g., `<!-- section: account.attributes -->` anchors).
- An optional second pass that uses a local LLM to compress the Markdown further before upload, useful when grounding multi-hundred-MB dictionaries.

## Alignment with Project Conventions

- **StyleCop** on, with the standard `stylecop.json` from the shared utility libraries.
- **Moq** for unit testing.
