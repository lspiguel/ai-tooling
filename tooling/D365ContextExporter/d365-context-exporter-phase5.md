# D365 CE Context Exporter — Phase 5 Plan: Embedded IronPython, No External Runtime

## What Phase 4 Left Behind

Phase 4 completed packaging, first-run setup, LEGAL.md, config validation, and the GitHub Actions pipeline. One structural dependency was explicitly left in place: the plugin still requires an external Python installation and calls `pip show` before every run to verify that Jinja2 and tiktoken are available. This means:

- Users must install Python 3.x and run `pip install -r requirements.txt` before the plugin works.
- `PythonBootstrapHelper.Check()` executes a subprocess on every run button click.
- `ProcessRunner` shells out to the system Python interpreter.
- `transform.py` uses `argparse` to receive arguments via command-line, and calls `tiktoken` to count tokens.
- The nuspec ships only the plugin DLL; the Python runtime is entirely the user's responsibility.

Phase 5 eliminates this dependency. IronPython 3 is embedded via NuGet; Jinja2 and MarkupSafe are bundled as a zip archive embedded in the assembly. No external Python installation is required. Token counting is removed entirely.

---

## Phase 5 Exit Criteria

1. The plugin builds, packs, and installs with no external Python requirement.
2. Clicking Run executes `transform.py` in-process via IronPython — no subprocess is spawned.
3. Jinja2 templates render correctly; all filters in `filters.py` produce the same output as before.
4. The token-count blockquote is absent from all output files; no `token_count.txt` sidecar is written.
5. `transform.py` can still be executed standalone with CPython (for developer testing) via `--input / --template / --out / --spec` arguments.
6. The `.nupkg` contains the IronPython runtime DLLs, the IronPython standard library, and the plugin DLL. No loose Python files are required on the user's machine.
7. `PythonBootstrapHelper`, `ProcessRunner`, and all subprocess infrastructure are deleted.

---

## Source File Inventory

### C# — New Files

---

#### `Helpers/LoggingStream.cs`

**Description.** A write-only `Stream` that buffers UTF-8 bytes from IronPython's `sys.stdout` / `sys.stderr` and fires a log delegate for each complete line. Required by `engine.Runtime.IO.SetOutput()` and `engine.Runtime.IO.SetErrorOutput()`.

**Responsibilities.**

- Implements `CanWrite = true`; all other capability flags return `false`.
- `Write(byte[] buffer, int offset, int count)` — decodes the bytes as UTF-8, appends to an internal `StringBuilder`, and emits every complete newline-terminated line to the log delegate (stripping the trailing `\r\n` or `\n`). Any trailing partial line stays buffered until the next write or `Flush()`.
- `Flush()` — emits any remaining buffered content as a final log line, then clears the buffer.
- Constructor takes a single `Action<string> log` delegate.
- `Read`, `Seek`, `SetLength`, `Position`, and `Length` throw `NotSupportedException`.

**Key design constraint.** IronPython may write a single `print()` call as multiple partial `Write` calls; the stream must buffer correctly and never emit partial lines to the log.

---

### C# — Modified Files

---

#### `Orchestration/PythonInvoker.cs`

**Current state.** Resolves a system Python interpreter via `PythonBootstrapHelper.ResolveExecutable()`, constructs a command-line argument string, and delegates to `ProcessRunner.Run()`. Raises `PythonInvocationException` on non-zero exit.

**Required changes — complete rewrite.** The file keeps its name and exception type; the implementation is replaced entirely.

**New implementation.**

`PythonInvocationException` — unchanged; keep as-is. The exception remains useful for wrapping IronPython runtime failures.

`PythonInvoker` — replace the body:

1. **Engine creation.** Call `Python.CreateEngine()` (from `IronPython.Hosting`) to obtain a `ScriptEngine`. Create once per `Invoke()` call; do not cache across calls.

2. **Output capture.** Create two `LoggingStream` instances — one prefixing `[Python]`, one prefixing `[Python:ERR]` — and wire them:
   ```csharp
   engine.Runtime.IO.SetOutput(stdoutStream, Encoding.UTF8);
   engine.Runtime.IO.SetErrorOutput(stderrStream, Encoding.UTF8);
   ```

3. **sys.path setup.** Compute paths in this order:
   - `pluginDir` = `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)` — the directory containing the plugin DLL (i.e., XrmToolBox's `Plugins\` folder at runtime).
   - `libDir` = `Path.Combine(pluginDir, "Lib")` — IronPython standard library, shipped in the nupkg.
   - `pylibsZip` = `Path.Combine(transformationsDir, "pylibs.zip")` — Jinja2 + MarkupSafe, deployed by `FirstRunHelper` alongside `transform.py`.
   - `transformationsDir` = `Path.Combine(baseDir, "config", "transformations")` — `transform.py`, `filters.py`, and Jinja2 templates.

   Add all four to `engine.GetSearchPaths()` (skip any that do not exist on disk) and call `engine.SetSearchPaths(paths)`. The order matters: stdlib first, then pylibs zip, then transformations dir. Because `pylibsZip` is inside `transformationsDir`, both are on the path: the zip is used for `import jinja2` / `import markupsafe`; the directory itself is used for `import filters`.

4. **Guard checks.** Verify that `transform.py` exists at `transformationsDir\transform.py`; verify that the template file `job.Transformation` exists in `transformationsDir`. Throw `FileNotFoundException` with the same messages as the current implementation if either is absent.

5. **Scope injection.** Create a scope via `engine.CreateScope()` and inject four string variables:
   ```csharp
   scope.SetVariable("input_path", Path.Combine(runDir, "intermediate.json"));
   scope.SetVariable("template",   Path.Combine(transformationsDir, job.Transformation));
   scope.SetVariable("out_dir",    runDir);
   scope.SetVariable("spec",       job.Spec);
   ```

6. **Cancellation check.** Call `cancellationToken.ThrowIfCancellationRequested()` immediately before `ExecuteFile`. IronPython does not honour `CancellationToken` mid-execution; the pre-flight check prevents starting a long run after the user has already cancelled.

7. **Execution and error handling.**
   ```csharp
   try
   {
       engine.ExecuteFile(transformScript, scope);
   }
   catch (OperationCanceledException)
   {
       throw;
   }
   catch (Exception ex)
   {
       var ops = engine.GetService<ExceptionOperations>();
       var detail = ops != null ? ops.FormatException(ex) : ex.ToString();
       this.log($"[Python:ERR] {detail}");
       throw new PythonInvocationException(-1,
           "IronPython execution failed. Check the log for details.", detail);
   }
   ```
   `ExceptionOperations.FormatException()` produces a Python-style traceback (file, line number, exception type) that is far more useful than a raw .NET exception message.

8. **Remove.** The `Invoke` signature stays the same (`ExportJob job, string baseDir, string runDir, CancellationToken cancellationToken`). The `job.Python` property is read but not used (the interpreter field is silently ignored); no reference to `PythonBootstrapHelper` or `ProcessRunner` remains.

**New usings required.**
```csharp
using System.Reflection;
using System.Text;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
```

---

#### `Orchestration/ExportJobRunner.cs`

**Current state.** Calls `invoker.Invoke(...)`, then `AppendTokenCount(runDir)`, then `CopyOutputToSpecDir(...)`, then `PrependLegalNotice(...)`.

**Required changes.**

1. Remove the `AppendTokenCount(runDir)` call from `Run()`.
2. Delete the `AppendTokenCount()` private method entirely (reads `token_count.txt`, prepends the `> Token count (gpt-4o): N` blockquote to `output.md`).

No other changes. `CopyOutputToSpecDir` and `PrependLegalNotice` are unchanged. The `PythonInvoker` is still instantiated the same way — its public API did not change.

---

#### `Helpers/FirstRunHelper.cs`

**Current state.** `DeployedFiles` array deploys Python scripts, templates, queries, and sample specs. `DeployReferenceConfig` extracts them to `baseDir`.

**Required changes.**

1. **Deploy `pylibs.zip` to the base directory's transformations folder.** `pylibs.zip` is not user-editable and must always reflect the currently installed plugin version. Handle it separately from `DeployedFiles` using a new private static method `WriteBinaryResource` and always overwrite regardless of the `overwrite` flag. Call it from `DeployReferenceConfig` after the main `DeployedFiles` loop:

   ```csharp
   // pylibs.zip is always overwritten — it is never user-modified.
   var pylibsDest = Path.Combine(baseDir, "config", "transformations", "pylibs.zip");
   WriteBinaryResource(
       "Lspiguel.Xrm.D365ContextExporter.PyLibs.pylibs.zip",
       pylibsDest,
       log,
       "[Setup] Deployed");
   ```

   Add `WriteBinaryResource` as a private static helper alongside the existing `WriteResource`:

   ```csharp
   private static void WriteBinaryResource(string resourceName, string destPath, Action<string> log, string logPrefix)
   {
       using var stream = Asm.GetManifestResourceStream(resourceName)
           ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
       Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
       using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write);
       stream.CopyTo(fs);
       log($"{logPrefix}: {destPath}");
   }
   ```

   `WriteBinaryResource` uses `Stream.CopyTo` rather than `StreamReader` to avoid UTF-8 decoding corrupting binary zip content.

2. **Remove `requirements.txt` from `DeployedFiles`.** Jinja2 is now bundled in `pylibs.zip`; users no longer need to install it. Remove the tuple `("SampleConfig.transformations.requirements.txt", @"config\transformations\requirements.txt")` from the array.

3. No other changes to `FirstRunHelper`. The existing `version.txt` / `CheckVersion` / `DeployReferenceConfig(overwrite: true)` upgrade flow automatically redeploys `pylibs.zip` whenever the plugin version changes — no new mechanism required.

---

#### `ContextExporterPluginControl.cs`

**Current state.** `btnRun_Click()` calls `PythonBootstrapHelper.Check(job.Python, baseDir, log)` before starting the background task.

**Required changes.**

1. Delete the `PythonBootstrapHelper.Check(...)` call and its surrounding try/catch from `btnRun_Click()`. The check is no longer needed; IronPython is always available.
2. Remove any `using` directive for `PythonBootstrapHelper` if it becomes unused.

No other changes.

---

#### `Models/ExportJob.cs`

**Current state.** Has a `Python` property of type `PythonSettings` (`[JsonProperty("python")]`).

**Required changes.**

1. Keep the `Python` property and `PythonSettings` class **unchanged** so that existing config files with `"python": {"interpreter": "auto"}` continue to deserialise without errors. The property is now silently ignored at runtime.
2. Update the XML doc comment on `Python` to read:
   > Gets or sets the Python runtime settings. Ignored in Phase 5 and later; IronPython is the built-in runtime.

No structural changes to the class.

---

#### `D365ContextExporter.csproj`

**Current state.** References `StyleCop.Analyzers`, `Newtonsoft.Json`, `XrmToolBoxPackage`, `MscrmTools.Xrm.Connection`. Has `EmbeddedResource` glob for `SampleConfig\**\*.*`. References `Microsoft.VisualBasic`.

**Required changes.**

1. Add IronPython NuGet references:
   ```xml
   <PackageReference Include="IronPython" Version="3.4.1" />
   <PackageReference Include="IronPython.StdLib" Version="3.4.1" />
   ```
   `IronPython.StdLib` is a content package; it copies the Python standard library to `bin\<config>\net48\Lib\` at build time.

2. Add `pylibs.zip` as an `EmbeddedResource`:
   ```xml
   <EmbeddedResource Include="PyLibs\pylibs.zip">
     <LogicalName>Lspiguel.Xrm.D365ContextExporter.PyLibs.pylibs.zip</LogicalName>
   </EmbeddedResource>
   ```
   `PyLibs\pylibs.zip` must be created as a human task (see H1) and committed to the repository.

3. No other changes.

---

#### `D365ContextExporter.nuspec`

**Current state.** `<files>` block ships the DLL, PDB, and schema. No IronPython entries.

**Required changes.** Extend `<files>` with IronPython runtime DLLs and the standard library:

```xml
<!-- IronPython runtime -->
<file src="bin\Release\net48\IronPython.dll"                   target="lib\net472\Plugins" />
<file src="bin\Release\net48\IronPython.Modules.dll"           target="lib\net472\Plugins" />
<file src="bin\Release\net48\Microsoft.Dynamic.dll"            target="lib\net472\Plugins" />
<file src="bin\Release\net48\Microsoft.Scripting.dll"          target="lib\net472\Plugins" />
<file src="bin\Release\net48\Microsoft.Scripting.Metadata.dll" target="lib\net472\Plugins" />

<!-- IronPython standard library (required by Jinja2 at runtime) -->
<file src="bin\Release\net48\Lib\**\*" target="lib\net472\Plugins\Lib" />
```

The `pylibs.zip` (Jinja2 + MarkupSafe) is embedded in the DLL as a manifest resource and deployed by `FirstRunHelper` to `<baseDir>\config\transformations\` — it does not need to appear in the nuspec.

**Notes on the Lib folder size.** The full `IronPython.StdLib` content is approximately 15–20 MB. If package size is a concern, unused stdlib modules (e.g. `email`, `http`, `html`, `xmlrpc`, `lib2to3`, `unittest`) can be excluded from the glob via `<Exclude>` patterns in the nuspec or by pruning the output folder in a pre-pack MSBuild target. For Phase 5, ship the full stdlib and address pruning in a later optimisation pass.

---

#### `SampleConfig/transformations/transform.py`

**Current state.** Uses `argparse` to receive `--input`, `--template`, `--out`, `--spec`. Calls `tiktoken` for token counting and writes `token_count.txt`.

**Required changes.**

Restructure into a `run()` function called from two entry points: the embedded (IronPython scope) path and the standalone (argparse) path. Remove all token-counting code.

```python
"""D365 Context Exporter — Jinja2 template orchestrator."""

import json
import os
import sys

try:
    from jinja2 import Environment, FileSystemLoader, StrictUndefined
    import filters as _filters_module
except ImportError as e:
    print(f"[transform] Import error: {e}", file=sys.stderr)
    print(
        "[transform] Ensure pylibs.zip is extracted and on sys.path, "
        "or activate a venv with Jinja2 installed.",
        file=sys.stderr,
    )
    sys.exit(1)


def run(input_path, template_path, out_dir, spec_name):
    with open(input_path, encoding="utf-8") as f:
        context = json.load(f)

    context["_spec"] = spec_name

    template_dir = os.path.dirname(os.path.abspath(template_path))
    template_name = os.path.basename(template_path)

    env = Environment(
        loader=FileSystemLoader(template_dir),
        undefined=StrictUndefined,
        keep_trailing_newline=True,
    )
    env.filters.update(_filters_module.get_filters())

    template = env.get_template(template_name)
    rendered = template.render(**context)

    output_path = os.path.join(out_dir, "output.md")
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(rendered)

    print(f"[transform] output.md written ({len(rendered.encode('utf-8'))} bytes)")


# Embedded mode: C# host injects these four variables into the module scope before execution.
if "input_path" in globals():
    run(input_path, template, out_dir, spec)  # noqa: F821
elif __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(
        description="Render a Jinja2 template from intermediate.json."
    )
    parser.add_argument("--input",    required=True, help="Path to intermediate.json")
    parser.add_argument("--template", required=True, help="Path to the .j2 template file")
    parser.add_argument("--out",      required=True, help="Output directory for output.md")
    parser.add_argument("--spec",     required=True, help="Spec name injected as _spec")
    args = parser.parse_args()
    try:
        run(args.input, args.template, args.out, args.spec)
    except Exception as exc:
        print(f"[transform] ERROR: {exc}", file=sys.stderr)
        import traceback
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)
```

**How the entry-point selection works.** When `PythonInvoker` calls `engine.ExecuteFile(path, scope)`, IronPython executes the script in the provided scope. The four variables injected via `scope.SetVariable()` are visible as module-level globals. The `if "input_path" in globals():` branch fires immediately, calling `run()` with the injected values. When run standalone with CPython, none of the four variables are in `globals()`, so the `elif __name__ == "__main__":` branch fires instead and parses `sys.argv`.

**Removed.** `_count_tokens()`, the `token_count.txt` write block, the `tiktoken` import, and the `argparse` logic from `main()` (which is now replaced by the two-branch pattern above).

---

#### `SampleConfig/transformations/requirements.txt`

**Current state.** Contains `Jinja2>=3.1.4` and `tiktoken>=0.9.0`.

**Required changes.** Replace contents with:

```
# Development reference only — not used by the plugin.
# The plugin bundles Jinja2 via PyLibs\pylibs.zip (IronPython mode).
# Install these packages if you want to run transform.py standalone with CPython:
Jinja2>=3.1.4
```

Remove `tiktoken` entirely. The file is kept as a development convenience but is no longer deployed to the user's base directory (see FirstRunHelper changes).

---

### C# — Deleted Files

---

#### `Helpers/PythonBootstrapHelper.cs`

**Reason.** This class exists solely to verify that a system Python interpreter and pip packages are installed. With IronPython embedded, there is no external runtime to verify. The `ResolveExecutable()` method is also removed from `PythonInvoker`'s dependency chain.

**Action.** Delete the file. Confirm no other file references `PythonBootstrapHelper` before deleting.

---

#### `Helpers/ProcessRunner.cs`

**Reason.** This class launches external processes. Phase 5 removes all subprocess usage; `PythonInvoker` runs in-process and `PythonBootstrapHelper` (the only other caller) is deleted.

**Action.** Delete the file. Confirm no other file references `ProcessRunner` before deleting.

---

### Non-Code Files — New

---

#### `PyLibs/pylibs.zip`

**Description.** A zip archive containing the pure-Python distributions of Jinja2 and MarkupSafe, structured so that `import jinja2` and `import markupsafe` resolve correctly when the zip is on `sys.path`. Committed to the repository and embedded as a manifest resource in the DLL. Deployed by `FirstRunHelper.DeployReferenceConfig()` to `<baseDir>\config\transformations\pylibs.zip` alongside `transform.py` and `filters.py`. Always overwritten on both first-run and upgrade so it tracks the plugin version.

**Contents structure.**
```
pylibs.zip
├── jinja2/
│   ├── __init__.py
│   ├── environment.py
│   ├── ... (all Jinja2 source files)
└── markupsafe/
    ├── __init__.py
    ├── _native.py
    └── ... (all MarkupSafe pure-Python source files)
```

**Important — MarkupSafe C extension.** MarkupSafe ships a C extension (`_speedups`) that IronPython cannot load. MarkupSafe's `__init__.py` gracefully falls back to `_native.py` when the C extension is absent. The zip should **not** include any `.pyd` or `.so` files.

**Creation procedure.** See Human Task H1.

**Approximate size.** Jinja2 3.1.x + MarkupSafe 2.x pure Python: ~800 KB uncompressed, ~300 KB compressed.

---

## Data Flow — Updated for Phase 5

Token counting is removed. The final rendering pipeline in `ExportJobRunner.Run()` is:

```
ExportJobRunner.Run()
  ...
  ├─ ConfigValidator.Validate()
  ├─ [queries executed, intermediate.json written]
  ├─ PythonInvoker.Invoke()          [runs transform.py in-process via IronPython]
  │     engine.ExecuteFile(transform.py, scope)
  │       → jinja2 renders template → writes output.md
  │     (no token_count.txt written)
  ├─ CopyOutputToSpecDir()           [copies output.md → output/<spec>.context.md]
  └─ PrependLegalNotice()            [prepends LEGAL.md if job.Legal is set]
```

Final content order in `output/<SpecName>.context.md`:
1. LEGAL.md content (if `legal` is set)
2. Blank line separator
3. Rendered Markdown from the Jinja2 template

The `> Token count (gpt-4o): N` blockquote is **gone** from all output files.

---

## Human Tasks

| # | Task | Notes |
|---|------|-------|
| H1 | **Create `PyLibs\pylibs.zip`** | Run the commands below, verify no `.pyd`/`.so` files are included, and commit the zip to the repository. This must be done before the project can build with the embedded resource. |
| H2 | **Verify Jinja2 compatibility under IronPython 3.4** | Run a smoke test: `engine.ExecuteFile("transform.py", scope)` with a simple template and confirm the output is correct. Pay special attention to `datetime.fromisoformat()` in `filters.py` — confirm it is implemented in IronPython 3.4's stdlib. |
| H3 | **Test all three sample specs end-to-end** | After Phase 5 is implemented, connect to a sandbox org and run `EntityDictionary`, `SecurityModel`, and `SolutionsReference`. Verify all three produce valid Markdown output without the token-count blockquote. |
| H4 | **Remove `"python"` field from sample spec files** | The `"python": {"interpreter": "auto"}` field in all three `SampleConfig/*.context-exporter-config.json` files is now a no-op. Remove it to keep configs clean, and update `context-exporter.schema.json` to mark `python` as deprecated (or remove it). |
| H5 | **Evaluate stdlib pruning** | After confirming the plugin works end-to-end, measure the installed size. If the `Lib\` folder is unacceptably large, identify and exclude modules not reachable by Jinja2's import graph (`email`, `http`, `html`, `xmlrpc`, `lib2to3`, `unittest`, `tkinter`, etc.). |

**Commands for H1 — creating `pylibs.zip`.**

```powershell
# From the solution root (tooling\D365ContextExporter\)
$tempDir = "pylibs_temp"
Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
python -m pip install Jinja2==3.1.4 MarkupSafe==2.1.5 `
    --target $tempDir --no-compile --only-binary=:all:

# Remove .dist-info folders, __pycache__, compiled extensions, and pip/setuptools
Get-ChildItem $tempDir -Directory | Where-Object { $_.Name -match "\.dist-info$|bin$" } |
    Remove-Item -Recurse -Force
Get-ChildItem $tempDir -Recurse -Filter "__pycache__" -Directory |
    Remove-Item -Recurse -Force
Get-ChildItem $tempDir -Recurse -Include "*.pyd","*.so" | Remove-Item -Force

# Zip and place in the project
$projectDir = "D365ContextExporter"
New-Item -ItemType Directory -Path "$projectDir\PyLibs" -Force | Out-Null
Compress-Archive -Path "$tempDir\*" -DestinationPath "$projectDir\PyLibs\pylibs.zip" -Force
Remove-Item $tempDir -Recurse -Force
```

---

## Artifacts Inventory

| # | Artifact | Action | Notes |
|---|----------|--------|-------|
| 1 | `Helpers/LoggingStream.cs` | **New** | Write-only `Stream` that routes IronPython stdout/stderr to the log delegate |
| 2 | `Orchestration/PythonInvoker.cs` | **Rewrite** | Replace subprocess launch with `Python.CreateEngine()` + `engine.ExecuteFile()`; keep class name and `PythonInvocationException` |
| 3 | `Orchestration/ExportJobRunner.cs` | **Modify** | Remove `AppendTokenCount()` call and method |
| 4 | `Helpers/FirstRunHelper.cs` | **Modify** | Add `WriteBinaryResource` helper; deploy `pylibs.zip` to `config\transformations\` (always overwrite); remove `requirements.txt` from `DeployedFiles` |
| 5 | `ContextExporterPluginControl.cs` | **Modify** | Remove `PythonBootstrapHelper.Check()` call from `btnRun_Click()` |
| 6 | `Models/ExportJob.cs` | **Modify** | Update XML doc on `Python` property to note it is ignored |
| 7 | `D365ContextExporter.csproj` | **Modify** | Add `IronPython` + `IronPython.StdLib` NuGet refs; add `PyLibs\pylibs.zip` as `EmbeddedResource` |
| 8 | `D365ContextExporter.nuspec` | **Modify** | Add IronPython DLLs + `Lib\**\*` to `<files>` |
| 9 | `SampleConfig/transformations/transform.py` | **Modify** | Remove argparse/tiktoken; add two-branch embedded/standalone entry point; keep `run()` function |
| 10 | `SampleConfig/transformations/requirements.txt` | **Modify** | Remove tiktoken; add comment that it is for development reference only |
| 11 | `Helpers/PythonBootstrapHelper.cs` | **Delete** | No longer needed |
| 12 | `Helpers/ProcessRunner.cs` | **Delete** | No longer needed |
| 13 | `PyLibs/pylibs.zip` | **New (human task H1)** | Jinja2 + MarkupSafe pure-Python, zipped for `sys.path` import; embedded as manifest resource |

---

## Testing Strategy

**Unit tests — `LoggingStream`.**
- Single complete line emits exactly one log call with the line content (no newline character).
- Two lines in one `Write` call emit two separate log calls.
- A partial line followed by a second `Write` completing it emits one call after the second write.
- `Flush()` emits any remaining buffered content.
- An empty write or a write of only `\n` does not crash.

**Unit tests — `PythonInvoker` (integration-style, requires IronPython).**
- A valid `transform.py` with a trivial template produces `output.md` in the run directory.
- A syntax error in `transform.py` raises `PythonInvocationException`.
- A missing `transform.py` raises `FileNotFoundException`.
- A missing template file raises `FileNotFoundException`.
- stdout from `print()` in `transform.py` reaches the log delegate.

**Unit tests — `FirstRunHelper` pylibs deployment.**
- After `DeployReferenceConfig`, `<baseDir>\config\transformations\pylibs.zip` exists.
- `pylibs.zip` is overwritten even when `overwrite: false` (first-run path) — it is never skipped.
- `pylibs.zip` is overwritten when `overwrite: true` (upgrade path).
- `DeployReferenceConfig` no longer deploys `requirements.txt` to the base directory.

**Manual smoke test.**
- Point the plugin at a clean temp directory, accept first-run setup.
- Confirm `pylibs.zip` appears in `config\transformations\` alongside `transform.py`.
- Confirm `requirements.txt` is **not** deployed to `config\transformations\`.
- Run `EntityDictionary` against a sandbox org.
- Confirm `output\EntityDictionary.context.md` is produced and contains no token-count blockquote.
- Confirm the log shows `[Python] output.md written (N bytes)`.

**Regression — existing Phase 3/4 tests.**
- Python snapshot tests (Phase 3) should still pass when run with CPython directly against `transform.py` — confirm the standalone `__main__` branch still works.
- `ConfigValidator` tests are unaffected.
- `FirstRunHelper` first-run and upgrade tests should still pass; add one test for `EnsurePyLibs`.

---

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| `datetime.fromisoformat()` not implemented in IronPython 3.4's stdlib | `filters.py` uses this in `_iso_date()`. If absent, IronPython will raise `AttributeError` at the first template that uses the `iso_date` filter. Mitigation: human task H2 tests this before Phase 5 ships. If broken, add a shim in `filters.py`: `fromisoformat = getattr(datetime, 'fromisoformat', lambda s: datetime.strptime(s[:10], '%Y-%m-%d'))`. |
| Jinja2 3.1.x uses Python 3.10+ features not yet in IronPython 3.4 | Jinja2 3.1.x targets Python 3.7+; no walrus operators, no `match/case`. Minimal risk. If an incompatibility surfaces, pin to an older compatible Jinja2 version. |
| MarkupSafe C extension import failure produces an error instead of falling back | MarkupSafe's `__init__.py` wraps the C extension import in a `try/except` and falls back to `_native.py`. This is by design. Verify the fallback is exercised during H2 smoke test. |
| `engine.ExecuteFile` cannot be cancelled mid-execution | IronPython has no built-in cooperative cancellation. The `CancellationToken` is checked before execution begins. For long-running templates, the only recourse is closing the IronPython engine's streams. Document this limitation; add a note to README. |
| IronPython `Lib\` folder ships `IronPython.StdLib` in `Debug` configuration | `IronPython.StdLib` is a content package — it copies files at build time regardless of configuration. The nuspec references `bin\Release\net48\Lib\`. Ensure CI builds in Release mode before packing. |
| `pylibs.zip` becomes stale relative to the deployed IronPython version | If `IronPython` is bumped in the NuGet reference, re-create `pylibs.zip` and commit it. Jinja2 and MarkupSafe versions are pinned; document the pinned versions in `PyLibs\README.txt`. Because `pylibs.zip` is always overwritten by `DeployReferenceConfig`, the next plugin launch after an upgrade will automatically redeploy the new zip. |
| `pluginDir` path wrong in non-XrmToolBox hosting (tests, dev console) | In unit tests, `Assembly.GetExecutingAssembly().Location` points to the test output directory, not XrmToolBox's `Plugins\`. If `libDir` does not exist there, `PythonInvoker` silently skips it from `sys.path`. Tests must add the stdlib path explicitly or use a test base directory that has a pre-deployed `pylibs.zip`. |
| Increased nupkg size (from ~2 MB to ~20–25 MB) may be rejected by XrmToolBox NuGet feed or surprise users | XrmToolBox has no hard size limit. Communicate the size increase in the release notes. If pruning is required, address it in a follow-up (human task H5). |
