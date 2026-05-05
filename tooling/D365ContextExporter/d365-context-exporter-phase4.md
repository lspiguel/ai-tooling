# D365 CE Context Exporter — Phase 4 Plan: Polish, Packaging, and Distribution

## What Phase 3 Left Behind

Phase 3 completed the end-to-end pipeline: Python is invoked, `output.md` is rendered and copied. Several structural and distribution concerns were explicitly deferred:

- `transform.py` and `filters.py` are always extracted from the embedded assembly to `%LOCALAPPDATA%\D365ContextExporter\python\`, overwriting any user edits. They should instead live in the base directory's `config\transformations\` folder and never be overwritten once placed there.
- There is no first-run experience. A user who points the plugin at a new empty folder sees an empty spec list with no guidance.
- The `LEGAL.md` prepend feature is not implemented. The `legal` field does not exist in `ExportJob` or the JSON schema.
- Config validation is limited to Newtonsoft.Json deserialization; missing required fields and bad paths produce cryptic runtime errors.
- The sample specs (`Sample.context-exporter-config.json`, `Sample2.context-exporter-config.json`) still live in `config/` at the solution root, outside the C# project, and cannot be embedded with the DLL for distribution. The `python/` folder likewise lives outside the canonical distribution path.
- The `.nuspec` `<files>` section contains only a placeholder comment; the plugin cannot be packed or published.
- There is no GitHub Actions workflow.
- There is no `README.md`.

Specifically, at the end of Phase 3:

- `PythonInvoker.EnsureScripts()` — always overwrites `transform.py` and `filters.py` in LocalAppData; wrong for Phase 4 (scripts should live in the base dir, editable by the user).
- `Helpers/FirstRunHelper.cs` — not created.
- `ExportJob.Validate()` — not implemented; no field-level validation.
- `ExportJob` — no `Legal` property; `legal` field not in schema.
- `ExportJobRunner.Run()` — no LEGAL.md prepend step.
- `ContextExporterPluginControl` — no first-run offer on directory selection.
- `config/` at solution root — contains developer working configs; sample files not yet moved into the C# project as embedded resources.
- `D365ContextExporter.nuspec` — `<files>` section empty.
- `.github/workflows/` — does not exist.
- `README.md` — does not exist.

## Phase 4 Exit Criteria

1. A user who installs the plugin and selects a new empty folder is offered a reference configuration and receives a working set of sample specs, FetchXML queries, Jinja2 templates, and Python scripts in the expected folder structure.
2. `transform.py` and `filters.py` live in `<baseDir>\config\transformations\` and are never overwritten by the plugin once present.
3. A spec with a `legal` field causes the content of the referenced `LEGAL.md` file to be prepended to `output/<SpecName>.context.md` before the file is made available.
4. Config validation catches missing `spec`, unresolvable query paths, missing transformation file, and missing `resultKey`, and surfaces clear messages in the log before any query runs.
5. The plugin builds and packs to a `.nupkg` that installs cleanly from a local file path in XrmToolBox.
6. A GitHub Actions workflow produces the `.nupkg` on every push to `main`.

---

## Source File Inventory

### C# — New Files

---

#### `Helpers/FirstRunHelper.cs`

**Description.** Manages the lifecycle of the reference configuration in a base directory: initial deployment on first use, and upgrade deployment when the plugin version advances. All embedded-resource files are written to the expected folder structure; user-created files are never touched.

**Responsibilities.**

- `IsConfigured(string baseDir) → bool` — returns `true` if `<baseDir>\config\` exists and contains at least one `*.context-exporter-config.json` file.
- `OfferSetup(string baseDir, IWin32Window owner) → bool` — shows a `MessageBox.Show(...)` with `MessageBoxButtons.YesNo` offering to create the reference configuration. Returns `true` if the user selected Yes.
- `DeployReferenceConfig(string baseDir, IWin32Window owner, Action<string> log, bool overwrite = false)` — extracts all embedded sample files to their correct locations:
  - `<baseDir>\config\queries\*.fetch.xml` — FetchXML query files from `D365ContextExporter.SampleConfig.queries.*`.
  - `<baseDir>\config\transformations\*.j2` — Jinja2 templates from `D365ContextExporter.SampleConfig.transformations.*`.
  - `<baseDir>\config\transformations\transform.py` — Python orchestrator from `D365ContextExporter.SampleConfig.transformations.transform.py`.
  - `<baseDir>\config\transformations\filters.py` — Python filters.
  - `<baseDir>\config\transformations\requirements.txt` — pip requirements.
  - `<baseDir>\config\*.context-exporter-config.json` — sample spec configs.
  - `<baseDir>\LEGAL.md` — legal notice template, with `[ORGANISATION NAME]` substituted via `PromptOrgName(owner)` (see below).
  - When `overwrite` is `false` (first-run path): every file is guarded by `File.Exists`; existing files are skipped and logged as `[Setup] Skipped (exists): <path>`.
  - When `overwrite` is `true` (upgrade path): all files are overwritten **except `LEGAL.md`**, which is always skipped to protect user customisations. Logs each overwritten file as `[Setup] Updated: <path>`.
  - After all files are written, writes `<baseDir>\version.txt` containing the current assembly version string (e.g. `1.0.0.0`).
  - Does **not** create `runs\` or `output\` — those are created at run time by `ExportJobRunner`.

- `CheckVersion(string baseDir, IWin32Window owner, Action<string> log)` — called every time a base directory is loaded (startup or directory change). Reads `<baseDir>\version.txt` and compares its contents to the current assembly version via `Assembly.GetExecutingAssembly().GetName().Version.ToString()`.
  - If `version.txt` does not exist, the directory is either new (handled by `OfferSetup`) or was set up before this feature was added. Write `version.txt` with the current version and return without prompting.
  - If the version matches, return without action.
  - If the version differs, show a `MessageBox` informing the user (e.g. _"The plugin has been updated from 1.0.x.x to 1.0.y.y. Would you like to update the reference configuration files (queries, templates, Python scripts, and sample specs)? Your LEGAL.md and any custom files will not be modified."_) with `MessageBoxButtons.YesNo`.
    - If Yes: call `DeployReferenceConfig(baseDir, owner, log, overwrite: true)` — this writes `version.txt` at the end.
    - If No: write `version.txt` with the current version so the prompt is not shown again for this version, then log `[Setup] Upgrade skipped by user.`.

- `PromptOrgName(IWin32Window owner) → string` — private static method. Calls `Microsoft.VisualBasic.Interaction.InputBox` to ask the user for their organisation name before writing `LEGAL.md`:
  ```csharp
  using Microsoft.VisualBasic;

  private static string PromptOrgName(IWin32Window owner)
  {
      return Interaction.InputBox(
          "Enter your organisation name for the legal notice:",
          "Legal Notice Setup",
          "My Organisation");
  }
  ```
  If the user cancels (returns empty string), `LEGAL.md` is deployed with the `[ORGANISATION NAME]` placeholder intact. The validator warning covers that case. Called only when `LEGAL.md` does not already exist.

- `ApplyOrgName(string template, string orgName) → string` — internal static method (unit-testable). Returns `template` with every occurrence of `[ORGANISATION NAME]` replaced by `orgName`. If `orgName` is null or whitespace, returns `template` unchanged.

**How it is used.** `ContextExporterPluginControl.CheckAndOfferFirstRun(string dir)` calls `IsConfigured`, then `OfferSetup` + `DeployReferenceConfig` for new directories, then `CheckVersion` for all directories (new and existing). See the modified control section for the full call sequence.

**Key design constraints.**
- `LEGAL.md` is never overwritten on upgrade — it is user-owned once deployed.
- `version.txt` is always written/updated at the end of any deployment (first-run or upgrade) so the comparison is stable on the next load.
- `PromptOrgName` is called only when `LEGAL.md` does not already exist.
- Does not require a Dataverse connection.
- All paths are resolved through `PathResolver.Resolve(baseDir, ...)` for consistency.

---

#### `Helpers/ConfigValidator.cs`

**Description.** Validates a loaded `ExportJob` against the file system and config constraints. Called before the export run starts.

**Responsibilities.**
- `Validate(ExportJob job, string baseDir)` — static method. Collects all violations into a list and throws `ConfigValidationException` if any are found. Does not throw on the first error; reports all violations at once.
- Checks:
  - `job.Spec` is non-empty.
  - `job.Transformation` is non-empty and the file `<baseDir>\config\transformations\<transformation>` exists.
  - For each query: `query.Id` is non-empty; `query.ResultKey` is non-empty; if `type == "fetchxml"`, `query.Source` is non-empty and the file `<baseDir>\config\queries\<query.Source>` exists.
  - `job.Legal` is either null/empty (no LEGAL.md required) or the resolved path `PathResolver.Resolve(baseDir, job.Legal)` refers to an existing file.
  - No two queries share the same `id`.
  - No two queries share the same `resultKey`.

**`ConfigValidationException`** — a new internal typed exception carrying a `List<string> Violations`. The message is the violations joined with newlines.

**How it is used.** Called from `ContextExporterPluginControl.btnRun_Click()` after `PythonBootstrapHelper.Check()` and before starting the background `Task`. On failure, each violation is appended to the log and the run is aborted.

---

### C# — Modified Files

---

#### `Models/ExportJob.cs`

**Current state.** Has `Spec`, `Version`, `Transformation`, `Queries`, `Python`, `FrontMatter`, `Output`, `ConfigFilePath`.

**Required changes.**
1. Add `Legal` property:
   ```csharp
   [JsonProperty("legal")]
   public string Legal { get; set; } = string.Empty;
   ```
   This field holds a path to the LEGAL.md file, relative to the base directory (e.g. `"LEGAL.md"`). When empty or absent, no legal notice is prepended.

No other changes to `ExportJob.cs`.

---

#### `Orchestration/ExportJobRunner.cs`

**Current state.** Runs queries, writes intermediate JSON, calls `PythonInvoker.Invoke()`, appends token count, copies `output.md` to `output/<SpecName>.context.md`.

**Required changes.**

1. After `CopyOutputToSpecDir()`, call `PrependLegalNotice(job, baseDir, runDir)`:
   ```
   private void PrependLegalNotice(ExportJob job, string baseDir, string runDir)
   ```
   - If `job.Legal` is null or empty, return immediately.
   - Resolve the LEGAL.md path: `PathResolver.Resolve(baseDir, job.Legal)`.
   - If the file does not exist, log a warning and return; do not throw.
   - Read the legal file content.
   - Rewrite `output/<SpecName>.context.md` with the legal content prepended, followed by two blank lines, then the existing content.
   - The raw `output.md` in the run directory is **not** modified — only the `output/<SpecName>.context.md` copy.

2. Add a call to `ConfigValidator.Validate(job, baseDir)` at the very start of `Run()` (before creating the run directory). This surfaces config errors before any file system work begins.

**Change summary.** Two additions; no structural changes to query execution or intermediate JSON building.

---

#### `Orchestration/PythonInvoker.cs`

**Current state.** `EnsureScripts()` extracts `transform.py`, `filters.py`, and `requirements.txt` from embedded resources to `%LOCALAPPDATA%\D365ContextExporter\python\` on every invocation and always overwrites.

**Required changes.**

1. The scripts are no longer launched from LocalAppData. They live in `<baseDir>\config\transformations\`. `PythonInvoker.Invoke()` must be updated to reference this path:
   - `transformScript` → `Path.Combine(baseDir, "config", "transformations", "transform.py")`
   - Verify that `transform.py` exists before constructing arguments. If absent, throw `FileNotFoundException` with the message `"transform.py not found at: <path>. Run first-time setup to deploy it."`.

2. Remove `EnsureScripts()` entirely. Script deployment is now the sole responsibility of `FirstRunHelper.DeployReferenceConfig()`.

3. The argument string changes accordingly:
   ```
   <python> "<baseDir>\config\transformations\transform.py"
       --input "<runDir>\intermediate.json"
       --template "<baseDir>\config\transformations\<job.Transformation>"
       --out "<runDir>"
       --spec "<job.Spec>"
   ```

4. The `filters.py` import in `transform.py` must also work from this location. Because `transform.py` and `filters.py` both live in `config\transformations\`, the `FileSystemLoader` for the Jinja2 `Environment` is already configured to the same directory, so `import filters` (or a relative import from `transform.py`) will resolve correctly as long as the working directory passed to `ProcessRunner` is `<baseDir>\config\transformations\` instead of `<runDir>`. Update `workingDirectory` argument accordingly.

**Change summary.** Remove `EnsureScripts()`; update script path and working directory; add `FileNotFoundException` guard.

---

#### `ContextExporterPluginControl.cs`

**Current state.** Wires directory picker, spec picker, run button, log, progress, and output preview. Calls `PythonBootstrapHelper.Check()` and `ExportJobRunner.Run()`.

**Required changes.**

1. In `dirPicker_DirectoryChanged()`: after `LoadSpecs(newDir)`, call `CheckAndOfferFirstRun(newDir)`.
2. In `ContextExporterPluginControl_Load()`: after `LoadSettings()`, if `SelectedDirectory` is non-empty, call `CheckAndOfferFirstRun(SelectedDirectory)`.
3. Add `CheckAndOfferFirstRun(string dir)` private method. The sequence is: check for a new/empty directory first, then check for a version change. `CheckVersion` runs for all directories — including ones just freshly set up — so that `version.txt` is always written on the first deployment:
   ```csharp
   private void CheckAndOfferFirstRun(string dir)
   {
       var log = (Action<string>)(msg => this.progressControl.AppendLog(msg));

       if (!FirstRunHelper.IsConfigured(dir) && FirstRunHelper.OfferSetup(dir, this))
       {
           FirstRunHelper.DeployReferenceConfig(dir, this, log, overwrite: false);
           this.specPicker.LoadSpecs(dir);
       }

       FirstRunHelper.CheckVersion(dir, this, log);
   }
   ```
   `CheckVersion` is always called because a freshly deployed directory also needs `version.txt` written, and on subsequent loads it provides the upgrade check.
4. In `btnRun_Click()`: add a call to `ConfigValidator.Validate(job, baseDir)` wrapped in a try/catch for `ConfigValidationException`. On failure, log each violation and return without starting the task. This sits after `PythonBootstrapHelper.Check()`.

---

### Non-Code Files — New

---

#### `SampleConfig/` folder (inside the C# project)

**Description.** A new folder `D365ContextExporter\SampleConfig\` containing all reference configuration files that `FirstRunHelper.DeployReferenceConfig()` extracts on first run. These files are embedded as resources (`EmbeddedResource` build action) so they travel with the assembly.

**Contents.**

| Subfolder / File | Purpose |
|---|---|
| `SampleConfig\queries\*.fetch.xml` | All FetchXML query files (moved from `config\queries\` at solution root) |
| `SampleConfig\transformations\*.j2` | All Jinja2 templates (moved from `config\transformations\` at solution root) |
| `SampleConfig\transformations\transform.py` | Python orchestrator (moved from `python\transform.py`) |
| `SampleConfig\transformations\filters.py` | Python filters (moved from `python\filters.py`) |
| `SampleConfig\transformations\requirements.txt` | pip requirements (moved from `python\requirements.txt`) |
| `SampleConfig\EntityDictionary.context-exporter-config.json` | Simplest spec: entity dictionary only |
| `SampleConfig\SecurityModel.context-exporter-config.json` | Medium spec: security roles + entity list |
| `SampleConfig\SolutionsReference.context-exporter-config.json` | Full spec: all queries |
| `SampleConfig\LEGAL.md` | Default legal notice template |

The `python\` folder is deleted from the C# project entirely. `SampleConfig\transformations\` is the single location for all Python files in the project. `FirstRunHelper.DeployReferenceConfig` deploys them from the `D365ContextExporter.SampleConfig.transformations.*` resource namespace. There are no duplicate resource entries.

**Spec streamlining.** The three sample specs replace `Sample.context-exporter-config.json` and `Sample2.context-exporter-config.json`:

- **`EntityDictionary.context-exporter-config.json`** — one query (`entity-attributes` via WebAPI), transformation `entity-dictionary.j2`. Minimal, good for first test.
- **`SecurityModel.context-exporter-config.json`** — two queries (`entity-attributes` + `security-roles` FetchXML), transformation `security-model.j2`. Demonstrates mixed query types.
- **`SolutionsReference.context-exporter-config.json`** — all eleven queries from the current config (solutions, entities, globalOptionSets, pluginAssemblies, sdkSteps, customApiRequests, customApiResponses, workflows, appModules, envVars, securityRoles), transformation `solutions-reference.j2`. The complete grounding pack; all include a `"legal": "LEGAL.md"` field.

All three specs set `"legal": "LEGAL.md"` so that the LEGAL.md feature is exercised out of the box.

The existing `config/` folder at the solution root becomes the developer's own working directory (useful during development to test changes locally). It is not removed; it just no longer serves as the canonical source of sample files.

---

#### `SampleConfig/LEGAL.md`

**Description.** A default legal notice template that `FirstRunHelper` deploys to `<baseDir>\LEGAL.md` on first run. Contains boilerplate text warning that the exported content is confidential and subject to copyright, and that distribution, publication, or use for AI model training may infringe on the content owner's rights.

**Contents (suggested).**
```markdown
> [!WARNING]
> This document was generated by D365 CE Context Exporter and contains confidential
> business data exported from a Microsoft Dynamics 365 Customer Engagement environment.
>
> **Copyright notice:** The content of this file is the exclusive property of
> [ORGANISATION NAME]. All rights reserved.
>
> **Restrictions:** Do not distribute, publish, or share this file outside of
> [ORGANISATION NAME] without explicit written authorisation. Do not use the
> contents of this file to train, fine-tune, or evaluate any artificial intelligence
> or machine learning model.
>
> This file is provided solely as grounding context for authorised use within
> [ORGANISATION NAME]'s internal AI assistant workflows.
```

Users are expected to edit this file to replace the placeholder text with their organisation's name and any additional legal language required by their compliance team.

---

#### `README.md`

**Location.** `tooling/D365ContextExporter/README.md` (alongside the `.sln` file).

**Required sections.**
1. **What it does** — one-paragraph summary.
2. **Prerequisites** — XrmToolBox version, .NET 4.8, Python 3.11+, required pip packages (with one-liner install command).
3. **Quickstart** — numbered steps: install plugin → connect → pick directory → run first-time setup → select spec → click Run → upload `.context.md` to AI assistant.
4. **Folder structure** — the `Context-Exporter/` tree from the main plan document.
5. **Configuration reference** — table of all JSON spec fields including the new `legal` field.
6. **Authoring transformations** — how to add a new `.j2` file and reference it in a spec.
7. **Editing Python scripts** — explain that `transform.py` and `filters.py` live in `config\transformations\` and can be edited in place.
8. **Building from source** — `dotnet build` / `dotnet pack` instructions.
9. **License** — MIT.

**Note: this is a human-authored document.** The AI can produce a first draft, but screenshots must be added by a human after the plugin is running. Insert `<!-- TODO: screenshot -->` placeholders where screenshots belong.

---

#### `.github/workflows/build-and-pack.yml`

**Description.** GitHub Actions workflow that builds the plugin, runs tests, packs a `.nupkg`, and uploads it as a workflow artifact. Triggered on push to `main` and on pull requests targeting `main`.

**Required steps.**

```yaml
name: Build and Pack D365ContextExporter

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.x'   # SDK used for build; output still targets net48

      - name: Restore
        run: dotnet restore tooling/D365ContextExporter/D365ContextExporter.sln

      - name: Build
        run: dotnet build tooling/D365ContextExporter/D365ContextExporter.sln
               --configuration Release --no-restore

      - name: Test
        run: dotnet test tooling/D365ContextExporter/D365ContextExporter.Tests/D365ContextExporter.Tests.csproj
               --configuration Release --no-build --verbosity normal

      - name: Pack
        run: |
          $version = "1.0.${{ github.run_number }}"
          nuget pack tooling/D365ContextExporter/D365ContextExporter/D365ContextExporter.nuspec `
            -Version $version `
            -BasePath tooling/D365ContextExporter/D365ContextExporter `
            -OutputDirectory nupkg

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: D365ContextExporter-nupkg
          path: nupkg/*.nupkg
```

**Publish step (conditional).** Add a separate `publish` job that depends on `build`, runs only on push to `main`, and pushes the `.nupkg` to nuget.org using a `NUGET_API_KEY` repository secret. This step is written into the YAML but the secret must be created by a human (see Human Tasks below).

---

### Non-Code Files — Modified

---

#### `D365ContextExporter.nuspec`

**Current state.** Metadata is correct; `<files>` section has a placeholder comment with no actual entries.

**Required changes.** Replace the `<files>` block with complete entries:

```xml
<files>
  <!-- Plugin DLL and debug symbols -->
  <file src="bin\Release\net48\D365ContextExporter.dll" target="Plugins" />
  <file src="bin\Release\net48\D365ContextExporter.pdb" target="Plugins" />

  <!-- Schema for IDE IntelliSense (installed alongside plugin) -->
  <file src="..\schema\context-exporter.schema.json" target="Plugins\schema" />
</files>
```

**Notes.**
- XrmToolBox requires plugin DLLs under a `Plugins\` folder in the package. Both the DLL and PDB go there.
- `SampleConfig\` and `config\` are **not** included in the nupkg. The Python scripts and sample configs are embedded as assembly resources and deployed by `FirstRunHelper` at runtime; they do not need to be shipped as loose files.
- The `schema\` directory is included as a `Plugins\schema\` content folder so VS Code can find it via a relative `$schema` reference from any config file deployed beside the plugin.

---

#### `schema/context-exporter.schema.json`

**Current state.** Defines all existing fields; does not include `legal`.

**Required changes.** Add the `legal` property to the root schema object:

```json
"legal": {
  "type": "string",
  "description": "Path to a LEGAL.md file (relative to the base directory) whose content is prepended to the output context file before delivery. Omit or leave empty to skip the legal notice step."
}
```

---

#### `D365ContextExporter.csproj`

**Current state.** Embeds `python\transform.py`, `python\filters.py`, `python\requirements.txt` as separate `EmbeddedResource` items. Includes `schema\` as `None` with `CopyToOutputDirectory`.

**Required changes.**

1. Remove the three individual `python\*.py` / `python\requirements.txt` `EmbeddedResource` items. The `python\` folder is deleted.
2. Add a single glob `EmbeddedResource` item that picks up everything under `SampleConfig\`:
   ```xml
   <EmbeddedResource Include="SampleConfig\**\*.*">
     <LogicalName>D365ContextExporter.SampleConfig.%(RecursiveDir)%(Filename)%(Extension)</LogicalName>
   </EmbeddedResource>
   ```
   This is the only embedded resource group for Python scripts, templates, queries, specs, and LEGAL.md.
3. Remove the `<None Include="..\schema\**\*" CopyToOutputDirectory="PreserveNewest" />` item — the schema is shipped via the nuspec `<files>` block to the `Plugins\schema\` folder and does not need to be copied to the build output directory.
4. Add a framework assembly reference for `Microsoft.VisualBasic`:
   ```xml
   <ItemGroup>
     <Reference Include="Microsoft.VisualBasic" />
   </ItemGroup>
   ```
5. Remove the `PostBuild` `xcopy` target that copies the DLL to the local XrmToolBox development install. This path is machine-specific and will fail in CI. Replace it with a `README` note directing developers to configure this via a local `Directory.Build.targets` override.

---

### Python Files — Moved, Not Modified

`transform.py`, `filters.py`, and `requirements.txt` move from `python\` to `SampleConfig\transformations\`. Their implementations are unchanged. The `python\` folder is deleted from the project. The only behavioural change is that `PythonInvoker` now resolves them from `<baseDir>\config\transformations\` rather than `%LOCALAPPDATA%\D365ContextExporter\python\`.

---

## Human Tasks

The following items require action by a human developer and cannot be automated by the AI:

| # | Task | Notes |
|---|------|-------|
| H1 | **Add `[ORGANISATION NAME]` text to `LEGAL.md`** | Edit `SampleConfig\LEGAL.md` and `config\LEGAL.md` (if it exists) to substitute the real organisation name and any additional legal language required by the compliance team. |
| H2 | **Create a NuGet account and API key** | Register at [nuget.org](https://nuget.org). Under your profile, generate an API key scoped to `D365ContextExporter`. Store it as a GitHub Actions repository secret named `NUGET_API_KEY`. |
| H3 | **Add screenshots to README** | After Phase 4 is implemented and the plugin is running, capture screenshots of: the spec picker with the three sample configs, the progress log during a run, and the output preview panel. Replace the `<!-- TODO: screenshot -->` placeholders in `README.md`. |
| H4 | **Remove the `PostBuild` xcopy in `.csproj`** | The machine-specific path `..\..\..\..\Git\XrmToolBox\...` in the `PostBuild` target must be replaced with a local `Directory.Build.targets` approach. Each developer creates a `Directory.Build.targets` in the solution root that overrides the output copy path for their own machine. Add an example file `Directory.Build.targets.example` to the repo. |
| H5 | **End-to-end sandbox test of all three sample specs** | After implementation, connect to a real sandbox org, run each of `EntityDictionary`, `SecurityModel`, and `SolutionsReference`, verify the rendered Markdown is structurally valid, and confirm the LEGAL.md header appears in all three output files. |
| H6 | **Publish to XrmToolBox Tool Library** | Once the pipeline produces a verified `.nupkg`, push it to nuget.org using the stored API key. Verify the listing appears in XrmToolBox under the tags `XrmToolBox Documentation AI Markdown D365 Dataverse Grounding`. See [Jonas Rapp's guide](https://jonasr.app/xtbvsts/) and the [XrmToolBox wiki](https://github.com/MscrmTools/XrmToolBox/wiki/Distribute-your-plugins-through-XrmToolBox-and-Nuget). |

---

## Artifacts Inventory

| # | Artifact | Action | Notes |
|---|----------|--------|-------|
| 1 | `Helpers/FirstRunHelper.cs` | **New** | `IsConfigured`, `OfferSetup`, `DeployReferenceConfig`; never overwrites user files |
| 2 | `Helpers/ConfigValidator.cs` | **New** | `Validate()` + `ConfigValidationException`; checks all fields before run starts |
| 3 | `Models/ExportJob.cs` | **Modify** | Add `Legal` property (`[JsonProperty("legal")]`) |
| 4 | `Orchestration/ExportJobRunner.cs` | **Modify** | Add `ConfigValidator.Validate()` at start; add `PrependLegalNotice()` after copy step |
| 5 | `Orchestration/PythonInvoker.cs` | **Modify** | Remove `EnsureScripts()`; resolve `transform.py` from `<baseDir>\config\transformations\`; update `workingDirectory` |
| 6 | `ContextExporterPluginControl.cs` | **Modify** | Wire `CheckAndOfferFirstRun()`; wire `ConfigValidator` error display |
| 7 | `python/` folder | **Delete** | `transform.py`, `filters.py`, `requirements.txt` move to `SampleConfig/transformations/` |
| 8 | `SampleConfig/` folder | **New** | All sample specs, queries, templates, Python scripts, LEGAL.md as `EmbeddedResource`; `version.txt` written to base dir at runtime (not embedded) |
| 9 | `SampleConfig/transformations/transform.py` | **New (moved)** | Moved from `python/transform.py`; content unchanged |
| 10 | `SampleConfig/transformations/filters.py` | **New (moved)** | Moved from `python/filters.py`; content unchanged |
| 11 | `SampleConfig/transformations/requirements.txt` | **New (moved)** | Moved from `python/requirements.txt`; content unchanged |
| 12 | `SampleConfig/EntityDictionary.context-exporter-config.json` | **New** | Simple spec; replaces `Sample.context-exporter-config.json` |
| 13 | `SampleConfig/SecurityModel.context-exporter-config.json` | **New** | Medium spec; replaces `Sample2.context-exporter-config.json` |
| 14 | `SampleConfig/SolutionsReference.context-exporter-config.json` | **New** | Full spec; moved from `config/` and updated with `"legal"` field |
| 15 | `SampleConfig/LEGAL.md` | **New** | Default legal notice template; deployed by `FirstRunHelper` |
| 16 | `README.md` | **New** | Quickstart, config reference, authoring guide; screenshots deferred (human task H3) |
| 17 | `.github/workflows/build-and-pack.yml` | **New** | Build + test + pack; conditional publish job wired to `NUGET_API_KEY` secret |
| 18 | `D365ContextExporter.nuspec` | **Modify** | Complete `<files>` block: DLL + PDB to `Plugins\`; schema to `Plugins\schema\` |
| 19 | `schema/context-exporter.schema.json` | **Modify** | Add `legal` string property |
| 20 | `D365ContextExporter.csproj` | **Modify** | Remove `python\*` embedded resources; add `SampleConfig\**` glob; add `Microsoft.VisualBasic` reference; remove `PostBuild` xcopy; remove schema `CopyToOutputDirectory` |
| 21 | `Directory.Build.targets.example` | **New** | Example for developers to redirect the post-build DLL copy to their local XrmToolBox install |

---

## Data Flow — LEGAL.md Prepend

The legal notice is added as a final post-processing step in `ExportJobRunner.Run()`, after the token-count header is already in place:

```
ExportJobRunner.Run()
  ...
  ├─ PythonInvoker.Invoke()                  [renders output.md; copies to output/<spec>.context.md]
  ├─ AppendTokenCount()                      [prepends token count blockquote to output.md]
  ├─ CopyOutputToSpecDir()                   [overwrites output/<spec>.context.md with token-count version]
  └─ PrependLegalNotice()                    [Phase 4 — prepends LEGAL.md to output/<spec>.context.md only]
```

The final content order in `output/<SpecName>.context.md`:
1. LEGAL.md content (if `legal` is set)
2. Blank line separator
3. Token count blockquote
4. Blank line
5. Rendered Markdown from the Jinja2 template

The run-directory `output.md` contains only items 3–5 (token count + rendered output). The legal notice is not injected into the run archive — it only appears in the deliverable grounding file.

---

## Testing Strategy

**Unit tests — `FirstRunHelper`.**
- `IsConfigured` returns `false` for an empty temp directory and `true` after `DeployReferenceConfig` runs against it.
- `DeployReferenceConfig` with `overwrite: false` does not overwrite an existing file (write a sentinel file, call deploy, read back — content must be unchanged).
- `DeployReferenceConfig` with `overwrite: true` overwrites embedded-source files but never touches `LEGAL.md`.
- All expected files are present after a clean deploy.
- `version.txt` is written with the current assembly version after `DeployReferenceConfig` completes.
- `CheckVersion`: when `version.txt` is absent, the file is created and no prompt is shown.
- `CheckVersion`: when version matches, no prompt is shown.
- `CheckVersion`: when version differs and user accepts, `DeployReferenceConfig(overwrite: true)` is called and `version.txt` is updated.
- `CheckVersion`: when version differs and user declines, `DeployReferenceConfig` is not called but `version.txt` is still updated to the current version.
- `ApplyOrgName`: replaces all occurrences of `[ORGANISATION NAME]` when a non-empty name is given.
- `ApplyOrgName`: returns the template unchanged when `orgName` is null or whitespace.
- When `orgName` is non-empty, `LEGAL.md` on disk does not contain `[ORGANISATION NAME]`.
- When `orgName` is empty (user cancelled), `LEGAL.md` on disk still contains `[ORGANISATION NAME]` (placeholder preserved).

Note: `PromptOrgName` (the `InputBox` call) is not unit-tested — it is a thin UI call. The replacement logic is covered via `ApplyOrgName` independently.

**Unit tests — `ConfigValidator`.**
- Happy path: a fully valid job passes without exception.
- Missing `spec`: exception with a message containing `"spec"`.
- Missing `resultKey` on one query: exception names the offending query `id`.
- Unresolvable FetchXML source path: exception names the bad path.
- Missing transformation file: exception names the template file.
- Missing LEGAL.md when `legal` is set: exception names the missing file.
- Duplicate `resultKey`: exception lists both query ids.

**Unit tests — `ExportJobRunner.PrependLegalNotice`.**
- When `job.Legal` is empty, output file is unchanged.
- When legal file exists, its content appears verbatim at the top of the output file.
- When legal file is missing, a warning is logged and the output file is unchanged (no throw).

**Integration — first-run flow.**
Test manually on a clean temp directory: point the plugin at it, accept the setup offer, verify all files are present in the expected structure, then run the `EntityDictionary` spec against a sandbox org and confirm the output contains the LEGAL.md header.

**Python — no changes.** The snapshot tests from Phase 3 continue to pass unchanged.

---

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| User edits `transform.py` in the base dir; a future plugin update ships a new version of the script. | `FirstRunHelper.DeployReferenceConfig` never overwrites an existing file. Users who want the updated script must manually delete (or rename) their copy. Document this in the README upgrade notes. |
| `EmbeddedResource` logical name pattern with `%(RecursiveDir)` replaces backslashes with dots on Windows. | Normalise the resource name in `FirstRunHelper` by replacing `\` with `.` when computing the manifest resource stream name. Cover this in unit tests by extracting a known resource by its computed name. |
| User cancels the `InputBox` prompt; `LEGAL.md` is deployed with `[ORGANISATION NAME]` still in place. | `ConfigValidator` checks: if the resolved `LEGAL.md` file contains the literal string `[ORGANISATION NAME]`, log a warning (not an error) so the user is reminded to edit it before the output file is used. |
| `version.txt` is absent in a base directory configured before Phase 4 shipped. | `CheckVersion` treats a missing `version.txt` as a silent first write — it writes the current version and returns without prompting. The user is not offered an upgrade for that first load; they will be prompted normally if they later install a newer version of the plugin. |
| `Microsoft.VisualBasic` not referenced at build time. | `Microsoft.VisualBasic.dll` is a framework assembly bundled with every .NET Framework 4.8 installation — it cannot be absent on any machine that can run XrmToolBox. The only action required is a build-time `<Reference Include="Microsoft.VisualBasic" />` in the `.csproj` so the compiler can resolve `Interaction.InputBox`. No NuGet package or runtime deployment step is needed. |
| GitHub Actions NuGet publish step fails if `NUGET_API_KEY` secret is absent. | Make the `publish` job conditional on `vars.NUGET_API_KEY != ''` so the pipeline still produces a build artifact without failing when the secret is not yet configured. |
| `PostBuild` xcopy removal breaks local developer workflow. | Provide `Directory.Build.targets.example`; document in README. Developers who relied on the xcopy must opt in explicitly by creating their own override. |
