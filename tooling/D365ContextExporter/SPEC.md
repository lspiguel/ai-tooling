# D365 CE Context Exporter — specification

> **Status:** Released · **Version:** 1.2026.11.1 · **Date:** 2026-11-09 · **Plugin:** `D365ContextExporter` · **Package:** `Lspiguel.Xrm.D365ContextExporter`

An XrmToolBox plugin that runs the queries named in a spec file against a connected Dynamics 365 CE (Dataverse) environment, writes the results to an intermediate JSON file, and renders them through a Scriban template into one Markdown file. That file is uploaded as grounding context to a general-purpose AI assistant that cannot reach the environment itself.

This document describes the plugin as built. It was derived from the code at 1.2026.7.2, the original plan and the six phase plans, and kept up to date through 1.2026.11.1; how the plugin got here, the designs it left behind, and the open items found while writing this spec are in [notes.md](notes.md). User documentation is the [README](README.md).

---

## Contents

1. [Problem](#1-problem)
2. [Rules](#2-rules)
3. [Scope](#3-scope)
4. [Architecture](#4-architecture)
5. [Base directory](#5-base-directory)
6. [Spec configuration](#6-spec-configuration)
7. [Query execution](#7-query-execution)
8. [Intermediate JSON](#8-intermediate-json)
9. [Rendering](#9-rendering)
10. [Output and legal notice](#10-output-and-legal-notice)
11. [User experience](#11-user-experience)
12. [Reference configuration](#12-reference-configuration)
13. [Packaging and distribution](#13-packaging-and-distribution)
14. [Testing](#14-testing)
15. [Future enhancements](#15-future-enhancements)
16. [References](#16-references)

---

## 1. Problem

A general-purpose AI assistant — a chat assistant on the web, or an enterprise assistant such as Microsoft 365 Copilot — cannot browse a codebase or connect to a Dataverse environment. Without grounding it does not know a project's tables, columns, option sets, security roles, plug-ins, flows or solutions, and every conversation starts by explaining them again.

The exporter closes that gap for people who have no coding agent, MCP server or live environment access to give the assistant. It produces a structured, current snapshot of the environment as a Markdown file the person uploads to the assistant.

The design separates three concerns:

| Concern | Where it lives | Why |
|---|---|---|
| **What to pull** from Dataverse | C# query runners inside the plugin | XrmToolBox already holds an authenticated connection and the SDK |
| **How results become Markdown** | Scriban templates and FetchXML files on disk, selected by a JSON spec | New output formats need no recompile; practitioners author, review and version them as text |
| **The bridge** between the two | `intermediate.json` in a per-run folder | Each run is inspectable, and a template can be developed against a saved intermediate |

---

## 2. Rules

| # | Rule | Why |
|---|---|---|
| R1 | **Read-only.** The plugin never creates, updates or deletes Dataverse data or metadata | It is a grounding tool, not an administration tool |
| R2 | **No client data in the repository** — sample configs, queries, templates, tests and fixtures are synthetic. Base directories with real output stay outside the repository | Client data boundary |
| R3 | **The legal notice is added by the plugin, not by a template.** A spec that names a `legal` file gets its content prepended to the delivered file; a user's `LEGAL.md` is never overwritten | The boundary notice must not depend on template authors remembering it |
| R4 | **No DLL ships next to the plugin.** Dependencies are source-embedded (Scriban) or provided by XrmToolBox (Newtonsoft.Json, SDK). The package holds only the plugin DLL and its PDB in `Plugins\` | XrmToolBox locks DLLs in `Plugins` subfolders at start-up, which broke Tool Library update and uninstall ([notes](notes.md#post-phase-6--releases)) |
| R5 | **Templates must not call `object.from_json` / `object.to_json`.** Scriban is compiled without System.Text.Json support; these built-ins throw. JSON text in a column is parsed with the `parse_json` function instead ([§9.3](#93-custom-functions)) | Consequence of R4; query results are already template variables |
| R6 | **Late-bound SDK access only** — no early-bound classes | Tables change too fast in a live environment to keep generated classes current |
| R7 | **Transformation is configuration.** Adding a query, template or spec needs no rebuild of the plugin | The whole point of the spec/template split ([§1](#1-problem)) |
| R8 | **Upgrades touch only files the plugin shipped, and only with consent.** Redeploying the reference configuration is offered, never forced; a shipped file the person edited is backed up under `config\backup\` before it is replaced — the one place an upgrade writes besides the shipped files and `version.txt`; files of other names and `LEGAL.md` are never touched | User-authored specs, queries and templates live in the same folders |
| R9 | **Runs are never deleted.** Every run keeps its own timestamped folder; the plugin never cleans `runs\` | A run is the evidence for what a grounding file contained |
| R10 | **Version bumps touch two files** — `AssemblyVersion` / `FileVersion` / `Version` in the `.csproj` and `<version>` in the `.nuspec`, kept identical. Scheme `1.YYYY.M.N` | Tool Library validation; the base directory upgrade check compares the assembly version ([§5.3](#53-upgrade)) |
| R11 | **A new template filter** goes in `TemplateFilters.cs` and in the README's built-in function table | Template authors only see the README |

R2, R4, R5, R6, R8, R10 and R11 are also stated in [INSTRUCTIONS.md](/INSTRUCTIONS.md#tooling-conventions--d365-context-exporter).

---

## 3. Scope

### In scope

- One XrmToolBox connection at a time, supplied by XrmToolBox's connection manager; the plugin holds no credentials.
- Three query types: FetchXML, Dataverse Web API (OData GET), and the typed metadata SDK.
- One template per spec, rendering one Markdown file per run.
- A reference configuration (nine specs, their queries and templates, a JSON schema and a legal notice) deployed into the user's base directory on first use and redeployed on upgrade by consent.
- Distribution through the XrmToolBox Tool Library as a NuGet package.

### Out of scope

| Item | Note |
|---|---|
| Writing to Dataverse | R1 |
| Running prompts or uploading files to an AI assistant | The person uploads the file |
| Generic ETL or non-Markdown output | Output is always one Markdown file per spec run |
| Several connections in one run | One run uses the tab's current connection |
| Token counting | Built in Phase 3, removed in Phase 5 ([notes](notes.md#phase-5--net-native-rendering-with-scriban)) |
| Headless or scheduled runs | See [§15](#15-future-enhancements) |
| Automated integration tests against a live environment | XrmToolBox plugins are UI-coupled; integration is checked by hand ([§14](#14-testing)) |

---

## 4. Architecture

```
XrmToolBox (WinForms, .NET Framework 4.8)
└─ ContextExporterPluginControl ── base directory picker · spec picker · Run · progress/log · output panel
     │  IOrganizationService + ConnectionDetail (from PluginControlBase)
     ▼
   ExportJobRunner  (on the XrmToolBox worker thread)
     ├─ ConfigValidator
     ├─ FetchXmlQueryRunner ─► EntityJsonSerializer ─┐
     ├─ WebApiQueryRunner  (bearer token reused) ────┼─► IntermediateJsonBuilder ─► runs\<ts>\output.<id>.fetch.json
     ├─ MetadataQueryRunner ─────────────────────────┘                             runs\<ts>\intermediate.json
     ├─ TemplateRenderer (Scriban, TemplateFilters) ──────────────────────────────► runs\<ts>\output.md
     └─ copy + legal prepend ─────────────────────────────────────────────────────► output\<Spec>.context.md
```

Root namespace `Lspiguel.Xrm.D365ContextExporter`; assembly `D365ContextExporter`. Internal types are visible to the test assembly.

| Folder / file | Responsibility |
|---|---|
| `D365ContextExporterPlugin.cs` | MEF export (`IXrmToolBoxPlugin`): name, description, base64 icons, colours; returns the control. No assembly-resolve handler (R4) |
| `ContextExporterPluginControl.cs` | The `PluginControlBase`. Composes the UI controls, owns the cancellation source, starts runs and batch runs through `WorkAsync`, triggers first-run setup and the upgrade check |
| `Models/` | `ExportJob` (the spec, with `Load(path)`), `QueryDefinition`, `OutputSettings` (deny list), `PythonSettings` (ignored; kept so older configs deserialize) |
| `Queries/` | `FetchXmlQueryRunner`, `WebApiQueryRunner`, `MetadataQueryRunner` ([§7](#7-query-execution)) |
| `Orchestration/` | `ExportJobRunner` (one run end to end), `ExportBatchRunner` and `ExportBatchResult` (every spec in turn, [§11.2](#112-flow)), `IntermediateJsonBuilder` ([§8](#8-intermediate-json)), `TemplateRenderer` ([§9](#9-rendering)) |
| `Helpers/` | `ConfigValidator`, `EntityJsonSerializer`, `FirstRunHelper` (deploy and upgrade), `PathResolver`, `TemplateFilters` |
| `UI/` | `BaseDirectoryPickerControl`, `SpecPickerControl`, `ExportProgressControl`, `OutputPreviewControl`, `WelcomeForm` |
| `SampleConfig/` | The reference configuration, every file an embedded resource ([§12](#12-reference-configuration)) |
| `Resources/WelcomeQuickStart.html` | Embedded HTML for the one-time welcome dialog |
| `Properties/Settings.settings` | User-scoped `BaseDirectory` (string) and `WelcomeShown` (bool) |

---

## 5. Base directory

### 5.1 Layout

All runtime files live under one folder the person chooses. The plugin creates `config\`, `runs\` and `output\` as needed.

```
<base directory>/
├── config/
│   ├── <Spec>.context-exporter-config.json   one per spec; discovered by the spec picker
│   ├── context-exporter.schema.json           JSON schema, referenced as "./context-exporter.schema.json"
│   ├── queries/*.fetch.xml                    shared FetchXML files
│   └── transformations/*.sbn                  shared Scriban templates
├── runs/<yyyyMMdd-HHmmss>[-n]/                one folder per run (UTC timestamp; -2, -3… for a later run in the same second), never deleted
│   ├── output.<queryId>.fetch.json            raw result of each query
│   ├── intermediate.json                      all results plus _meta
│   └── output.md                              rendered template, no legal notice
├── output/<Spec>.context.md                   the grounding file — overwritten by each run of that spec
├── LEGAL.md                                   legal notice, user-owned once deployed
└── version.txt                                assembly version that last deployed or acknowledged this folder
```

Queries and templates are shared: any spec can reference any of them. There are no per-spec query or template files.

### 5.2 First-run setup

Run whenever a base directory is loaded — at plugin load (the saved directory) and on every directory change.

1. **Configured?** A directory is configured when `config\` holds at least one `*.context-exporter-config.json` at its top level.
2. **Offer.** If not configured, a Yes/No dialog offers to deploy the reference configuration.
3. **Deploy** (on Yes) — every file in the deployment list ([§12](#12-reference-configuration)) is written to its place; a file that already exists is skipped and logged `[Setup] Skipped (exists)`.
4. **Legal notice.** If `LEGAL.md` does not exist, an input box asks for the organisation name and every `[ORGANISATION NAME]` in the template is replaced with it. A cancelled or empty answer leaves the placeholder in place. When the owner window has no handle (tests), no prompt is shown.
5. **`version.txt`** is written with the current assembly version.
6. The spec list is reloaded.

### 5.3 Upgrade

After the first-run step, the plugin compares `version.txt` with its own assembly version (`1.YYYY.M.N`).

| State | Behaviour |
|---|---|
| `version.txt` missing | Written with the current version, silently (a folder set up before the check existed) |
| Same version | Nothing |
| Different version (newer or older) | Dialog offers to update the reference files, saying that shipped queries, templates and sample specs are replaced, edited ones are backed up first, and `LEGAL.md` and files of other names are not touched. **Yes:** every file in the deployment list is overwritten, after any whose content differs from the new version is backed up (below); then `version.txt` is updated. **No:** `version.txt` is updated so the offer does not return for this version |

**Backup.** Before overwriting a shipped file whose content differs from the new version, the upgrade copies it to `config\backup\<previous version>\` under the same path relative to `config\` — `config\queries\x.fetch.xml` goes to `config\backup\<previous version>\queries\x.fetch.xml` — and logs `[Setup] Backed up`. The previous version is the one read from `version.txt`. If that backup folder already exists from an earlier upgrade, the next free name `<previous version>-2`, `-3`, … is used, so a backup is never overwritten. The folder is created only when a file needs backing up, and the plugin never deletes it (as with runs, R9). Backed-up specs sit below `config\`'s top level, so the spec picker ignores them.

Never touched by an upgrade: `LEGAL.md`, and every file whose name is not in the deployment list (R8). A file removed from the deployment list in a later version stays in the user's folder.

---

## 6. Spec configuration

A spec is a `*.context-exporter-config.json` file directly under `config\`. The spec picker lists them by file name with the `.context-exporter-config` suffix removed; the output file name comes from the `spec` field. Deserialization is Newtonsoft.Json; the JSON schema exists for editor IntelliSense only and is not applied at run time.

### 6.1 Fields

| Field | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `spec` | string | yes | — | Spec name; stem of `output\<spec>.context.md` and `_meta.spec` |
| `version` | string | no | `"1.0.0"` | Version of this config file (schema pattern `n.n.n`); informational |
| `transformation` | string | yes | — | Template file name in `config\transformations\` |
| `queries` | array | yes | — | Ordered query definitions ([§6.2](#62-query-definition)) |
| `legal` | string | no | empty | Path to the legal notice, relative to the base directory (absolute and `%VAR%` allowed). Empty: no notice |
| `frontMatter` | object of strings | no | empty | Copied to `_meta.frontMatter`. The plugin does not write front matter itself — the shipped templates emit it as a YAML block ([§9.2](#92-template-context)) |
| `output.attributeDenyList` | string[] | no | `password`, `secret`, `token`, `key` | Case-insensitive substrings; matching attribute names are dropped from FetchXML results ([§7.4](#74-serialization-of-fetchxml-results)) |
| `python` | object | no | — | Ignored. Accepted so configs from before Phase 5 still load; the shipped specs no longer carry it, and the schema marks it deprecated |
| `$schema` | string | no | — | Editor hint; the shipped specs use `"./context-exporter.schema.json"` |

### 6.2 Query definition

| Field | Type | Applies to | Meaning |
|---|---|---|---|
| `id` | string | all | Unique within the spec; names the raw file `output.<id>.fetch.json` and log lines |
| `type` | string | all | `fetchxml`, `webapi` or `metadata` |
| `resultKey` | string | all | Unique within the spec; top-level key in `intermediate.json` and top-level template variable |
| `source` | string | `fetchxml` | FetchXML file name, resolved against `config\queries\` (absolute and `%VAR%` allowed) |
| `path` | string | `webapi` | OData resource path after `/api/data/v9.2/`, query options included |
| `select` | string[] | `webapi` | Appended as `$select=a,b` (with `?` or `&` as needed) |
| `metadataTarget` | string | `metadata` | `entities`, `attributes`, `optionsets` or `relationships` |
| `maxRecords` | integer | `fetchxml`, `webapi` | Stop paging once at least this many records are held. The cap is checked per page, so the result can exceed it by up to one page; records are not trimmed |

### 6.3 Validation

`ConfigValidator.Validate` runs when **Run** is clicked (violations go to the log as `[Validation] …` and the run does not start) and again at the start of `ExportJobRunner.Run`. It collects every violation before failing.

| Check | Violation when |
|---|---|
| `spec` | Empty or whitespace |
| `transformation` | Empty, or the file is missing from `config\transformations\` |
| `legal` | Set, and the resolved file does not exist |
| Query `id` | Missing (the query's other checks are skipped), or duplicated (case-insensitive) |
| Query `resultKey` | Missing, or shared with another query (case-insensitive; both ids named) |
| FetchXML `source` | Missing for a `fetchxml` query, or the file is not in `config\queries\` |

Not validated up front — these fail at run time as a per-query error: an unknown `type`, a missing or invalid `path`, an unknown `metadataTarget`. A spec that fails to deserialize is rejected when selected in the picker, with a message box.

---

## 7. Query execution

Queries run in the order listed, on the XrmToolBox worker thread.

### 7.1 FetchXML

- Reads the FetchXML file and pages it server-side: `page`, `count` (5,000) and, after the first page, the `paging-cookie` attribute are set on the root `<fetch>` element. Any `count` or `page` in the file is overwritten.
- Loops while `MoreRecords` is true, or until `maxRecords` is reached. Cancellation is checked before each page.
- Returns SDK `Entity` objects, serialized by `EntityJsonSerializer` ([§7.4](#74-serialization-of-fetchxml-results)).

### 7.2 Web API

- `GET <environment URL>/api/data/v9.2/<path>[$select]` with `Authorization: Bearer`, `Accept: application/json; odata.metadata=minimal`, `OData-MaxVersion: 4.0`, `OData-Version: 4.0`.
- The token is the connection's `CrmServiceClient.CurrentAccessToken`, read once per query. A connection that yields no token (not modern authentication) fails the query with an explanatory message.
- Follows `@odata.nextLink` until exhausted or `maxRecords` is reached; accumulates the `value` arrays into one JSON array. Record-level `@odata.*` annotations are kept as returned.
- A non-success status fails the query with the status code, reason and response body.
- Results are raw JSON: property names keep the Web API's casing (`LogicalName`, `DisplayName.UserLocalizedLabel.Label`, …). The deny list is not applied.

### 7.3 Metadata

Calls the typed metadata SDK and flattens each item into a dictionary with camelCase keys. Labels are reduced to their user-localized string. The deny list is not applied; `maxRecords` is ignored.

| `metadataTarget` | Request | Fields per item |
|---|---|---|
| `entities` | `RetrieveAllEntitiesRequest` (`Entity`) | `logicalName`, `displayName`, `primaryIdAttribute`, `primaryNameAttribute`, `ownershipType`, `isCustomEntity` |
| `attributes` | `RetrieveAllEntitiesRequest` (`Attributes`) | One item per attribute: `entityLogicalName`, `logicalName`, `displayName`, `attributeType`, `isCustomAttribute`, `requiredLevel` |
| `optionsets` | `RetrieveAllOptionSetsRequest` | `name`, `displayName`, `optionSetType`, `options[]` of `value`, `label` |
| `relationships` | `RetrieveAllEntitiesRequest` (`Relationships`) | De-duplicated by schema name. 1:N: `type`, `schemaName`, `referencedEntity`, `referencedAttribute`, `referencingEntity`, `referencingAttribute`. N:N: `type`, `schemaName`, `entity1LogicalName`, `entity2LogicalName` |

All three requests use `RetrieveAsIfPublished = false`. The shipped specs use `webapi` for metadata instead; the metadata runner is there for specs that want the SDK's shapes.

### 7.4 Serialization of FetchXML results

Each `Entity` becomes a dictionary keyed by attribute name — aliased columns keep their alias key, for example `pub.publisherfriendlyname` — plus `_id` (the record id). Attributes whose name contains a deny-list substring are dropped.

| SDK value | JSON value |
|---|---|
| `EntityReference` | `{ id, logicalName, name }` |
| `OptionSetValue` | integer value (labels are not resolved) |
| `OptionSetValueCollection` | array of integers |
| `Money` | decimal |
| `AliasedValue` | its inner value, converted by the same rules |
| `EntityCollection` | array of serialized entities (no deny list applied inside) |
| `BooleanManagedProperty` | its boolean `Value` (for example `iscustomizable`) |
| `DateTime` | UTC, `yyyy-MM-ddTHH:mm:ssZ` |
| `Guid` | string |
| `bool`, `int`, `long`, `double`, `decimal`, `string`, `null` | unchanged |
| anything else | `ToString()` |

### 7.5 Failures, cancellation and progress

| Situation | Behaviour |
|---|---|
| A query throws | Logged as `[Export] ERROR in query '<id>'`; the remaining queries still run. After the loop, any failure raises an `AggregateException` (`n of m queries failed`) and **nothing is rendered**. The raw files of the queries that succeeded stay in the run folder |
| Cancel | The token is checked between queries and between pages. Cancellation logs `[Export] Cancelled.` and ends the run without an error line. Rendering, once started, is not cancellable |
| Progress | After each successful query the progress label shows `Query i / n`; per-page counts and per-query timings go to the log |
| Spec with no queries | No intermediate, render or output is produced; the run folder is still created |

---

## 8. Intermediate JSON

`IntermediateJsonBuilder` writes UTF-8 without BOM, indented, through a `JsonTextWriter`.

- **Per query**, as soon as it completes: `runs\<ts>\output.<id>.fetch.json` holding that query's result.
- **After all queries succeed:** `runs\<ts>\intermediate.json`:

```json
{
  "_meta": {
    "exportedAtUtc": "<ISO-8601 round-trip>",
    "environment": { "url": "<WebApplicationUrl without trailing slash>", "orgName": "<friendly name or organization>" },
    "spec": "<spec>",
    "frontMatter": { "<key>": "<value>" }
  },
  "<resultKey>": [ … ],
  "<resultKey>": [ … ]
}
```

The intermediate is the contract between queries and templates: a template sees exactly these keys. Because it is kept in every run folder, a template can be inspected or debugged against it without reconnecting.

---

## 9. Rendering

### 9.1 Engine

`TemplateRenderer` renders in process with Scriban (7.2.5, source-embedded; [§13.1](#131-dependencies)).

1. Resolve `config\transformations\<transformation>`; a missing file is an error.
2. Parse. Parse errors fail the run with every message as `file(line,column): message`, line and column counted from one.
3. Load `intermediate.json` and convert it recursively: JSON objects to `ScriptObject`, arrays to `ScriptArray`, values to their .NET value.
4. Add `_spec` and register the custom filters ([§9.3](#93-custom-functions)).
5. Render with `StrictVariables = true` (an undefined variable is an error, not an empty string), `LoopLimit = int.MaxValue` and `LimitToString = 0` (no limits — large environments produce long loops and long strings). `include` resolves file names against `config\transformations\`.
6. Write `runs\<ts>\output.md` (UTF-8, no BOM). A render exception fails the run as `Template render error in '<template>'`.

Rendering a large data set can take minutes; the log says so before it starts.

### 9.2 Template context

| Variable | Content |
|---|---|
| `<resultKey>` | One per query, as in `intermediate.json` |
| `_meta` | `exportedAtUtc`, `environment.url`, `environment.orgName`, `spec`, `frontMatter` |
| `_spec` | The spec name |
| Scriban built-ins | `array.*`, `string.*`, `date.*`, … — except `object.from_json` / `object.to_json` (R5) |
| Custom functions | [§9.3](#93-custom-functions) |

Template files use the `.sbn` extension. The shipped templates open with a YAML front-matter block built from `_meta.frontMatter` plus `exportedAt` and `environment`, then an H1 naming the organisation.

### 9.3 Custom functions

Registered by `TemplateFilters.RegisterAll`; callable as functions or with pipe syntax (`{{ x | f arg }}`).

| Function | Arguments | Result |
|---|---|---|
| `schemaname_to_title` | name | `MyCustomField` → `My Custom Field` |
| `display_label` | label object, fallback | `UserLocalizedLabel.Label`, or the fallback |
| `markdown_table` | rows, columns | Markdown pipe table; `_No data._` when empty |
| `csv_list` | items, attribute | Comma-separated values, optionally of one attribute |
| `pluck` | items, key | Unique non-null values of a key |
| `group_by_key` | items, key | Array of `{ key, items }`, in first-seen order |
| `optionset_label` | options, value | Label of the matching option value |
| `attr_type_abbrev` | attribute type | Short type (`Lookup` → `lkp`, `Picklist` → `opt`, …) |
| `req_indicator` | required level | `**R**` (`Required` or `ApplicationRequired` — the value the Web API and SDK report for business-required columns), `r` (`Recommended`), `-` otherwise — including `SystemRequired` |
| `iso_date` | date string | `yyyy-MM-dd`; empty for empty input |
| `component_type_name` | solution component type | Readable name for common types; `Unknown (n)` otherwise |
| `format_forms` | forms | `Name(type,type), …` grouped by form name, with the documented `systemform.type` values (`main` 2, `quickView` 6, `quickCreate` 7, `dialog` 8, `card` 11, …) |
| `format_views` | views | Comma-separated unique view names, skipping special-purpose query types (16, 32, 64, 128, 256, 512, 1024, 4096, 16384); `—` when none |
| `entity_forms` | forms, entity logical name | Forms of one entity |
| `entity_views` | views, entity logical name | Views of one entity |
| `plugin_stage` | stage | `PreVal` (10), `Pre` (20), `Post` (40, 45) |
| `plugin_mode` | mode | `Sync` (0), `Async` (1) |
| `flow_trigger` | flow name | `HTTP`, `Sched`, `Child`, `Manual` or `Auto`, guessed from the name — wrong for 68 of 471 flows in a reviewed environment; kept for existing templates |
| `parse_json` | JSON text | Objects and arrays, converted as the intermediate is ([§9.1](#91-engine)); `null` for empty or invalid JSON. Newtonsoft.Json, provided by XrmToolBox (R4) |
| `flow_trigger_info` | flow definition (`clientdata`) | The trigger read from the definition: Dataverse change type, table, scope, columns and filter (codes as in `callbackregistration`); Dataverse action performed; schedule; HTTP request; manual or child flow; Power Apps; Power Pages; Copilot; selected row; business process flow step; Customer Insights – Journeys; otherwise `connector: operation`. `—` without a definition |
| `classic_trigger` | workflow | `Create/Update/Delete` from the trigger flags; `Manual` when none is set |
| `envvar_type` | type | `String`, `Number`, `Boolean`, `JSON`, `DataSource`, `Secret` |
| `api_param_type` | type | Short custom API parameter type (`bool`, `str`, `EntityReference`, …) |
| `priv_depth` | depth | `None`, `User`, `Business Unit`, `Parent: Child BUs`, `Organization` |

---

## 10. Output and legal notice

After a successful render:

1. `runs\<ts>\output.md` is copied to `output\<spec>.context.md`, overwriting the previous file.
2. If `legal` is set and the file exists, its content followed by a blank line (`\n\n`) is prepended to `output\<spec>.context.md`. A missing legal file at this point is logged as a warning and skipped — validation normally stops the run first.

The delivered file is therefore: legal notice → template output (front matter first, in the shipped templates). The run folder's `output.md` never contains the notice.

The shipped `LEGAL.md` is a GitHub-style `[!WARNING]` blockquote stating that the content is confidential, the property of `[ORGANISATION NAME]`, not to be distributed outside it, and not to be used to train, fine-tune or evaluate AI models. All shipped specs set `"legal": "LEGAL.md"`.

---

## 11. User experience

### 11.1 Main panel

Top to bottom: base directory (read-only path and **Browse…**), spec drop-down with a refresh button, **Run Export** and **Run All Specs**, progress (status label, spinner, **Cancel**) above a read-only log, and the output panel. The output panel is hidden until a run succeeds.

### 11.2 Flow

| Event | Behaviour |
|---|---|
| First load ever | The welcome dialog shows the Quick Start (embedded HTML in a `WebBrowser` control). `WelcomeShown` is set before it opens, so it shows once per user and never again, upgrades included |
| Load | Restores the saved base directory if it still exists, loads its specs, runs first-run setup and the upgrade check ([§5](#5-base-directory)) |
| **Browse…** | Folder dialog (no new-folder button). On OK: saves the setting, reloads specs, logs the change, runs first-run setup and the upgrade check |
| Connection changed | Logs the URL and reloads the spec list (clearing the selection) |
| Spec list loaded | **Run All Specs** is enabled when the list has at least one spec |
| Spec selected | Loads the file; **Run Export** is enabled when a spec loaded. A load error shows a message box and clears the selection |
| **Run Export** | With no connection, XrmToolBox's connect prompt runs first, then the export. Validates ([§6.3](#63-validation)); clears the log; disables **Run**; hides the output panel; runs through `WorkAsync` |
| **Run All Specs** | Asks for confirmation first: lists every spec in the drop-down and warns that the batch can take a long time — several minutes per spec on a large environment — that a failed spec does not stop the others, and where the context files go. **No** is the default. With no connection, XrmToolBox's connect prompt runs next. Clears the log, disables both run buttons, hides the output panel and runs the specs one after another, in drop-down order, through one `WorkAsync`. Each spec is loaded, validated and run exactly as **Run Export** would, into its own run folder and output file (R9). A spec that fails to load, validate or run is logged and the batch moves on; **Cancel** stops the batch, and the specs already finished keep their output. The progress label reads `Spec i / n (<spec>) · Query x / y` |
| Batch ends | Re-enables both run buttons. The log ends with a `[Batch] Finished` line naming the specs that failed. When at least one spec succeeded, the output panel shows the path of the `output\` folder and only **Open Folder**, with "`n` specs exported" (or "`n` of `m` specs exported; `k` failed (see the log)") and the upload line |
| Run ends | Re-enables **Run**. On success the output panel shows the path of `output\<spec>.context.md`, **Open**, **Reveal** (Explorer, selected), **Copy path**, and the line "Upload `<file>` to claude.ai, ChatGPT, or your AI assistant as a grounding file." On error the base exception's message is logged |
| Plugin hidden | Saves the base directory setting |

### 11.3 Log

Every step writes one line with a bracketed source: `[Setup]`, `[Validation]`, `[Batch]`, `[Export]`, `[fetchxml]`, `[webapi]`, `[metadata]`, `[Render]`, `[Legal]`. Lines are marshalled to the UI thread.

---

## 12. Reference configuration

Every file under `SampleConfig\` is an embedded resource. `FirstRunHelper` holds the deployment list explicitly — adding a file to `SampleConfig\` does not deploy it until the list names it.

### 12.1 Specs

| Spec | Template | Queries (`resultKey`) |
|---|---|---|
| `EntityDictionary` | `entity-dictionary.sbn` | Web API `EntityDefinitions` with every column except companions (`entityAttributes`); FetchXML `project-tables` (`projectTables`) |
| `FormsAndViews` | `forms-and-views.sbn` | Web API `EntityDefinitions` names (`entities`); FetchXML `project-forms` (`forms`), `project-views` (`views`) |
| `Optionsets` | `optionsets.sbn` | Web API `GlobalOptionSetDefinitions` (`globalOptionSets`), filtered to unmanaged in the template |
| `PowerAutomateFlows` | `power-automate-flows.sbn` | FetchXML `cloud-flows` (`flows`), including the flow definitions |
| `PowerPages` | `power-pages.sbn` | FetchXML `power-pages-sites` (`ppSites`), `power-pages-components` (`ppComponents`, with `content`), `power-pages-component-names` (`ppComponentNames`, names only: web templates, web files, sensitive site settings and inactive components) |
| `SecurityModel` | `security-model.sbn` | FetchXML `security-roles` (`securityRoles`) — the project's roles, with a table × action matrix of privilege depths and the other privileges listed |
| `SolutionInventory` | `solution-inventory.sbn` | FetchXML `solutions-detail` (`solutions`) — visible solutions with publisher |
| `Relationships` | `relationships.sbn` | Web API `RelationshipDefinitions` cast to one-to-many (`oneToMany`: columns, navigation properties, hierarchy, cascade, related menu) and to many-to-many (`manyToMany`: intersect table, navigation properties, related menus); `EntityDefinitions` names (`tables`) |
| `SolutionsReference` | `solutions-reference.sbn` | Web API `EntityDefinitions` with unmanaged custom columns (`entities`), `RelationshipDefinitions` one-to-many (`oneToMany`) and many-to-many (`manyToMany`) with names and columns only, and `GlobalOptionSetDefinitions`; FetchXML `solutions-detail`, `project-tables`, `project-forms`, `project-views`, `plugin-assemblies`, `sdk-steps`, `custom-apis`, `custom-api-responses`, `workflows` (with the flow definitions, for the trigger), `business-process-flows`, `bpf-stages`, `app-modules`, `env-vars`, `security-role-names` |

`SolutionsReference` is the complete customization reference: the project's solutions, custom tables, customized managed tables, virtual tables, option sets, plug-ins and steps (with their state), custom APIs, classic workflows, cloud flows, business rules (drafts marked), business process flows (each stage once), security roles, app modules and environment variables. Cloud flows show the trigger read from their definition. Managed tables that are in a project solution but have no project columns, forms or views are listed by name in one line.

### 12.2 Customization scope

The reference configuration describes what the project built or customized, not everything installed. It assumes a **development environment**, where the project's work is unmanaged; against an environment that holds the project's solutions managed, most sections are empty. A *project solution* is a visible, unmanaged solution other than `Default` and `Active` (`Default` holds every table and is excluded).

| Component | Rule | Where it is applied |
|---|---|---|
| Table | Unmanaged; or has unmanaged custom columns; or has a form or view in scope; or is a component of a project solution. Intersect tables excluded | Templates, from `IsManaged`, the expanded columns, `project-tables`, `project-forms` and `project-views` |
| Virtual table | `TableType` is `Virtual`, or a non-empty `DataProviderId` on a table that is not elastic | Templates |
| Column | `SolutionsReference`: unmanaged custom columns. `EntityDictionary`: every column of an included table. Both drop columns with an `AttributeOf` (lookup and choice name companions) on the server, and currency `_base` columns in the template | Web API `$filter` inside `$expand=Attributes`; templates |
| Form | Main, quick view, quick create or card; unmanaged or a component of a project solution | `project-forms` (a `link-type="any"` link inside an `or` filter) |
| View | Public, Advanced Find, associated, Quick Find or lookup; unmanaged or a component of a project solution | `project-views` |
| App module | Unmanaged or a component of a project solution | `app-modules` |
| Global option set | Unmanaged | Templates — `GlobalOptionSetDefinitions` rejects `$filter` (HTTP 405), so all global option sets are fetched |
| Workflow, cloud flow, custom API, environment variable | Unmanaged. Cloud flows named `CXP_` (generated by Customer Insights – Journeys) excluded | Queries |
| Plug-in step | Registered on an unmanaged plug-in assembly; state included | `sdk-steps` |
| Relationship | Custom (`IsCustomRelationship`) and unmanaged; system relationships every table gets and relationships of managed solutions are left out, and so is a managed relationship whose behavior the project changed | Web API `RelationshipDefinitions` filter in `Relationships` and `SolutionsReference` |
| Power Pages site and component | Active sites on the enhanced data model, and their active components; inactive components by name only, so that a reference to one is shown as inactive. Web templates, web files and site settings whose names contain *secret*, *password*, *key*, *token*, *credential*, *connectionstring* or *certificate* are read by name only, so their content is never fetched | `power-pages-*` queries |
| Security role | Unmanaged, root business unit (`parentroleid` null), not based on a role template (`roletemplateid` null) | `security-roles`; `security-role-names` (names only, for `SolutionsReference`) |
| Solution | Visible, other than `Default` and `Active`. `SolutionsReference` lists the unmanaged ones | `solutions-detail`; template |

Not covered: a standard column the project changed without creating it, such as a new label or required level. The Web API cannot filter `$expand=Attributes` to a list of IDs, so listing those would mean fetching every column of every included table.

The rules were checked against a development environment on 2026-11-09 through the Agent Companion MCP: every FetchXML query above ran as shipped. The Web API `$filter` expressions could not be run through it; the first export showed that `GlobalOptionSetDefinitions` does not accept `$filter`, so that filter moved to the templates.

### 12.3 Queries and other files

| Kind | Files |
|---|---|
| FetchXML (`config\queries\`) | `app-modules`, `bpf-stages`, `business-process-flows`, `cloud-flows`, `custom-api-responses`, `custom-apis`, `env-vars`, `plugin-assemblies`, `power-pages-component-names`, `power-pages-components`, `power-pages-sites`, `project-forms`, `project-tables`, `project-views`, `sdk-steps`, `security-role-names`, `security-roles`, `solutions-detail`, `workflows` (each `.fetch.xml`) |
| Templates (`config\transformations\`) | The nine `.sbn` files above |
| Schema (`config\`) | `context-exporter.schema.json` — draft-07; `spec`, `transformation`, `queries` required; no additional properties; per-query `id`, `type`, `resultKey` required |
| Legal (`LEGAL.md`) | Deployed once, with the organisation name substituted ([§5.2](#52-first-run-setup)) |

---

## 13. Packaging and distribution

### 13.1 Dependencies

| Package | Use | Ships a DLL? |
|---|---|---|
| `XrmToolBoxPackage` 1.2025.10.74, `MscrmTools.Xrm.Connection` | Plugin contracts, connection, SDK | No — provided by XrmToolBox |
| `Newtonsoft.Json` 13.0.3 | Config, intermediate JSON, Web API responses | No — provided by XrmToolBox |
| `Scriban` 7.2.5 with `PackageScribanIncludeSource=true`, `IncludeAssets="Build"` | Template engine compiled into the plugin. Scriban's build targets define `SCRIBAN_NO_SYSTEM_TEXT_JSON` (R5) | No |
| `Microsoft.CSharp`, `System.Threading.Tasks.Extensions`, `PolySharp` | Required by Scriban's source on .NET Framework 4.8 | No — framework-resolved or source-only |
| `Microsoft.VisualBasic` (framework reference) | `InputBox` for the organisation name | No |
| `StyleCop.Analyzers`, `Microsoft.SourceLink.GitHub` | Build-time only | No |

### 13.2 Package

- Id `Lspiguel.Xrm.D365ContextExporter`, tags `XrmToolBox Documentation AI Markdown D365 Dataverse Grounding`, MIT licence, dependency on `XrmToolBox` ≥ 1.2025.10.74.
- Contents: `LICENSE` and `readme.md` (the tooling README) at the root; `D365ContextExporter.dll` and `.pdb` in `lib\net472\Plugins`. Nothing in a `Plugins` subfolder (R4).
- Release notes in the nuspec describe the release for Tool Library users — new, changed, fixed, upgrading — and keep, under *Upgrading*, the one-time clean-up for people coming from a version that shipped `System.Text.Json.dll` in a subfolder (1.2026.7.1 or earlier).
- Version: `1.YYYY.M.N`, set by hand in the csproj and nuspec (R10); CI does not stamp it.

### 13.3 CI

[build-and-pack-d365-context-exporter.yml](/.github/workflows/build-and-pack-d365-context-exporter.yml), on push and pull request to `main` under `tooling/D365ContextExporter/**`, on `windows-latest` with the .NET 8 SDK:

1. **build** — restore, build Release, test, `nuget pack` the nuspec, upload the `.nupkg` as an artifact.
2. **publish** — push to `main` only: exchanges the GitHub OIDC token for a nuget.org key (trusted publishing; no stored secret) and pushes the package. The Tool Library lists it from nuget.org.

### 13.4 Local development

- `D365ContextExporter.sln` holds the plugin and the tests. `D365ContextExporter.sln.ignore` is a developer solution that adds the XrmToolBox source projects (a sibling checkout) for step-through debugging, with XrmToolBox as start-up project.
- `Directory.Build.targets.example`, copied to a git-ignored `Directory.Build.targets`, copies the Debug DLL and PDB into a local XrmToolBox `Plugins` folder after build.

---

## 14. Testing

NUnit 4 with Moq and Moq.Contrib.HttpClient, `net48`, mirroring the source folders.

| Fixture | Covers |
|---|---|
| `HelpersTests/PathResolverTests` | Absolute, relative and `%VAR%` resolution; spec discovery; spec name from path |
| `HelpersTests/ConfigValidatorTests` | Each violation in [§6.3](#63-validation), and all reported together |
| `HelpersTests/EntityJsonSerializerTests` | Every SDK type in [§7.4](#74-serialization-of-fetchxml-results), deny list, nulls, nested aliased values |
| `HelpersTests/FirstRunHelperTests` | `IsConfigured`, deploy without overwrite, upgrade overwrite sparing `LEGAL.md` and files of other names, the full file list, organisation-name substitution, the upgrade backup (edited files only, never overwriting an earlier backup, hidden from spec discovery) and `version.txt`. `CheckVersion` through an internal overload that takes the current version and the confirmation, so missing, same, different-accepted and different-declined are tested without a dialog or the build's version |
| `HelpersTests/TemplateFiltersTests` | Every function in [§9.3](#93-custom-functions), rendered through a template — `flow_trigger_info` once per trigger kind, from synthetic definitions — with the edge cases: unknown component type, empty `markdown_table`, empty `iso_date`, `req_indicator` of `SystemRequired`, the `group_by_key` shape, `pluck` uniqueness, the `display_label` fallback |
| `ModelsTests/ExportJobTests` | Loading and defaults |
| `OrchestrationTests/IntermediateJsonBuilderTests` | `_meta`, raw file naming, result keys |
| `OrchestrationTests/TemplateRendererTests` | `_spec` and `_meta`, nested objects and arrays, `include` from `transformations\`, parse errors with line and column, an undefined variable failing, a missing template, UTF-8 without BOM |
| `OrchestrationTests/PowerPagesTemplateTests` | The PowerPages spec against one synthetic enhanced-data-model site: URL paths from the parent chain, hosted forms and lists, access rules, custom-code flags, roles and what each can do, table permission scope and parent, multistep step order, Web API exposure, a sensitive setting masked and a name-only one listed, templates and navigation; forms and lists embedded with Liquid in page copy, a reference to an inactive component, multistep steps in flow order through a condition and a redirect, access rules by page, duplicate role grants listed once |
| `OrchestrationTests/ShippedTemplatesTests` | Every shipped spec rendered through its shipped template, deployed from the embedded configuration, against one synthetic intermediate: the customization scope of [§12.2](#122-customization-scope) (which tables, forms and views appear), column types from `AttributeTypeName`, form and view type labels, step and process state, cloud flow triggers from the definition, the one-line list of solution-only tables, BPF stages listed once, the publisher column, option order, the security role privilege matrix; each table's relationships in SolutionsReference; in Relationships, the behavior and grouped cascade of a referential, a parental hierarchical and a restrict-delete relationship, navigation properties, related menus (collection name, label, hidden), a many-to-many relationship and the lookup index with a customer lookup |
| `OrchestrationTests/ExportJobRunnerTests` | Through an internal constructor that takes the three query runners: a failed query stopping the render while the remaining queries run, cancellation between queries, progress, the copy to `output\`, the legal notice in the delivered file only, a spec with no queries, validation before the run folder is created, a run folder per run when runs start in the same second |
| `OrchestrationTests/ExportBatchRunnerTests` | With a stand-in for the single-spec run: every spec in order with progress, a failing or unreadable spec not stopping the others, the summary line, cancellation between specs and inside one |
| `QueriesTests/*QueryRunnerTests` | Paging, `maxRecords`, empty results, missing files, HTTP errors, metadata targets |
| `QueriesTests/*RealDataTests` | Realistic response shapes, synthetic (R2) |

`OrchestrationTests/TemplateHarness` is the shared helper for the template tests: a temporary base directory with one run folder. Filters are tested by rendering rather than by calling `TemplateFilters` directly, because Scriban is compiled into the plugin and the test project also references the Scriban package.

Not covered by unit tests: the shipped queries against a real environment, the public `ExportJobRunner` constructor (it needs a live connection for the token) and the WinForms controls, including the upgrade dialog. The shipped templates are covered only against synthetic data. So queries and templates are still verified by hand: deploy into a clean folder, run each shipped spec against a development environment, check the Markdown and the legal notice.

---

## 15. Future enhancements

### 15.1 Planned fixes

Fixes decided for open items in [notes.md](notes.md#open-items). F1–F4 change documentation, the schema and tests, not how a run behaves; F5 changes the upgrade ([§5.3](#53-upgrade)) and R8. A fix that changes an embedded file — the welcome HTML, `SampleConfig\` — reaches users with the next version (R10), and a `SampleConfig\` change reaches a base directory only if the person accepts the upgrade.

**Status:** F1–F5 are built, the sections above describe them, and they were released in 1.2026.11.1. Writing the F4 tests also found that parse errors reported zero-based lines and columns; [§9.1](#91-engine) now counts from one.

| # | Open item | Fix |
|---|---|---|
| F1 | O1 — `Directory.Build.targets.example` still copies `Scriban.dll`, `System.Text.Json.dll` and `System.Text.Encodings.Web.dll` into a private-lib subfolder | Copy only the DLL and PDB into the `Plugins` root. Remove the subfolder property, the `PrivateDeps` item group and the layout comment that describes them, and say why: R4. A developer-only file, so no version bump |
| F2 | O2 — the README lists six sample specs while seven ship, and swaps the SolutionsReference and SolutionInventory descriptions; the folder tree is incomplete; the welcome dialog also says six | In the README's sample spec table, add `PowerAutomateFlows` (cloud flows with their definitions), describe `SolutionsReference` as the complete customization reference ([§12.1](#121-specs)) and `SolutionInventory` as solutions with publisher, version, managed state and install date. Add `power-automate-flows.sbn` and `context-exporter.schema.json` to the folder tree. In the README Quickstart and `WelcomeQuickStart.html`, drop the count ("sample specs are provided out of the box") so it cannot drift again |
| F3 | O3 — the schema documents `python` as live and names a different repository in `$id`; every shipped spec still carries `"python"` | Remove `"python"` from the seven shipped specs. In the schema, keep `python` and its two properties so existing configs still validate, but describe it as ignored since rendering moved to Scriban, with a `deprecationMessage` for VS Code. Set `$id` to the schema's path in this repository. In the same change, correct the stale doc comments on `ExportJob.Transformation` and `PythonSettings` (O5) |
| F4 | O6 — no unit tests for `TemplateRenderer`, `TemplateFilters` or `ExportJobRunner`; `CheckVersion` untested | **`TemplateFiltersTests`:** every function in [§9.3](#93-custom-functions), with the edge cases Phase 5 listed — unknown component type, empty `markdown_table`, empty `iso_date`, `req_indicator` of `SystemRequired`, the `group_by_key` shape, `pluck` uniqueness, the `display_label` fallback. **`TemplateRendererTests`** on a temporary base directory: `_spec` rendered, parse errors with line and column, an undefined variable fails, a missing template fails, nested objects and arrays, `include` from `transformations\`. **`ExportJobRunnerTests`:** add an internal constructor that takes the three query runners (today they are created inside `Run`), then cover a failed query stopping the render, cancellation, the copy to `output\`, the legal notice in the delivered file only, and a spec with no queries. **`CheckVersion`:** add an internal overload taking the current version and the confirmation as parameters, so missing, same, different-accepted and different-declined are tested without a dialog and without depending on the build's version — the reason the earlier tests were removed |
| F5 | O13 — the upgrade dialog says custom files are not modified, but **Yes** overwrites edits to shipped files | Before overwriting a shipped file whose content differs from the new version, copy it to `config\backup\<previous version>\` under the same relative path and log `[Setup] Backed up`. Backed-up specs sit below `config\`'s top level, so the spec picker ignores them. Reword the dialog: shipped queries, templates and sample specs are replaced, edited ones are backed up first, and `LEGAL.md` and files of other names are not touched. Update the README's *Plugin upgrades* section, §5.3 and R8 (the backup folder becomes the one place an upgrade writes besides shipped files) |

### 15.2 Open GitHub issues

Open issues labelled [D365 Context Exporter](https://github.com/lspiguel/ai-tooling/issues?q=is%3Aissue+is%3Aopen+label%3A%22D365+Context+Exporter%22), retrieved 2026-11-09. The issue tracker is the live list; this table is a snapshot, and none of these is scheduled.

| Issue | Opened | Request | Bearing on this spec |
|---|---|---|---|
| [#21](https://github.com/lspiguel/ai-tooling/issues/21) Sample configurations for relationships | 2026-06-21 | A relationship list per table in `SolutionsReference`, and a new spec for all relationships with extended information | **Released in 1.2026.11.1.** The `Relationships` spec and a **Relationships** line per table in `SolutionsReference` ([§12.1](#121-specs)), configuration only (R7). Both read the Web API's `RelationshipDefinitions`, not the `metadata` runner, whose `relationships` target has no cascade, navigation or menu detail and cannot be filtered to the project's relationships |
| [#22](https://github.com/lspiguel/ai-tooling/issues/22) Specs for Power Pages | 2026-06-21 | Page list, permissions, site settings | **Released in 1.2026.11.1.** The `PowerPages` spec ([§12.1](#121-specs)), for sites on the enhanced data model. Needed one new function, `parse_json`, because the enhanced model keeps each component's settings as JSON in `powerpagecomponent.content`. Sites on the standard data model (`adx_` tables) are not covered |
| [#23](https://github.com/lspiguel/ai-tooling/issues/23) Choose a solution to export entities, forms, views and option sets from | 2026-06-26 | Limit an export to the components of chosen solutions; today's output covers the whole environment and is large | Maintainer's reply (2026-07-22): filter the solutions query, then filter entities by solution in the `solutions-reference.sbn` entities loop. The hard part is keeping standard tables that are customized (such as account) while dropping others. Configuration only unless a solution picker is wanted in the UI ([§11](#11-user-experience)) |
| [#31](https://github.com/lspiguel/ai-tooling/issues/31) Output to PDF or other formats | 2026-07-29 | Microsoft 365 Copilot Notebooks accept only Office and PDF files | Changes [§10](#10-output-and-legal-notice) and the out-of-scope line on non-Markdown output ([§3](#3-scope)). Any converter must be source-embedded or framework-provided, with no loose DLLs (R4) — the issue says so too |
| [#33](https://github.com/lspiguel/ai-tooling/issues/33) "Run all" specs button | 2026-08-07 | Run every spec in the base directory after a confirmation that warns it can take a long time; the result panel then offers only to open the output folder | **Released in 1.2026.11.1.** **Run All Specs** ([§11.2](#112-flow)). A failed spec is logged and the others still run; **Cancel** stops the batch. Run folders gained a `-n` suffix for runs that start in the same second, so each spec keeps its own (R9) |
| [#39](https://github.com/lspiguel/ai-tooling/issues/39) Higher-resolution icons in the Tool Library | 2026-11-02 | The Tool Library shows 64×64 px bitmaps that look poor | **Released in 1.2026.11.1.** The plugin embeds a 32 px small and an 80 px big icon (16 and 32 px before), and the package icon is 150 px (128 px before), in `D365ContextExporterPlugin.cs`, the nuspec `iconUrl` and the csproj `PackageIcon` ([§13.2](#132-package)). The `iconUrl` points to `main`, so the Tool Library shows the new icon once this is merged. Done the same way for the Agent Companion MCP |

### 15.3 Ideas

Carried from the original plan and the repository's [activity matrix](/workflows/ai-augmented-d365ce-activity-matrix.md); none is scheduled.

| Idea | Note |
|---|---|
| Headless or CI refresh | Regenerate packs on a schedule and commit them — the matrix's "Context Exporter CI refresh"; would need a host other than XrmToolBox |
| Incremental "update grounding" | Regenerate only what changed in Dataverse since the last run |
| Shared template catalogue | Templates per domain (Field Service, Sales, Customer Service, Power Pages) |
| Section anchors | Stable markers that let an assistant cite a section back |
| Compression pass | Shrink very large dictionaries before upload |
| Several outputs per spec | Split one run into several files for assistants with upload limits |

---

## 16. References

[^1]: XrmToolBox — *Deploy your tool in Tool Library*, <https://www.xrmtoolbox.com/documentation/for-developers/deploy-your-plugin-in-plugins-store/>; XrmToolBox wiki — *Distribute your plugins through XrmToolBox and NuGet*, <https://github.com/MscrmTools/XrmToolBox/wiki/Distribute-your-plugins-through-XrmToolBox-and-Nuget>; Jonas Rapp — *Use Azure DevOps to publish XrmToolBox tools*, <https://jonasr.app/xtbvsts/>. Cited in the original plan (2026-04) and not rechecked here. Support: tag rules (`XrmToolBox` plus other tags), the `Plugins` folder layout, the dependency on the `XrmToolBox` package, and Tool Library pick-up from nuget.org ([§13.2](#132-package)).

[^2]: Scriban — <https://github.com/scriban/scriban>. Source embedding and the `SCRIBAN_NO_SYSTEM_TEXT_JSON` constant were confirmed in the package's `build/Scriban.targets` (7.2.5, checked 2026-11-09) ([§13.1](#131-dependencies)).
