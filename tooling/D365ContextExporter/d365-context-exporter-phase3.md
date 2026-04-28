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
3. `output/<ProjectName>.context.md` — copied from `output.md` by the plugin after a successful run

The plugin UI must also show the generated files with actions to open them.

---

## Source File Inventory

### C# — New Files

---

#### `Orchestration/PythonInvoker.cs`

**Description.** Locates a Python interpreter and invokes `transform.py` as a child process, passing the paths to `intermediate.json`, the Jinja2 template, the output directory, and the project name. Streams stdout and stderr to the log delegate in real time. Enforces a configurable timeout and surfaces failures as typed exceptions.

**Responsibilities.**
- Resolve the Python interpreter to use, following discovery order:
  1. `PythonSettings.Interpreter` (when not `"auto"`) — treated as an absolute path to `python.exe`.
  2. `Scripts\python.exe` inside the expanded `PythonSettings.Venv` path — the venv's own interpreter.
  3. `py.exe -3` — the Windows Python launcher.
  4. `python` on `PATH` — last resort.
- Verify the resolved interpreter exists before launching; surface a clear error if none is found.
- Locate `transform.py` relative to the plugin assembly directory (packed by MSBuild into `python\transform.py` alongside the `.dll`).
- Build the command-line call:
  ```
  <python> python\transform.py
      --input <runDir>\intermediate.json
      --template <baseDir>\config\transformations\<job.Transformation>
      --out <runDir>
      --project <job.Project>
  ```
- Launch the process with `ProcessRunner`, redirecting stdout and stderr. Pipe each line to the `log` delegate so the user sees real-time progress.
- Enforce a default 5-minute timeout (overridable from `PythonSettings.TimeoutSeconds` if added to the model in a future phase). Kill the process forcibly on timeout.
- Throw `PythonInvocationException` (a typed exception carrying exit code, stdout snippet, and stderr snippet) when the process exits non-zero.
- After a successful run, call `CopyOutputToProjectDir(runDir, baseDir, job.Project)` — copies `<runDir>\output.md` to `<baseDir>\output\<ProjectName>.context.md`, creating the `output\` directory if needed.

**How it is used from configuration.**
- `python.interpreter` — `"auto"` enables discovery; any other string is an absolute path.
- `python.venv` — expands `%LOCALAPPDATA%` and other `%VAR%` tokens via `PathResolver.Resolve`; the plugin looks for `Scripts\python.exe` inside this directory.
- `transformation` — the `.j2` filename passed as `--template`; resolved relative to `<baseDir>\config\transformations\`.
- `project` — passed as `--project`; determines the final output filename.

**Called from.** `ExportJobRunner.Run()` after `IntermediateJsonBuilder.WriteIntermediate()` succeeds.

---

#### `UI/OutputPreviewControl.cs` (+ `.Designer.cs`)

**Description.** A WinForms `UserControl` displayed after a successful export run. Shows the paths to the two output files (`output.md` in the run directory, and `output/<ProjectName>.context.md`) with actions to open them. Provides a prominent call-to-action directing the user to upload the grounding file to their AI assistant.

**Responsibilities.**
- Accept paths for the run-specific `output.md` and the project-level `<ProjectName>.context.md` via `ShowResult(string runOutputPath, string projectOutputPath)`.
- Display both paths in read-only text boxes.
- Provide three action buttons per file: **Open** (shell-launches the file with the default editor), **Reveal** (opens Explorer with the file selected), and **Copy Path** (copies the path to the clipboard).
- Display a labelled `LinkLabel` or `RichTextBox` banner reading something like:
  > Upload `<ProjectName>.context.md` to claude.ai, ChatGPT, or your AI assistant as a grounding file.
- Hide itself (or show a placeholder) when no run has completed yet.
- Log nothing itself; all logging goes through `ExportProgressControl`.

**How it is used from configuration.** The project name in `project` determines which file name is highlighted as the upload target. No other config keys affect this control directly.

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
- Begin async reads of stdout and stderr immediately after `Start()`, forwarding each line to the respective callback.
- Block until the process exits or `timeoutMs` elapses. On timeout, call `Kill()` and throw `TimeoutException`.
- Observe `cancellationToken`; on cancellation, call `Kill()` and throw `OperationCanceledException`.
- Return the process `ExitCode`.

**How it is used from configuration.** Not configured directly; all parameters come from `PythonInvoker`, which resolves config values before calling `ProcessRunner`.

**Called from.** `PythonInvoker` exclusively.

---

#### `Orchestration/ExportJobRunner.cs`

**Current state.** Fully implemented but stops after `IntermediateJsonBuilder.WriteIntermediate()`. Never invokes Python or copies output.

**Required changes.**
1. After `WriteIntermediate`, construct a `PythonInvoker` and call its `Invoke(job, baseDir, runDir, cancellationToken)` method.
2. If `PythonInvoker.Invoke` throws, log the error and surface it to the caller — do not swallow it.
3. After a successful invocation, log the path of the copied `output/<ProjectName>.context.md`.
4. Pass the run directory back to the caller (or raise an event) so the UI can show `OutputPreviewControl`.

No structural changes to query execution or intermediate JSON building.

---

#### `UI/ExportProgressControl.cs`

**Current state.** Log-only stub. The `ExportProgressControl` comment in the source reads "Phase 1 stub — progress bars and cancel button are deferred to Phase 3."

**Required changes.**
1. Add a **Cancel** button. When clicked, raise a `CancelRequested` event (the plugin control owns the `CancellationTokenSource` and calls `cts.Cancel()` in response).
2. Add a `ProgressBar` or `Label` showing the current query index out of total (e.g., "Query 2 / 5").
3. Expose `SetProgress(int current, int total, string queryId)` so `ExportJobRunner` can update progress via the `BeginInvoke`-safe log delegate pattern already in use.
4. Expose `SetRunning(bool running)` to enable/disable the Cancel button and toggle a spinner or status label.

---

#### `ContextExporterPluginControl.cs`

**Current state.** Wires up directory picker, project picker, run button, and log. Calls `ExportJobRunner.Run()` on a background `Task`.

**Required changes.**
1. Add `OutputPreviewControl` to the form layout (hidden until a run completes).
2. In the `Task.ContinueWith` callback, if the run succeeded, call `outputPreview.ShowResult(runOutputPath, projectOutputPath)` and make the control visible.
3. Wire the `ExportProgressControl.CancelRequested` event to call `cts.Cancel()`.
4. On run start, call `progressControl.SetRunning(true)`, `outputPreview.Hide()`. On completion (success or failure), call `progressControl.SetRunning(false)`.
5. Add a first-run Python bootstrap check: before calling `ExportJobRunner.Run()`, call `PythonBootstrapHelper.EnsureVenvAsync(job.Python, log)` (see below).

---

### C# — New Helper

---

#### `Helpers/PythonBootstrapHelper.cs`

**Description.** Checks whether the configured virtual environment exists and offers to create it on first run.

**Responsibilities.**
- `EnsureVenv(PythonSettings settings, string requirementsTxtPath, Action<string> log)` — static method.
- Expand the venv path using `PathResolver.Resolve`.
- If `Scripts\python.exe` exists inside the venv, return immediately (nothing to do).
- If the venv does not exist, show a `MessageBox` dialog:
  > "No Python virtual environment was found at `<venvPath>`. Would you like the plugin to create one and install the required packages? This requires Python 3.11+ on PATH."
- On **Yes**: resolve the base Python interpreter (`py -3` or `python`); call `ProcessRunner.Run` twice — first `python -m venv <venvPath>` then `<venvPath>\Scripts\pip install -r <requirementsTxtPath>`. Stream output to the log.
- On **No**: throw `InvalidOperationException` with a clear message pointing the user to the README.
- Surface any `ProcessRunner` failure as a clear dialog and rethrow.

**How it is used from configuration.**
- `python.venv` — the target venv directory.
- `python.interpreter` — used if not `"auto"` to pick the base interpreter for `venv` creation.

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
  - `--project` — project name (injected into the template context as `_project`)
- Load `intermediate.json` with `json.load()`.
- Build a Jinja2 `Environment` with:
  - `loader = FileSystemLoader(os.path.dirname(template_path))` so templates can `{% include %}` siblings.
  - `undefined = StrictUndefined` to surface typos in templates immediately.
  - All custom filters from `filters.py` registered via `env.filters.update(get_filters())`.
- Render the template with the full intermediate JSON dict as the context (so `entityAttributes`, `securityRoles`, `_meta`, etc. are all top-level variables).
- Write the rendered string to `<out>/output.md` in UTF-8.
- Print a summary line to stdout on success: `[transform] output.md written ({len} bytes)`.
- Exit with code 0 on success, 1 on any error (exceptions are caught at the top level, printed to stderr, and cause exit 1).

**How it is used from configuration.**
- The `transformation` key in the project config (`*.context-exporter-config.json`) names the `.j2` file that `PythonInvoker` passes as `--template`. `transform.py` itself is not referenced in config; it is always the entry point.
- Template context variables match the `resultKey` values defined in the `queries` array plus `_meta` (always present).

**Invoked by.** `PythonInvoker.cs` via `ProcessRunner.cs`.

---

#### `python/filters.py`

**Current state.** Bare Python module with a `COMPONENT_TYPES` dict and a single `component_type_name(n)` helper function. No Jinja2 filter registration.

**Description.** Custom Jinja2 filters reusable across all templates. Exposes a single `get_filters()` function that returns a dict of `{filter_name: callable}` for `transform.py` to register.

**Responsibilities.**
Implement and register the following filters:

| Filter name | Signature | Purpose |
|---|---|---|
| `component_type_name` | `(value: int) -> str` | Maps a Dataverse solution component type integer to a readable label (uses the existing `COMPONENT_TYPES` dict). |
| `schemaname_to_title` | `(value: str) -> str` | Converts a camelCase or underscore-separated schema name to a Title Case display label (e.g. `account_name` → `Account Name`). |
| `markdown_table` | `(rows: list[dict], columns: list[str]) -> str` | Renders a Markdown table from a list of dicts using the specified columns as headers and column order. |
| `csv_list` | `(items: list, attr: str = None) -> str` | Joins a list (or list of dicts) into a comma-separated string; if `attr` is given, uses that dict key per item. |
| `optionset_label` | `(options: list[dict], value: int) -> str` | Given an option set's `Options` list, returns the label for a given integer value (or the value itself as a fallback). |
| `iso_date` | `(value: str) -> str` | Formats an ISO-8601 date string as `YYYY-MM-DD` (strips the time portion). |

Expose `get_filters() -> dict` as the public API so `transform.py` calls `env.filters.update(get_filters())`.

**How it is used from configuration.** Not directly configured. All templates in `config/transformations/` can use these filters by name (e.g. `{{ role.name | schemaname_to_title }}`). No config changes are required to add or use filters.

---

#### `python/requirements.txt`

**Description.** Pinned pip dependencies for the virtual environment.

**Contents.**
```
Jinja2==3.1.4
MarkupSafe==2.1.5
PyYAML==6.0.1
python-dateutil==2.9.0.post0
```

**How it is used from configuration.** `PythonBootstrapHelper` passes the path to this file when running `pip install -r requirements.txt`. The file is packed into the plugin output alongside `transform.py` and `filters.py` via MSBuild `CopyToOutputDirectory` items in the `.csproj`.

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
- Activated by setting `"transformation": "entity-dictionary.j2"` in the project config.
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
- `project` (string, required)
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
  └─ PythonBootstrapHelper.EnsureVenv()          [new — check/create venv]
  └─ ExportJobRunner.Run()
       ├─ FetchXmlQueryRunner.Run()               [phase 2]
       ├─ WebApiQueryRunner.Run()                 [phase 2]
       ├─ MetadataQueryRunner.Run()               [phase 2]
       ├─ EntityJsonSerializer.SerializeEntities() [phase 2]
       ├─ IntermediateJsonBuilder.WriteQueryResult() ×N  [phase 2]
       ├─ IntermediateJsonBuilder.WriteIntermediate()    [phase 2]
       └─ PythonInvoker.Invoke()                  [new — phase 3]
            └─ ProcessRunner.Run()                [new — replaces stub]
                 └─ python transform.py           [new]
                      └─ filters.py              [completed]
                      └─ *.j2 template           [authored, tested]
            └─ CopyOutputToProjectDir()           [new]
  └─ OutputPreviewControl.ShowResult()            [new]
```

---

## Artifacts Inventory

| # | Artifact | Status | Action |
|---|----------|--------|--------|
| 1 | `Orchestration/PythonInvoker.cs` | Not created | **Create** |
| 2 | `Helpers/ProcessRunner.cs` | Stub | **Implement** |
| 3 | `Helpers/PythonBootstrapHelper.cs` | Not created | **Create** |
| 4 | `Orchestration/ExportJobRunner.cs` | Implemented | **Extend** (add Python step) |
| 5 | `UI/OutputPreviewControl.cs` | Not created | **Create** |
| 6 | `UI/ExportProgressControl.cs` | Log-only stub | **Extend** (cancel button, progress) |
| 7 | `ContextExporterPluginControl.cs` | Implemented | **Extend** (wire new controls) |
| 8 | `python/transform.py` | Not created | **Create** |
| 9 | `python/filters.py` | Stub (wrong location) | **Complete and move** |
| 10 | `python/requirements.txt` | Not created | **Create** |
| 11 | `schema/context-exporter.schema.json` | Not created | **Create** |
| 12 | `config/transformations/entity-dictionary.j2` | Authored | **Test and fix** |
| 13 | `config/transformations/security-model.j2` | Authored | **Test and fix** |
| 14 | `config/transformations/optionsets.j2` | Authored | **Test and fix** |
| 15 | `config/transformations/forms-and-views.j2` | Authored | **Test and fix** |
| 16 | `config/transformations/solution-inventory.j2` | Authored | **Test and fix** |
| 17 | `config/transformations/filters.py` | Stub (wrong location) | **Remove** (replaced by `python/filters.py`) |
| 18 | `config/queries/security-roles.fetch.xml` | Exists | **Verify/update joins** |
| 19 | `D365ContextExporter.csproj` | Exists | **Add MSBuild items for `python/` and `schema/`** |

---

## MSBuild — Packing Python and Schema Alongside the Assembly

The `.csproj` must include the following `None` items so that `python\` and `schema\` are copied to the output directory and packaged into the `.nupkg`:

```xml
<ItemGroup>
  <None Include="python\**\*" CopyToOutputDirectory="PreserveNewest" Pack="true" PackagePath="content\python\" />
  <None Include="schema\**\*" CopyToOutputDirectory="PreserveNewest" Pack="true" PackagePath="content\schema\" />
</ItemGroup>
```

`PythonInvoker` resolves `transform.py` relative to `Assembly.GetExecutingAssembly().Location`, which points to the plugin output directory where XrmToolBox copies everything. The `python\` subfolder will be present there after this MSBuild change.

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
