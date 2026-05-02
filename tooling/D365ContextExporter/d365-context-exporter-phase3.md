# D365 CE Context Exporter — Phase 3 Plan: Python Post-Processor and Output

## What Phase 2 Left Behind

Phase 2 fully implemented query execution and the intermediate JSON pipeline. Every query type works (`fetchxml`, `webapi`, `metadata`), per-query raw files are written, and `intermediate.json` is assembled. However, the pipeline stops there: no Python is invoked, and no Markdown is produced.

Specifically, at the end of Phase 2:

- `ProcessRunner.cs` — stub that throws `NotImplementedException("Phase 3")`
- `Orchestration/PythonInvoker.cs` — not created at all
- `UI/OutputPreviewControl.cs` — not created at all
- `ExportProgressControl` — log-only stub; cancel button and per-query progress deferred
- `python/transform.py` — not written (only `filters.py` exists in `config/transformations/` as a bare module with one helper function, no Jinja2 filter registration)
- `python/requirements.txt` — not written
- `ExportJobRunner.Run()` — stops after writing `intermediate.json`; never calls Python or copies output

The Jinja2 template files (`.j2`) and FetchXML queries exist in `config/` and are consistent with what `intermediate.json` would provide, but have not been tested end-to-end.

## Phase 3 Exit Criteria

An end-to-end run against a real sandbox org must produce:
1. `runs/<timestamp>/intermediate.json` (already working from Phase 2)
2. `runs/<timestamp>/output.md` — rendered by `transform.py` from the intermediate JSON
3. `output/<SpecName>.context.md` — copied from `output.md` by the plugin after a successful run

The plugin UI must also show the generated files with actions to open them.

---

## Source File Inventory

### C# — New Files

---

#### `Orchestration/PythonInvoker.cs`

**Description.** Locates a Python interpreter, extracts embedded Python scripts to `LocalAppData`, and invokes `transform.py` as a child process, passing the paths to `intermediate.json`, the Jinja2 template, the output directory, and the spec name. Streams stdout and stderr to the log delegate in real time. Enforces a configurable timeout and surfaces failures as typed exceptions.

**Responsibilities.**
- Resolve the Python interpreter via `PythonBootstrapHelper.ResolveExecutable(settings)`:
  - `PythonSettings.Interpreter` (when not `"auto"`) — used as-is.
  - `"python"` — when set to `"auto"` (relies on PATH).
- Extract embedded Python scripts (`transform.py`, `filters.py`, `requirements.txt`) from the assembly manifest to `%LOCALAPPDATA%\D365ContextExporter\python\` via `EnsureScripts()`. This runs on every invocation and overwrites existing files, keeping them in sync with the assembly version.
- Build the command-line call:
  ```
  <python> "<LocalAppData>\D365ContextExporter\python\transform.py"
      --input <runDir>\intermediate.json
      --template <baseDir>\config\transformations\<job.Transformation>
      --out <runDir>
      --spec <job.Spec>
  ```
- Launch the process with `ProcessRunner`, redirecting stdout and stderr. Pipe each line to the `log` delegate so the user sees real-time progress.
- Enforce a default 5-minute timeout. Kill the process forcibly on timeout.
- Throw `PythonInvocationException` (a typed exception carrying exit code and last ~500 chars of stderr) when the process exits non-zero.
- After a successful run, call `CopyOutputToSpecDir(runDir, baseDir, job.Spec)` — copies `<runDir>\output.md` to `<baseDir>\output\<SpecName>.context.md`, creating the `output\` directory if needed.

**How it is used from configuration.**
- `python.interpreter` — `"auto"` resolves to `"python"` on PATH; any other string is used verbatim.
- `transformation` — the `.j2` filename passed as `--template`; resolved relative to `<baseDir>\config\transformations\`.
- `spec` — passed as `--spec`; determines the final output filename.

**Called from.** `ExportJobRunner.Run()` after `IntermediateJsonBuilder.WriteIntermediate()` succeeds.

---

#### `UI/OutputPreviewControl.cs` (+ `.Designer.cs`)

**Description.** A WinForms `UserControl` displayed after a successful export run. Shows the paths to the two output files (`output.md` in the run directory, and `output/<SpecName>.context.md`) with actions to open them. Provides a prominent call-to-action directing the user to upload the grounding file to their AI assistant.

**Responsibilities.**
- Accept paths for the run-specific `output.md` and the spec-level `<SpecName>.context.md` via `ShowResult(string runOutputPath, string projectOutputPath)`.
- Display both paths in read-only text boxes.
- Provide three action buttons per file: **Open** (shell-launches the file with the default editor), **Reveal** (opens Explorer with the file selected), and **Copy Path** (copies the path to the clipboard).
- Display a labelled `LinkLabel` or `RichTextBox` banner reading something like:
  > Upload `<SpecName>.context.md` to claude.ai, ChatGPT, or your AI assistant as a grounding file.
- Hide itself (or show a placeholder) when no run has completed yet.
- Log nothing itself; all logging goes through `ExportProgressControl`.

**How it is used from configuration.** The spec name in `spec` determines which file name is highlighted as the upload target. No other config keys affect this control directly.

**Called from.** `ContextExporterPluginControl` after `ExportJobRunner.Run()` completes successfully.

---

### C# — Modified Files

---

#### `Helpers/ProcessRunner.cs`

**Current state.** Stub — throws `NotImplementedException`.

**Description.** A thin managed wrapper over `System.Diagnostics.Process` that launches an external executable, optionally captures stdout/stderr line-by-line via callbacks, enforces a timeout, and returns the exit code.

**Responsibilities.**
- Expose a single static method with signature:
  ```csharp
  public static int Run(
      string executable,
      string arguments,
      string workingDirectory,
      Action<string> onStdout,
      Action<string> onStderr,
      int timeoutMs,
      CancellationToken cancellationToken)
  ```
- Configure `ProcessStartInfo` with `RedirectStandardOutput = true`, `RedirectStandardError = true`, `UseShellExecute = false`, `CreateNoWindow = true`.
- Begin async reads of stdout and stderr immediately after `Start()`, forwarding each line to the respective callback. The timeout is split: half is used for `WaitForExit`, half for draining stdout/stderr streams.
- Block until the process exits or `timeoutMs` elapses. On timeout, call `Kill()` and throw `TimeoutException`.
- Observe `cancellationToken`; on cancellation, call `Kill()` and throw `OperationCanceledException`.
- Return the process `ExitCode`.

**How it is used from configuration.** Not configured directly; all parameters come from `PythonInvoker`, which resolves config values before calling `ProcessRunner`.

**Called from.** `PythonInvoker` and `PythonBootstrapHelper` exclusively.

---

#### `Orchestration/ExportJobRunner.cs`

**Current state.** Fully implemented but stops after `IntermediateJsonBuilder.WriteIntermediate()`. Never invokes Python or copies output.

**Implemented changes.**
1. After `WriteIntermediate`, constructs a `PythonInvoker` and calls `Invoke(job, baseDir, runDir, cancellationToken)`.
2. After `Invoke` succeeds, calls `AppendTokenCount(runDir)` — reads `token_count.txt` written by `transform.py` and prepends a `> Token count (gpt-4o): N` blockquote to `output.md`.
3. Calls `CopyOutputToSpecDir(runDir, baseDir, job.Spec)` to write the final `output/<SpecName>.context.md`. (Note: `PythonInvoker` also has its own `CopyOutputToSpecDir` which runs first; `ExportJobRunner`'s copy runs after `AppendTokenCount` so the token-count header is included in the project output file.)
4. Returns the run directory path to the caller so the UI can show `OutputPreviewControl`.
5. Per-query failures are collected without short-circuiting; an `AggregateException` is thrown after the query loop if any failed.
6. Progress callback (`onProgress`) is invoked after each query with `(i+1, total, queryId)`.

No structural changes to query execution or intermediate JSON building.

---

#### `UI/ExportProgressControl.cs`

**Current state.** Log-only stub. The `ExportProgressControl` comment in the source reads "Phase 1 stub — progress bars and cancel button are deferred to Phase 3."

**Required changes.**
1. **Cancel** button raises `CancelRequested` event. The plugin control owns the `CancellationTokenSource` and calls `cts.Cancel()` in response.
2. `SetProgress(int current, int total, string queryId)` updates a progress label (e.g., "Query 2 / 5") with thread-safe marshalling via `BeginInvoke`.
3. `SetRunning(bool running)` enables/disables the Cancel button.
4. `AppendLog(string message)` and `ClearLog()` use the `InvokeRequired`/`BeginInvoke` pattern for thread safety. The RichTextBox auto-scrolls to the caret after each append.

---

#### `ContextExporterPluginControl.cs`

**Current state.** Wires up directory picker, spec picker, run button, and log. Calls `ExportJobRunner.Run()` on a background `Task`.

**Required changes.**
1. `OutputPreviewControl` is in the form layout (hidden until a run completes).
2. In the `Task.ContinueWith` callback, if the run succeeded, constructs `runOutputPath` and `specOutputPath` and calls `outputPreview.ShowResult(runOutputPath, projectOutputPath)`.
3. `ExportProgressControl.CancelRequested` is wired to call `cts.Cancel()`.
4. On run start: `progressControl.SetRunning(true)`, `outputPreview.Hide()`. On completion (success or failure): `progressControl.SetRunning(false)`.
5. Python pre-check: calls `PythonBootstrapHelper.Check(job.Python, log)` on the UI thread before launching the async run. If `Check` throws, the run is aborted and the error is displayed without starting a background task.

---

### C# — New Helper

---

#### `Helpers/PythonBootstrapHelper.cs`

**Description.** Verifies that a usable Python interpreter with the required packages installed is available before any export run starts.

**Responsibilities.**
- `Check(PythonSettings settings, Action<string> log)` — static method. Called on the UI thread before the async export run starts.
  - Resolves the interpreter via `ResolveExecutable(settings)`.
  - Reads package names from `%LOCALAPPDATA%\D365ContextExporter\python\requirements.txt` (extracted there by `PythonInvoker.EnsureScripts` on a prior run; absent on a true first run).
  - Runs `python -m pip show <packages...>` with a 10-second timeout.
  - If exit code is non-zero, throws `InvalidOperationException` with a message directing the user to install the requirements manually.
  - Logs `[Python] OK (<interpreter>)` on success.
- `ResolveExecutable(PythonSettings settings)` — `internal static` method used by both `PythonBootstrapHelper.Check` and `PythonInvoker`. Returns `settings.Interpreter` when not `"auto"`, otherwise returns `"python"`.

**How it is used from configuration.**
- `python.interpreter` — `"auto"` resolves to `"python"` on PATH; any other string is used verbatim.

**Note:** There is no venv creation logic in this helper. The plugin does not manage a virtual environment. Users are expected to have Python and the required packages installed in their active environment. The `requirements.txt` (extracted to LocalAppData) shows which packages are needed.

---

### Python — New Files

---

#### `python/transform.py`

**Description.** The universal Jinja2 orchestrator. The single Python entry point invoked by `PythonInvoker`. Reads `intermediate.json`, loads the specified Jinja2 template, renders it, and writes `output.md` into the run directory. Kept under 150 lines.

**Responsibilities.**
- Parse CLI arguments:
  - `--input` — absolute path to `intermediate.json`
  - `--template` — absolute path to the `.j2` template file
  - `--out` — absolute path to the run directory where `output.md` will be written
  - `--spec` — spec name (injected into the template context as `_spec`)
- Load `intermediate.json` with `json.load()`.
- Build a Jinja2 `Environment` with:
  - `loader = FileSystemLoader(os.path.dirname(template_path))` so templates can `{% include %}` siblings.
  - `undefined = StrictUndefined` to surface typos in templates immediately.
  - All custom filters from `filters.py` registered via `env.filters.update(get_filters())`.
- Render the template with the full intermediate JSON dict as the context (so `entityAttributes`, `securityRoles`, `_meta`, etc. are all top-level variables).
- Write the rendered string to `<out>/output.md` in UTF-8.
- Print a summary line to stdout on success: `[transform] output.md written ({len} bytes)`.
- Count tokens using `tiktoken` (encoding `o200k_base`). If `tiktoken` is unavailable, fall back to `max(1, len(text) // 4)` (GPT-4o averages ~4 chars/token). Write the token count as a plain integer to `<out>/token_count.txt`. Print `[transform] token count (gpt-4o): {N}` to stdout.
- Exit with code 0 on success, 1 on any error (exceptions are caught at the top level, printed to stderr, and cause exit 1).

**Token count sidecar.** `ExportJobRunner` reads `token_count.txt` after a successful Python invocation and prepends `> Token count (gpt-4o): N` as a blockquote to `output.md` before copying it to the project output directory. This means the project-level `<SpecName>.context.md` includes the token count header; the raw `output.md` in the run directory does not (it is the unmodified Jinja2 output).

**How it is used from configuration.**
- The `transformation` key in the spec config (`*.context-exporter-config.json`) names the `.j2` file that `PythonInvoker` passes as `--template`. `transform.py` itself is not referenced in config; it is always the entry point.
- Template context variables match the `resultKey` values defined in the `queries` array plus `_meta` (always present).

**Invoked by.** `PythonInvoker.cs` via `ProcessRunner.cs`.

---

#### `python/filters.py`

**Current state.** Fully implemented (19 filters registered).

**Description.** Custom Jinja2 filters reusable across all templates. Exposes a single `get_filters()` function that returns a dict of `{filter_name: callable}` for `transform.py` to register.

**Mapping dictionaries** (module-level constants used by filters):

| Constant | Key type | Purpose |
|---|---|---|
| `COMPONENT_TYPES` | `int` | Solution component type integer → readable label |
| `ATTR_TYPE_ABBREV` | `str` | Dataverse `AttributeType` string → short abbreviation (e.g. `"Lookup"` → `"lkp"`) |
| `FORM_TYPE_SHORT` | `int` | Form type integer → short label (e.g. `8` → `"main"`) |
| `PLUGIN_STAGE` | `int` | Plugin step stage integer → label (e.g. `20` → `"Pre"`) |
| `PLUGIN_MODE` | `int` | Plugin execution mode → `"Sync"` / `"Async"` |
| `ENVVAR_TYPE` | `int` | Environment variable type integer → type name |
| `API_PARAM_TYPE` | `int` | Custom API parameter type integer → short type abbreviation |

**Registered filters:**

| Filter name | Signature | Purpose |
|---|---|---|
| `component_type_name` | `(value: int) -> str` | Maps a solution component type integer to a readable label. |
| `schemaname_to_title` | `(value: str) -> str` | Converts camelCase or underscore schema name to Title Case. |
| `markdown_table` | `(rows: list, columns: list[str]) -> str` | Renders a Markdown pipe table. Returns `_No data._` for empty input. |
| `csv_list` | `(items: list, attr: str = None) -> str` | Joins a list into a comma-separated string; uses `attr` key for dicts. |
| `optionset_label` | `(options: list[dict], value: int) -> str` | Returns the label for a given option value from an option set `Options` list. |
| `iso_date` | `(value: str) -> str` | Formats an ISO-8601 string as `YYYY-MM-DD`. |
| `attr_type_abbrev` | `(value) -> str` | Maps a Dataverse `AttributeType` string to a short abbreviation. |
| `req_indicator` | `(value) -> str` | Maps `RequiredLevel.Value` to `**R**`, `r`, or `-`. |
| `format_forms` | `(forms) -> str` | Groups forms by `Name`, appends abbreviated type list: `"Information(card,main)"`. |
| `format_views` | `(views) -> str` | Returns comma-separated view names, skipping system/personal view types. |
| `plugin_stage` | `(value) -> str` | Maps plugin step stage integer to label. |
| `plugin_mode` | `(value) -> str` | Maps plugin execution mode integer to `"Sync"` / `"Async"`. |
| `flow_trigger` | `(name: str) -> str` | Infers cloud flow trigger category (`HTTP`, `Sched`, `Child`, `Manual`, `Auto`) from the flow name. |
| `classic_trigger` | `(wf) -> str` | Derives classic workflow trigger from boolean flags (`Create/Update/Delete/Manual`). |
| `envvar_type` | `(value) -> str` | Maps environment variable type integer to type name. |
| `api_param_type` | `(value) -> str` | Maps Custom API parameter type integer to short type abbreviation. |
| `pluck` | `(items, key: str) -> list` | Extracts unique non-None values for a key from a list of dicts (supports literal-dot keys). |
| `group_by_key` | `(items, key: str) -> list` | Groups a list of dicts by a key, returning `(value, [items])` tuples in insertion order. |
| `display_label` | `(label_obj, fallback: str = "—") -> str` | Safely extracts `UserLocalizedLabel.Label` from a D365 Label object. |

Expose `get_filters() -> dict` as the public API so `transform.py` calls `env.filters.update(get_filters())`.

**How it is used from configuration.** Not directly configured. All templates in `config/transformations/` can use these filters by name (e.g. `{{ role.name | schemaname_to_title }}`). No config changes are required to add or use filters.

---

#### `python/requirements.txt`

**Description.** Pinned pip dependencies for the virtual environment.

**Contents.**
```
Jinja2==3.1.4
tiktoken==0.9.0
MarkupSafe==2.1.5
PyYAML==6.0.1
python-dateutil==2.9.0.post0
```

`tiktoken` is used by `transform.py` for GPT-4o token counting (`o200k_base` encoding). It is an optional runtime dependency: if import fails, `transform.py` falls back to a character-count estimate and still exits successfully.

**How it is used from configuration.** `PythonBootstrapHelper.Check` reads package names from this file (extracted to `%LOCALAPPDATA%\D365ContextExporter\python\`) to run `pip show` verification. The file is embedded in the assembly as an `EmbeddedResource` and extracted at runtime by `PythonInvoker.EnsureScripts()`.

---

### Configuration Files — Refinements

The following config-layer files require updates or creation to support the end-to-end pipeline.

---

#### `config/transformations/filters.py` → moved to `python/filters.py`

The current `config/transformations/filters.py` is a stub in the wrong location. It belongs in `python/filters.py` (inside the C# project, packed with the assembly) so that `transform.py` can import it without needing the `config/` directory on `sys.path`. The `config/transformations/` folder is for `.j2` templates only.

**Action:** Move and complete as described above. Remove the file from `config/transformations/`.

---

#### `config/transformations/entity-dictionary.j2`

**Current state.** Authored and structurally correct. References `entityAttributes` grouped by `LogicalName`. Accesses nested `DisplayName.UserLocalizedLabel.Label`.

**Responsibilities.** Renders a per-entity Markdown section with display name, primary attribute names, ownership type, custom flag, and a pipe table of attributes sorted by `LogicalName`.

**How it is used from configuration.**
- Activated by setting `"transformation": "entity-dictionary.j2"` in the spec config.
- Requires `entityAttributes` in `resultKey` — supplied by a `webapi` query to `EntityDefinitions?$expand=Attributes(...)`.

**Phase 3 action.** Test end-to-end against a sandbox. The `DisplayName.UserLocalizedLabel.Label` access path will fail if the Web API returns `DisplayName` as a flat string (as it does for some Metadata endpoints vs the schema-API response). May need conditional logic: `first.DisplayName.UserLocalizedLabel.Label | default(first.DisplayName) | default(entity_name)`.

---

#### `config/transformations/security-model.j2`

**Current state.** Authored. Groups `securityRoles` by `businessunitid_name`; accesses `priv_accessright`, `priv_name`, `rp_privilegedepthmask` as aliased attributes from the FetchXML join.

**Responsibilities.** Renders one Markdown section per business unit listing each security role and its privilege depths.

**How it is used from configuration.**
- Activated by `"transformation": "security-model.j2"`.
- Requires `securityRoles` result key from a FetchXML query that joins `rolePrivilege` and `privilege` tables.

**Phase 3 action.** Verify `security-roles.fetch.xml` actually retrieves `priv_name` and `rp_privilegedepthmask` as aliased columns; update the FetchXML or the template attribute names if needed.

---

#### `config/transformations/optionsets.j2`

**Current state.** Authored. References `globalOptionSets` — expects each item to have `Name`, `DisplayName.UserLocalizedLabel.Label`, `OptionSetType`, and `Options[].Value / Options[].Label.UserLocalizedLabel.Label`.

**Responsibilities.** Renders one section per global option set with a pipe table of option values and labels.

**How it is used from configuration.**
- Activated by `"transformation": "optionsets.j2"`.
- Requires `globalOptionSets` result key — supplied by either a `webapi` query to `GlobalOptionSetDefinitions` or a `metadata` query with `"metadataTarget": "optionsets"`.

**Phase 3 action.** The Web API path returns OData metadata with `@odata.type` discriminators; the metadata runner returns plain dicts. Verify the template handles both shapes, or restrict the sample config to one query type.

---

#### `config/transformations/forms-and-views.j2`

**Current state.** Authored. References `formsAndViews` grouped by entity; uses `SystemForms` and `SavedQueries` sub-arrays; maps type integers to labels via a local dict.

**Responsibilities.** Renders per-entity form and view lists as pipe tables.

**How it is used from configuration.**
- Activated by `"transformation": "forms-and-views.j2"`.
- Requires `formsAndViews` result key — supplied by a `webapi` query to `EntityDefinitions?$expand=SystemForms(...),SavedQueries(...)`.

**Phase 3 action.** `form_type_labels.get(form.Type | string, ...)` will fail in Jinja2 if `form.Type` is an integer, since `| string` is not a built-in Jinja2 filter (it is in Django). Replace with `form.Type | string` using the Jinja2 `string` filter, or use `"" ~ form.Type` (string concatenation trick). Test and fix.

---

#### `config/transformations/solution-inventory.j2`

**Current state.** Authored. References `solutions` — expects aliased attributes from the FetchXML join (`pub.publisherfriendlyname` accessed as `sol["pub.publisherfriendlyname"]`).

**Responsibilities.** Renders a summary table and per-solution detail sections.

**How it is used from configuration.**
- Activated by `"transformation": "solution-inventory.j2"`.
- Requires `solutions` result key — supplied by `solutions.fetch.xml`.

**Phase 3 action.** `EntityJsonSerializer` stores aliased attributes under the format `"pub.publisherfriendlyname"` (dot-prefixed alias). Confirm this matches what the template accesses via dict key lookup `sol["pub.publisherfriendlyname"]`.

---

#### `config/queries/security-roles.fetch.xml`

**Current state.** Exists (not shown in detail). Queries the `role` table.

**Phase 3 action.** Confirm the FetchXML joins `rolePrivilege` and `privilege` and retrieves `priv_name`, `accessright`, and depth mask as aliased attributes, since `security-model.j2` references those field names. Update if missing.

---

#### `config/queries/solutions-components.fetch.xml`

**Current state.** File exists (`solutions-components.fetch.xml` shown in glob output). Not referenced by any sample config yet.

**Phase 3 action.** Add it to a sample config as a second query if a `solution-components.j2` template is authored. Otherwise, leave as a reference query for Phase 4.

---

### Schema File

#### `schema/context-exporter.schema.json`

**Description.** JSON Schema document that provides IDE IntelliSense (autocomplete, hover docs, and validation) for `*.context-exporter-config.json` files in VS Code, Rider, and Visual Studio.

**Responsibilities.**
Define types and constraints for all config keys:
- `spec` (string, required)
- `version` (string, pattern `^\d+\.\d+\.\d+$`)
- `transformation` (string, required — name of a `.j2` file)
- `frontMatter` (object, additional string properties allowed)
- `python.interpreter` (string, default `"auto"`)
- `python.venv` (string, supports `%VAR%` tokens)
- `output.attributeDenyList` (array of strings)
- `queries` (array of `QueryDefinition` objects):
  - `id` (string, required)
  - `type` (enum: `fetchxml`, `webapi`, `metadata`, required)
  - `source` (string — required when `type == fetchxml`)
  - `path` (string — required when `type == webapi`)
  - `metadataTarget` (enum: `entities`, `attributes`, `optionsets`, `relationships` — required when `type == metadata`)
  - `resultKey` (string, required)
  - `maxRecords` (integer, optional)
  - `select` (array of strings, optional, only applicable to `webapi`)

**How it is used from configuration.** The `$schema` property at the top of each config file points to this document. VS Code picks it up automatically. The plugin itself does not load or validate against this schema at runtime (validation uses `ExportJob` deserialization errors).

---

## Data Flow — Completed End-to-End

After Phase 3, the complete pipeline from button click to grounding file is:

```
btnRun_Click
  └─ PythonBootstrapHelper.Check()               [pre-flight: pip show all packages]
  └─ ExportJobRunner.Run()  [Task.Run]
       ├─ FetchXmlQueryRunner.Run()               [phase 2]
       ├─ WebApiQueryRunner.Run()                 [phase 2]
       ├─ MetadataQueryRunner.Run()               [phase 2]
       ├─ EntityJsonSerializer.SerializeEntities() [phase 2]
       ├─ IntermediateJsonBuilder.WriteQueryResult() ×N  [phase 2]
       ├─ IntermediateJsonBuilder.WriteIntermediate()    [phase 2]
       ├─ PythonInvoker.Invoke()                  [phase 3]
       │    ├─ EnsureScripts()                   [extract embedded py files to LocalAppData]
       │    ├─ ProcessRunner.Run()                [phase 3]
       │    │    └─ python transform.py
       │    │         ├─ filters.py              [19 filters]
       │    │         ├─ *.j2 template
       │    │         ├─ writes output.md
       │    │         └─ writes token_count.txt  [tiktoken / char-estimate fallback]
       │    └─ CopyOutputToSpecDir()             [run output.md → output/<spec>.context.md]
       ├─ AppendTokenCount()                      [prepends token count blockquote to output.md]
       └─ CopyOutputToSpecDir()                   [run output.md → output/<spec>.context.md, now with header]
  └─ OutputPreviewControl.ShowResult()            [phase 3]
```

---

## Artifacts Inventory

| # | Artifact | Status | Notes |
|---|----------|--------|-------|
| 1 | `Orchestration/PythonInvoker.cs` | **Done** | Includes `EnsureScripts()` embedded resource extraction |
| 2 | `Helpers/ProcessRunner.cs` | **Done** | Timeout split between WaitForExit and stream drain |
| 3 | `Helpers/PythonBootstrapHelper.cs` | **Done** | `Check()` + `ResolveExecutable()`; no venv creation |
| 4 | `Orchestration/ExportJobRunner.cs` | **Done** | Adds Python step, `AppendTokenCount()`, and second copy |
| 5 | `UI/OutputPreviewControl.cs` | **Done** | Open/Reveal/Copy for both output files |
| 6 | `UI/ExportProgressControl.cs` | **Done** | Cancel button, progress label, thread-safe log |
| 7 | `ContextExporterPluginControl.cs` | **Done** | Wired; calls `PythonBootstrapHelper.Check()` pre-run |
| 8 | `python/transform.py` | **Done** | Jinja2 render + tiktoken counting + `token_count.txt` sidecar |
| 9 | `python/filters.py` | **Done** | 19 filters registered; embedded resource |
| 10 | `python/requirements.txt` | **Done** | Includes `tiktoken==0.9.0`; embedded resource |
| 11 | `schema/context-exporter.schema.json` | **Done** | Full Draft-7 schema with conditional required fields |
| 12 | `config/transformations/entity-dictionary.j2` | Authored | Pending end-to-end sandbox test |
| 13 | `config/transformations/security-model.j2` | Authored | Pending end-to-end sandbox test |
| 14 | `config/transformations/optionsets.j2` | Authored | Pending end-to-end sandbox test |
| 15 | `config/transformations/forms-and-views.j2` | Authored | Pending end-to-end sandbox test |
| 16 | `config/transformations/solution-inventory.j2` | Authored | Pending end-to-end sandbox test |
| 17 | `config/transformations/filters.py` | **Removed** | Replaced by embedded `python/filters.py` |
| 18 | `config/queries/security-roles.fetch.xml` | Exists | Joins still need sandbox verification |
| 19 | `D365ContextExporter.csproj` | **Done** | Python files as `EmbeddedResource`; schema as `None` |

---

## MSBuild — Packing Python and Schema Alongside the Assembly

The `.csproj` uses **embedded resources** for the Python scripts and a `None` item for the schema:

```xml
<ItemGroup>
  <EmbeddedResource Include="python\transform.py" />
  <EmbeddedResource Include="python\filters.py" />
  <EmbeddedResource Include="python\requirements.txt" />
  <None Include="..\schema\**\*" CopyToOutputDirectory="PreserveNewest" Pack="true" PackagePath="content\schema\" />
</ItemGroup>
```

The Python files are embedded in the assembly manifest (resource names `D365ContextExporter.python.transform.py`, etc.). `PythonInvoker.EnsureScripts()` extracts them to `%LOCALAPPDATA%\D365ContextExporter\python\` on every invocation, overwriting stale files. This means the installed scripts are always in sync with the assembly without requiring a writable plugin output directory.

The `schema\` directory is one level above the `.csproj` (at solution root), hence the `..\..\` relative path. Schema files are copied to the output directory and packaged as NuGet content at `content\schema\`.

---

## Testing Strategy

**Unit tests — `ProcessRunner`.**
Wrap `System.Diagnostics.Process` behind an `IProcessFactory` interface so tests can inject a fake that returns a pre-set exit code and stdout/stderr lines without spawning real processes. Cover: exit 0, non-zero exit, timeout, cancellation.

**Unit tests — `PythonInvoker`.**
Inject a fake `IProcessFactory`. Cover: interpreter resolution order (explicit path beats venv beats py launcher beats PATH), non-zero exit surfaced as `PythonInvocationException`, timeout propagated, copy step writes to the correct path.

**Unit tests — `PythonBootstrapHelper`.**
Use `IProcessFactory` fake. Cover: venv already exists (no subprocess call), venv missing and user accepts (two subprocess calls in order), user declines (exception thrown).

**Integration — end-to-end.**
Run manually against a sandbox org using `Sample.context-exporter-config.json`. Inspect `runs/<timestamp>/output.md` and `output/Sample.context.md`. Verify the Markdown is syntactically valid and each section renders as expected.

**Python — snapshot tests.**
Write a single `pytest` file (`python/test_transform.py`) with one test per template. Each test provides a minimal `intermediate.json` fixture (covering the shapes the templates access) and asserts the rendered `output.md` matches a known-good snapshot file checked into `python/tests/snapshots/`. Run with `pytest python/` from the solution root.

---

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| `DisplayName` shape differs between Web API and Metadata SDK | In templates, use `\| default` chaining: `obj.DisplayName.UserLocalizedLabel.Label \| default(obj.DisplayName) \| default("—")`. Confirm shape in sandbox before declaring done. |
| Jinja2 `string` filter not available (it is, but `int \| string` inside a dict lookup needs care) | Use `"" ~ value` for string coercion in templates; document this convention in template comments. |
| Alias attribute names use `.` in the key (e.g. `pub.publisherfriendlyname`) | `EntityJsonSerializer` already stores them verbatim; templates access via `sol["pub.publisherfriendlyname"]` dict syntax, which Jinja2 supports. Confirm in snapshot tests. |
| Python process not found | `PythonBootstrapHelper.EnsureVenv` runs before any run attempt. If bootstrap fails, `Run` is never called. |
| `intermediate.json` too large for Jinja2 to hold in memory | Templates are split by topic; the `maxRecords` cap on individual queries limits worst-case size. If needed, `transform.py` can `stream_templates` in Phase 4. |
