# D365 CE Context Exporter — Phase 1 Detailed Plan

**Phase:** 1 of 4 — Skeleton and Wiring  
**Sprint goal:** Plugin installs into XrmToolBox, loads, connects to a Dataverse org, shows the project list, and prints the loaded project config to the log panel.  
**Artefacts from the master plan addressed in this phase:** #1, #2, #3, #10, #12, #15, #16, #19, #31 (partial), plus stub shells for #4 and #17.

---

## 1. Prerequisites

Before writing any C#:

| Item | Check |
|------|-------|
| Visual Studio 2022 (any edition) with **.NET desktop development** workload | Required |
| XrmToolBox installed locally (latest stable, ≥ 1.2025.10.74) | Required — to host the plugin during manual testing |
| .NET Framework 4.8 Developer Pack | Required — SDK-style projects targeting `net48` need the targeting pack; ships with VS 2022 |
| Git checkout of the main D365CE repo on a feature branch (`tooling/d365-content-exporter`) | In progress — already exists |

No Python, no Dataverse sandbox connection is needed to complete Phase 1 — the stub runner does not execute queries.

---

## 2. Repository Layout

Create the following directory tree. All paths are relative to the repo root.

```
/tooling/D365ContextExporter/
  D365ContextExporter.sln
  config/                             ← sample base directory (solution root = Context-Exporter working dir)
    Sample.context-exporter-config.json
    queries/
      security-roles.fetch.xml
      solutions.fetch.xml
    transformations/
      entity-dictionary.j2
      forms-and-views.j2
      optionsets.j2
      security-model.j2
      solution-inventory.j2
  D365ContextExporter/
    D365ContextExporter.csproj
    ContextExporterPluginControl.cs
    ContextExporterPluginControl.Designer.cs
    ContextExporterPluginControl.resx
    Orchestration/
      ExportJobRunner.cs              ← stub in phase 1
    Models/
      ExportJob.cs
      QueryDefinition.cs
      PythonSettings.cs
      OutputSettings.cs
    Helpers/
      PathResolver.cs
    UI/
      BaseDirectoryPickerControl.cs
      BaseDirectoryPickerControl.Designer.cs
      BaseDirectoryPickerControl.resx
      ProjectPickerControl.cs
      ProjectPickerControl.Designer.cs
      ProjectPickerControl.resx
      ExportProgressControl.cs        ← stub shell only
      ExportProgressControl.Designer.cs
      ExportProgressControl.resx
    stylecop.json
    D365ContextExporter.nuspec
  D365ContextExporter.Tests/
    D365ContextExporter.Tests.csproj
    HelpersTests/
      PathResolverTests.cs
```

The `Utils/`, `Queries/`, `python/`, and `schema/` folders are **not created in Phase 1** — they are stubs or out-of-scope for this sprint. The `config/` folder at the solution root is already in place (moved from `sample-config/` during Phase 1).

---

## 3. Solution and Project Setup

Two projects in the solution: the main plugin (`D365ContextExporter.csproj`) and the test project (`D365ContextExporter.Tests.csproj`).

Key properties for the main project: `TargetFramework=net48`, `UseWindowsForms=true`, `Nullable=enable`. Packages: `XrmToolBoxPackage`, `Microsoft.PowerPlatform.Dataverse.Client`, `Newtonsoft.Json`, `StyleCop.Analyzers` (analyzer-only).

Test project targets `net48` as well. Packages: `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`, `Moq`.

---

## 4. StyleCop Configuration

`stylecop.json` at the plugin project root. Settings: XML doc headers off, `documentInterfaces` off, `usingDirectivesPlacement` outside namespace, Hungarian prefixes disallowed.

Zero StyleCop warnings is the bar for Phase 1 — no `#pragma` suppressions without an explanatory comment.

---

## 5. Models

Plain C# DTOs in `Models/`. No business logic. Deserialised from `*.context-exporter-config.json` by `ExportJob.Load(path)`.

- **`ExportJob`** — top-level config: `Project`, `Version`, `Transformation`, `Queries`, `Python`, `FrontMatter`. `ConfigFilePath` is set after load (`[JsonIgnore]`). Static `Load(path)` reads the file and throws `InvalidOperationException` on null deserialisation.
- **`QueryDefinition`** — per-query config: `Id`, `Type` (fetchxml / webapi / metadata), `Source` (FetchXML path), `Path` (Web API resource), `ResultKey`, optional `MaxRecords`.
- **`PythonSettings`** — `Interpreter` (default `"auto"`) and `Venv` (default `%LOCALAPPDATA%\D365ContextExporter\venv`).
- **`OutputSettings`** — `AttributeDenyList` defaulting to `["password", "secret", "token", "key"]`.

---

## 6. Helpers

### 6.1 `Helpers/PathResolver.cs`

Static helper used by `ProjectPickerControl` to locate `config/*.context-exporter-config.json` and by the runner to resolve query/template paths.

- `Resolve(path, baseDir)` — expands `%VAR%` tokens then resolves relative paths against `baseDir`; returns absolute paths unchanged.
- `DiscoverProjectConfigs(baseDir)` — enumerates `*.context-exporter-config.json` under `<baseDir>/config/`; returns empty if the directory does not exist.
- `ProjectNameFromPath(configFilePath)` — strips the `.context-exporter-config` suffix from the filename to yield the display name.

Unit tests for this class are written in the test project (see §10).

---

## 7. Orchestration Layer (Stub)

### 7.1 `Orchestration/ExportJobRunner.cs`

Stub in Phase 1 — logs the loaded config without executing any queries. Constructor takes `IOrganizationService` and an `Action<string>` log delegate. `Run(job, baseDir, cancellationToken)` logs the project name, config path, and each query definition, then writes a completion line. Full implementation deferred to Phase 2.

---

## 8. UI Controls

All controls are WinForms `UserControl` subclasses. Phase 1 layouts are functional but not polished.

### 8.1 `UI/BaseDirectoryPickerControl`

Read-only `TextBox` + "Browse…" `Button` in a `TableLayoutPanel`. Clicking Browse opens a `FolderBrowserDialog`; on confirmation it updates `SelectedDirectory`, persists to `Settings.Default.BaseDirectory`, and fires `DirectoryChanged`. `LoadSettings()` / `SaveSettings()` restore and persist the path between sessions.

### 8.2 `UI/ProjectPickerControl`

`ComboBox` (`DropDownList`) + "↺" refresh `Button`. `LoadProjects(baseDir)` calls `PathResolver.DiscoverProjectConfigs`, populates the combo with display names (file path stored as `Tag`). On selection change, calls `ExportJob.Load(path)`, sets `SelectedJob`, and fires `ProjectSelected`. JSON errors clear the selection and show a `MessageBox`.

### 8.3 `UI/ExportProgressControl` (stub shell)

Single `RichTextBox rtbLog` docked to `Fill`, read-only, Consolas 9 pt. Exposes `AppendLog(message)` and `ClearLog()`. Progress bars and cancel button deferred to Phase 3.

---

## 9. Main Plugin Control

### 9.1 `ContextExporterPluginControl.cs`

This is the root `UserControl` that XrmToolBox hosts. It must:

- Inherit from `PluginControlBase` (from `XrmToolBoxPackage`).
- Implement `IXrmToolBoxPluginControl` (satisfied by `PluginControlBase`).
- Compose `BaseDirectoryPickerControl`, `ProjectPickerControl`, and `ExportProgressControl` as child controls.

**Key wiring:**

```csharp
public partial class ContextExporterPluginControl : PluginControlBase
{
    private CancellationTokenSource? _cts;

    // Called by XrmToolBox when the user connects to an org or switches connection.
    public override void UpdateConnection(IOrganizationService newService,
        ConnectionDetail detail, string actionName, object parameter)
    {
        base.UpdateConnection(newService, detail, actionName, parameter);
        progressControl.AppendLog($"Connected: {detail.WebApplicationUrl}");
        projectPicker.LoadProjects(dirPicker.SelectedDirectory); // refresh in case base dir was already set
    }

    private void dirPicker_DirectoryChanged(object sender, string newDir)
    {
        projectPicker.LoadProjects(newDir);
        progressControl.AppendLog($"Base directory set: {newDir}");
    }

    private void projectPicker_ProjectSelected(object sender, ExportJob? job)
    {
        btnRun.Enabled = job != null && Service != null;
        if (job != null)
        {
            progressControl.AppendLog($"Project selected: {job}");
        }
    }

    private void btnRun_Click(object sender, EventArgs e)
    {
        if (Service == null || projectPicker.SelectedJob == null) return;

        btnRun.Enabled = false;
        _cts = new CancellationTokenSource();
        var job = projectPicker.SelectedJob;
        var baseDir = dirPicker.SelectedDirectory;

        Task.Run(() =>
        {
            var runner = new ExportJobRunner(Service, msg => BeginInvoke(() => progressControl.AppendLog(msg)));
            runner.Run(job, baseDir, _cts.Token);
        }, _cts.Token).ContinueWith(t =>
        {
            BeginInvoke(() =>
            {
                btnRun.Enabled = true;
                if (t.IsFaulted)
                {
                    progressControl.AppendLog($"ERROR: {t.Exception?.GetBaseException().Message}");
                }
            });
        });
    }
}
```

**Layout (Designer):**

Use a `TableLayoutPanel` as the root container:
- Row 0 (auto height): `BaseDirectoryPickerControl dirPicker`
- Row 1 (auto height): `ProjectPickerControl projectPicker`
- Row 2 (auto height): `FlowLayoutPanel` containing `Button btnRun` (text "Run Export", `Enabled = false`) and future controls.
- Row 3 (star height): `ExportProgressControl progressControl`

Padding: 8 px on all sides. All controls `Dock = Fill` within their cell.

**Form load / close:**

```csharp
private void ContextExporterPluginControl_Load(object sender, EventArgs e)
{
    dirPicker.LoadSettings();
    if (!string.IsNullOrEmpty(dirPicker.SelectedDirectory))
    {
        projectPicker.LoadProjects(dirPicker.SelectedDirectory);
    }
}

private void ContextExporterPluginControl_VisibleChanged(object sender, EventArgs e)
{
    if (!Visible) dirPicker.SaveSettings();
}
```

**XrmToolBox plugin metadata class:**

In addition to the control, create a companion class (often called `D365ContextExporterPlugin`) in the project root:

```csharp
[Export(typeof(IXrmToolBoxPlugin))]
public sealed class D365ContextExporterPlugin : PluginBase
{
    public override IXrmToolBoxPluginControl GetControl() => new ContextExporterPluginControl();
}
```

This class requires the `[Export]` attribute from `System.ComponentModel.Composition` (MEF), which `XrmToolBoxPackage` brings in transitively.

---

## 10. Unit Tests

### 10.1 `HelpersTests/PathResolverTests.cs`

Cover these cases in NUnit:

| Test | Input | Expected |
|------|-------|----------|
| Resolve absolute path unchanged | `C:\abs\path`, any baseDir | `C:\abs\path` |
| Resolve relative path joined to baseDir | `queries/foo.xml`, `C:\base` | `C:\base\queries\foo.xml` |
| Expand `%TEMP%` env var | `%TEMP%\foo`, any baseDir | `<expanded temp>\foo` |
| DiscoverProjectConfigs — no config dir | baseDir with no `config/` subfolder | returns empty |
| DiscoverProjectConfigs — finds two configs | two `.context-exporter-config.json` files present | returns both paths |
| ProjectNameFromPath — standard suffix | `Contoso.context-exporter-config.json` | `"Contoso"` |
| ProjectNameFromPath — no suffix | `SomethingElse.json` | `"SomethingElse"` |

Use `Path.GetTempPath()` + `Directory.CreateTempSubdirectory()` for file-system tests; clean up in `[TearDown]`.

---

## 11. NuSpec

Create `D365ContextExporter.nuspec` in the plugin project root. This will be used in Phase 4 for Tool Library submission, but should be correct from the start so the `dotnet pack` dry run in the Phase 1 exit check passes.

```xml
<?xml version="1.0"?>
<package>
  <metadata>
    <id>D365ContextExporter</id>
    <version>$version$</version>
    <title>D365 CE Context Exporter</title>
    <authors>YourName</authors>
    <owners>YourName</owners>
    <description>
      XrmToolBox plugin that executes FetchXML and Web API queries against a Dataverse environment,
      serialises results to an intermediate JSON file, and invokes a Python/Jinja2 post-processor
      to produce Markdown grounding files for non-agentic AI assistants.
    </description>
    <tags>XrmToolBox Documentation AI Markdown D365 Dataverse Grounding</tags>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <projectUrl>https://github.com/YourOrg/YourRepo</projectUrl>
  </metadata>
  <files>
    <file src="bin\Release\net48\D365ContextExporter.dll" target="lib\net48" />
    <!-- python/, Context-Exporter/, schema/ added in Phase 3 -->
  </files>
</package>
```

The `$version$` token is replaced by MSBuild during `dotnet pack` from the `<Version>` property in the `.csproj`.

---

## 12. Exit Criteria and Verification Steps

Perform these steps manually before closing the Phase 1 PR:

1. **Build succeeds with zero warnings** (including StyleCop warnings):
   ```
   dotnet build D365ContextExporter.sln --configuration Release
   ```

2. **Tests pass**:
   ```
   dotnet test D365ContextExporter.Tests/D365ContextExporter.Tests.csproj
   ```

3. **NuGet pack completes**:
   ```
   dotnet pack D365ContextExporter/D365ContextExporter.csproj --configuration Release
   ```
   The resulting `.nupkg` appears in `bin/Release/`.

4. **Plugin loads in XrmToolBox:**
   - Copy the `Release` output folder to `%APPDATA%\MscrmTools\XrmToolBox\Plugins\D365ContextExporter\`.
   - Launch XrmToolBox.
   - Open the plugin from the tool list — it should appear as "D365 CE Context Exporter".
   - The plugin panel loads without exceptions.

5. **Base directory picker works:**
   - Click "Browse…" and select a folder that contains a `config/` subfolder.
   - The path appears in the text box.
   - On next restart of the plugin (close and reopen without restarting XrmToolBox) the path is restored from settings.

6. **Project list populates:**
   - Point the plugin at the solution root (`tooling/D365ContextExporter/`) as the base directory — the `config/` folder with `Sample.context-exporter-config.json` is already there.
   - The project combo box shows "Sample".

7. **Connection and stub run:**
   - Connect to any Dataverse org in XrmToolBox.
   - Select the "PhaseOneTest" project.
   - Click "Run Export".
   - The log panel shows the stub output lines (project name, query count, completion message) — no exception.

---

## 13. What is Explicitly Out of Scope for Phase 1

The following items appear in the master plan but are **not started in this phase**:

- `FetchXmlQueryRunner`, `WebApiQueryRunner`, `MetadataQueryRunner` (Phase 2)
- `IntermediateJsonBuilder`, full `ExportJobRunner` (Phase 2)
- `EntityJsonSerializer` (Phase 2)
- `ProcessRunner`, `PythonInvoker` (Phase 3)
- `OutputPreviewControl` beyond a placeholder class (Phase 3)
- All Python scripts and Jinja2 templates (Phase 3)
- JSON Schema file (Phase 4)
- Config validation / error messages (Phase 4)
- README, screenshots, MRU list (Phase 4)
- CI pipeline YAML (Phase 4)
