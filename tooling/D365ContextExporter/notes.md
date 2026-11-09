# D365 CE Context Exporter — development notes

> How the plugin specified in [SPEC.md](SPEC.md) was built: the original plan, six phases, the releases after them, the decisions taken on the way, and the items still open. The spec is the source of truth for behaviour; these notes are the history. **No client data** (SPEC R2).

These notes merge the original plan and the six phase plans (`d365-context-exporter-plan.md`, `d365-context-exporter-phase1.md` … `-phase6.md`), removed on 2026-11-09. The originals remain in git history — for example `git show 15597aa:tooling/D365ContextExporter/d365-context-exporter-phase3.md`.

---

## Status

| Phase | Scope | Merged | Version |
|---|---|---|---|
| [Plan](#original-plan) | Architecture, folder structure, Python/Jinja2 post-processor | 2026-04 (in #11) | — |
| [1](#phase-1--skeleton-and-wiring) | Skeleton: solution, models, pickers, stub runner | 2026-05-05 (#11) | 1.0.0 |
| [2](#phase-2--query-execution) | Query runners, serializer, intermediate JSON | 2026-05-05 (#11, via #5) | 1.0.0 |
| [3](#phase-3--python-post-processor) | Python/Jinja2 rendering, output panel, cancel and progress | 2026-05-05 (#11, via #9) | 1.0.0 |
| [4](#phase-4--polish-packaging-distribution) | First-run setup, upgrade check, `LEGAL.md`, validation, packaging, CI | 2026-05-05 (#11, via #10) | 1.0.0 → 0.2026.0.5 |
| [5](#phase-5--net-native-rendering-with-scriban) | Python replaced by Scriban; IronPython attempt abandoned | 2026-05-28 (#17) | 0.2026.0.7 |
| — | Business process flows, cloud flows spec, template fixes | 2026-06-12 (#18) | 0.2026.0.7 |
| [6](#phase-6--scriban-source-embedding-and-welcome-dialog) | Scriban source-embedded; welcome dialog | 2026-06-12 (#19) | 1.2026.6.1 |
| [Releases](#post-phase-6--releases) | README and infographic; July release candidate; System.Text.Json removed | 2026-06-18 → 2026-07-26 (#20, #25, #28) | 1.2026.7.1 → 1.2026.7.2 |
| [Planned fixes](#post-phase-6--releases) | F1–F5 from [SPEC §15.1](SPEC.md#151-planned-fixes): developer targets, README drift, `python` deprecation, unit tests, upgrade backup | Not yet merged | Unreleased — needs a version bump |

Released in the XrmToolBox Tool Library. Remaining work is in [Open items](#open-items).

---

## Original plan

Written in April 2026 as *D365 CE Context Exporter — XrmToolBox Plugin with Python/Jinja2 Post-Processor*, and annotated as later phases changed it.

**Intent.** Produce Markdown grounding files for non-agentic assistants (claude.ai, ChatGPT, Gemini, Microsoft 365 Copilot chat). Query execution in C#, where the authenticated `IOrganizationService` is; transformation in Python and Jinja2 templates on disk, so practitioners can add output formats without a recompile; a JSON file on disk as the bridge, so every run is inspectable and the post-processing re-runnable. Read-only; not a generic ETL tool; no prompt execution or upload.

**Python hosting options considered.**

| Option | Verdict |
|---|---|
| IronPython | Rejected at first — lags CPython; Jinja2 runs best on CPython. Tried again in Phase 5 and abandoned |
| Python.NET | Rejected — binds the plugin to one Python binary; a `pip` mismatch crashes XrmToolBox |
| `python.exe` subprocess | **Chosen for Phases 1–4** — decoupled runtimes, any user script can be swapped in |

All Python was removed in Phase 5 in favour of Scriban.

**Folder structure.** One user-chosen base directory with `config\` (specs, shared `queries\` and `transformations\`), `runs\<timestamp>\` (immutable, never deleted) and `output\<Spec>.context.md` (the upload target, overwritten each run). This survived unchanged into the current spec.

**Initial transformation catalogue.** Five templates: entity dictionary, security model, option sets, forms and views, solution inventory. The shipped set grew to seven ([SPEC §12](SPEC.md#12-reference-configuration)).

**Planned components that were never built:** `ProcessRunner` as a reusable helper outside Python use, the `Utils\` copies of shared utility files, a separate `OutputDefinition` model, an Azure DevOps pipeline (GitHub Actions was used instead), `Microsoft.PowerPlatform.Dataverse.Client` (the XrmToolBox connection's `CrmServiceClient` supplies the token), Python `pytest` snapshot tests, and an `IProcessFactory` abstraction for process tests.

**Risks recorded.** Python missing on the user's machine (resolved by Phase 5); runaway template loops (Scriban's `LoopLimit` — later set to unlimited, see Phase 5); very large intermediates (streamed with `JsonTextWriter`, capped by `maxRecords`); upload size limits (templates split by topic).

---

## Phase 1 — skeleton and wiring

**Goal.** The plugin installs, loads, connects, lists specs and prints the loaded spec to the log.

**Built.**

- SDK-style `net48` WinForms project with `Nullable`, StyleCop; NUnit + Moq test project.
- MEF plugin class and `ContextExporterPluginControl` (`PluginControlBase`), laid out in a `TableLayoutPanel`: directory picker, spec picker, **Run Export**, log.
- Models `ExportJob` (with `Load(path)`), `QueryDefinition`, `PythonSettings` (`interpreter`, `venv`), `OutputSettings` (deny list defaulting to `password`, `secret`, `token`, `key`).
- `PathResolver`: `%VAR%` expansion and relative resolution, spec discovery under `config\`, spec name from file name — with the seven unit tests the plan listed.
- `BaseDirectoryPickerControl` (persisted in user settings), `SpecPickerControl` (combo + refresh), `ExportProgressControl` (log only), stub `ExportJobRunner` that logs the spec.
- A first nuspec.

**Debugging setup.** The XrmToolBox source projects (`XrmToolBox`, `.Extensibility`, `.AutoUpdater`, `.ToolLibrary`, from a sibling checkout) were added to a developer solution with XrmToolBox as start-up project and a post-build `xcopy` of the DLL and PDB into its `Plugins` folder. XrmToolBox's own post-build step used `$(SolutionDir)` and broke when hosted in another solution; it was changed locally to `$(MSBuildProjectDirectory)\..\Commands\*.Command.xml`. That solution survives as `D365ContextExporter.sln.ignore`; the `xcopy` was replaced in Phase 4.

**Changed during the phase.** The sample `config\` folder moved from `sample-config\`; the `Business` namespace was renamed `Orchestration`.

---

## Phase 2 — query execution

**Goal.** Run every query in a spec, write one raw file per query and `intermediate.json` into `runs\<timestamp>\`.

**Built.**

| Component | Notes |
|---|---|
| `FetchXmlQueryRunner` | Injects `page` / `count` / `paging-cookie`; loops on `MoreRecords`; `maxRecords` cap |
| `WebApiQueryRunner` | `HttpClient` against `/api/data/v9.2/`, bearer token from the connection, `@odata.nextLink` paging, `$select` from `select` |
| `MetadataQueryRunner` | New `metadataTarget` field: `entities`, `attributes`, `optionsets`, `relationships`; typed metadata flattened to dictionaries |
| `EntityJsonSerializer` | SDK types to plain values, `_id` added, deny list applied |
| `IntermediateJsonBuilder` | Per-query file as each query finishes; `_meta` + result keys streamed with `JsonTextWriter` |
| `ExportJobRunner` | Dispatch by `type`; per-query timings; a failed query is logged and the rest continue, then an `AggregateException` |
| `ExportJob.Output` | Deny-list settings wired into the job |

Unit tests for all five components, with `IOrganizationService` mocked by Moq and `HttpClient` by Moq.Contrib.HttpClient.

**Plan versus built.**

- The plan said both that OData annotations are kept and that they are stripped. They are kept (`odata.metadata=minimal`).
- `_meta.project` became `_meta.spec` when "project" was renamed "spec" across the code (during Phase 3).
- The sample config used `webapi` for entity and option-set metadata; `MetadataQueryRunner` was built because the design called for it, and no shipped spec uses it.
- `ProcessRunner` was added as a stub for Phase 3.
- A second sample config was added holding "the entries that don't fail".

---

## Phase 3 — Python post-processor

**Goal.** End-to-end: render `output.md` from the intermediate and copy it to `output\<Spec>.context.md`; show the result in the UI.

**Built.**

- `PythonInvoker` — extracted the embedded `transform.py`, `filters.py` and `requirements.txt` to `%LOCALAPPDATA%\D365ContextExporter\python\` on every run (overwriting), then ran `python transform.py --input --template --out --spec` through `ProcessRunner` with a 5-minute timeout, streaming stdout and stderr to the log; non-zero exit raised `PythonInvocationException` (exit code + stderr tail).
- `ProcessRunner` — `Process` wrapper with redirected streams, timeout split between waiting and draining, kill on timeout or cancel.
- `PythonBootstrapHelper` — before each run, `python -m pip show <requirements>` with a 10-second timeout. Interpreter resolution was simplified to: `"auto"` means `python` on `PATH`, anything else is used as given. **No virtual environment was created**, despite the plan's first-run venv bootstrap.
- `transform.py` — Jinja2 `Environment` with `StrictUndefined` and a `FileSystemLoader` on the template's folder; the whole intermediate as context plus `_spec`; wrote `output.md`.
- `filters.py` — 19 filters: `component_type_name`, `schemaname_to_title`, `markdown_table`, `csv_list`, `optionset_label`, `iso_date`, `attr_type_abbrev`, `req_indicator`, `format_forms`, `format_views`, `plugin_stage`, `plugin_mode`, `flow_trigger`, `classic_trigger`, `envvar_type`, `api_param_type`, `pluck`, `group_by_key`, `display_label`.
- **Token count** — `tiktoken` (`o200k_base`), falling back to characters ÷ 4, written to `token_count.txt`; `ExportJobRunner` prepended `> Token count (gpt-4o): N` to the output. Because `PythonInvoker` also copied the output, the file was copied twice — once before and once after the header.
- `OutputPreviewControl` — paths, **Open**, **Reveal**, **Copy path**, upload banner. `ExportProgressControl` gained **Cancel**, a `Query i / n` label and thread-safe logging.
- JSON schema (draft-07) with conditional required fields.
- The complete `SolutionsReference` spec and template; Web API error messages now include the response body; plugin icons.

**Template findings.** Web API and metadata SDK return `DisplayName` in different shapes (label object versus string) — handled by `display_label` and default chaining. Aliased FetchXML columns arrive as dotted keys (`pub.friendlyname`) and are read with index syntax. Jinja2 string coercion inside dictionary lookups needed `"" ~ value`.

---

## Phase 4 — polish, packaging, distribution

**Goal.** A user who picks an empty folder gets a working reference configuration; the plugin packs, installs from a local file and builds in CI.

**Built.**

| Area | What |
|---|---|
| `FirstRunHelper` | `IsConfigured`, Yes/No offer, `DeployReferenceConfig` (skip existing on first run; overwrite all but `LEGAL.md` on upgrade), `version.txt`, `CheckVersion` upgrade prompt, organisation name by `InputBox` (`Microsoft.VisualBasic`) with `ApplyOrgName` unit-tested separately |
| `SampleConfig\` | Specs, queries, templates, Python scripts and `LEGAL.md` moved into the project as embedded resources; `python\` deleted. The scripts were now deployed to `config\transformations\` and run from there, so users could edit them |
| `LEGAL.md` | New `legal` field; content prepended to `output\<Spec>.context.md` only, never to the run's `output.md` |
| `ConfigValidator` | All violations collected and reported before any query runs |
| Packaging | Nuspec `<files>`; machine-specific post-build `xcopy` replaced by `Directory.Build.targets.example` |
| CI | GitHub Actions: build, test, pack, artifact; publish job on `main` |
| README | Quickstart, configuration reference, authoring guide |

**Plan versus built.**

- The plan proposed three sample specs (EntityDictionary, SecurityModel, SolutionsReference); six shipped by Phase 5 and seven by #25.
- The planned validator *warning* when `LEGAL.md` still contains `[ORGANISATION NAME]` was not built ([O7](#open-items)).
- The plan said deployed scripts would never be overwritten; the upgrade path does overwrite every shipped file on consent. With the scripts gone in Phase 5 this now applies to queries, templates and specs. Since F5, a shipped file the person edited is backed up under `config\backup\` before it is overwritten ([D13](#decisions)).
- A unit test that did not work in GitHub Actions was dropped.

**Human tasks.**

| # | Task | Outcome |
|---|---|---|
| H1 | Organisation name in `LEGAL.md` | Replaced by the first-run `InputBox` substitution |
| H2 | nuget.org account and `NUGET_API_KEY` secret | Superseded: CI publishes with trusted publishing (OIDC), no stored key |
| H3 | README screenshots | **Open** — the README still has a screenshot placeholder ([O12](#open-items)) |
| H4 | Remove the `PostBuild` xcopy | Done — `Directory.Build.targets.example` |
| H5 | End-to-end sandbox test of the sample specs | Carried into Phase 5 H3 |
| H6 | Publish to the Tool Library | Done |

**After Phase 4, before Phase 5** (2026-05-06 → 05-09, inside #17): namespace and package id changed to `Lspiguel.Xrm.D365ContextExporter`; several rounds of nuspec metadata and dependency fixes; version scheme moved to `0.2026.0.x`; a fix for opening the plugin with no connection; requirement versions relaxed to minimums; UI fixes. The `FirstRunHelper` version tests were removed because they failed whenever a rebuild changed the assembly version. F4 restored that coverage through an internal `CheckVersion` overload that takes the current version and the confirmation as parameters.

---

## Phase 5 — .NET-native rendering with Scriban

**Goal.** No Python on the user's machine.

### Abandoned: embedded IronPython

First plan (2026-05-18): run `transform.py` in process with IronPython 3, Jinja2 and MarkupSafe bundled as a zip in the assembly, output captured through a `LoggingStream`, token counting removed. A trial bundled `pylibs.zip` and a WIP implementation was committed as "does not work". **Jinja2 3.1.x uses `async def`, which IronPython 3.4 does not support.** Both commits were reverted and the phase re-planned.

### Built: Scriban

- `TemplateRenderer` replaced `PythonInvoker` and `transform.py`: parse (errors with file, line, column — printed zero-based, as Scriban holds them, until the F4 tests caught it), intermediate converted recursively to `ScriptObject` / `ScriptArray`, `_spec` added, `StrictVariables = true` (the equivalent of `StrictUndefined`), a file-system loader for `include`, output written as UTF-8 without BOM.
- `TemplateFilters` ported the 19 filters and added three during implementation: `entity_forms`, `entity_views`, `priv_depth`. `group_by_key` now returns `{ key, items }` objects instead of tuples.
- All templates ported to Scriban and **renamed `.j2` → `.sbn`** (the plan had kept `.j2` so specs would not change; the specs were updated instead).
- Token counting removed: no `token_count.txt`, no header.
- Deleted: `PythonInvoker`, `PythonBootstrapHelper`, `ProcessRunner`, `transform.py`, `filters.py`, `requirements.txt`. `PythonSettings` kept so older configs still load.
- Scriban planned at 5.12, adopted at **7.2.0** after a vulnerability in the older version.
- Scriban and its `System.Text.Json` dependencies shipped in a private `Plugins\D365ContextExporter\` subfolder, loaded by an `AppDomain.AssemblyResolve` handler — moved from the control into the plugin class after resolution failures. Phase 6 and #28 removed this step by step.
- The background task moved from `Task.Run` to the XrmToolBox worker (`WorkAsync`); a working progress indicator (spinner) was added.
- **Forms and views:** forms and views went missing from the output; fixed by querying `systemforms` and `savedqueries` separately and matching them per entity with `entity_forms` / `entity_views`.
- **Extra whitespace** in the rendered output made an assistant loop endlessly when reading it; fixed within #17 (the commit log does not record how).
- `LoopLimit` and `LimitToString` were set to unlimited for large environments, and the log warns that rendering can take minutes.

**Human tasks.**

| # | Task | Outcome |
|---|---|---|
| H1 | Port the templates to Scriban | Done |
| H2 | Check `TemplateFilters` against `filters.py` | Not recorded. Automated tests for every filter were added later, in F4 ([O6](#open-items)) |
| H3 | End-to-end test of the sample specs | Not recorded in detail; the forms-and-views and whitespace fixes above came out of real runs |
| H4 | Remove `python` from the sample specs; mark it deprecated in the schema | Done later, in F3 ([O3](#open-items)) |

**Jinja2 to Scriban — the differences that mattered when porting.**

| Jinja2 | Scriban |
|---|---|
| `{% for x in items %}` … `{% endfor %}` | `{{ for x in items }}` … `{{ end }}` |
| `{% if %}` / `{% elif %}` / `{% endif %}` | `{{ if }}` / `{{ else if }}` / `{{ end }}` |
| `{% set x = v %}` | `{{ x = v }}` |
| `{{ x \| f(arg) }}` | `{{ x \| f arg }}` |
| `loop.index` (1-based), `loop.first`, `loop.last` | `for.index` (0-based), `for.first`, `for.last` |
| `x is defined`, `x \| default('v')` | `x != null`, `x ?? 'v'` |
| `upper`, `lower`, `trim`, `join`, `length` | `string.upcase`, `string.downcase`, `string.strip`, `array.join`, `array.size` |
| `selectattr`, `namespace()` | A loop with `if`; an outer variable |
| `for k, items in x \| group_by_key('k')` | `for g in x \| group_by_key 'k'` with `g.key`, `g.items` |

---

## Phase 6 — Scriban source embedding and welcome dialog

**Goal.** No `Scriban.dll` in the output; a one-time welcome dialog.

**Built.**

- `PackageScribanIncludeSource=true` and `IncludeAssets="Build"` on Scriban 7.2.0 compile its source into the plugin as internal types. Polyfills for .NET Framework 4.8: `Microsoft.CSharp`, `System.Threading.Tasks.Extensions`, `PolySharp` (source-only).
- `Scriban.dll` removed from the nuspec and from the private-deps copy. `System.Text.Json` and `System.Text.Encodings.Web` still shipped in the subfolder, with the `AssemblyResolve` handler kept for them — the cause of the #28 failure.
- `WelcomeForm` — a `WebBrowser` control showing `Resources\WelcomeQuickStart.html` (with `X-UA-Compatible: IE=edge`), shown on the first `Load` before any directory prompt. `WelcomeShown` is set before the dialog opens so a failure cannot re-trigger it; it is never reset, upgrades included.
- Version scheme moved to `1.YYYY.M.N`: **1.2026.6.1**.

**Verification planned:** no `Scriban.dll` in Debug output or in the package; welcome dialog shown exactly once; tests pass; a full EntityDictionary export renders as before.

---

## Post-Phase 6 — releases

| Date | PR | Version | Change |
|---|---|---|---|
| 2026-06-18 | #20 | — | README rewritten for the release; `infographic.html` / `.png` added |
| 2026-07-15 | #25 | 1.2026.7.1 | Progress label placement; the PowerAutomateFlows spec restored and business process flow queries fixed; JSON schema moved into `SampleConfig\` and deployed to `config\` beside the specs, so their `"./context-exporter.schema.json"` reference resolves; unused `solutions`, `solutions-components` and `solutions-and-entities` queries removed; Scriban 7.2.5 |
| 2026-07-26 | #28 | 1.2026.7.2 | **Tool Library update from 1.2026.6.1 to 1.2026.7.1 failed:** "The process cannot access the file `…\Plugins\D365ContextExporter\System.Text.Json.dll` because it is being used by another process." XrmToolBox locks every DLL in a `Plugins` subfolder shortly after start-up, even if the tool is never opened. Fix: the `System.Text.Json` package reference and the `AssemblyResolve` handler removed — Scriban's source-include build already compiles without System.Text.Json (`SCRIBAN_NO_SYSTEM_TEXT_JSON`), at the cost of `object.from_json` / `object.to_json`. The package now ships no loose dependency. The nuspec release notes give the one-time manual clean-up for people upgrading from an older version. Recorded as SPEC R4 and R5 and in INSTRUCTIONS.md |
| 2026-11-09 | — | unreleased | The planned fixes F1–F5 from [SPEC §15.1](SPEC.md#151-planned-fixes): `Directory.Build.targets.example` copies only the DLL and PDB (F1); README and welcome dialog sample-spec drift fixed (F2); `"python"` removed from the shipped specs and deprecated in the schema, schema `$id` set to this repository (F3); unit tests for `TemplateFilters`, `TemplateRenderer`, `ExportJobRunner` and `CheckVersion`, through internal seams that take the query runners and the version and confirmation (F4); edited shipped files backed up under `config\backup\` before an upgrade overwrites them (F5). The F4 tests found that parse errors reported zero-based lines and columns; now counted from one. 90 tests added, 228 in all. Not yet checked by hand: the new upgrade dialog wording in XrmToolBox, and an upgrade over a base directory with edited files |
| 2026-11-09 | — | unreleased | **Customization scope**, after a review of a full run against a development environment, cross-checked through the Agent Companion MCP. SolutionsReference took 7 m 42 s: 6 minutes of it rendering, 4½ of those in the per-table forms and views scans. Its intermediate was 188 MB: 2,942 tables with 118,917 columns, 95,421 SDK steps (163 were the project's), 22,822 role-privilege rows. The reference configuration now covers what the project built or customized ([SPEC §12.2](SPEC.md#122-customization-scope)): new `project-tables`, `project-forms` and `project-views` queries; filtered columns, option sets, steps, roles, apps and solutions; forms and views indexed by table once. Fixed on the way: forms and views truncated at 5,000 (the environment had 17,169 views); the publisher column always empty (alias mismatch); form and view type labels wrong in FormsAndViews and SolutionsReference; virtual tables never detected (`IsVirtual` is not a metadata property); multi-select, file and image columns shown as `-`; every role under one "Root" heading; disabled steps and draft workflows shown as active; `req_indicator` never marking `ApplicationRequired` columns (5,490 in that environment, none marked). `ShippedTemplatesTests` added; 238 tests |
| 2026-11-09 | — | unreleased | **Follow-up after re-running every spec.** The re-run took about 2½ minutes for all seven specs; SolutionsReference 27 s instead of 7 m 42 s, 99k tokens instead of 662k. `GlobalOptionSetDefinitions` rejected `$filter` (HTTP 405), so option sets are filtered in the templates. SecurityModel shows a table × action matrix of privilege depths (251k → about 100k tokens on the same data); SolutionsReference lists solution-only managed tables in one line, lists each BPF stage once (the process held repeated stage records), fetches role names only (`security-role-names`) and has no runs of blank lines. Plugin code: `flow_trigger_info` (O15), `BooleanManagedProperty` serialized as its value (O16), `format_forms` type codes (O17). 255 tests |
| 2026-11-09 | — | unreleased | **PowerPages spec** ([#22](https://github.com/lspiguel/ai-tooling/issues/22)), researched through the Agent Companion MCP against a development environment with one active site on the enhanced data model. Three queries over `powerpagesite` and `powerpagecomponent` and one template: the page map with URLs, templates, the forms and lists each page hosts and its access rules; web roles and what each can do; table permissions; page access rules; basic forms, lists and multistep forms with their steps in order; cloud flows; Web API exposure; site settings grouped by area; templates and navigation. The enhanced model keeps each component's settings as JSON in `content`, and its `mspp_` virtual tables ignore link-entities, so the template reads one query of components and parses `content` with a new function, `parse_json` (Newtonsoft, so R5 still holds). Secret-looking site settings, web templates and web files are fetched by name only. `PowerPagesTemplateTests` added; 265 tests. Not yet checked: a run of the spec from XrmToolBox |
| 2026-11-09 | — | unreleased | **Run All Specs** ([#33](https://github.com/lspiguel/ai-tooling/issues/33)): a button beside **Run Export** that runs every spec in the list after a confirmation warning that it can take a long time, then shows only the output folder with **Open Folder**. `ExportBatchRunner` runs the specs in turn through the existing `ExportJobRunner`. Specs run back to back can start within one second, which would have put two runs in one folder; a run folder now gets a `-2`, `-3`… suffix when its timestamp is taken. `ExportBatchRunnerTests` added; 272 tests. Not yet checked by hand: the button, dialog and output panel in XrmToolBox |
| 2026-11-09 | — | unreleased | **PowerPages after its first real run** (all eight specs through **Run All Specs** in about 1½ minutes), cross-checked through the Agent Companion MCP. Most pages embed their forms and lists with Liquid in the page copy (`{% webform %}`, `{% entityform %}`, `{% include 'entity_list' %}`), so the page map and the forms' **On pages** were mostly empty; the template now reads those tags, and names an embed that matches no active form. Every "(not found)" but one was an inactive component (4 web roles, 4 pages, a basic form, a multistep form); `power-pages-component-names` now also returns inactive components by name, shown as *(inactive)*. The remaining one is a page template pointing to a web template that no longer exists. Multistep steps followed only Next Step, so a condition's else branch landed at the end in no order; steps are now in flow order with a **Next** column (condition, redirect target and query string). Also: role summaries list each table once; access rules ordered by page, with the rule name; a note when no active role is assigned automatically to anonymous or authenticated users; blanks for unset relationships and tables. 275 tests |
| 2026-11-09 | — | unreleased | **Higher-resolution icons** ([#39](https://github.com/lspiguel/ai-tooling/issues/39)), as for the Agent Companion MCP: 80 px and 150 px images added; the plugin embeds the 32 px image as its small icon and the 80 px one as its big icon (16 and 32 px before); the nuspec `iconUrl` and the csproj `PackageIcon` use the 150 px image (128 px before). The `iconUrl` points to `main` on GitHub, so the Tool Library shows it once merged |

---

## Decisions

| # | Decision | Why | When |
|---|---|---|---|
| D1 | Queries in C#, transformation in text files, JSON between them | Authenticated SDK in the plugin; output formats without recompiling; inspectable runs | Plan |
| D2 | One base directory, shared queries and templates, immutable run folders, one upload file per spec | Specs differ only in configuration; runs are evidence | Plan |
| D3 | Python as a subprocess, not embedded | Decoupled runtimes | Plan — superseded by D6 |
| D4 | Reference configuration embedded in the DLL and deployed on first use; redeployed on upgrade only with consent; `LEGAL.md` never overwritten | A working start from an empty folder without loose files in the package; protect user edits | Phase 4 |
| D5 | Legal notice prepended by the plugin, only to the delivered file | The boundary notice cannot depend on templates | Phase 4 |
| D6 | Scriban instead of Python/Jinja2 | IronPython cannot run Jinja2 3.1; no Python prerequisite for users | Phase 5 |
| D7 | No token counting | Its only native dependency; marginal value | Phase 5 |
| D8 | Strict variables, unlimited loops and strings | Surface template errors; large environments | Phase 5 |
| D9 | Scriban source-embedded | No `Scriban.dll` to ship or resolve | Phase 6 |
| D10 | No DLL beside the plugin; no System.Text.Json | Locked subfolder DLLs broke Tool Library updates | #28 |
| D11 | Late binding only | Tables change too fast for generated classes | Throughout |
| D12 | Publish with trusted publishing | No stored API key | Phase 4 → CI |
| D13 | An upgrade backs up an edited shipped file before replacing it, into a versioned folder below `config\` that is never overwritten | **Yes** silently discarded edits while the dialog said custom files were safe (O13); a backup keeps the overwrite simple and the edit recoverable | F5 |
| D14 | The reference configuration exports what the project built or customized: unmanaged components, managed tables with project columns, forms or views, and components of project solutions. It assumes a development environment | The full-environment export was mostly first-party components, too large for any assistant, and slow to render | Customization scope |
| D15 | Power Pages is read from `powerpagecomponent` on the enhanced data model only; values that may be secret are never fetched, and page and template source is summarized, not exported | The `mspp_` virtual tables cannot be joined, while one component query carries every relationship in `content`; new sites use the enhanced model. An export is shared with assistants, so a secret must not reach the intermediate, let alone the output; source code would dominate the token count | #22 |
| D16 | **Run All Specs** keeps going when a spec fails and stops only on **Cancel**; each spec is still a separate run with its own folder and output file | Specs are independent, and the batch is meant to be left running; stopping on the first failure would leave the rest to be rerun one by one. One run per spec keeps R9 and the existing single-spec path | #33 |

---

## Open items

Found while deriving the spec from the code on 2026-11-09. None changes the plugin's core behaviour; most are documentation drift. O1, O2, O3 (with O5), O6 and O13 had decided fixes in [SPEC §15.1](SPEC.md#151-planned-fixes), as F1–F5; those are built (unreleased), and marked below.

| # | Item | Where | Status |
|---|---|---|---|
| O1 | `Directory.Build.targets.example` still describes the private-lib subfolder and copies `Scriban.dll`, `System.Text.Json.dll` and `System.Text.Encodings.Web.dll`, none of which exist since #28 | `D365ContextExporter/Directory.Build.targets.example` | Fixed (F1) |
| O2 | README lists six sample specs; seven ship (PowerAutomateFlows is missing). The descriptions of SolutionsReference and SolutionInventory appear swapped. The folder tree omits `power-automate-flows.sbn` and the schema. The welcome dialog also says six | `README.md`, `Resources/WelcomeQuickStart.html` | Fixed (F2). The welcome dialog in fact said "several", not six; its wording now matches the README's |
| O3 | The schema still documents `python` (interpreter discovery, `venv`) as live, and every shipped spec still carries `"python"` (Phase 5 H4). The schema's `$id` names a different repository URL | `SampleConfig/context-exporter.schema.json`, `SampleConfig/*.json` | Fixed (F3) |
| O4 | README and schema say `frontMatter` is "injected as YAML front-matter into the output"; the plugin only passes it to `_meta.frontMatter` and the shipped templates emit the block | `README.md`, schema | Open |
| O5 | Stale comments: `ExportJob.Transformation` says Jinja2; `PythonSettings.Interpreter` describes interpreter discovery; the csproj mentions adding `Microsoft.PowerPlatform.Dataverse.Client` in Phase 2; the nuspec keeps a commented-out schema entry | Source | Partly fixed (F3): `ExportJob.Transformation` and `PythonSettings`. The csproj and nuspec comments remain |
| O6 | No unit tests for `TemplateRenderer`, `TemplateFilters` or `ExportJobRunner`, although Phase 5 planned them; `CheckVersion` and `version.txt` are untested | Tests | Fixed (F4) |
| O7 | No warning when `LEGAL.md` still contains `[ORGANISATION NAME]` (planned in Phase 4) | `ConfigValidator` | Open |
| O8 | The attribute deny list applies only to FetchXML results; Web API and metadata results are not filtered. Decide whether that is intended and document it | `ExportJobRunner`, README | Open |
| O9 | `maxRecords` is checked per page, so a result can exceed it by up to one page | Query runners | Open |
| O10 | The validator resolves a FetchXML `source` with `Path.Combine`, the runner with `PathResolver` (which expands `%VAR%`): a source using an environment variable passes the runner but fails validation | `ConfigValidator` | Open |
| O11 | The folder dialog hides the new-folder button, while the Quick Start says "choose (or create)" a folder | `BaseDirectoryPickerControl` | Open |
| O12 | README screenshot placeholder (Phase 4 H3) | `README.md` | Open |
| O13 | The upgrade dialog says custom files are not modified; edits to a *shipped* file (same name) are overwritten on **Yes** | `FirstRunHelper.CheckVersion` | Fixed (F5) |
| O14 | The schema requires at least one query; the plugin accepts a spec with none and produces nothing | Schema, `ExportJobRunner` | Open |
| O15 | Cloud flow triggers are guessed from flow names (`flow_trigger`): 68 of 471 were wrong in the reviewed environment. The trigger — Dataverse table and message, schedule, HTTP, manual or child, Power Apps, Power Pages, selected row — is in `clientdata`. Templates cannot parse JSON (R5), so this needs a C# filter. SolutionsReference no longer shows the guess; PowerAutomateFlows still shows `primaryentity`, which is `none` for most flows | `TemplateFilters`, templates | Fixed: `flow_trigger_info` reads the trigger from the definition; it classified all 421 flows of the reviewed environment. Used by SolutionsReference and PowerAutomateFlows |
| O16 | `EntityJsonSerializer` writes `BooleanManagedProperty` values (for example `iscustomizable`) as the type name instead of `.Value` | `EntityJsonSerializer` | Fixed |
| O17 | `format_forms` maps form types wrongly (8 as main, 7 as quick, 106 as quick create; main is 2, quick view 6, quick create 7, dialog 8). The shipped templates no longer use it | `TemplateFilters` | Fixed: the documented `systemform.type` values |
| O18 | The Web API `$filter` expressions of the customization scope (`IsManaged`, `IsCustomAttribute` and `AttributeOf` inside `$expand=Attributes`) and virtual-table detection through `TableType` / `DataProviderId` could not be run through the MCP. The run time after the change is not measured. The first export failed on `GlobalOptionSetDefinitions?$filter=…` with HTTP 405 ("The query parameter $filter is not supported"); the option-set filter moved to the templates | SampleConfig | Open — confirm on the first export |
| O19 | A standard column the project changed without creating it (label, required level) is not listed in SolutionsReference | SampleConfig | Open — accepted limitation |
