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
  D365ContextExporter/
    D365ContextExporter.csproj
    ContextExporterPluginControl.cs
    ContextExporterPluginControl.Designer.cs
    ContextExporterPluginControl.resx
    Business/
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

The `Utils/`, `Queries/`, `python/`, `Context-Exporter/`, and `schema/` folders are **not created in Phase 1** — they are stubs or out-of-scope for this sprint.

---

## 3. Solution and Project Setup

### 3.1 Solution file

Create `D365ContextExporter.sln` using Visual Studio's "New Solution" wizard or `dotnet new sln`. Add both projects.

### 3.2 Main project — `D365ContextExporter.csproj`

SDK-style project file. Key properties:

```xml
<Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <RootNamespace>D365ContextExporter</RootNamespace>
    <AssemblyName>D365ContextExporter</AssemblyName>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
    <Version>1.0.0</Version>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <Platforms>AnyCPU</Platforms>
    <!-- StyleCop -->
    <CodeAnalysisRuleSet>$(MSBuildThisFileDirectory)stylecop.json</CodeAnalysisRuleSet>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>CS1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="XrmToolBoxPackage" Version="1.2025.*" />
    <PackageReference Include="Microsoft.PowerPlatform.Dataverse.Client" Version="1.*" />
    <PackageReference Include="Newtonsoft.Json" Version="13.*" />
    <PackageReference Include="StyleCop.Analyzers" Version="1.1.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

Notes:
- Use wildcard version ranges (`1.2025.*`) only in the plan — pin to exact versions in `packages.lock.json` once resolved.
- `UseWindowsForms` is required for XrmToolBox plugins.
- `Nullable>enable` is intentional — surfacing null-safety warnings now is cheaper than fixing them in phase 3.

### 3.3 Test project — `D365ContextExporter.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\D365ContextExporter\D365ContextExporter.csproj" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="NUnit" Version="4.*" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.*" />
    <PackageReference Include="Moq" Version="4.*" />
  </ItemGroup>
</Project>
```

---

## 4. StyleCop Configuration

Create `stylecop.json` in the plugin project root. Copy the shared `stylecop.json` from an adjacent plugin project in `tooling/`. If no shared file exists yet, use:

```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    "documentationRules": {
      "xmlHeader": false,
      "documentInterfaces": false
    },
    "orderingRules": {
      "usingDirectivesPlacement": "outsideNamespace"
    },
    "namingRules": {
      "allowCommonHungarianPrefixes": false
    }
  }
}
```

All StyleCop violations must be zero warnings by the end of Phase 1 — do not suppress with `#pragma` without a comment explaining why.

---

## 5. Models

These are plain C# DTOs. No business logic. Deserialised from `*.context-exporter-config.json` by `ExportJob.Load()`.

### 5.1 `Models/ExportJob.cs`

```csharp
using Newtonsoft.Json;

namespace D365ContextExporter.Models
{
    /// <summary>Represents a loaded project configuration file.</summary>
    public sealed class ExportJob
    {
        [JsonProperty("project")]
        public string Project { get; set; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonProperty("transformation")]
        public string Transformation { get; set; } = string.Empty;

        [JsonProperty("queries")]
        public List<QueryDefinition> Queries { get; set; } = new();

        [JsonProperty("python")]
        public PythonSettings Python { get; set; } = new();

        [JsonProperty("frontMatter")]
        public Dictionary<string, string> FrontMatter { get; set; } = new();

        /// <summary>Full path to the config file from which this job was loaded.</summary>
        [JsonIgnore]
        public string ConfigFilePath { get; set; } = string.Empty;

        public static ExportJob Load(string configFilePath)
        {
            var json = File.ReadAllText(configFilePath);
            var job = JsonConvert.DeserializeObject<ExportJob>(json)
                      ?? throw new InvalidOperationException($"Failed to deserialise {configFilePath}");
            job.ConfigFilePath = configFilePath;
            return job;
        }

        public override string ToString() => $"{Project} v{Version} ({Queries.Count} queries)";
    }
}
```

### 5.2 `Models/QueryDefinition.cs`

```csharp
using Newtonsoft.Json;

namespace D365ContextExporter.Models
{
    public sealed class QueryDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>One of: fetchxml, webapi, metadata.</summary>
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>For fetchxml queries: path to the .fetch.xml file (relative to config/queries/).</summary>
        [JsonProperty("source")]
        public string? Source { get; set; }

        /// <summary>For webapi queries: the OData resource path (e.g. "GlobalOptionSetDefinitions").</summary>
        [JsonProperty("path")]
        public string? Path { get; set; }

        [JsonProperty("resultKey")]
        public string ResultKey { get; set; } = string.Empty;

        /// <summary>Optional row cap for large result sets.</summary>
        [JsonProperty("maxRecords")]
        public int? MaxRecords { get; set; }
    }
}
```

### 5.3 `Models/PythonSettings.cs`

```csharp
using Newtonsoft.Json;

namespace D365ContextExporter.Models
{
    public sealed class PythonSettings
    {
        /// <summary>
        /// "auto" (default) uses discovery order: explicit &gt; venv &gt; py launcher &gt; PATH.
        /// Any other value is treated as an absolute path to python.exe.
        /// </summary>
        [JsonProperty("interpreter")]
        public string Interpreter { get; set; } = "auto";

        /// <summary>Path to the venv directory. Supports %LOCALAPPDATA% and similar env vars.</summary>
        [JsonProperty("venv")]
        public string Venv { get; set; } = @"%LOCALAPPDATA%\D365ContextExporter\venv";
    }
}
```

### 5.4 `Models/OutputSettings.cs`

```csharp
using Newtonsoft.Json;

namespace D365ContextExporter.Models
{
    public sealed class OutputSettings
    {
        /// <summary>Attribute logical name fragments that are stripped before JSON serialisation.</summary>
        [JsonProperty("attributeDenyList")]
        public List<string> AttributeDenyList { get; set; } = new()
        {
            "password", "secret", "token", "key",
        };
    }
}
```

---

## 6. Helpers

### 6.1 `Helpers/PathResolver.cs`

The only helper needed in Phase 1. Resolves paths relative to a base directory and expands environment variables. This is used by `ProjectPickerControl` to locate `config/*.context-exporter-config.json`.

```csharp
namespace D365ContextExporter.Helpers
{
    public static class PathResolver
    {
        /// <summary>Expands %VAR% and $VAR$ tokens, then resolves relative to baseDir.</summary>
        public static string Resolve(string path, string baseDir)
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            return Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(baseDir, expanded));
        }

        /// <summary>Returns all *.context-exporter-config.json files under baseDir/config/.</summary>
        public static IEnumerable<string> DiscoverProjectConfigs(string baseDir)
        {
            var configDir = Path.Combine(baseDir, "config");
            if (!Directory.Exists(configDir))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(configDir, "*.context-exporter-config.json",
                SearchOption.TopDirectoryOnly);
        }

        /// <summary>Derives the project display name from the config filename.</summary>
        public static string ProjectNameFromPath(string configFilePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(configFilePath); // e.g. "Contoso.context-exporter-config"
            const string suffix = ".context-exporter-config";
            return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? fileName[..^suffix.Length]
                : fileName;
        }
    }
}
```

Unit tests for this class are written in the test project (see §10).

---

## 7. Business Layer (Stub)

### 7.1 `Business/ExportJobRunner.cs`

In Phase 1 this is a stub that only logs the loaded config. It will be fully implemented in Phase 2.

```csharp
using D365ContextExporter.Models;
using Microsoft.Xrm.Sdk;

namespace D365ContextExporter.Business
{
    /// <summary>Orchestrates a single export run. Phase 1: stub that logs the loaded config.</summary>
    internal sealed class ExportJobRunner
    {
        private readonly IOrganizationService _service;
        private readonly Action<string> _log;

        public ExportJobRunner(IOrganizationService service, Action<string> log)
        {
            _service = service;
            _log = log;
        }

        public void Run(ExportJob job, string baseDir, CancellationToken cancellationToken)
        {
            _log($"[Phase 1 stub] Starting export for project: {job.Project}");
            _log($"  Config: {job.ConfigFilePath}");
            _log($"  Queries defined: {job.Queries.Count}");

            foreach (var query in job.Queries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _log($"  - [{query.Type}] {query.Id} → resultKey: {query.ResultKey}");
            }

            _log($"[Phase 1 stub] Run complete. (No queries executed; Phase 2 will implement this.)");
        }
    }
}
```

---

## 8. UI Controls

All controls are WinForms `UserControl` subclasses. In Phase 1 the layouts are functional but not polished.

### 8.1 `UI/BaseDirectoryPickerControl`

**Purpose:** Displays a label, a read-only text box showing the selected path, and a "Browse…" button. Persists the selected path to `Settings.Default.BaseDirectory`. Raises a `DirectoryChanged` event when the path changes.

**Public surface:**

```csharp
public partial class BaseDirectoryPickerControl : UserControl
{
    public event EventHandler<string>? DirectoryChanged;

    /// <summary>The currently selected base directory. Empty string if not yet set.</summary>
    public string SelectedDirectory { get; private set; } = string.Empty;

    public void LoadSettings();   // call on form load; restores persisted path
    public void SaveSettings();   // call before form close
}
```

**Layout (Designer):**
- `TableLayoutPanel` — 2 columns (star, auto), 1 row.
- Column 0: `TextBox txtBaseDir` (read-only, `Dock = Fill`, `BackColor = SystemColors.Window`).
- Column 1: `Button btnBrowse` (text "Browse…", fixed width 80 px).
- Label above the row: "Context-Exporter base directory:"

**Behaviour:**
- `btnBrowse_Click` opens a `FolderBrowserDialog`. On OK, sets `SelectedDirectory`, updates the text box, persists to `Settings.Default.BaseDirectory`, and fires `DirectoryChanged`.
- `LoadSettings` reads `Settings.Default.BaseDirectory` and sets `SelectedDirectory` if the directory exists.

**Settings:** Add `BaseDirectory` (type `string`, default `""`) to the project's `Settings.settings` file.

### 8.2 `UI/ProjectPickerControl`

**Purpose:** Shows a label and a `ComboBox`. When given a base directory, scans `config/*.context-exporter-config.json` and populates the drop-down with project names. Raises a `ProjectSelected` event when the selection changes.

**Public surface:**

```csharp
public partial class ProjectPickerControl : UserControl
{
    public event EventHandler<ExportJob?>? ProjectSelected;

    /// <summary>The currently loaded job. Null if no project is selected or scan found nothing.</summary>
    public ExportJob? SelectedJob { get; private set; }

    /// <summary>Rescans config/ under baseDir and repopulates the combo box.</summary>
    public void LoadProjects(string baseDir);
}
```

**Layout (Designer):**
- `TableLayoutPanel` — 2 columns (star, auto), 1 row.
- Column 0: `ComboBox cmbProjects` (`DropDownStyle = DropDownList`, `Dock = Fill`).
- Column 1: `Button btnRefresh` (text "↺", fixed width 32 px, tooltip "Refresh project list").
- Label above: "Project:"
- Placeholder text in combo when empty: "(no projects found)"

**Behaviour:**
- `LoadProjects(baseDir)` calls `PathResolver.DiscoverProjectConfigs(baseDir)`, derives display names via `PathResolver.ProjectNameFromPath`, populates `cmbProjects`. Stores the file path as the `ComboBoxItem.Tag`.
- On `cmbProjects.SelectedIndexChanged`, loads the selected config via `ExportJob.Load(path)`, sets `SelectedJob`, and fires `ProjectSelected`.
- `btnRefresh_Click` calls `LoadProjects` with the last-used base directory.
- On any `JsonException` during load, clears `SelectedJob`, sets the combo back to index -1, and shows a `MessageBox` with the error.

### 8.3 `UI/ExportProgressControl` (stub shell)

In Phase 1 this is a skeleton only — just an empty `UserControl` with a `RichTextBox` log panel and an `AppendLog(string message)` public method. Full implementation (progress bars, cancel button) is deferred to Phase 3.

```csharp
public partial class ExportProgressControl : UserControl
{
    public void AppendLog(string message);
    public void ClearLog();
}
```

Layout: single `RichTextBox rtbLog` docked to `Fill`, read-only, monospace font (Consolas 9pt).

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
   - Create a minimal `config/` subfolder with one `.context-exporter-config.json` file:
     ```json
     { "project": "PhaseOneTest", "version": "1.0.0", "transformation": "entity-dictionary.j2", "queries": [] }
     ```
   - The project combo box shows "PhaseOneTest".

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
