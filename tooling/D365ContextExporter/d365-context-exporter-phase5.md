# D365 CE Context Exporter — Phase 5 Plan: .NET-Native Rendering (Scriban + C#)

## What Phase 4 Left Behind

Phase 4 completed packaging, first-run setup, LEGAL.md, config validation, and the GitHub Actions pipeline. One structural dependency was explicitly deferred: the plugin still requires an external Python installation and calls `pip show` before every run to verify that Jinja2 and tiktoken are available. This means:

- Users must install Python 3.x and run `pip install -r requirements.txt` before the plugin works.
- `PythonBootstrapHelper.Check()` executes a subprocess on every run button click.
- `ProcessRunner` shells out to the system Python interpreter.
- `transform.py` uses `argparse` to receive arguments, calls Jinja2, and calls `tiktoken` to count tokens.
- The nuspec ships only the plugin DLL; the Python runtime is entirely the user's responsibility.

## What This Phase Corrects

An IronPython-based embedding was initially planned as Phase 5. That approach was abandoned when Jinja2 3.1.x was found to use `async def` constructs not supported by IronPython 3.4's Python language compatibility layer, making it impossible to run Jinja2 under IronPython without downgrading or patching the library.

This phase takes a different route: replace the Python post-processor entirely with .NET-native equivalents. The Jinja2 rendering engine is replaced by **Scriban** (a .NET template engine with very similar syntax), the `filters.py` logic is ported to a new **`TemplateFilters.cs`** class, and the `transform.py` orchestration logic is absorbed into a new **`TemplateRenderer.cs`** class. No Python runtime of any kind is required.

Token counting (`tiktoken`) is also removed in this phase, as it was the only C-extension dependency and provides marginal value.

---

## Phase 5 Exit Criteria

1. The plugin builds, packs, and installs with no external Python requirement whatsoever.
2. Clicking Run renders the Jinja2-style template in-process via Scriban — no subprocess is spawned.
3. All six templates produce equivalent Markdown output to the Jinja2 originals (verified manually against a sandbox org).
4. All custom filters previously in `filters.py` are available in templates via Scriban's pipe syntax.
5. The token-count blockquote is absent from all output files.
6. `PythonBootstrapHelper`, `ProcessRunner`, `PythonInvoker`, `transform.py`, and `filters.py` are deleted.
7. The `.nupkg` contains only the plugin DLL, PDB, and schema — no Python files of any kind.

---

## Source File Inventory

### C# — New Files

---

#### `Orchestration/TemplateRenderer.cs`

**Description.** In-process Scriban template renderer. This class replaces `PythonInvoker` entirely: it owns the full pipeline that was previously split between `PythonInvoker` (subprocess) and `transform.py` (Python). The public API is intentionally similar to `PythonInvoker.Invoke()` so the change in `ExportJobRunner` is minimal.

**Responsibilities.**

- `Render(ExportJob job, string baseDir, string runDir)` — the single public method.

  1. Resolve `templatePath = <baseDir>\config\transformations\<job.Transformation>`. Throw `FileNotFoundException` with a clear message if absent (same guard as `PythonInvoker`).
  2. Read and parse the template via `Template.Parse(templateText, templatePath)`. If `template.HasErrors`, throw `InvalidOperationException` whose message concatenates all `template.Messages` (file, line, column, and description) separated by newlines.
  3. Load `<runDir>\intermediate.json` and convert it into a Scriban `ScriptObject` via a private recursive `ConvertJToken(JToken)` helper:
     - `JObject` → new `ScriptObject` with each property imported recursively.
     - `JArray` → new `ScriptArray` with each element converted recursively.
     - `JValue` → the underlying .NET value (`string`, `long`, `double`, `bool`, or `null`).
  4. Set `scriptObject["_spec"] = job.Spec` on the root `ScriptObject`.
  5. Call `TemplateFilters.RegisterAll(scriptObject)` to register all custom filters.
  6. Create a `TemplateContext { StrictVariables = true }` (equivalent to Jinja2's `StrictUndefined`). Push the root `ScriptObject` via `context.PushGlobal(scriptObject)`. Set `context.TemplateLoader` to a new `FileSystemTemplateLoader(transformationsDir)` so that `{{ include 'file.j2' }}` directives resolve relative to the transformations folder.
  7. Call `template.Render(templateContext)`. If `templateContext.HasErrors`, throw `InvalidOperationException` with the error details.
  8. Write the rendered string to `<runDir>\output.md` (UTF-8, no BOM).
  9. Log `[Render] output.md written (N bytes)`.

**`FileSystemTemplateLoader`.** A private sealed nested class (or a file-scoped class in the same file) implementing Scriban's `ITemplateLoader`:
- `GetPath(context, callerSpan, templateName)` → `Path.Combine(baseDir, templateName)`.
- `Load(context, callerSpan, templatePath)` → `File.ReadAllText(templatePath, Encoding.UTF8)`.
- `LoadAsync(...)` → `new ValueTask<string>(Load(...))`.

**Key design constraints.**
- No subprocess, no `CancellationToken` parameter (Scriban rendering is synchronous and fast; the bottleneck is query execution, which already flows the token). If future templates are found to be slow, cancellation can be added later.
- `StrictVariables = true` surfaces template authoring errors immediately rather than silently rendering empty strings.
- The `ConvertJToken` helper must handle deeply nested JSON (the intermediate can contain arrays of objects with nested arrays).

**Usings required.**
```csharp
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
```

---

#### `Helpers/TemplateFilters.cs`

**Description.** C# port of all filter functions from `filters.py`. Registered as named delegates on the root `ScriptObject` before rendering. Scriban's pipe syntax `{{ value | filter_name }}` calls `filter_name(value)`; `{{ value | filter_name: arg }}` calls `filter_name(value, arg)`. Filters whose Jinja2 counterparts accepted an optional keyword argument use a defaulted C# parameter.

**Static dictionaries** (mirrors the module-level dicts in `filters.py`):

```csharp
private static readonly IReadOnlyDictionary<int, string> ComponentTypes = new Dictionary<int, string>
{
    [1] = "Table (Entity)", [3] = "Column (Attribute)", [5] = "Relationship",
    [20] = "Option Set (Global)", [50] = "Role", [60] = "Form",
    [90] = "Model-driven App", [91] = "Canvas App", [92] = "Cloud Flow",
};
// AttrTypeAbbrev, FormTypeShort, PluginStageMap, PluginModeMap,
// EnvVarTypeMap, ApiParamTypeMap — same mappings as filters.py.
```

**`RegisterAll(ScriptObject target)`** — static method that assigns each filter by its snake_case name:

```csharp
target["component_type_name"] = (Func<object, string>)ComponentTypeName;
target["schemaname_to_title"]  = (Func<string, string>)SchemaNameToTitle;
target["markdown_table"]       = (Func<ScriptArray, ScriptArray, string>)MarkdownTable;
target["csv_list"]             = (Func<ScriptArray, string?, string>)CsvList;
target["optionset_label"]      = (Func<ScriptArray, object, string>)OptionsetLabel;
target["iso_date"]             = (Func<string, string>)IsoDate;
target["attr_type_abbrev"]     = (Func<object, string>)AttrTypeAbbrev;
target["req_indicator"]        = (Func<object, string>)ReqIndicator;
target["format_forms"]         = (Func<ScriptArray, string>)FormatForms;
target["format_views"]         = (Func<ScriptArray, string>)FormatViews;
target["plugin_stage"]         = (Func<object, string>)PluginStage;
target["plugin_mode"]          = (Func<object, string>)PluginMode;
target["flow_trigger"]         = (Func<string, string>)FlowTrigger;
target["classic_trigger"]      = (Func<ScriptObject, string>)ClassicTrigger;
target["envvar_type"]          = (Func<object, string>)EnvvarType;
target["api_param_type"]       = (Func<object, string>)ApiParamType;
target["pluck"]                = (Func<ScriptArray, string, ScriptArray>)Pluck;
target["group_by_key"]         = (Func<ScriptArray, string, ScriptArray>)GroupByKey;
target["display_label"]        = (Func<object, string, string>)DisplayLabel;
```

**Filter method signatures and semantics.** Each method is a direct port of the corresponding Python function. Input types use `ScriptObject` for JSON objects (implements `IDictionary<string, object>`) and `ScriptArray` for JSON arrays (implements `IList<object>`). Primitive values arrive as `string`, `long`, `double`, or `bool`.

| Filter | C# signature | Notes |
|--------|-------------|-------|
| `ComponentTypeName` | `(object value) → string` | `Convert.ToInt32(value)` then dict lookup |
| `SchemaNameToTitle` | `(string value) → string` | Regex + split, same logic as Python |
| `MarkdownTable` | `(ScriptArray rows, ScriptArray columns) → string` | Builds `\| col \| ... \|` header + separator + rows |
| `CsvList` | `(ScriptArray items, string? attr) → string` | Join extracted values or string representations |
| `OptionsetLabel` | `(ScriptArray options, object value) → string` | Find matching Value in options list |
| `IsoDate` | `(string value) → string` | `DateTime.TryParse` → `yyyy-MM-dd` |
| `AttrTypeAbbrev` | `(object value) → string` | Dict lookup on string of value |
| `ReqIndicator` | `(object value) → string` | `"Required"` → `"**R**"`, `"Recommended"` → `"r"`, else `"-"` |
| `FormatForms` | `(ScriptArray forms) → string` | Group by Name, collect short type codes, format as `Name(types)` |
| `FormatViews` | `(ScriptArray views) → string` | Filter by QueryType, deduplicate, join names |
| `PluginStage` | `(object value) → string` | Dict lookup: 10→PreVal, 20→Pre, 40→Post, 45→Post |
| `PluginMode` | `(object value) → string` | Dict lookup: 0→Sync, 1→Async |
| `FlowTrigger` | `(string name) → string` | Same keyword checks as Python |
| `ClassicTrigger` | `(ScriptObject wf) → string` | Check trigger boolean keys, join with `/` |
| `EnvvarType` | `(object value) → string` | Dict lookup |
| `ApiParamType` | `(object value) → string` | Dict lookup |
| `Pluck` | `(ScriptArray items, string key) → ScriptArray` | Extract unique non-null values for key |
| `GroupByKey` | `(ScriptArray items, string key) → ScriptArray` | Returns `ScriptArray` of `ScriptObject` with `key` and `items` properties (see note) |
| `DisplayLabel` | `(object labelObj, string fallback) → string` | Navigate `UserLocalizedLabel.Label` in nested ScriptObject |

**`GroupByKey` return shape.** In `filters.py`, `group_by_key` returned a list of `(group_value, [items])` tuples. In C#/Scriban, tuples are not idiomatic. Instead, each group is a `ScriptObject` with two properties: `key` (the group value) and `items` (a `ScriptArray` of the grouped items). Template syntax changes accordingly — see the Template Migration section.

**Helper: `GetDictValue(ScriptObject obj, string key)`** — private static helper that does `obj.TryGetValue(key, out var v) ? v : null` with null safety. Used by multiple filters.

**Helper: `ToInt(object value)`** — private static helper: `Convert.ToInt32(value ?? 0)`. Used by dict-lookup filters that receive numeric values from JSON (`long` from `JValue`).

---

### C# — Modified Files

---

#### `Orchestration/ExportJobRunner.cs`

**Current state.** Calls `new PythonInvoker(this.log).Invoke(job, baseDir, runDir, cancellationToken)`, then `AppendTokenCount(runDir)`, then `CopyOutputToSpecDir(...)`, then `PrependLegalNotice(...)`.

**Required changes.**

1. Replace `new PythonInvoker(this.log).Invoke(job, baseDir, runDir, cancellationToken)` with `new TemplateRenderer(this.log).Render(job, baseDir, runDir)`. The call site becomes a single line.
2. Remove the `AppendTokenCount(runDir)` call.
3. Delete the `AppendTokenCount()` private method entirely.
4. Remove the `using` for `PythonInvoker` if it becomes unused after the replacement.

No other changes. `CopyOutputToSpecDir` and `PrependLegalNotice` are unchanged.

---

#### `Helpers/FirstRunHelper.cs`

**Current state.** `DeployedFiles` array includes three Python entries: `transform.py`, `filters.py`, and `requirements.txt`.

**Required changes.**

Remove the three Python tuples from `DeployedFiles`:
```csharp
// Remove these three lines:
("SampleConfig.transformations.transform.py",   @"config\transformations\transform.py"),
("SampleConfig.transformations.filters.py",     @"config\transformations\filters.py"),
("SampleConfig.transformations.requirements.txt", @"config\transformations\requirements.txt"),
```

No other changes. Template `.j2` files remain in `DeployedFiles` — their filenames and locations are unchanged; only their content changes (human task H2).

---

#### `ContextExporterPluginControl.cs`

**Current state.** `btnRun_Click()` calls `PythonBootstrapHelper.Check(job.Python, baseDir, log)` before starting the background task.

**Required changes.** Delete the `PythonBootstrapHelper.Check(...)` call and its surrounding try/catch. Remove the `using` directive for `PythonBootstrapHelper` if it becomes unused.

---

#### `Models/ExportJob.cs`

**Current state.** Has a `Python` property of type `PythonSettings`.

**Required changes.** Update the XML doc comment on `Python` to:
> Gets or sets the Python runtime settings. Ignored from Phase 5 onward; the plugin renders templates in-process via Scriban. Retained for JSON deserialization compatibility with existing config files.

No structural changes. `PythonSettings` class is kept for the same reason.

---

#### `D365ContextExporter.csproj`

**Current state.** References `StyleCop.Analyzers`, `Newtonsoft.Json`, `XrmToolBoxPackage`, `MscrmTools.Xrm.Connection`, `Microsoft.VisualBasic`.

**Required changes.**

1. Add Scriban NuGet reference:
   ```xml
   <PackageReference Include="Scriban" Version="5.12.0" />
   ```
2. No other changes. No IronPython packages were ever merged; this is a clean addition.

---

#### `D365ContextExporter.nuspec`

**Current state (Phase 4).** Ships DLL + PDB + schema.

**Required changes.** None. The nuspec remains exactly as Phase 4 left it — no Python files, no IronPython DLLs, no Lib folder. Scriban is a NuGet dependency whose DLL lands in the build output automatically; it must be present in the `Plugins\` folder. Add it explicitly:

```xml
<file src="bin\Release\net48\Scriban.dll" target="lib\net472\Plugins" />
```

---

### Python Files — Deleted

---

#### `SampleConfig/transformations/transform.py`

**Reason.** The orchestration logic (load JSON, render template, write output) is now in `TemplateRenderer.cs`. There is no Python execution step.

**Action.** Delete the file. Remove its entry from `FirstRunHelper.DeployedFiles` (already covered above).

---

#### `SampleConfig/transformations/filters.py`

**Reason.** All filter functions are ported to `TemplateFilters.cs`. There is no Python filter module.

**Action.** Delete the file. Remove its entry from `FirstRunHelper.DeployedFiles` (already covered above).

---

#### `SampleConfig/transformations/requirements.txt`

**Reason.** No Python packages are required. The file has no runtime role.

**Action.** Delete the file. Remove its entry from `FirstRunHelper.DeployedFiles` (already covered above).

---

### C# — Deleted Files

---

#### `Orchestration/PythonInvoker.cs`

**Reason.** Replaced by `TemplateRenderer.cs`. The `PythonInvocationException` type it contained is also deleted; `TemplateRenderer` throws standard `InvalidOperationException` and `FileNotFoundException` directly.

---

#### `Helpers/PythonBootstrapHelper.cs`

**Reason.** No external Python runtime to check.

---

#### `Helpers/ProcessRunner.cs`

**Reason.** No subprocess is launched anywhere. Confirm no remaining callers before deleting.

---

## Template Migration — Jinja2 to Scriban Syntax

The six `.j2` template files must be rewritten from Jinja2 syntax to Scriban syntax. The files keep their `.j2` extension (so spec configs and `FirstRunHelper` require no changes), but their contents change. This is **Human Task H2**.

### Key syntax differences

| Jinja2 | Scriban | Notes |
|--------|---------|-------|
| `{% for x in items %}` | `{{ for x in items }}` | Scriban uses `{{ }}` for all tags |
| `{% endfor %}` | `{{ end }}` | |
| `{% if cond %}` | `{{ if cond }}` | |
| `{% elif cond %}` | `{{ else if cond }}` | |
| `{% endif %}` | `{{ end }}` | |
| `{% set x = val %}` | `{{ x = val }}` | |
| `{%- -%}` | `{{- -}}` | Whitespace stripping works the same way |
| `{% include 'file' %}` | `{{ include 'file' }}` | Requires `TemplateLoader` on context (already set) |
| `{{ x \| filter }}` | `{{ x \| filter }}` | Pipe syntax identical |
| `{{ x \| filter: arg }}` | `{{ x \| filter arg }}` | Scriban uses space, not colon, for extra args |
| `loop.index` (1-based) | `for.index` (0-based) | Off-by-one: add 1 in templates if needed |
| `loop.first` | `for.first` | |
| `loop.last` | `for.last` | |
| `{% if x is defined %}` | `{{ if x != null }}` | |
| `{{ x \| default('val') }}` | `{{ x ?? 'val' }}` | Scriban null-coalescing operator |
| `{{ x \| upper }}` | `{{ x \| string.upcase }}` | Scriban built-in string functions |
| `{{ x \| lower }}` | `{{ x \| string.downcase }}` | |
| `{{ x \| trim }}` | `{{ x \| string.strip }}` | |
| `{{ x \| join(', ') }}` | `{{ x \| array.join ', ' }}` | |
| `{{ x \| length }}` | `{{ x \| array.size }}` | For arrays |
| `{{ x \| selectattr('k','eq','v') }}` | Use `group_by_key` or iterate + `if` | No direct equivalent; use custom filter |
| `namespace(x=0)` | Use an outer variable | Scriban supports mutable outer variables in loops |
| `{% raw %}` | `{{% raw %}}` | |

### `group_by_key` return shape change

In templates, `group_by_key` previously returned a list of `(group_value, items)` tuples accessed as `for key, items in result`. The C# version returns a `ScriptArray` of `ScriptObject` with `key` and `items` properties. Templates using this filter must change from:

```jinja2
{% for group_val, group_items in entities | group_by_key('solutionId') %}
```

to:

```scriban
{{ for group in entities | group_by_key 'solutionId' }}
  {{ group.key }} — {{ group.items | array.size }} items
{{ end }}
```

---

## Data Flow — Updated for Phase 5

Token counting is removed. The rendering pipeline in `ExportJobRunner.Run()`:

```
ExportJobRunner.Run()
  ...
  ├─ ConfigValidator.Validate()
  ├─ [queries executed, intermediate.json written]
  ├─ TemplateRenderer.Render()       [Scriban renders template in-process]
  │     ConvertJToken(intermediate.json) → ScriptObject
  │     TemplateFilters.RegisterAll()
  │     template.Render(context)     → output.md written
  │     (no token_count.txt written)
  ├─ CopyOutputToSpecDir()           [copies output.md → output/<spec>.context.md]
  └─ PrependLegalNotice()            [prepends LEGAL.md if job.Legal is set]
```

Final content order in `output/<SpecName>.context.md`:
1. LEGAL.md content (if `legal` is set)
2. Blank line separator
3. Rendered Markdown from the Scriban template

---

## Human Tasks

| # | Task | Notes |
|---|------|-------|
| H1 | **Port all six `.j2` templates to Scriban syntax** | Use the syntax table above. Files: `entity-dictionary.j2`, `forms-and-views.j2`, `optionsets.j2`, `security-model.j2`, `solution-inventory.j2`, `solutions-reference.j2`. Update `group_by_key` call sites for the new return shape. Keep the `.j2` extension. |
| H2 | **Verify `TemplateFilters` output matches `filters.py` output** | Run each filter function against representative inputs (copy them from a real intermediate.json) and compare the C# output to the Python output. Pay special attention to `format_forms`, `format_views`, `group_by_key`, and `display_label` — these have the most complex logic. |
| H3 | **End-to-end test all three sample specs** | After implementing, connect to a sandbox org and run `EntityDictionary`, `SecurityModel`, and `SolutionsReference`. Verify Markdown structure is correct and no token-count blockquote appears. |
| H4 | **Remove `"python"` field from sample spec JSON files** | The `"python": {"interpreter": "auto"}` field in all three `SampleConfig/*.context-exporter-config.json` files is a no-op. Remove it to keep configs clean. Update `context-exporter.schema.json` to mark `python` as deprecated. |

---

## Artifacts Inventory

| # | Artifact | Action | Notes |
|---|----------|--------|-------|
| 1 | `Orchestration/TemplateRenderer.cs` | **New** | Scriban-based in-process renderer; replaces `PythonInvoker`; includes `FileSystemTemplateLoader` nested class |
| 2 | `Helpers/TemplateFilters.cs` | **New** | C# port of `filters.py`; 19 named filters registered as delegates on `ScriptObject` |
| 3 | `Orchestration/ExportJobRunner.cs` | **Modify** | Replace `PythonInvoker.Invoke()` with `TemplateRenderer.Render()`; remove `AppendTokenCount()` |
| 4 | `Helpers/FirstRunHelper.cs` | **Modify** | Remove `transform.py`, `filters.py`, `requirements.txt` from `DeployedFiles` |
| 5 | `ContextExporterPluginControl.cs` | **Modify** | Remove `PythonBootstrapHelper.Check()` call |
| 6 | `Models/ExportJob.cs` | **Modify** | Update XML doc on `Python` property |
| 7 | `D365ContextExporter.csproj` | **Modify** | Add `Scriban` NuGet reference |
| 8 | `D365ContextExporter.nuspec` | **Modify** | Add `Scriban.dll` to `<files>` |
| 9 | `SampleConfig/transformations/*.j2` (×6) | **Modify (human task H1)** | Port from Jinja2 syntax to Scriban syntax; keep `.j2` extension |
| 10 | `SampleConfig/transformations/transform.py` | **Delete** | Logic moved to `TemplateRenderer.cs` |
| 11 | `SampleConfig/transformations/filters.py` | **Delete** | Logic moved to `TemplateFilters.cs` |
| 12 | `SampleConfig/transformations/requirements.txt` | **Delete** | No Python packages needed |
| 13 | `Orchestration/PythonInvoker.cs` | **Delete** | Replaced by `TemplateRenderer.cs` |
| 14 | `Helpers/PythonBootstrapHelper.cs` | **Delete** | No longer needed |
| 15 | `Helpers/ProcessRunner.cs` | **Delete** | No subprocess launched anywhere |

---

## Testing Strategy

**Unit tests — `TemplateRenderer`.**
- A valid Scriban template with `{{ _spec }}` produces `output.md` containing the spec name.
- A template parse error (e.g. unclosed `{{ for }}`) throws `InvalidOperationException` before rendering.
- An undefined variable with `StrictVariables = true` throws during render.
- A missing template file throws `FileNotFoundException`.
- A deeply nested JSON structure (object containing array of objects) is accessible in the template without error.
- An `{{ include 'file.j2' }}` directive in a template resolves correctly from the transformations directory.

**Unit tests — `TemplateFilters`.**
- `ComponentTypeName(1)` → `"Table (Entity)"`.
- `ComponentTypeName(99)` → `"Unknown (99)"`.
- `SchemaNameToTitle("AccountId")` → `"Account Id"`.
- `MarkdownTable` with one header column and two rows produces a valid three-line Markdown table.
- `MarkdownTable` with an empty rows array returns `"_No data._"`.
- `IsoDate("2026-04-21T14:22:05Z")` → `"2026-04-21"`.
- `IsoDate("")` → `""`.
- `GroupByKey` with three items grouped into two keys produces a `ScriptArray` of two `ScriptObject` entries with `key` and `items` properties.
- `Pluck` returns unique non-null values only.
- `DisplayLabel` navigates `UserLocalizedLabel.Label` and returns the fallback when absent.
- `ReqIndicator("Required")` → `"**R**"`.
- `ReqIndicator("SystemRequired")` → `"-"` (not bold — only user-set Required is bold).

**Unit tests — `ExportJobRunner` (existing).**
- Existing tests that mocked `PythonInvoker` must be updated to mock or stub `TemplateRenderer`. Since `TemplateRenderer` reads from the filesystem, tests should use a temp directory with a minimal template and intermediate JSON.

**Manual smoke test.**
- Point the plugin at a clean temp directory, accept first-run setup.
- Confirm `config\transformations\` contains only `.j2` files — no `.py` or `.txt` files.
- Run `EntityDictionary` against a sandbox org.
- Confirm `output\EntityDictionary.context.md` is produced and contains LEGAL.md header and valid Markdown table output.
- Confirm no `token_count.txt` is written to the run directory.

---

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Scriban syntax differences cause template authoring friction for users who know Jinja2 | Document the syntax table in the README. The differences are mechanical (tag delimiters, built-in function names) and are applied once during H1; end users editing templates will quickly adapt. |
| `StrictVariables = true` surfaces undefined variable errors that Jinja2 silently rendered as empty strings | This is intentional — it forces template correctness. Existing templates may reference variables that are conditionally absent; use `{{ x ?? '' }}` or `{{ if x != null }}` guards as needed during H1. |
| `group_by_key` return shape change breaks all templates that use it | All templates are being rewritten in H1 anyway. Document the new shape clearly in H1 instructions. |
| `MarkdownTable` filter receives a `ScriptArray` of `ScriptObject` — key access uses lowercase property names from JSON | Scriban lowercases property names by default when importing via `ScriptObject`. Intermediate JSON keys are already lowercase (e.g. `logicalname`). Verify filter access patterns match the actual JSON key casing. |
| Scriban `5.x` NuGet introduces a breaking API vs older versions | Pin the version in the csproj (`Version="5.12.0"`); do not use floating versions. |
| `include` directives in templates fail if `TemplateLoader` is not set | `TemplateRenderer` always sets `context.TemplateLoader` before rendering. If templates do not use `include`, there is no risk. If they do, the loader is already in place. |
| Template errors produce unhelpful messages | `TemplateRenderer` concatenates all `template.Messages` including file, line, and column. `ExceptionOperations`-style detail is not needed because Scriban's own error format is already human-readable. |
