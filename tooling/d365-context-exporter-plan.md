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

## Solution Architecture

```
+-------------------------------+
|  XrmToolBox (Windows Forms)   |
|  .NET Framework 4.8           |
|                               |
|  +-------------------------+  |
|  |  D365.ContextExporter   |  |       config.json
|  |       (plugin)          |<-+---- templates/*.j2
|  |                         |  |     queries/*.fetch.xml
|  |  - Config loader        |  |
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
  Dataverse         intermediate.json
  (D365 CE)              |
                         v
               +--------------------+
               | Python 3.11+       |
               | transform.py       |
               | jinja2, pyyaml     |
               +--------------------+
                         |
                         v
               output/*.md (grounding files)
```

The plugin hosts a small WinForms UI. The user picks a configuration file, the plugin reads it, runs each query against the connected environment, writes an intermediate JSON snapshot to a working folder, then shells out to `python transform.py` with arguments pointing to the JSON and the template set. Python reads the JSON, renders each template against it, and writes the `.md` files.

### Why shell out to Python instead of embedding it

Three options were considered for running Python inside a .NET 4.8 host:

- **IronPython** — pure managed implementation, but its ecosystem lags real CPython and Jinja2 runs best on CPython. It would also tie users to whatever IronPython version we ship.
- **Python.NET (pythonnet)** — embeds CPython in-process. Works, but tightly couples the plugin to a specific Python binary at load time, and any `pip install` mismatch crashes the XrmToolBox process.
- **Subprocess `python.exe`** — the plugin launches Python as a child process, passing JSON by file and reading stdout/stderr. Decouples the two runtimes, survives Python environment changes, and means any user-authored Python script can be swapped in. **Chosen.**

## Executing Environment

Target machine: **Windows 10/11 developer workstation**.

Required once per machine:

- Windows 10 22H2 or Windows 11, x64.
- .NET Framework 4.8 runtime (ships with current Windows; required by XrmToolBox 1.2025.x+).
- XrmToolBox latest stable release, installed from `xrmtoolbox.com`.
- Visual Studio 2022 (Community is fine) with the .NET desktop development workload — **only required for plugin developers**, not for end users of the plugin.
- Python 3.11 or later (64-bit), installed from `python.org` and added to `PATH`. A `py` launcher is acceptable; the plugin will prefer `py -3` when available.
- A Python virtual environment at a well-known path (for example `%LOCALAPPDATA%\D365ContextExporter\venv`) with the following packages pinned via `requirements.txt`: `jinja2`, `pyyaml`, `python-dateutil`.

Authenticated connection to Dataverse is inherited from XrmToolBox's Connection Manager — the plugin does not manage credentials itself. This aligns with XrmToolBox convention (`PluginControlBase.Service` gives you a ready-to-use `IOrganizationService`).

## Configuration Design

The configuration is a single JSON file (YAML optional) that describes the full export job. It lives on disk, is version-controlled with the rest of the D365CE repository under `Other/ContextExporter/` (following the standard repository layout), and is authored by hand or by other tools.

Suggested top-level schema:

```json
{
  "$schema": "./schema/context-exporter.schema.json",
  "name": "Full CE Context Pack",
  "version": "1.0.0",
  "output": {
    "directory": "./out",
    "clean": true,
    "frontMatter": {
      "client": "<ClientName>",
      "generatedBy": "D365 CE Context Exporter"
    }
  },
  "python": {
    "interpreter": "auto",
    "venv": "%LOCALAPPDATA%/D365ContextExporter/venv",
    "scriptPath": "./scripts/transform.py"
  },
  "queries": [
    {
      "id": "entities-with-attributes",
      "type": "fetchxml",
      "source": "./queries/entities-attributes.fetch.xml",
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
      "source": "./queries/security-roles.fetch.xml",
      "resultKey": "securityRoles"
    }
  ],
  "outputs": [
    {
      "template": "./templates/entity-dictionary.j2",
      "file": "entity-dictionary.md",
      "requires": ["entityAttributes", "globalOptionSets"]
    },
    {
      "template": "./templates/security-roles.j2",
      "file": "security-roles.md",
      "requires": ["securityRoles"]
    }
  ]
}
```

Key principles: every query has a `resultKey` that names the slot in the intermediate JSON where its results will be placed; every output template declares the `requires` keys it consumes, so the plugin can skip or warn when a required slot is missing.

## Components and Modules

### C# side — XrmToolBox plugin

Following the standard folder/namespace pattern, with the plugin name `D365ContextExporter`:

- **`D365ContextExporter` (root project)** — the plugin class itself (`ContextExporterPluginControl : PluginControlBase, IXrmToolBoxPluginControl`). Hosts the UI, wires events, owns nothing else.
- **`Business/`** — orchestration logic.
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
  - `ConfigPickerControl` — file picker + recent configs list.
  - `ExportProgressControl` — per-query progress, log panel, cancel button.
  - `OutputPreviewControl` — after a run, shows the list of generated `.md` files with open/reveal actions.

### Python side — post-processor

Very small, deliberately. Python should be used as a **post-processor only**.

- **`scripts/transform.py`** — the single entry point. Reads CLI args (intermediate JSON path, output directory, list of template+output pairs), loads JSON, renders each Jinja2 template, writes each Markdown file. ~100 lines.
- **`scripts/filters.py`** — custom Jinja2 filters reusable across templates (`schemaname_to_title`, `optionset_values`, `markdown_table`, `group_by`, `csv_list`, etc.). This is where the kind of "pivot the field names into a comma separated list" logic lives.
- **`scripts/requirements.txt`** — pinned dependencies.

### Templates

Authored as plain Jinja2 files (`.j2` extension) under `templates/`. Templates are data, not code — they can be copied between projects and edited without a build step. Initial templates to ship with the plugin are listed in the **Template Catalog** section below.

### Queries

FetchXML files under `queries/*.fetch.xml`. Standard FetchXML authored in FetchXML Builder. One query per file for reusability and diffability.

## Artifacts Inventory

The following artifacts will be built. Each row maps to a concrete deliverable.

| # | Artifact | Type | Location | Notes |
|---|----------|------|----------|-------|
| 1 | `D365ContextExporter.sln` | VS Solution | `/Assemblies/XrmToolboxPlugins/D365ContextExporter/` | Standard repo layout |
| 2 | `D365ContextExporter.csproj` | C# project | same | SDK-style, `net48`, StyleCop on |
| 3 | `ContextExporterPluginControl.cs` | C# / UserControl | project root | The `IXrmToolBoxPluginControl` |
| 4 | `ExportJobRunner.cs` | C# static class | `Business/` | Orchestration |
| 5 | `IntermediateJsonBuilder.cs` | C# static class | `Business/` | Shapes the JSON contract |
| 6 | `PythonInvoker.cs` | C# class | `Business/` | Subprocess management |
| 7 | `FetchXmlQueryRunner.cs` | C# static class | `Queries/` | Paging, error wrapping |
| 8 | `WebApiQueryRunner.cs` | C# class | `Queries/` | Uses `HttpClient`, reuses bearer |
| 9 | `MetadataQueryRunner.cs` | C# static class | `Queries/` | `RetrieveAllEntitiesRequest` etc. |
| 10 | `ExportJob.cs` + sibling models | C# POCOs | `Models/` | Configuration DTOs |
| 11 | `EntityJsonSerializer.cs` | C# static class | `Helpers/` | Cleans Dataverse types for JSON |
| 12 | `PathResolver.cs` | C# static class | `Helpers/` | Path + env-var expansion |
| 13 | `ProcessRunner.cs` | C# class | `Helpers/` | `Process` wrapper |
| 14 | Shared utility `*.cs` files | C# static classes | `Utils/` | Shared utility copies |
| 15 | `ConfigPickerControl.cs` | C# UserControl | `UI/` | Config file chooser |
| 16 | `ExportProgressControl.cs` | C# UserControl | `UI/` | Progress + log |
| 17 | `OutputPreviewControl.cs` | C# UserControl | `UI/` | Generated file list |
| 18 | `stylecop.json` | Config | project root | StyleCop configuration |
| 19 | `transform.py` | Python script | `/python/` inside plugin package | ~100 LOC entry point |
| 20 | `filters.py` | Python module | `/python/` | Shared Jinja2 filters |
| 21 | `requirements.txt` | Python deps | `/python/` | Pinned versions |
| 22 | `context-exporter.schema.json` | JSON Schema | `/schema/` | IDE IntelliSense for configs |
| 23 | `templates/entity-dictionary.j2` | Jinja2 | `/templates/` | See catalog below |
| 24 | `templates/security-model.j2` | Jinja2 | `/templates/` | See catalog below |
| 25 | `templates/optionsets.j2` | Jinja2 | `/templates/` | See catalog below |
| 26 | `templates/forms-and-views.j2` | Jinja2 | `/templates/` | See catalog below |
| 27 | `templates/solution-inventory.j2` | Jinja2 | `/templates/` | See catalog below |
| 28 | `queries/*.fetch.xml` | FetchXML | `/queries/` | One per query |
| 29 | `README.md` | Markdown | repo root | User + developer docs |
| 30 | `D365ContextExporter.nuspec` | NuGet spec | project root | For XrmToolBox Tool Library listing |
| 31 | Unit test project | C# project | `/Tests/D365ContextExporter.Tests/` | Moq-based |
| 32 | Azure DevOps pipeline YAML | YAML | `/Pipeline/` | Build + pack + publish nupkg |

## Libraries and Dependencies

### C# — NuGet packages

- `XrmToolBoxPackage` (latest, currently `1.2025.10.74`, targets .NET Framework 4.8) — brings in XrmToolBox plugin contracts and the connection control.
- `Microsoft.PowerPlatform.Dataverse.Client` (latest stable) — the current supported Dataverse client. Use `ServiceClient` rather than the legacy `CrmServiceClient`.
- `Newtonsoft.Json` — already a transitive dependency; use it for config loading and intermediate JSON authoring to avoid dragging in `System.Text.Json` at 4.8.
- `StyleCop.Analyzers` 1.1.118+ — required for C# style enforcement.
- `Moq` and `Moq.Contrib.HttpClient` — test project only.

### Python — pip packages (pinned in `requirements.txt`)

- `Jinja2` (latest 3.x).
- `PyYAML` — lets users optionally author configs or template fragments in YAML.
- `python-dateutil` — forgiving date parsing inside templates.
- `markupsafe` — pulled in transitively by Jinja2; listed for clarity.

No C extensions beyond what Jinja2 already uses, so `pip install` works out-of-the-box on a clean Windows machine.

### External tools (not libraries, but required)

- **Python 3.11+** on `PATH` (or discoverable via the `py` launcher).
- **XrmToolBox** 1.2025.10.x or later.
- **Visual Studio 2022** (developers only).

## Data Flow

### Inputs

1. **Configuration file** (`*.context-export.json`) — authored in VS Code or any editor, validated against `context-exporter.schema.json`.
2. **Query files** (`*.fetch.xml`) — authored in FetchXML Builder.
3. **Template files** (`*.j2`) — authored in any editor; VS Code has decent Jinja2 extensions.
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
    "frontMatter": { "client": "<ClientName>" }
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

One Markdown file per entry in the `outputs` array of the configuration. Files land in the directory specified by `output.directory`. Each file starts with an optional YAML front-matter block carrying the values from `output.frontMatter`, which most LLM-facing tools safely ignore but which helps humans.

### End-to-end flow

1. User opens the plugin inside XrmToolBox, which supplies an authenticated `IOrganizationService` via `PluginControlBase.Service`.
2. User picks a config file and clicks **Run Export**.
3. The plugin loads the config (`ExportJob`), resolves all relative paths, and validates against the JSON schema.
4. For each `QueryDefinition`, the appropriate runner (`FetchXmlQueryRunner`, `WebApiQueryRunner`, `MetadataQueryRunner`) executes against Dataverse. Results are collected into a dictionary keyed by `resultKey`.
5. `IntermediateJsonBuilder` merges all results plus `_meta` into one `intermediate.json` in the working directory.
6. `PythonInvoker` launches `python transform.py --input intermediate.json --config <config.json> --out <output.directory>`. Stdout streams into the plugin's log panel; a non-zero exit code is surfaced as an error.
7. `transform.py` iterates over `outputs`, loads each template, renders it against the JSON data (plus custom filters from `filters.py`), and writes the `.md` file.
8. The plugin reads the output directory, shows the generated files in `OutputPreviewControl`, and offers **Open folder** / **Open in VS Code** / **Copy path** actions.

## Template Catalog (initial set)

Five templates ship with the plugin. Each one targets a common grounding need for non-agentic AI assistants working on D365 CE projects.

- **`entity-dictionary.j2`** — groups by entity logical name and, for each entity, emits a section with display name, primary attribute, ownership type, and a comma-separated list of attributes with their schema names and types. This is the canonical use case from our earlier conversation.
- **`security-model.j2`** — for each security role, lists the business unit, a summary of privilege depth per entity (Org / BU / User / None), and associated teams.
- **`optionsets.j2`** — for each global option set, emits the logical name, display name, and a bulleted list of options (value, label, color if set).
- **`forms-and-views.j2`** — for each entity, lists its forms (by type: main, quick create, card) and its system views, with column lists as comma-separated schema names.
- **`solution-inventory.j2`** — lists all solutions in the environment with publisher, version, and a grouped summary of components (counted by type: entity, form, view, workflow, plugin assembly, etc.).

Practitioners are expected to add project-specific templates as needed. Adding a template means: drop a `.j2` file in the `templates/` folder and reference it from the config. No C# rebuild.

## Project Structure and Code Organization

Placed into the repository following the standard layout:

```
/Assemblies/XrmToolboxPlugins/D365ContextExporter/
  D365ContextExporter.sln
  D365ContextExporter/
    D365ContextExporter.csproj
    ContextExporterPluginControl.cs
    Business/
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
      ConfigPickerControl.cs (+ .Designer.cs)
      ExportProgressControl.cs (+ .Designer.cs)
      OutputPreviewControl.cs (+ .Designer.cs)
    python/
      transform.py
      filters.py
      requirements.txt
    templates/
      entity-dictionary.j2
      security-model.j2
      optionsets.j2
      forms-and-views.j2
      solution-inventory.j2
    queries/
      entities-attributes.fetch.xml
      security-roles.fetch.xml
      optionsets-global.fetch.xml
      ... etc
    schema/
      context-exporter.schema.json
    stylecop.json
    D365ContextExporter.nuspec
  D365ContextExporter.Tests/
    D365ContextExporter.Tests.csproj
    BusinessTests/
    QueriesTests/
    HelpersTests/
/Pipeline/
  d365contextexporter-build.yml
```

The `python/`, `templates/`, `queries/`, and `schema/` folders are packed into the XrmToolBox plugin's output directory at build time via MSBuild `CopyToOutputDirectory` items so they travel with the assembly.

## Implementation Phases

Work is broken into four phases, each independently deliverable.

### Phase 1 — Skeleton and wiring (sprint 1)

- Create the solution, projects, StyleCop config.
- Implement `ContextExporterPluginControl` with minimum UI: connect to org, pick config, run button, log panel.
- Wire `PluginControlBase.Service` through to a stub `ExportJobRunner` that just prints the config it loaded.
- Set up the ADO pipeline to build and pack a `.nupkg`.
- **Exit criteria:** the plugin installs into XrmToolBox, loads, connects to a Dataverse org, and can print a loaded config.

### Phase 2 — Query execution (sprint 2)

- Implement `FetchXmlQueryRunner` (including paging with `PagingInfo`).
- Implement `MetadataQueryRunner` for entities, attributes, option sets, relationships.
- Implement `WebApiQueryRunner` with token reuse from `ServiceClient`.
- Implement `EntityJsonSerializer` with unit tests covering `EntityReference`, `OptionSetValue`, `Money`, `AliasedValue`, `EntityCollection`, null handling.
- Implement `IntermediateJsonBuilder`. Write `intermediate.json` to a `runs/<timestamp>/` folder.
- **Exit criteria:** given a config with a couple of FetchXML + metadata queries, the plugin produces a well-formed `intermediate.json` that can be opened and inspected.

### Phase 3 — Python post-processor and templates (sprint 3)

- Author `transform.py` and `filters.py`. Keep `transform.py` under 150 lines.
- Implement the five initial templates listed in the Template Catalog.
- Implement `PythonInvoker` with interpreter discovery (explicit path > config's venv > `py -3` > `python` on PATH), timeout, and stderr capture.
- Implement `OutputPreviewControl`.
- Document the Python venv bootstrap in the plugin's **first-run experience** — if no venv is found, the plugin offers to create one and run `pip install -r requirements.txt` for the user.
- **Exit criteria:** end-to-end run against a real sandbox org produces all five Markdown files correctly.

### Phase 4 — Polish, packaging, distribution (sprint 4)

- JSON schema for configuration with `$schema` URL hint so VS Code gives IntelliSense.
- Config validation with clear error messages (missing `resultKey`, template `requires` not satisfied, unresolvable template path).
- Recent configs MRU list.
- Cancellation support (`CancellationToken` flowed through runners).
- README with screenshots, quickstart, and sample configs.
- `.nuspec` with proper tags (`XrmToolBox`, `Documentation`, `AI`, `Markdown`) for Tool Library listing.
- Submit to XrmToolBox Tool Library following Jonas Rapp's guidance on Nuget publishing.
- **Exit criteria:** plugin installable from XrmToolBox Tool Library by anyone with the required Python prerequisites.

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
- Because the plugin bundles Python scripts and Jinja2 templates, the nuspec explicitly includes `python/**`, `templates/**`, `queries/**`, `schema/**` as `content` files. These are copied alongside the assembly when XrmToolBox extracts the plugin package.
- Assembly version, file version, and NuGet version are kept in sync via the ADO `Assembly Info` task so the plugin passes the Tool Library validation criteria.
- ADO pipeline (`/Pipeline/d365contextexporter-build.yml`) modeled on the standard Azure Function build template but targeting pack rather than deploy.

## Risks and Mitigations

- **Python not installed on the user's machine.** Mitigation: the plugin's first run detects missing Python, shows a dialog with a direct link to `python.org`, and refuses to proceed rather than failing obscurely. An optional install-helper button can invoke `winget install Python.Python.3.11` on systems where `winget` is available.
- **Jinja2 templates are Turing-complete and can loop forever.** Mitigation: the subprocess call is wrapped with a default 5-minute timeout, configurable per job. Process is forcibly killed on timeout and the user sees a clear message.
- **Large Dataverse orgs can produce enormous intermediate JSON files.** Mitigation: the intermediate is streamed to disk (not held fully in memory) by using `Newtonsoft.Json`'s `JsonTextWriter` directly. Per-query row limits can be set in the config (`maxRecords`).
- **.NET Framework 4.8 conflicts with the general project preference for 4.6.2.** Mitigation: document the exception explicitly in the plugin's README — the target is dictated by `XrmToolBoxPackage`, not by choice. All business logic remains portable and could be lifted into a `netstandard2.0` shared library later if needed.
- **Credentials exposure through intermediate JSON.** Mitigation: `EntityJsonSerializer` has a configurable attribute deny-list; by default it drops attributes whose logical names contain `password`, `secret`, `token`, or `key`. The deny-list is extensible per job.
- **AI tools consuming the Markdown may have upload size limits.** Mitigation: templates are designed to be split by topic (five initial templates rather than one giant file). The config can target multiple small outputs instead of one big one.

## Future Enhancements (out of scope for v1)

- A template marketplace where practitioners share `.j2` files by domain (Field Service, Sales, Customer Service, Power Pages).
- An "update grounding" mode that detects changes in Dataverse since the last run and only regenerates affected Markdown files.
- A companion **Claude Code / Cursor integration** that watches the output folder and automatically refreshes the AI tool's context.
- YAML front-matter conventions that let LLMs cite specific sections back to the user (e.g., `<!-- section: account.attributes -->` anchors).
- An optional second pass that uses a local LLM to compress the Markdown further before upload, useful when grounding multi-hundred-MB dictionaries.

## Alignment with Project Conventions

- **StyleCop** on, with the standard `stylecop.json` from the shared utility libraries.
- **Moq** for unit testing.
